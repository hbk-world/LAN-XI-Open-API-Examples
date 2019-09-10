// Uses IdaArp to find any reachable LAN-XI modules on the network and change the IP configuration of the one with the serial no given.
// Using IdaArp, modules in a different subnet (but still physically reachable from the PC) may be controlled.
// This simple example does not handle errors - and will not terminate if the module requested is never found.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Shared;

namespace IdaArpChangeIp
{
    class Program
    {
        static void Main(string[] args)
        {
            UInt32 serialNo = 100402; // Serial no of the LAN-XI module to control

            byte[] new_ip = new byte[] { 192, 168, 0, 2 }; // New IP address of the LAN-XI module. Set to 0.0.0.0 to use DHCP
            byte[] new_gw = new byte[] { 192, 168, 0, 1 }; // New gateway configuration
            byte[] new_subnet = new byte[] { 255, 255, 255, 0 }; // New subnet configuration

            // Create a socket. This machine will broadcast the discovery request, and any LAN-XI modules within the subnet will reply to that request - by unicast if possible, otherwise using a broadcast message.
            // UDP port 1024 is used for both sending and receiving.
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, 1024);
            IPEndPoint receiveEP = new IPEndPoint(IPAddress.Any, 1024);
            UdpClient udpClient = new UdpClient(receiveEP);
            udpClient.EnableBroadcast = true;

            // Construct the request for module discovery.
            string requestText = "Request for IP address on B&K IDA frame";
            byte[] requestBytes = new byte[50];
            Array.Copy(Encoding.ASCII.GetBytes(requestText), requestBytes, requestText.Length);

            // Send (broadcast) the discovery request.
            udpClient.Send(requestBytes, requestBytes.Length, broadcastEP);

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
                        Console.WriteLine("IP: {0}.{1}.{2}.{3}", idaArp.Ipaddr[0], idaArp.Ipaddr[1], idaArp.Ipaddr[2], idaArp.Ipaddr[3]);
                        Console.WriteLine("Module found, type: {0}", idaArp.TypeNo);

                        found = true;
                    }
                    else
                    {
                        // this was not the module requested.
                        Console.WriteLine("IP: {0}.{1}.{2}.{3}", idaArp.Ipaddr[0], idaArp.Ipaddr[1], idaArp.Ipaddr[2], idaArp.Ipaddr[3]);
                    }
                }
            }

            // Create a message for setting the IP of the module found.
            // See the SetIp class for formatting of the message sent to the module.
            SetIp setIp = new SetIp();
            // Set version
            setIp.Version = 2; // Note that when sent to the LAN-XI module, this should be in big-endian format.
            // Set magic / request text
            setIp.Text = "Set IP address on B&K IDA frame".ToCharArray(); // Note that when sent to the LAN-XI module, this array should be 32 bytes.
            // Set ethernet / MAC address from saved IdaArp message
            setIp.Etaddr = idaArp.Etaddr;
            // Set IP/subnet/gateway settings from values set at the top of the example.
            setIp.Ipaddr = new_ip;
            setIp.NetMask = new_subnet;
            setIp.GateWay = new_gw;

            // Create byte array to send to the module.
            byte[] setIpBytes = setIp.ToBytes();

            // Send IP settings. Broadcast is used again.
            udpClient.Send(setIpBytes, setIpBytes.Length, broadcastEP);

            // Close socket.
            udpClient.Close();

            // Example over. Keep the console open until ENTER has been pressed.
            Console.WriteLine("Finished. Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}
