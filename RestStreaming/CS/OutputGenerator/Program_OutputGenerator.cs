// Simple example using the built-in generators of a 3160 module for outputting a signal.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared;
using System.IO;
using System.Threading;

namespace OutputGenerator
{
    class Program
    {
        static readonly string LANXI_IP = "10.10.3.1"; // IP address (or hostname) of the LAN-XI module
        static readonly int OUTPUT_TIME = 5000; // Number of milliseconds to play using each configuration
        static readonly int IN_A_FRAME = 1; //

        static void Main(string[] args)
        {
            // Use program arguments if specified, otherwise use constants
            // Arguments are in the sequence [[[<lanxi_ip>] <output_time>] <output_time2>]
            string lanxi_ip = LANXI_IP;
            int output_time1 = OUTPUT_TIME;
            int output_time2 = OUTPUT_TIME;
            if (args.Length >= 1)
            {
                lanxi_ip = args[0];
            }
            if (args.Length >= 2)
            {
                output_time1 = Convert.ToInt32(args[1]);
                output_time2 = output_time1;
            }
            if (args.Length >= 3)
            {
                output_time2 = Convert.ToInt32(args[2]);
            }

            LanXIRESTBoundary rest = new LanXIRESTBoundary(lanxi_ip);

            // Open recorder application
            rest.PutRequestWithPath("/rest/rec/open", null);

            // Prepare generator
            string outputChannelStart = File.ReadAllText(@"OutputGenerator_OutputChannelStart.json");
            rest.PutRequestWithPath("/rest/rec/generator/prepare", outputChannelStart);

            // Configure generator channels
            string outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetup.json");
            rest.PutRequestWithPath("/rest/rec/generator/output", outputChannelConfiguration);

            // Start output
            rest.PutRequestWithPath("/rest/rec/generator/start", outputChannelStart);
            if (IN_A_FRAME != 0)
                rest.PutRequestWithPath("/rest/rec/apply", null);

            Thread.Sleep(output_time1);

            // Change generator configuration (frequencies and amplitude)
            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetup2.json");
            rest.PutRequestWithPath("/rest/rec/generator/output", outputChannelConfiguration);

            Thread.Sleep(output_time2);

            // Stop output
            rest.PutRequestWithPath("/rest/rec/generator/stop", outputChannelStart);

            // Close recorder application
            rest.PutRequestWithPath("/rest/rec/close", null);
        }
    }
}
