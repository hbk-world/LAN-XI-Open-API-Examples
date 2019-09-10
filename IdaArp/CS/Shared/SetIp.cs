using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;

namespace Shared
{
    public class SetIp
    {
        // All integer values are transmitted to/from the LAN-XI module as big-endian.

        public UInt32 Version;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public char[] Text;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] Etaddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Ipaddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] NetMask;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] GateWay;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private byte[] DnsServer1 = new byte[] {0, 0, 0, 0};
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private byte[] DnsServer2 = new byte[] { 0, 0, 0, 0 };
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private byte[] Reserved = new byte[] { 0, 0, 0, 0 };

        public byte[] ToBytes()
        {
            // Create byte array.
            byte[] output = new byte[66];

            // Copy fields into the correct positions in the byte array.
            Array.Copy(BitConverter.GetBytes((UInt32)IPAddress.HostToNetworkOrder((int)this.Version)), 0, output, 0, 4); // Version needs to be in network order.
            Array.Copy(Encoding.UTF8.GetBytes(this.Text), 0, output, 4, this.Text.Length);
            Array.Copy(this.Etaddr, 0, output, 36, 6);
            Array.Copy(this.Ipaddr, 0, output, 42, 4);
            Array.Copy(this.NetMask, 0, output, 46, 4);
            Array.Copy(this.GateWay, 0, output, 50, 4);
            Array.Copy(this.DnsServer1, 0, output, 54, 4);
            Array.Copy(this.DnsServer2, 0, output, 58, 4);
            Array.Copy(this.Reserved, 0, output, 62, 4);

            return output;
        }
    }
}
