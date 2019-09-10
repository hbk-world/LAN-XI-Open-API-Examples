// Simple example streaming from all inputs of a 3160 module and storing a few samples in a file. The file may be read using e.g. Matlab, Microsoft Excel or LibreOffice Calc.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Shared;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;

namespace InputStreaming
{
    class Program
    {
        static readonly string LANXI_IP = "10.100.35.199"; // IP address (or hostname) of the LAN-XI module
        static readonly string OUTPUT_FILE = "LANXI.out"; // Path to the file where the samples received should be stored
        static readonly int SAMPLES_TO_RECEIVE = 4096 * 4; // Number of samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s

        static int[] samplesReceived = {0, 0, 0, 0}; // Used to count the number of samples received - for demo purpose
        static Socket sock;

        static ArrayList[] outputSamples = {new ArrayList(), new ArrayList(), new ArrayList(), new ArrayList()}; // Pointers to output buffers
        static ArrayList socketBuffer = new ArrayList(); // Buffer for incoming bytes from module.

        static int samples_to_receive = SAMPLES_TO_RECEIVE;

        static void Main(string[] args)
        {
            // Use program arguments if specified, otherwise use constants.
            // Arguments are in the sequence [[[<lanxi_ip>] <output_file>] <samples_to_receive>]
            string lanxi_ip = LANXI_IP;
            string output_file = OUTPUT_FILE;
            if (args.Length >= 1)
            {
                lanxi_ip = args[0];
            }
            if (args.Length >= 2)
            {
                output_file = args[1];
            }
            if (args.Length >= 3)
            {
                samples_to_receive = Convert.ToInt32(args[2]);
            }

            Dictionary<string, dynamic> dict;

            // Initialize boundary objects
            LanXIRESTBoundary rest = new LanXIRESTBoundary(lanxi_ip);

            // Start measurement
            string syncParametersMaster = "{\"synchronization\": {\r\n\"mode\": \"ptp\",\r\n\"domain\": 11,\"preferredMaster\": true } } }";
            dict = rest.PutRequestWithPath("/rest/rec/syncmode", syncParametersMaster);
            if (dict == null)
            {
                Console.WriteLine("\r\nPress ENTER to terminate");
                Console.ReadLine();
                return;
            }
            // Open the Recorder application on the LAN-XI module
            rest.PutRequestWithPath("/rest/rec/open", null);

            // Wait for all modules to be ready; Input in Sampling state, and module in the RecorderOpened state.
            rest.WaitForInputState("Sampling");
            rest.WaitForRecorderState("RecorderOpened");

            // Create Recorder configuration
            rest.PutRequestWithPath("/rest/rec/create", null);
            rest.WaitForRecorderState("RecorderConfiguring");

            // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default
            // In this example a JSON file has been prepared with the desired configuration.
            string inputChannelConfiguration = File.ReadAllText(@"InputStreaming_InputChannelSetup.json");
            rest.PutRequestWithPath("/rest/rec/channels/input", inputChannelConfiguration);
            rest.WaitForRecorderState("RecorderStreaming");

            dict = rest.PutRequestWithPath("/rest/rec/channels/input/bridgeNulling",
                         "{\"channels\" : [{\"nulling\" : \"Automatic\", \"channel\" : 1},{\"nulling\" : \"Automatic\", \"channel\" : 2},{\"nulling\" : \"Automatic\", \"channel\" : 3}]}");

            double val1 = (double)dict["channels"][0]["nullingVoltage"];
            double val2 = (double)dict["channels"][1]["nullingVoltage"];
            double val3 = (double)dict["channels"][2]["nullingVoltage"];
            Console.WriteLine("Nulling: {0}, {1}, {2}", val1, val2, val3);
            // Get the TCP port provided by the LAN-XI module for streaming samples
            dict = rest.GetRequestWithPath("/rest/rec/destination/socket");

            UInt16 port = (UInt16)dict["tcpPort"];
            Console.WriteLine("Streaming TCP port: {0}", port);

            // Start measuring
            rest.PostRequestWithPath("/rest/rec/measurements", null);
            rest.WaitForRecorderState("RecorderRecording");

            // Streaming should now be running

            // Let connectSocketAndStream() method handle socket connection
            // The socket connection may be established while the Recorder was in the "RecorderStreaming" state
            ConnectSocketAndStream(lanxi_ip, port);

            // Stop measuring and close socket
            rest.PutRequestWithPath("/rest/rec/measurements/stop", null);
            rest.WaitForRecorderState("RecorderStreaming");
            sock.Shutdown(SocketShutdown.Both);
            sock.Close();

            rest.PutRequestWithPath("/rest/rec/finish", null);
            rest.WaitForRecorderState("RecorderOpened");

            // Close Recorder application
            rest.PutRequestWithPath("/rest/rec/close", null);
            rest.WaitForRecorderState("Idle");

            StreamWriter file = new StreamWriter(output_file);
            for (int i = 0; i < outputSamples[0].Count; i++)
            {
                file.WriteLine("{0}\t{1}\t{2}\t{3}", outputSamples[0][i], outputSamples[1][i], outputSamples[2][i], /*outputSamples[3][i]*/0);
            }
            file.Close();
        }

        /// <summary>
        /// Sets up socket communication
        /// </summary>
        public static void ConnectSocketAndStream(string address, UInt16 port)
        {
            // Connect to the streaming port on the LAN-XI module
            IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostAddresses(address)[0], port);
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Connect(remoteEP);

            // Socket is connected and everything is ready to go. Run until the desired number of samples have been received.
            while (samplesReceived[0] < samples_to_receive || samplesReceived[1] < samples_to_receive || samplesReceived[2] < samples_to_receive /*|| samplesReceived[3] < samples_to_receive*/)
            {
                StreamingHeader streamingHeader = ReadHeader();
                ReadMessage((int)streamingHeader.dataLength, streamingHeader.messageType);
            }
        }

        /// <summary>
        /// Reads the StreamingHeader from the socket data stream.
        /// </summary>
        /// <returns>StreamingHeader struct describing the following message.</returns>
        public static StreamingHeader ReadHeader()
        {
            // Initialize StreamingHeader struct and temporary buffer for processing
            StreamingHeader streamingHeader;
            byte[] inputBuffer = new byte[28];
            int bytesReceived = 0;

            // Receive the header
            while (bytesReceived < inputBuffer.Length)
                bytesReceived += sock.Receive(inputBuffer, bytesReceived, inputBuffer.Length - bytesReceived, SocketFlags.None);

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
        public static void ReadMessage(int dataLength, UInt16 messageType)
        {
            // Initialize a temporary buffer
            byte[] inputBuffer = new byte[dataLength];
            int bytesReceived = 0;

            // Read the full message contents into the temporary buffer
            while (bytesReceived < dataLength)
                bytesReceived += sock.Receive(inputBuffer, bytesReceived, dataLength-bytesReceived, SocketFlags.None);

            // Simple examples - we only care about signal data.
            if (messageType == 1)
            {
                // Populate a header struct
                SignalDataMessage signalDataMessage;
                signalDataMessage.numberOfSignals = BitConverter.ToUInt16(inputBuffer, 0);
                signalDataMessage.reserved1 = BitConverter.ToInt16(inputBuffer, 2);
                signalDataMessage.signalId = BitConverter.ToUInt16(inputBuffer, 4);
                signalDataMessage.numberOfValues = BitConverter.ToUInt16(inputBuffer, 6);

                // Maintain an offset in the buffer and parse through each sample.
                int offset = 8;
                for (int i = 0; i < signalDataMessage.numberOfValues; i++)
                {
                    // Collect 3 bytes for a sample.
                    Byte low = inputBuffer[offset++];
                    Byte mid = inputBuffer[offset++];
                    Byte high = inputBuffer[offset++];

                    // Assemble the bytes into a 24-bit sample
                    Int32 sample = ((high << 24) + (mid << 16) + (low << 8)) >> 8;

                    // Store sample in output ArrayList
                    outputSamples[signalDataMessage.signalId - 1].Add(sample);

                    // Increment the number of samples gathered for this signal ID.
                    samplesReceived[signalDataMessage.signalId - 1]++;
                }
                Console.WriteLine("Samples received: {0} {1} {2} {3}", samplesReceived[0], samplesReceived[1], samplesReceived[2], samplesReceived[3]);
            }
        }
    }
}
