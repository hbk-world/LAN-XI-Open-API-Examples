using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

partial class Program
{
    public delegate void IdaArpResponseEventHandler(object sender, IdaArpResponseEventArgs e);

    public class IdaArpResponseEventArgs : EventArgs
    {
        public IdaArpResponseEventArgs()
        {
            MacAddress = new byte[6];
        }

        // Module type number, e.g. 3050
        public string TypeNumber;

        // The module serial number
        public int SerialNumber;

        // Module IP address
        public IPAddress IpAddress;

        // Module MAC address
        public byte[] MacAddress;

        // Module hostname on network
        public string HostName;

        // User-defined 'Contact' setting
        public string Contact;

        // User-defined 'Location' setting
        public string Location;

        // Module currently connected to a client? (0 = not connected)
        public bool Connected;
    }

    public class IdaArp : IDisposable 
    {
        List<UdpClient> udpClients = new List<UdpClient>();
        List<Thread> threads = new List<Thread>();

        // Message to send to LAN-XI modules to request information
        readonly byte[] requestMessage;
        const string requestMessageText = "Request for IP address on B&K IDA frame";

        // Message to send to the local UDP client, to break out of the receive loop
        readonly byte[] stopMessage;
        const string stopMessageText = "==StopIdaArpListener==";

        // Receives events when modules are detected
        public event IdaArpResponseEventHandler IdaArpResponseReceived;

        public IdaArp()
        {
            // Prepare a broadcast message to request information from LAN-XI modules
            requestMessage = new byte[50];
            var messageBytes = Encoding.ASCII.GetBytes(requestMessageText);
            messageBytes.CopyTo(requestMessage, 0);

            // Prepare a message to be used when shutting down
            stopMessage = Encoding.ASCII.GetBytes(stopMessageText);

            // Create UDP clients for sending and receiving IdaArp packets.
            // We need one client per network interface, or Windows will
            // randomly select an interface, which may not be the one that
            // our LAN-XI modules are connected to.
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach(var iface in interfaces)
            {
                // We're only interested in Ethernet and Wi-Fi adapters
                if(iface.NetworkInterfaceType != NetworkInterfaceType.Ethernet && iface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    continue;

                // If the interface isn't enabled then ignore it
                if(iface.OperationalStatus != OperationalStatus.Up)
                    continue;

                // Get all IP addresses associated with this network interface
                var addrs = iface.GetIPProperties().UnicastAddresses;
                foreach(var addr in addrs)
                {
                    // If not an IPv4 address then ignore it
                    if(addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    // Create UDP client for this IP address
                    var udpClient = new UdpClient(new IPEndPoint(addr.Address, 0));
                    udpClients.Add(udpClient);

                    // Start receiver thread for this UDP client
                    var thread = new Thread(() => { ReceiveThread(udpClient); });
                    threads.Add(thread);
                    thread.Start();
                }
            }
        }

        public void Dispose()
        {
            // Send a stop message to each UDP client
            foreach(var udpClient in udpClients)
            {
                // Create a destination end point to send to, this will be
                // the address and port that the client is receiving from
                var ipEndPoint = (IPEndPoint)udpClient.Client.LocalEndPoint;

                // Send the stop message to the client
                var sender = new UdpClient();
                sender.Send(stopMessage, stopMessage.Length, ipEndPoint);
            }

            // Wait for all threads to exit
            foreach(var thread in threads)
            {
                thread.Join();
            }

            // Close all UDP receivers
            foreach(var udpClient in udpClients)
            {
                udpClient.Close();
            }
        }

        // Request information from all LAN-XI modules on the network
        public void Detect()
        {
            // LAN-XI modules listen on UDP port 1024
            var moduleEndPoint = new IPEndPoint(IPAddress.Broadcast, 1024);

            // The response from the module will be sent to the UDP port that
            // the request came from, so use our existing UDP clients to send
            // from, and then the receiver threads will pick up the responses.
            foreach(var udpClient in udpClients)
            {
                udpClient.Send(requestMessage, requestMessage.Length, moduleEndPoint);
            }
        }

        void ReceiveThread(UdpClient udpClient)
        {
            for(;;)
            {
                var moduleEndpoint = new IPEndPoint(IPAddress.Any, 0);

                // Blocking call waiting for UDP responses
                var bytes = udpClient.Receive(ref moduleEndpoint);

                // Exit the loop if this is a stop message
                if ((bytes.Length == stopMessageText.Length) && (Encoding.ASCII.GetString(bytes) == stopMessageText))
                    break;

                // Ignore if message is our own request sent from Detect()
                if ((bytes.Length == requestMessage.Length) && (Encoding.ASCII.GetString(bytes) == requestMessageText))
                    continue;

                // If we got less than four bytes we can't even determine the IdaArp version, so ignore it
                if (bytes.Length < 4)
                    continue;

                // Get the IdaArp version from the first four bytes
                var idaArpVersion = Ntohl(BitConverter.ToInt32(bytes, 0));

                // We only support IdaArp version 4 or above
                if (idaArpVersion < 4)
                    continue;

                // Ignore responses that are too small
                if (bytes.Length < Marshal.SizeOf(typeof(IdaArpResponseV4)))
                    continue;

                var response = (IdaArpResponseV4)GetObject(bytes, typeof(IdaArpResponseV4));

                // Ignore responses that don't contain the magic words
                if (response.Text != "Reply on request for IP address from B&K IDA frame")
                    continue;

                OnIdaArpResponseReceived(response);                    
            }
        }

        void OnIdaArpResponseReceived(IdaArpResponseV4 response)
        {
            var args = new IdaArpResponseEventArgs();

            args.TypeNumber = (Ntohl((int)response.TypeNumber) & 0xffffff).ToString();
            args.SerialNumber = Ntohl((int)response.ModuleSerialNumber);

            args.IpAddress = new IPAddress(response.IpAddress);

            response.MacAddress.CopyTo(args.MacAddress, 0);

            args.HostName = response.HostName;
            args.Contact = response.Contact;
            args.Location = response.Location;
            args.Connected = response.Connected != 0;

            lock(this)
            {
                IdaArpResponseReceived?.Invoke(this, args);
            }
        }

        int Ntohl(int l)
        {
            return IPAddress.NetworkToHostOrder(l);
        }

        // IdaArp response version 4
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
        class IdaArpResponseV4
        {
            // IdaArp protocol version
            [MarshalAs(UnmanagedType.U4)]
            public uint Version;

            // Magic string in response
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string Text;

            // Module MAC address
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] MacAddress;

            // Module IP address
            [MarshalAs(UnmanagedType.U4)]
            public uint IpAddress;

            // Module type number, e.g. 3050
            [MarshalAs(UnmanagedType.U4)]
            public uint TypeNumber;

            // User-defined 'Contact' setting
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string Contact;

            // User-defined 'Location' setting
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string Location;

            // Module currently connected to a client? (0 = not connected)
            [MarshalAs(UnmanagedType.U4)]
            public uint Connected;

            // The network host that was last connected to the module (since power on)
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string LastHost;

            // The network user that was last connected to the module (since power on)
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string LastUser;

            // Reason for this message (1 = cold boot or power on, 0 = response to IdaArp request)
            [MarshalAs(UnmanagedType.U4)]
            public uint Boot;

            // The module serial number
            [MarshalAs(UnmanagedType.U4)]
            public uint ModuleSerialNumber;

            // The containing frame's serial number, 0 if stand-alone module
            [MarshalAs(UnmanagedType.U4)]
            public uint FrameSerialNumber;

            // Number of slots in the containing frame, 0 if stand-alone module
            [MarshalAs(UnmanagedType.U4)]
            public uint NumberOfSlots;

            // The slot number in containing frame, 0 if stand-alone module
            [MarshalAs(UnmanagedType.U4)]
            public uint SlotNumber;

            // The MAC address of the PC's network interface the message came from, 0 if power on message or not available
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] PCMacAddress;

            // The network hostname for the module
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string HostName;
        };

        // Convert an array of bytes to an object
        object GetObject(byte[] bytes, Type type)
        {
            var size = Marshal.SizeOf(type);
            if (size > bytes.Length)
                return null;
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var buffer = handle.AddrOfPinnedObject();
            var obj = Marshal.PtrToStructure(buffer, type);
            handle.Free();
            return obj;
        }
    }
}
