using BK.Lanxi.REST.Test.PowerControl;
using Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BK.Lanxi.REST.Test.StreamingOutputDelay
{
    class Program
    {
        static readonly string MODULE_IP = "10.116.122.167";
        static readonly string OUTPUT_FILE = "StreamingOutputDelay";
        static readonly int SAMPLES_TO_STREAM = 131072;
        static readonly int SAMPLES_TO_RECORD = 131072;
        static readonly int CHANNEL_TO_STORE = 1;
        static readonly int FREQ = 20000;
        static readonly uint FS = 131072;

        static readonly int RUNS = 10;

        static readonly int OUTPUT_SAMPLES_TO_PRIME = 1000;
        public static int output_samples_to_prime = OUTPUT_SAMPLES_TO_PRIME;

        static int[] samplesReceived = { 0, 0, 0, 0 };
        static Socket inputSocket;
        static ArrayList[] inputSamplesReceived = { new ArrayList(), new ArrayList(), new ArrayList(), new ArrayList() };
        static ArrayList inputSampleBuffer = new ArrayList();

        static int samples_to_record = SAMPLES_TO_RECORD;
        static UInt16 recorder_port;
        static uint fs = FS;

        static void Main(string[] args)
        {
            string module_ip = MODULE_IP;
            string output_file = OUTPUT_FILE;
            int samples_to_stream = SAMPLES_TO_STREAM;
            int freq = FREQ;
            int runs = RUNS;
            if (args.Length >= 2)
            {
                module_ip = args[0];
                module_ip = args[1];
            }
            if (args.Length >= 3)
                output_file = args[2];
            if (args.Length >= 5)
            {
                samples_to_stream = Convert.ToInt32(args[3]);
                samples_to_record = Convert.ToInt32(args[4]);
            }
            if (args.Length >= 6)
                freq = Convert.ToInt32(args[5]);
            if (args.Length >= 7)
                output_samples_to_prime = Convert.ToInt32(args[6]);
            if (args.Length >= 8)
                fs = Convert.ToUInt32(args[7]);
            if (args.Length >= 9)
                runs = Convert.ToInt32(args[8]);

            PowerControl.Actions.PowerCycle();

            for (int run = 0; run < runs; run++)
            {
                samplesReceived = new int[] { 0, 0, 0, 0 };
                inputSamplesReceived = new ArrayList[] { new ArrayList(), new ArrayList(), new ArrayList(), new ArrayList() };
                inputSampleBuffer = new ArrayList();

                LanXIRESTBoundary generator = new LanXIRESTBoundary(module_ip);
                LanXIRESTBoundary recorder = new LanXIRESTBoundary(module_ip);

                generator.PutRequestWithPath("/rest/rec/open", null);
                //recorder.PutRequestWithPath("/rest/rec/open", null);

                recorder.PutRequestWithPath("/rest/rec/create", null);

                string inputChannelConfiguration = File.ReadAllText(@"StreamingOutputDelay_InputChannelSetup.json");
                recorder.PutRequestWithPath("/rest/rec/channels/input", inputChannelConfiguration);

                Dictionary<string, dynamic> inputStreamingDestination = recorder.GetRequestWithPath("/rest/rec/destination/socket");
                UInt16 inputStreamingPort = (UInt16)inputStreamingDestination["tcpPort"];
                recorder_port = inputStreamingPort;

                string outputChannelStart = File.ReadAllText(@"StreamingOutputDelay_OutputChannelStart.json");
                generator.PutRequestWithPath("/rest/rec/generator/prepare", outputChannelStart);

                string outputChannelConfiguration = File.ReadAllText(@"StreamingOutputDelay_OutputChannelSetup.json");
                generator.PutRequestWithPath("/rest/rec/generator/output", outputChannelConfiguration);

                Dictionary<string, dynamic> streamingPorts = generator.GetRequestWithPath("/rest/rec/generator/output");
                UInt16 streamingPort1 = (UInt16)streamingPorts["outputs"][0]["inputs"][0]["port"];
                UInt16 streamingPort2 = (UInt16)streamingPorts["outputs"][1]["inputs"][0]["port"];

                OutputHelper streamingHelper1 = new OutputHelper(module_ip, streamingPort1, freq, fs);
                OutputHelper streamingHelper2 = new OutputHelper(module_ip, streamingPort2, freq, fs);
                Thread streamingThread1 = new Thread(new ThreadStart(streamingHelper1.StreamToChannel));
                Thread streamingThread2 = new Thread(new ThreadStart(streamingHelper2.StreamToChannel));
                streamingThread1.Start();
                streamingThread2.Start();

                Thread inputStreamingThread = new Thread(new ThreadStart(ConnectSocketAndStream));
                inputStreamingThread.Start();

                while (!streamingHelper1.primed || !streamingHelper2.primed) Thread.Sleep(10);

                recorder.PostRequestWithPath("/rest/rec/measurements", null);

                generator.PutRequestWithPath("/rest/rec/generator/start", outputChannelStart);

                while (streamingThread1.IsAlive || streamingThread2.IsAlive) Thread.Sleep(10);

                generator.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);

                while (inputStreamingThread.IsAlive) Thread.Sleep(10);

                recorder.PutRequestWithPath("/rest/rec/measurements/stop", null);

                inputSocket.Shutdown(SocketShutdown.Both);
                inputSocket.Close();

                recorder.PutRequestWithPath("/rest/rec/finish", null);

                //generator.PutRequestWithPath("/rest/rec/close", null);
                recorder.PutRequestWithPath("/rest/rec/close", null);

                StreamWriter file = new StreamWriter(output_file + "_" + run + ".out");
                for (int i = 0; i < inputSamplesReceived[0].Count; i++)
                {
                    file.WriteLine("{0}", inputSamplesReceived[CHANNEL_TO_STORE-1][i]);
                }
                file.Close();
            }
        }

        /// <summary>
        /// Sets up socket communication
        /// </summary>
        public static void ConnectSocketAndStream()//string address, UInt16 port)
        {
            string address = MODULE_IP;
            UInt16 port = recorder_port;
            // Connect to the streaming port on the LAN-XI module
            IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostAddresses(address)[0], port);
            inputSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            inputSocket.Connect(remoteEP);

            // Socket is connected and everything is ready to go. Run until the desired number of samples have been received.
            while (samplesReceived[0] < samples_to_record || samplesReceived[1] < samples_to_record || samplesReceived[2] < samples_to_record || samplesReceived[3] < samples_to_record)
            {
                StreamingHeader streamingHeader = ReadHeader();
                ReadMessage((int)streamingHeader.dataLength, streamingHeader.messageType);
            }

            //inputSocket.Shutdown(SocketShutdown.Both);
            //inputSocket.Close();
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

        static byte[] inputBuffer = new byte[32768];
        /// <summary>
        /// Read the message following the StreamingHeader from the socket data stream.
        /// </summary>
        /// <param name="dataLength">Number of bytes to read and parse.</param>
        /// <param name="messageType">Type of message to parse. 1 means SignalData, and is the only one supported. Other types are ignored.</param>
        public static void ReadMessage(int dataLength, UInt16 messageType)
        {
            // Initialize a temporary buffer
            //byte[] inputBuffer = new byte[dataLength];
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
                    inputSamplesReceived[signalDataMessage.signalId - 1].Add(sample);

                    // Increment the number of samples gathered for this signal ID.
                    samplesReceived[signalDataMessage.signalId - 1]++;
                }

                if (signalDataMessage.signalId == 4 && samplesReceived[3]%65536 == 0)
                    Console.WriteLine("Samples received: {0} {1} {2} {3}", samplesReceived[0], samplesReceived[1], samplesReceived[2], samplesReceived[3]);
            }
        }
    }

    class OutputHelper
    {
        public string host { get; set; }
        public int port { get; set; }
        public int freq { get; set; }
        public bool primed { get; set; }
        public uint fs { get; set; }

        public OutputHelper(string host, int port, int freq, uint fs)
        {
            this.host = host;
            this.port = port;
            this.freq = freq;
            this.fs = fs;
            this.primed = false;
        }

        /// <summary>
        /// Connects to the host/port specified in the object, generates a tone of the specified frequency and streams this to the connected host.
        /// </summary>
        public void StreamToChannel()
        {
            // Create socket and connect
            IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostAddresses(host)[0], port);
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Connect(remoteEP);

            // Generate buffer with a tone of the specified frequency
            UInt32 numberOfSamples = fs * 3;
            Int32[] sampleBuf = new Int32[numberOfSamples];

            for (int i = 0; i < numberOfSamples; i++)
            {
                double val = Math.Sin(i * 2 * Math.PI * freq / fs);
                sampleBuf[i] = (Int32)(val * 8372224);
            }

            // Create byte array representing the signal to send
            byte[] outputBuffer = new byte[sampleBuf.Length * sizeof(Int32)];
            Buffer.BlockCopy(sampleBuf, 0, outputBuffer, 0, outputBuffer.Length);

            // Send samples.
            int sent = 0;
            while (sent < outputBuffer.Length)
            {
                sent += sock.Send(outputBuffer, sent, 4096 * 4, SocketFlags.None);

                // Check if the channel buffers are primed and ready to output
                if (!primed && sent >= Program.output_samples_to_prime * sizeof(Int32))
                {
                    Console.WriteLine("Channel primed with {0} bytes", sent);
                    primed = true;
                }
            }

            // Close socket
            sock.Close();
        }

    }
}
