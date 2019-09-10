// Uses IdaArp to find any reachable LAN-XI modules on the network and display information about the one with the serial no given.
// Using IdaArp, modules in a different subnet (but still physically reachable from the PC) may be found.
// This simple example does not handle errors - and will not terminate if the module requested is never found.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Shared;

namespace IdaArpGetInfo
{
    class Program
    {
        static void Main(string[] args)
        {
            UInt32 serialNo = 100402; // Serial no of the LAN-XI module to find

            // Create a socket. This machine will broadcast the discovery request, and any LAN-XI modules within the subnet will reply to that request - by unicast if possible, otherwise using a broadcast message.
            // UDP port 1024 is used for both sending and receiving.
            IPEndPoint requestEP = new IPEndPoint(IPAddress.Broadcast, 1024);
            IPEndPoint receiveEP = new IPEndPoint(IPAddress.Any, 1024);
            UdpClient udpClient = new UdpClient(receiveEP);
            udpClient.EnableBroadcast = true;

            // Construct the request for module discovery.
            string requestText = "Request for IP address on B&K IDA frame";
            byte[] requestBytes = new byte[50];
            Array.Copy(Encoding.ASCII.GetBytes(requestText), requestBytes, requestText.Length);

            // Send (broadcast) the discovery request.
            udpClient.Send(requestBytes, requestBytes.Length, requestEP);

            // Variables to monitor whether the specified module has been found, and to hold information gathered about that module.
            bool found = false;
            IdaArp idaArp = null;

            while (!found)
            {
                // Get a chunk of data from the socket.
                byte[] bytes = udpClient.Receive(ref receiveEP);

                if (bytes.Length > 50)
                {
                    // Parse data into an IdaArp object.
                    idaArp = new IdaArp(bytes);
                    // Check if the serial no matches the one we seek.
                    if (idaArp.ModuleSerialNo == serialNo)
                    {
                        // Module has been found. Terminate the loop.
                        found = true;
                    }
                    else
                    {
                        // this was not the module requested.
                        Console.WriteLine("IP: {0}.{1}.{2}.{3}, S/N {4}", idaArp.Ipaddr[0], idaArp.Ipaddr[1], idaArp.Ipaddr[2], idaArp.Ipaddr[3], idaArp.ModuleSerialNo);
                    }
                }
            }

            // Print information on the module found.
            Console.WriteLine(idaArp.ToString());

            // Close socket.
            udpClient.Close();

            // Example over. Keep the console open until ENTER has been pressed.
            Console.WriteLine("Finished. Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}
