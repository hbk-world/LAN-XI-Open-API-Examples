// Example using the built-in generator of a 3677 module for outputting a signal.
// 3677 has four input and one output channel

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
            int i;
            if (args.Length >= 1)
            {
                lanxi_ip = args[0];
            }
            if (args.Length >= 2)
            {
                output_time1 = Convert.ToInt32(args[1]);
            }

            LanXIRESTBoundary rest = new LanXIRESTBoundary(lanxi_ip);
            string state = rest.GetRecorderState();
            if (state.Equals("Idle"))
            {
                // Open recorder application
                rest.PutRequestWithPath("/rest/rec/open", null);
                rest.WaitForRecorderState("RecorderOpened");

                // Prepare generator
                string outputChannelStart = File.ReadAllText(@"OutputGenerator_OutputChannelStart.json");
                string outputChannelConfiguration;
                for (i = 0; i < 8; i++)
                {
                    rest.PutRequestWithPath("/rest/rec/generator/prepare", outputChannelStart); //prepare, when channging signal type

                    // Configure generator channel
                    switch (i)
                    {
                        case 0://rember to set input channel to DC
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupDC.json"); break;
                        case 1:
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupSine.json"); break;
                        case 2://direction UP-UP-UP
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupLinSweep.json"); break;
                        case 3://direction UP-DOWN-UP-DOWN
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupLogSweep.json"); break;
                        case 4:
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupRandom.json"); break;
                        case 5:
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupPseudoRandom.json"); break;
                        case 6:
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupSquare.json"); break;
                        case 7:
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupBurst.json"); break;
                        default:
                            outputChannelConfiguration = File.ReadAllText(@"OutputGenerator_OutputChannelSetupSine.json"); break;
                    }
                    rest.PutRequestWithPath("/rest/rec/generator/output", outputChannelConfiguration);

                    // Start output
                    rest.PutRequestWithPath("/rest/rec/generator/start", outputChannelStart);
                    if (IN_A_FRAME != 0)
                        rest.PutRequestWithPath("/rest/rec/apply", null);

                    Thread.Sleep(output_time1);
                }

                // Close recorder application
                rest.PutRequestWithPath("/rest/rec/close", null);

            }
            else
            {
                Console.WriteLine("Module is not Idle, stat is {0}", state);
                Thread.Sleep(1000);

            }
        }
    }
}
