// Simple example streaming from all inputs of a 3160 module and echoing input 1 and 2 back through the PC to output 1 and 2.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using Shared;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net;

namespace InOutStreaming
{
    public class Program
    {
        static readonly string LANXI_IP = "10.10.3.1"; // IP address (or hostname) of the LAN-XI module
        static readonly int SAMPLES_TO_RECEIVE = 131072 * 10; // Number of samples to loop back
        static readonly int SAMPLES_TO_PRIME = 3 * 6000; // Number of samples to prime into the generator buffers before starting the output
        static readonly int IN_A_FRAME = 1; //

        static void Main(string[] args)
        {
            // Use program arguments if specified, otherwise use constants.
            // Arguments are in the sequence [[[<lanxi_ip>] <samples_to_receive>] <samples_to_prime>]
            string lanxi_ip = LANXI_IP;
            int samples_to_receive = SAMPLES_TO_RECEIVE;
            int samples_to_prime = SAMPLES_TO_PRIME;
            if (args.Length >= 1)
            {
                lanxi_ip = args[0];
            }
            if (args.Length >= 2)
            {
                samples_to_receive = Convert.ToInt32(args[1]);
            }
            if (args.Length >= 3)
            {
                samples_to_prime = Convert.ToInt32(args[2]);
            }

            // Initialize boundary object
            LanXIRESTBoundary rest = new LanXIRESTBoundary(lanxi_ip);

            // Start measurement

            // Open the Recorder application on the LAN-XI module
            rest.PutRequestWithPath("/rest/rec/open", null);
            rest.WaitForRecorderState("RecorderOpened");

            // Prepare generator
            string outputChannelStart = File.ReadAllText(@"InOutStreaming_OutputChannelStartStreaming.json");
            rest.PutRequestWithPath("/rest/rec/generator/prepare", outputChannelStart);
            
            // Configure generator channels
            string outputChannelConfiguration = File.ReadAllText(@"InOutStreaming_OutputChannelSetupStreaming.json");
            rest.PutRequestWithPath("/rest/rec/generator/output", outputChannelConfiguration);

            // Get port numbers to send samples to
            Dictionary<string, dynamic> dict = rest.GetRequestWithPath("/rest/rec/generator/output");
            UInt16[] outputPorts = new UInt16[2];
            outputPorts[0] = (UInt16)dict["outputs"][0]["inputs"][0]["port"];
            outputPorts[1] = (UInt16)dict["outputs"][1]["inputs"][0]["port"];

            Console.WriteLine("Output streaming TCP ports: {0} - {1}", outputPorts[0], outputPorts[1]);

            // Create Recorder configuration
            rest.PutRequestWithPath("/rest/rec/create", null);
            rest.WaitForRecorderState("RecorderConfiguring");

            // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default
            // In this example a JSON file has been prepared with the desired config.
            string inputChannelConfiguration = File.ReadAllText(@"InOutStreaming_InputChannelSetup.json");
            rest.PutRequestWithPath("/rest/rec/channels/input", inputChannelConfiguration);
            rest.WaitForRecorderState("RecorderStreaming");

            // Get the TCP port provided by the LAN-XI module for streaming samples
            dict = rest.GetRequestWithPath("/rest/rec/destination/socket");
            UInt16 inputPort = (UInt16)dict["tcpPort"];
            Console.WriteLine("Input streaming TCP port: {0}", inputPort);

            // Start measuring
            rest.PostRequestWithPath("/rest/rec/measurements", null);
            rest.WaitForRecorderState("RecorderRecording");

            // Streaming should now be running

            // Let connectSocketAndStream() method handle socket connection
            // The socket connection may be established while the Recorder was in the "RecorderStreaming" state
            SocketCommunication comm = new SocketCommunication(lanxi_ip, inputPort, outputPorts, samples_to_receive);
            Thread commThread = new Thread(new ThreadStart(comm.ConnectSocketAndStream));
            commThread.Start();

            while (SocketCommunication.samplesReceived[0] < samples_to_prime || SocketCommunication.samplesReceived[1] < samples_to_prime) Thread.Sleep(100);

            // Start output
            rest.PutRequestWithPath("/rest/rec/generator/start", outputChannelStart);
            if (IN_A_FRAME != 0)
                rest.PutRequestWithPath("/rest/rec/apply", null);

            while (commThread.IsAlive)
                Thread.Sleep(10);

            // Stop output
            rest.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);

            // Stop measuring and close sockets
            rest.PutRequestWithPath("/rest/rec/measurements/stop", null);
            rest.WaitForRecorderState("RecorderStreaming");
            comm.inputSocket.Close();
            for (int i = 0; i < 2; i++)
                comm.outputSockets[i].Close();

            // Finish recording
            rest.PutRequestWithPath("/rest/rec/finish", null);
            rest.WaitForRecorderState("RecorderOpened");

            // Close Recorder application
            rest.PutRequestWithPath("/rest/rec/close", null);
            rest.WaitForRecorderState("Idle");

            // Module should now be back in the Idle state without the Recorder application running
        }
    }

    class SocketCommunication
    {
        public string address { get; set; }
        public UInt16 inputPort { get; set; }
        public Socket inputSocket { get; set; }
        public UInt16[] outputPorts { get; set; }
        public Socket[] outputSockets { get; set; }

        public int samples_to_receive { get; set; }

        public static UInt32[] samplesReceived = { 0, 0, 0, 0 }; // Used to count the number of samples received - for demo purpose

        public SocketCommunication(string address, UInt16 inputPort, UInt16[] outputPorts, int samples_to_receive)
        {
            this.address = address;
            this.inputPort = inputPort;
            this.inputSocket = inputSocket;
            this.outputPorts = outputPorts;
            this.outputSockets = outputSockets;

            this.samples_to_receive = samples_to_receive;
        }

        public void ConnectSocketAndStream()
        {
            // Connect to the input streaming port on the LAN-XI module
            IPEndPoint remoteInputEP = new IPEndPoint(Dns.GetHostAddresses(address)[0], inputPort);
            inputSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            inputSocket.Connect(remoteInputEP);

            // Set up and connect output sockets (1 per channel)
            outputSockets = new Socket[2];
            for (int i = 0; i < 2; i++)
            {
                IPEndPoint remoteOutputEP = new IPEndPoint(Dns.GetHostAddresses(address)[0], outputPorts[i]);
                outputSockets[i] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                outputSockets[i].Connect(remoteOutputEP);
            }

            // Socket is connected and everything is ready to go. Run until the desired number of samples have been received.
            while (samplesReceived[0] < samples_to_receive || samplesReceived[1] < samples_to_receive || samplesReceived[2] < samples_to_receive || samplesReceived[3] < samples_to_receive)
            {
                StreamingHeader streamingHeader = ReadHeader();
                ReadMessage((int)streamingHeader.dataLength, streamingHeader.messageType);
            }
        }

        /// <summary>
        /// Reads the StreamingHeader from the socket data stream.
        /// </summary>
        /// <returns>StreamingHeader struct describing the following message.</returns>
        public StreamingHeader ReadHeader()
        {
            // Initialize StreamingHeader struct and temporary buffer for processing
            StreamingHeader streamingHeader;
            byte[] inputBuffer = new byte[28];
            int bytesReceived = 0;

            // Receive the header
            while (bytesReceived < inputBuffer.Length)
                bytesReceived += inputSocket.Receive(inputBuffer, bytesReceived, inputBuffer.Length - bytesReceived, SocketFlags.None);

            // Populate the StreamingHeader struct from the contents of the temporary buffer
            streamingHeader.magic = new Byte[2];
            for (int i = 0; i < 2; i++)
                streamingHeader.magic[i] = (Byte)BitConverter.ToChar(inputBuffer, i);
            streamingHeader.headerLength = BitConverter.ToUInt16(inputBuffer, 2);
            streamingHeader.messageType = BitConverter.ToUInt16(inputBuffer, 4);
            streamingHeader.reserved1 = BitConverter.ToInt16(inputBuffer, 6);
            streamingHeader.reserved2 = BitConverter.ToInt32(inputBuffer, 8);
            streamingHeader.timestampFamily = BitConverter.ToUInt32(inputBuffer, 12);
            streamingHeader.timestamp = BitConverter.ToUInt64(inputBuffer, 16);
            streamingHeader.dataLength = BitConverter.ToUInt32(inputBuffer, 24);

            return streamingHeader;
        }

        /// <summary>
        /// Read the message following the StreamingHeader from the socket data stream.
        /// </summary>
        /// <param name="dataLength">Number of bytes to read and parse.</param>
        /// <param name="messageType">Type of message to parse. 1 means SignalData, and is the only one supported. Other types are ignored.</param>
        public void ReadMessage(int dataLength, UInt16 messageType)
        {
            // Initialize a temporary buffer
            byte[] inputBuffer = new byte[dataLength];
            int bytesReceived = 0;

            // Read the full message contents into the temporary buffer
            while (bytesReceived < dataLength)
                bytesReceived += inputSocket.Receive(inputBuffer, bytesReceived, dataLength - bytesReceived, SocketFlags.None);

            // Simple examples - we only care about signal data.
            if (messageType == 1)
            {
                // Populate a header struct
                SignalDataMessage signalDataMessage;
                signalDataMessage.numberOfSignals = BitConverter.ToUInt16(inputBuffer, 0);
                signalDataMessage.reserved1 = BitConverter.ToInt16(inputBuffer, 2);
                signalDataMessage.signalId = BitConverter.ToUInt16(inputBuffer, 4);
                signalDataMessage.numberOfValues = BitConverter.ToUInt16(inputBuffer, 6);

                if (signalDataMessage.signalId <= 2)
                {
                    // Initialize temporary output buffer
                    Int32[] samples = new Int32[signalDataMessage.numberOfValues];

                    // Maintain an offset in the buffer and parse through each sample.
                    int offset = 8;
                    for (int i = 0; i < signalDataMessage.numberOfValues; i++)
                    {
                        // Collect 3 bytes for a sample.
                        Byte low = inputBuffer[offset++];
                        Byte mid = inputBuffer[offset++];
                        Byte high = inputBuffer[offset++];

                        // Assemble the bytes into a 32-bit sample
                        samples[i] = ((high << 24) + (mid << 16) + (low << 8)) >> 8;
                    }

                    // Create a byte array with the samples for the output channel
                    byte[] outputBuffer = new byte[samples.Length * sizeof(Int32)];
                    Buffer.BlockCopy(samples, 0, outputBuffer, 0, outputBuffer.Length);

                    // Send data
                    outputSockets[signalDataMessage.signalId - 1].Send(outputBuffer);
                }

                // Increment the number of samples gathered for this signal ID.
                samplesReceived[signalDataMessage.signalId - 1] += signalDataMessage.numberOfValues;
                Console.WriteLine("Samples received: {0} {1} {2} {3}", samplesReceived[0], samplesReceived[1], samplesReceived[2], samplesReceived[3]);
            }
        }
    }
}
