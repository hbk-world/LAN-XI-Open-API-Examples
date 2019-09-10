// Simple example outputting samples generated on the PC through a 3160 module.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace OutputStreaming
{
    class Program
    {
        static readonly string LANXI_IP = "10.10.3.1"; // IP address (or hostname) of the LAN-XI module
        static readonly int FREQ1 = 784; // Frequency to generate for channel 1
        static readonly int FREQ2 = 659; // Frequency to generate for channel 2
        public static readonly int SAMPLES_TO_PRIME = 3 * 6000; // Number of samples to prime into the generator buffers before starting the output
        static readonly int IN_A_FRAME = 1; //

        static int[] samplesSent = { 0, 0 };

        static void Main(string[] args)
        {
            // Use program arguments if specified, otherwise use constants.
            // Arguments are in the sequence [[[<lanxi_ip>] <freq1>] <freq2>]
            string lanxi_ip = LANXI_IP;
            int freq1 = FREQ1;
            int freq2 = FREQ2;
            if (args.Length >= 1)
            {
                lanxi_ip = args[0];
            }
            if (args.Length >= 3)
            {
                freq1 = Convert.ToInt32(args[1]);
                freq2 = Convert.ToInt32(args[2]);
            }
            Dictionary<string, dynamic> dict;

            LanXIRESTBoundary rest = new LanXIRESTBoundary(lanxi_ip);

            string syncParametersMaster = "{\"synchronization\": {\r\n\"mode\": \"stand-alone\" } } }";
            dict = rest.PutRequestWithPath("/rest/rec/syncmode", syncParametersMaster);
            if (dict == null)
            {
                Console.WriteLine("\r\nPress ENTER to terminate");
                Console.ReadLine();
                return;
            }

            // Open recorder application
            rest.PutRequestWithPath("/rest/rec/open", null);

            // Prepare generator
            string outputChannelStart = File.ReadAllText(@"OutputStreaming_OutputChannelStartStreaming.json");
            rest.PutRequestWithPath("/rest/rec/generator/prepare", outputChannelStart);

            // Configure generator channels
            string outputChannelConfiguration = File.ReadAllText(@"OutputStreaming_OutputChannelSetupStreaming.json");
            rest.PutRequestWithPath("/rest/rec/generator/output", outputChannelConfiguration);

            // Get port numbers to send samples to
            dict = rest.GetRequestWithPath("/rest/rec/generator/output");
            UInt16 port1 = (UInt16)dict["outputs"][0]["inputs"][0]["port"];
            UInt16 port2 = (UInt16)dict["outputs"][1]["inputs"][0]["port"];

            Console.WriteLine("Streaming TCP ports: {0} - {1}", port1, port2);

            // Let OutputHelper connect to the sockets and stream data
            OutputHelper output1Helper = new OutputHelper(lanxi_ip, port1, freq1);
            OutputHelper output2Helper = new OutputHelper(lanxi_ip, port2, freq2);
            Thread output1Thread = new Thread(new ThreadStart(output1Helper.StreamToChannel));
            Thread output2Thread = new Thread(new ThreadStart(output2Helper.StreamToChannel));
            output1Thread.Start();
            output2Thread.Start();

            // Wait until channels are primed
            while (!output1Helper.primed || !output2Helper.primed) Thread.Sleep(100);
            Console.WriteLine("Channels primed");

            // Start output
            rest.PutRequestWithPath("/rest/rec/generator/start", outputChannelStart);
            if (IN_A_FRAME != 0)
                rest.PutRequestWithPath("/rest/rec/apply", null);

            // Wait for output sampling to finish
            while (output1Thread.IsAlive || output2Thread.IsAlive)
                Thread.Sleep(10);

            // Stop output
            rest.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);

            // Close recorder application
            rest.PutRequestWithPath("/rest/rec/close", null);
        }

    }

    class OutputHelper
    {
        public string host { get; set; }
        public int port { get; set; }
        public int freq { get; set; }
        public bool primed { get; set; }

        public OutputHelper(string host, int port, int freq)
        {
            this.host = host;
            this.port = port;
            this.freq = freq;
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
            UInt32 numberOfSamples = 131072 * 10;
            Int32[] sampleBuf = new Int32[numberOfSamples];

            for (int i = 0; i < numberOfSamples; i++)
            {
                double val = Math.Sin(i * 2 * Math.PI * freq / 131072);
                sampleBuf[i] = (Int32)(val * 8372224);
            }

            // Create byte array representing the signal to send
            byte[] outputBuffer = new byte[sampleBuf.Length * sizeof(Int32)];
            Buffer.BlockCopy(sampleBuf, 0, outputBuffer, 0, outputBuffer.Length);

            // Send samples.
            int sent = 0;
            while (sent < outputBuffer.Length)
            {
                sent += sock.Send(outputBuffer, sent, 4096*4, SocketFlags.None);

                // Check if the channel buffers are primed and ready to output
                if (!primed && sent >= Program.SAMPLES_TO_PRIME * sizeof(Int32))
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
