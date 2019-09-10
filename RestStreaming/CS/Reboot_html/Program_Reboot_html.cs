// Example how to "press" the reboot button on the homepage
// No security, you risk rebooting a module doing importent measurements.

using System;
using System.Net;
using System.IO;

namespace Reboot_html
{
    class Program
    {
        static readonly string LANXI_IP = "10.100.35.192"; // IP address (or hostname) of the LAN-XI module
 
        static void Main(string[] args)
        {
            // Use program arguments if specified, otherwise use constants
            // Arguments are in the sequence [[[<lanxi_ip>] <output_time>] <output_time2>]
            string lanxi_ip = LANXI_IP;
            if (args.Length >= 1)
            {
                lanxi_ip = args[0];
            }

 
            // Open recorder application
            string boot = "reboot=1";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + lanxi_ip + "/");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = boot.Length;
            // Send any request body to the host
            StreamWriter requestWriter = new StreamWriter(request.GetRequestStream(), System.Text.Encoding.ASCII);
            requestWriter.Write(boot);
            requestWriter.Close();

        }
    }
}
