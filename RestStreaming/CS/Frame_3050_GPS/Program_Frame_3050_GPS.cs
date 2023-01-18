using System;
using System.Collections.Generic;
using System.Collections;
using Shared;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace Frame_3050_GPS
{
    class Program
    {
        static readonly string MASTER_IP = "192.168.1.10"; // IP address (or hostname) of the 3050 LAN-XI module (Frame master=slot 1)
        static readonly string OUTPUT_FILE = "LANXI.out"; // Path to the file where the samples received should be stored. Path is relative to the executed .exe file.
        static readonly int SAMPLES_TO_RECEIVE = 4096 * 64; // Number of samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s.

        static int samplesReceived = 0; // Used to count the number of samples received - for demo purpose. Array of one array per LAN-XI module, containing one integer per input channel.
        static Socket[] sock; // Array of sockets used when fetching samples from the LAN-XI modules

        static ArrayList[] outputSamples = { new ArrayList() };  // Buffers for storing samples fetched from the LAN-XI modules. Array of one array per LAN-XI module, containing one ArrayList per input channel.
        static ArrayList socketBuffer = new ArrayList(); // Buffer for incoming bytes.
        static int NumberOfModules = 1;

        static int samples_to_receive = SAMPLES_TO_RECEIVE;

        static void Main(string[] args)
        {
            Dictionary<string, dynamic> dict;
            int i;

            // Use program arguments if specified, otherwise use constants.
            // Arguments are in the sequence [[[<master_ip> <slave_ip>] <output_file>] <samples_to_receive]
            string master_ip = MASTER_IP;
            string output_file = OUTPUT_FILE;
            if (args.Length >= 1)
            {
                master_ip = args[0];
            }
            if (args.Length >= 2)
            {
                output_file = args[1];
            }
            if (args.Length >= 3)
            {
                samples_to_receive = Convert.ToInt32(args[2]);
            }

            // Instantiate Socket array.
            sock = new Socket[NumberOfModules];

            // Initialize boundary objects
            LanXIRESTBoundary master = new LanXIRESTBoundary(master_ip);

            // Set sync mode to stand-alone
            master.PutRequestWithPath("/rest/rec/syncmode", "{\"synchronization\": {\"mode\": \"stand-alone\",\"usegps\": true}}");

            master.WaitForPtpState("Locked");

            // Start measurement
            // During this process commands are generally performed on SLAVEs first, finished with MASTER

            // Open the Recorder application on the modules. The same body is sent to both modules, and is prepared in OpenParameters.json
            string openParameters = File.ReadAllText(@"Frame_3050_GPS_OpenParameters.json");
            master.PutRequestWithPath("/rest/rec/open", openParameters);

            // Wait for all modules to be ready; Input in Sampling state, and module in the RecorderOpened state.
            master.WaitForInputState("Sampling");
            master.WaitForRecorderState("RecorderOpened");

            // Create Recorder configuration on all modules
            master.PutRequestWithPath("/rest/rec/create", null);

            // Wait for all modules to be in the RecorderConfiguring state.
            master.WaitForRecorderState("RecorderConfiguring");

            // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default.
            // The body has been prepared and stored in PTPInputStreamingOutputGenerator_InputChannelSetup.json. In this example the setup is identical for the two modules, but it may differ as needed.
            string MasterInputChannelConfiguration = File.ReadAllText(@"Frame_3050_GPS_MasterInputChannelSetup.json");
            master.PutRequestWithPath("/rest/rec/channels/input", MasterInputChannelConfiguration);

            // Wait until all modules enter the Settled input state
            master.WaitForInputState("Settled");

            // Synchronize modules.
            master.PutRequestWithPath("/rest/rec/synchronize", null);

            // Wait for all modules to enter the Synchronized input state
            master.WaitForInputState("Synchronized");

            // Start streaming between internally in the LAN-XI modules.
            master.PutRequestWithPath("/rest/rec/startstreaming", null);

            // Wait for all modules to enter the RecorderStreaming state
            master.WaitForRecorderState("RecorderStreaming");

            // Get the TCP ports provided by each LAN-XI module for streaming samples
            dict = master.GetRequestWithPath("/rest/rec/destination/socket");
            UInt16 masterPort = (UInt16)dict["tcpPort"];

            // Connect to sockets. Let ConnectStreams() handle this.
            ConnectStreams(new string[] { master_ip }, new UInt16[] { masterPort });

            // Start measuring.
            master.PostRequestWithPath("/rest/rec/measurements", null);

            // Wait for modules to enter RecorderRecording state
            master.WaitForRecorderState("RecorderRecording");

            // Streaming should now be running.

            // Let Stream() method handle streaming
            Stream();

            // Stop measuring and close sockets for all modules.
            // During this process commands are generaly performed on MASTER module first, then on SLAVEs

            // Stop measurement on modules
            master.PutRequestWithPath("/rest/rec/measurements/stop", null);

            // Wait for all modules to enter RecorderStreaming state
            master.WaitForRecorderState("RecorderStreaming");

            // Close sockets
            for (i = 0; i < NumberOfModules; i++)
            {
                sock[i].Shutdown(SocketShutdown.Both);
                sock[i].Close();
            }

            // Finish recording
            master.PutRequestWithPath("/rest/rec/finish", null);

            // Stop output
            //            master.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);
            //            slave.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);

            // Wait for modules to enter the RecorderOpened state
            master.WaitForRecorderState("RecorderOpened");

            // Close Recorder application on all modules
            master.PutRequestWithPath("/rest/rec/close", null);

            // Wait for modules to enter the Idle state
            master.WaitForRecorderState("Idle");

            // Write collected samples to output file
            StreamWriter file = new StreamWriter(output_file);
            for (int j = 0; j < outputSamples[0].Count; j++)
            {
                file.WriteLine("{0}",
                    outputSamples[0][j]);
            }
            file.Close();
        }

        /// <summary>
        /// Connects the streaming sockets to the LAN-XI modules defined by address/port.
        /// </summary>
        /// <param name="address">Array of IP addresses/hostnames to connect to. The array must have NumberOfModules elements</param>
        /// <param name="port">Array of TCP port numbers to connect to. The array must have NumberOfModules elements</param>
        public static void ConnectStreams(string[] address, UInt16[] port)
        {
            // Connect to the streaming ports on the LAN-XI modules
            for (int i = 0; i < NumberOfModules; i++)
            {
                Thread.Sleep(50);
                IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostAddresses(address[i])[0], port[i]);
                sock[i] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock[i].Connect(remoteEP);
            }
        }

        /// <summary>
        /// Streams samples from LAN-XI modules. Assumes that sockets have been set up properly and connected.
        /// </summary>
        public static void Stream()
        {
            // Socket is connected and everything is ready to go. Run until the desired number of samples have been received for all input channels.
            while (samplesReceived < samples_to_receive)
            {
                // In this example modules are handled in turns. This assumes that sample rate / message size is equal for all modules.
                for (int i = 0; i < NumberOfModules; i++)
                {
                    StreamingHeader streamingHeader = ReadHeader(i);
                    ReadMessage(i, (int)streamingHeader.dataLength, streamingHeader);
                }
            }
        }

        /// <summary>
        /// Reads the StreamingHeader from the socket data stream.
        /// </summary>
        /// <param name="moduleNo">Module index. Module 0 = master 1+ = slaves</param>
        /// <returns>StreamingHeader struct describing the following message.</returns>
        public static StreamingHeader ReadHeader(int moduleNo)
        {
            // Initialize StreamingHeader struct and temporary buffer for processing
            StreamingHeader streamingHeader;
            byte[] inputBuffer = new byte[28];
            int bytesReceived = 0;

            // Receive the header
            while (bytesReceived < inputBuffer.Length)
                bytesReceived += sock[moduleNo].Receive(inputBuffer, bytesReceived, inputBuffer.Length - bytesReceived, SocketFlags.None);

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

 //           Console.WriteLine("Module {0} time: 0x{1:x} 0x{2:x}", moduleNo, streamingHeader.timestampFamily, streamingHeader.timestamp);

            return streamingHeader;
        }

        /// <summary>
        /// Read the message following the StreamingHeader from the socket data stream.
        /// </summary>
        /// <param name="moduleNo">Module index. Module 0 = master 1+ = slaves</param>
        /// <param name="dataLength">Number of bytes to read and parse.</param>
        /// <param name="messageType">Type of message to parse. 1 means SignalData, and is the only one supported. Other types are ignored.</param>
        static bool found = false;
        static bool armed = false;
        //       static Int32 sample_old = 0;
        public static void ReadMessage(int moduleNo, int dataLength, StreamingHeader streamingHeader)
        {
            // Initialize a temporary buffer
            byte[] inputBuffer = new byte[dataLength];
            int bytesReceived = 0;

            // Read the full message contents into the temporary buffer
            while (bytesReceived < dataLength)
                bytesReceived += sock[moduleNo].Receive(inputBuffer, bytesReceived, dataLength - bytesReceived, SocketFlags.None);

            // Simple examples - we only care about signal data.
            if (streamingHeader.messageType == 1)
            {
                // Populate a header struct
                SignalDataMessage signalDataMessage;
                signalDataMessage.numberOfSignals = BitConverter.ToUInt16(inputBuffer, 0);
                signalDataMessage.reserved1 = BitConverter.ToInt16(inputBuffer, 2);
                signalDataMessage.signalId = BitConverter.ToUInt16(inputBuffer, 4);
                signalDataMessage.numberOfValues = BitConverter.ToUInt16(inputBuffer, 6);

                // Maintain an offset in the buffer and parse through each sample.
                int offset = 8;
                for (ulong i = 0; i < signalDataMessage.numberOfValues; i++)
                {
                    // Collect 3 bytes for a sample.
                    Byte low = inputBuffer[offset++];
                    Byte mid = inputBuffer[offset++];
                    Byte high = inputBuffer[offset++];

                    // Assemble the bytes into a 24-bit sample
                    Int32 sample = ((high << 24) + (mid << 16) + (low << 8)) >> 8;

                    // Store sample in output ArrayList
                    outputSamples[0].Add(sample);
                    if (! armed)
                    {
                        if (sample < 300000)
                            armed = true;
                    }
                    else
                    if (! found)
                    {
                        if (sample >1000000)
                        {
                            ulong tid = streamingHeader.timestamp + (i << 15);
                            uint myhigh = (uint)(tid >> 32);
                            uint mylow = (uint)(tid & 0xFFFFFFFF);
                            Console.WriteLine("big value m={0}:s={1} i={2} t={3:X}:{4:X08} v={5}", moduleNo, samplesReceived, i, myhigh, mylow, sample);
                            found = true;
                        }
                    }

                    // Increment the number of samples gathered for this signal ID.
                    samplesReceived++;
                }
//                Console.WriteLine("Samples received in module {0}: {1} ", moduleNo, samplesReceived);
            }
        }
    }
}
