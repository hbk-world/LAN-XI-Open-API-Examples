using Shared;
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web.Script.Serialization;

class Program
{
    //
    // This program demonstrates how to stream sample data from a LAN-XI module in
    // multi-socket mode, i.e. using one TCP stream per input channel on the module.
    //
    // It works with any LAN-XI module, and will stream data from all input channels
    // using the highest supported bandwidth. About two seconds of streamed data
    // will be saved to a text file in a format suitable for import into e.g. Matlab
    // or Excel.
    //
    // Users may specify the IP address of the LAN-XI module and the name of the
    // output file on the command line.
    //
    static void Main(string[] args)
    {
        // LAN-XI module IP address
        var ipAddr = (args.Length >= 1) ? args[0] : "192.168.1.101";

        // Name of output file to store samples from the module
        var fileName = (args.Length >= 2) ? args[1] : "LANXI.out";

        Console.WriteLine("LAN-XI module at {0}, saving streamed data to {1}", ipAddr, fileName);

        var rest = new LanXIRESTBoundary(ipAddr);

        // Open the recorder application in the module
        rest.PutRequestWithPath("/rest/rec/open", null);
        rest.WaitForRecorderState("RecorderOpened");

        // Prepare the module for a new recording
        rest.PutRequestWithPath("/rest/rec/create", null);
        rest.WaitForRecorderState("RecorderConfiguring");

        // Get the default recording setup from the module
        var setup = rest.GetRequestWithPath("/rest/rec/channels/input/default");
        var numberOfChannels = ((ArrayList)setup["channels"]).Count;

        // Modify the setup to use multi-socket streaming on all channels
        for (var i = 0; i < numberOfChannels; i++)
        {
            setup["channels"][i]["destinations"] = new string [] { "multiSocket" };
        }

        // Convert modified setup back to JSON string
        var serializer = new JavaScriptSerializer();
        var setupJson = serializer.Serialize(setup);

        // Apply the setup
        rest.PutRequestWithPath("/rest/rec/channels/input", setupJson);
        rest.WaitForRecorderState("RecorderStreaming");

        // Request a list of TCP ports to connect to, one port for each enabled input channel
        var portsResponse = rest.GetRequestWithPath("/rest/rec/destination/sockets");
        var ports = (int [])((ArrayList)portsResponse["tcpPorts"]).ToArray(typeof(int));

        Console.WriteLine("Will stream samples from TCP ports {0}",
            string.Join(", ", Array.ConvertAll(ports, port => port.ToString())));

        var samples = new ArrayList[numberOfChannels];
        var threads = new Thread[numberOfChannels];
        var sockets = new Socket[numberOfChannels];

        // Connect sockets to each TCP port and spin up threads to read from the sockets
        for (var i = 0; i < numberOfChannels; i++)
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(ipAddr), ports[i]);
            sockets[i] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sockets[i].Connect(endPoint);

            Console.WriteLine("Connected to {0}", endPoint);

            samples[i] = new ArrayList();

            var sock = sockets[i];
            var samp = samples[i];

            threads[i] = new Thread(() =>
            {
                try
                {
                    for(;;)
                    {
                        var header = ReadHeader(sock);
                        ReadMessage(sock, samp, (int)header.dataLength, header.messageType);
                    }
                }
                catch(Exception ex) when(ex is ObjectDisposedException || ex is SocketException)
                {
                    // Thrown when we close the socket
                }
            });

            threads[i].Start();
        }

        // Start streaming
        rest.PostRequestWithPath("/rest/rec/measurements", null);
        rest.WaitForRecorderState("RecorderRecording");

        // Collect some samples
        Console.WriteLine("Streaming data for 2 seconds...");
        Thread.Sleep(2000);

        // Stop streaming
        rest.PutRequestWithPath("/rest/rec/measurements/stop", null);
        rest.WaitForRecorderState("RecorderStreaming");

        // Close sockets, wait for threads to exit
        for (var i = 0; i < numberOfChannels; i++)
        {
            sockets[i].Shutdown(SocketShutdown.Both);
            sockets[i].Close();

            threads[i].Join();
        }

        // Close streaming session
        rest.PutRequestWithPath("/rest/rec/finish", null);
        rest.WaitForRecorderState("RecorderOpened");

        // Close recorder application
        rest.PutRequestWithPath("/rest/rec/close", null);
        rest.WaitForRecorderState("Idle");

        // Samples are streamed in blocks starting from the first channel to
        // the last channel. Unless we got lucky and streaming was stopped
        // exactly after receiving a block from the last channel, we will
        // have ended up with more samples from the first channel(s) than
        // the last channel. Prevent out-of-bounds reads from the buffers
        // by only reading up to the number of samples stored in the buffer
        // from the last channel.
        var lastChannel = numberOfChannels - 1;
        var numberOfSamples = samples[lastChannel].Count;
        Console.WriteLine("Got {0} samples from each channel", numberOfSamples);

        Console.WriteLine("Writing output file");

        // Write samples to text file, each line containing
        // one sample from each channel, separated by tab
        using (var file = new StreamWriter(fileName))
        {
            for (var sample = 0; sample < numberOfSamples; sample++)
            {
                var line = "";
                var separator = "";

                for (var channel = 0; channel < numberOfChannels; channel++)
                {
                    line += separator + samples[channel][sample];
                    separator = "\t";
                }

                file.WriteLine(line);
            }
        }

        Console.WriteLine("Done");
    }

    // Read a data header from the socket stream
    static StreamingHeader ReadHeader(Socket socket)
    {
        var buffer = new byte[28];
        var bytesReceived = 0;

        // Read the header into the buffer
        while (bytesReceived < buffer.Length)
        {
            bytesReceived += socket.Receive(buffer, bytesReceived, buffer.Length - bytesReceived, SocketFlags.None);
        }

        // Extract the header from the buffer
        StreamingHeader header;
        header.magic = new byte[2];
        header.magic[0] = (byte)BitConverter.ToChar(buffer, 0);
        header.magic[1] = (byte)BitConverter.ToChar(buffer, 1);
        header.headerLength = BitConverter.ToUInt16(buffer, 2);
        header.messageType = BitConverter.ToUInt16(buffer, 4);
        header.reserved1 = BitConverter.ToInt16(buffer, 6);
        header.reserved2 = BitConverter.ToInt32(buffer, 8);
        header.timestampFamily = BitConverter.ToUInt32(buffer, 12);
        header.timestamp = BitConverter.ToUInt64(buffer, 16);
        header.dataLength = BitConverter.ToUInt32(buffer, 24);

        return header;
    }

    // Read the data message following the header from the socket data stream
    static void ReadMessage(Socket socket, ArrayList samples, int messageLength, int messageType)
    {
        var buffer = new byte[messageLength];
        var bytesReceived = 0;

        // Read the message into the buffer
        while (bytesReceived < messageLength)
        {
            bytesReceived += socket.Receive(buffer, bytesReceived, buffer.Length - bytesReceived, SocketFlags.None);
        }

        // We only care about message type 1, which is signal data
        if (messageType == 1)
        {
            // Populate a message struct
            SignalDataMessage message;
            message.numberOfSignals = BitConverter.ToUInt16(buffer, 0);
            message.reserved1 = BitConverter.ToInt16(buffer, 2);
            message.signalId = BitConverter.ToUInt16(buffer, 4);
            message.numberOfValues = BitConverter.ToUInt16(buffer, 6);

            // Maintain an offset in the buffer and read each sample
            var offset = 8;
            for (var i = 0; i < message.numberOfValues; i++)
            {
                // Collect 3 bytes for a 24-bit sample
                byte low = buffer[offset++];
                byte mid = buffer[offset++];
                byte high = buffer[offset++];

                // Combine the bytes to create a 24-bit sample
                int sample = ((high << 24) + (mid << 16) + (low << 8)) >> 8;

                // Store sample in ArrayList
                samples.Add(sample);
            }
        }
    }
}
