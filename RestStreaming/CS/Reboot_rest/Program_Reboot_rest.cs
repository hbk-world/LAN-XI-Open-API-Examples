// Example how to send a rest command to reboot the module
// No security, you risk rebooting a module doing importent measurements.

using System;
using Shared;

namespace Reboot_rest
{
    class Program
    {
        static readonly string LANXI_IP = "10.100.35.192"; // IP address (or hostname) of the LAN-XI module
 
        static void Main(string[] args)
        {
            // Use program arguments if specified, otherwise use constants.
            // Arguments are in the sequence [[[<lanxi_ip>] <output_file>] <samples_to_receive>]
            string lanxi_ip = LANXI_IP;
            if (args.Length >= 1)
            {
                lanxi_ip = args[0];
            }

            // Initialize boundary objects
            LanXIRESTBoundary rest = new LanXIRESTBoundary(lanxi_ip);

            //reboot the LAN-XI module
            rest.PutRequestWithPath("/rest/rec/reboot", null);
        }
    }
}
