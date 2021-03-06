﻿using System;
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
using System.Runtime.Serialization;
using System.ServiceModel.Dispatcher;

namespace PTP_MultiFrame
{
    class Program
    {
        static readonly string MASTER_IP = "10.100.35.79"; // IP address (or hostname) of the 3050 LAN-XI module (PTP master Frame master=slot 1)
        static readonly string MASTER_SLAVE_IP = "10.100.35.157"; // IP address (or hostname) of the 3160 LAN-XI module (Frame slave)
        static readonly string SLAVE_IP = "10.100.35.192"; // IP address (or hostname) of the 3160 LAN-XI module (PTP Slave  Frame master=slot 1)

        static readonly string OUTPUT_FILE = "LANXI.out"; // Path to the file where the samples received should be stored. Path is relative to the executed .exe file.
        static readonly int SAMPLES_TO_RECEIVE = 4096 * 64; // Number of samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s.

        static int[,] samplesReceived = { { 0, 0 }, { 0, 0 }, { 0, 0 } }; // Used to count the number of samples received - for demo purpose. Array of one array per LAN-XI module, containing one integer per input channel.
        static Socket[] sock; // Array of sockets used when fetching samples from the LAN-XI modules

        static ArrayList[,] outputSamples = {{new ArrayList(), new ArrayList()},  // Buffers for storing samples fetched from the LAN-XI modules. Array of one array per LAN-XI module, containing one ArrayList per input channel.
                                             {new ArrayList(), new ArrayList()},
                                             {new ArrayList(), new ArrayList()}};
        static ArrayList socketBuffer = new ArrayList(); // Buffer for incoming bytes.
        static int NumberOfModules = 3;

        static int samples_to_receive = SAMPLES_TO_RECEIVE;

        static void Main(string[] args)
        {
            Dictionary<string, dynamic> dict;
            int i;

            // Use program arguments if specified, otherwise use constants.
            // Arguments are in the sequence [[[<master_ip> <slave_ip>] <output_file>] <samples_to_receive]
            string master_ip = MASTER_IP;
            string master_slave_ip = MASTER_SLAVE_IP;
            string slave_ip = SLAVE_IP;
            string output_file = OUTPUT_FILE;
            if (args.Length >= 2)
            {
                master_ip = args[0];
                master_slave_ip = args[1];
                slave_ip = args[2];
            }
            if (args.Length >= 4)
            {
                output_file = args[3];
            }
            if (args.Length >= 5)
            {
                samples_to_receive = Convert.ToInt32(args[4]);
            }

            // Instantiate Socket array.
            sock = new Socket[NumberOfModules];

            // Initialize boundary objects
            LanXIRESTBoundary master = new LanXIRESTBoundary(master_ip);
            LanXIRESTBoundary master_slave = new LanXIRESTBoundary(master_slave_ip);
            LanXIRESTBoundary slave = new LanXIRESTBoundary(slave_ip);

            // Start measurement
            // During this process commands are generally performed on SLAVEs first, finished with MASTER

            // Set synchronization mode on the LAN-XI modules. The body tells which module is master and which is slave.
            //  Body for each module is prepared in SyncParametersMaster.json and SyncParametersSlave.json
            // For this command, the MASTER should be treated first, then the slave(s)
 
            string syncParametersMaster = File.ReadAllText(@"PTP_MultiFrame_SyncParametersMaster.json");  //PTP master and frame master
            master.PutRequestWithPath("/rest/rec/syncmode", syncParametersMaster);
            string syncParametersSlave = File.ReadAllText(@"PTP_MultiFrame_SyncParametersSlave.json");    //PTP slave and frame master
            slave.PutRequestWithPath("/rest/rec/syncmode", syncParametersSlave);

            // Wait until PTP is locked on all modules
            slave.WaitForPtpState("Locked");
            master.WaitForPtpState("Locked");

            // Open the Recorder application on the modules. The same body is sent to both modules, and is prepared in OpenParameters.json
            string openParameters = File.ReadAllText(@"PTP_MultiFrame_OpenParameters.json");
            slave.PutRequestWithPath("/rest/rec/open", openParameters);
            master_slave.PutRequestWithPath("/rest/rec/open", openParameters);
            master.PutRequestWithPath("/rest/rec/open", openParameters);

            // Prepare generator
            string outputChannelStart = File.ReadAllText(@"PTP_MultiFrame_OutputChannelStart.json");
            slave.PutRequestWithPath("/rest/rec/generator/prepare", outputChannelStart);

            // Configure generator channels
            string outputChannelConfiguration = File.ReadAllText(@"PTP_MultiFrame_OutputChannelSetup.json");
            slave.PutRequestWithPath("/rest/rec/generator/output", outputChannelConfiguration);
 
            // Start output
            slave.PutRequestWithPath("/rest/rec/generator/start", outputChannelStart);
            master.PutRequestWithPath("/rest/rec/apply", null);

            // Wait for all modules to be ready; Input in Sampling state, and module in the RecorderOpened state.
            slave.WaitForInputState("Sampling");
            master_slave.WaitForInputState("Sampling");
            master.WaitForInputState("Sampling");
            slave.WaitForRecorderState("RecorderOpened");
            master_slave.WaitForRecorderState("RecorderOpened");
            master.WaitForRecorderState("RecorderOpened");

            // Create Recorder configuration on all modules
            slave.PutRequestWithPath("/rest/rec/create", null);
            master_slave.PutRequestWithPath("/rest/rec/create", null);
            master.PutRequestWithPath("/rest/rec/create", null);

            // Wait for all modules to be in the RecorderConfiguring state.
            slave.WaitForRecorderState("RecorderConfiguring");
            master_slave.WaitForRecorderState("RecorderConfiguring");
            master.WaitForRecorderState("RecorderConfiguring");

            // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default.
            // The body has been prepared and stored in PTPInputStreamingOutputGenerator_InputChannelSetup.json. In this example the setup is identical for the two modules, but it may differ as needed.
            string InputChannelConfiguration = File.ReadAllText(@"PTP_MultiFrame_InputChannelSetup.json");
            slave.PutRequestWithPath("/rest/rec/channels/input", InputChannelConfiguration);
            master_slave.PutRequestWithPath("/rest/rec/channels/input", InputChannelConfiguration);
            master.PutRequestWithPath("/rest/rec/channels/input", InputChannelConfiguration);

            // Wait until all modules enter the Settled input state
            slave.WaitForInputState("Settled");
            master_slave.WaitForInputState("Settled");
            master.WaitForInputState("Settled");

            // Synchronize modules.
            slave.PutRequestWithPath("/rest/rec/synchronize", null);
            master_slave.PutRequestWithPath("/rest/rec/synchronize", null);
            master.PutRequestWithPath("/rest/rec/synchronize", null);

            // Wait for all modules to enter the Synchronized input state
            slave.WaitForInputState("Synchronized");
            master_slave.WaitForInputState("Synchronized");
            master.WaitForInputState("Synchronized");

            // Start streaming between internally in the LAN-XI modules.
            slave.PutRequestWithPath("/rest/rec/startstreaming", null);
            master_slave.PutRequestWithPath("/rest/rec/startstreaming", null);
            master.PutRequestWithPath("/rest/rec/startstreaming", null);

            // Wait for all modules to enter the RecorderStreaming state
            slave.WaitForRecorderState("RecorderStreaming");
            master_slave.WaitForRecorderState("RecorderStreaming");
            master.WaitForRecorderState("RecorderStreaming");

            // Get the TCP ports provided by each LAN-XI module for streaming samples
            dict = master.GetRequestWithPath("/rest/rec/destination/socket");
            UInt16 slavePort = (UInt16)dict["tcpPort"];
            dict = master_slave.GetRequestWithPath("/rest/rec/destination/socket");
            UInt16 masterslavePort = (UInt16)dict["tcpPort"];
            dict = slave.GetRequestWithPath("/rest/rec/destination/socket");
            UInt16 masterPort = (UInt16)dict["tcpPort"];

            // Connect to sockets. Let ConnectStreams() handle this.
            ConnectStreams(new string[] { master_ip, master_slave_ip, slave_ip }, new UInt16[] { masterPort, masterslavePort, slavePort });

            // Start measuring.
            slave.PostRequestWithPath("/rest/rec/measurements", null);
            master_slave.PostRequestWithPath("/rest/rec/measurements", null);
            master.PostRequestWithPath("/rest/rec/measurements", null);

            // Wait for modules to enter RecorderRecording state
            slave.WaitForRecorderState("RecorderRecording");
            master_slave.WaitForRecorderState("RecorderRecording");
            master.WaitForRecorderState("RecorderRecording");

            // Streaming should now be running.

            // Let Stream() method handle streaming
            Stream();

            // Stop measuring and close sockets for all modules.
            // During this process commands are generaly performed on MASTER module first, then on SLAVEs

            // Stop measurement on modules
            master.PutRequestWithPath("/rest/rec/measurements/stop", null);
            master_slave.PutRequestWithPath("/rest/rec/measurements/stop", null);
            slave.PutRequestWithPath("/rest/rec/measurements/stop", null);

            // Wait for all modules to enter RecorderStreaming state
            master.WaitForRecorderState("RecorderStreaming");
            master_slave.WaitForRecorderState("RecorderStreaming");
            slave.WaitForRecorderState("RecorderStreaming");

            // Close sockets
            for (i = 0; i < NumberOfModules; i++)
            {
                sock[i].Shutdown(SocketShutdown.Both);
                sock[i].Close();
            }

            // Finish recording
            master.PutRequestWithPath("/rest/rec/finish", null);
            master_slave.PutRequestWithPath("/rest/rec/finish", null);
            slave.PutRequestWithPath("/rest/rec/finish", null);

            // Stop output
            //            master.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);
            //            slave.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);

            // Wait for modules to enter the RecorderOpened state
            master.WaitForRecorderState("RecorderOpened");
            master_slave.WaitForRecorderState("RecorderOpened");
            slave.WaitForRecorderState("RecorderOpened");

            // Close Recorder application on all modules
            master.PutRequestWithPath("/rest/rec/close", null);
            master_slave.PutRequestWithPath("/rest/rec/close", null);
            slave.PutRequestWithPath("/rest/rec/close", null);

            // Wait for modules to enter the Idle state
            master.WaitForRecorderState("Idle");
            master_slave.WaitForRecorderState("Idle");
            slave.WaitForRecorderState("Idle");

            // Write collected samples to output file
            StreamWriter file = new StreamWriter(output_file);
            for (int j = 0; j < outputSamples[0, 0].Count; j++)
            {
                file.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                    outputSamples[0, 0][j], outputSamples[0, 1][j], outputSamples[1, 0][j], outputSamples[1, 1][j], outputSamples[2, 0][j], outputSamples[2, 1][j]);
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
            while (samplesReceived[0, 0] < samples_to_receive || samplesReceived[0, 1] < samples_to_receive ||
                   samplesReceived[1, 0] < samples_to_receive || samplesReceived[1, 1] < samples_to_receive ||
                   samplesReceived[2, 0] < samples_to_receive || samplesReceived[2, 1] < samples_to_receive)
            {
                // In this example modules are handled in turns. This assumes that sample rate / message size is equal for all modules.
                for (int i = 0; i < NumberOfModules; i++)
                {
                    StreamingHeader streamingHeader = ReadHeader(i);
                    ReadMessage(i, (int)streamingHeader.dataLength, streamingHeader.messageType);
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

            Console.WriteLine("Module {0} time: 0x{1:x} 0x{2:x}", moduleNo, streamingHeader.timestampFamily, streamingHeader.timestamp);

            return streamingHeader;
        }

        /// <summary>
        /// Read the message following the StreamingHeader from the socket data stream.
        /// </summary>
        /// <param name="moduleNo">Module index. Module 0 = master 1+ = slaves</param>
        /// <param name="dataLength">Number of bytes to read and parse.</param>
        /// <param name="messageType">Type of message to parse. 1 means SignalData, and is the only one supported. Other types are ignored.</param>
        public static void ReadMessage(int moduleNo, int dataLength, UInt16 messageType)
        {
            // Initialize a temporary buffer
            byte[] inputBuffer = new byte[dataLength];
            int bytesReceived = 0;

            // Read the full message contents into the temporary buffer
            while (bytesReceived < dataLength)
                bytesReceived += sock[moduleNo].Receive(inputBuffer, bytesReceived, dataLength - bytesReceived, SocketFlags.None);

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
                    outputSamples[moduleNo, signalDataMessage.signalId - 1].Add(sample);

                    // Increment the number of samples gathered for this signal ID.
                    samplesReceived[moduleNo, signalDataMessage.signalId - 1]++;
                }
                Console.WriteLine("Samples received in module {0}: {1} {2}", moduleNo, samplesReceived[moduleNo, 0], samplesReceived[moduleNo, 1]);
            }
        }
    }
}
