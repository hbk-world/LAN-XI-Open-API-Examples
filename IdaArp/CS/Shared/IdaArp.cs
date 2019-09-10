using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;

namespace Shared
{
    public class IdaArp
    {
        // All integer values are transmitted to/from the LAN-XI module as big-endian.

        // Version 1 (all versions)
        public UInt32 Version;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] Text;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] Etaddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Ipaddr;
        public UInt32 TypeNo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] Contact;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] Location;
        public UInt32 Connected;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] LastMachine;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] LastUser;

        // Version 2
        public UInt32 Boot;
        public UInt32 ModuleSerialNo;
        public UInt32 FrameSerialNo;
        public UInt32 NoOfSlots;
        public UInt32 SlotNo;
        
        // Version 3
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] PCEtaddr;

        // Version 4
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public char[] HostName;

        // Version 5
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
        public char[] Variant;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] FrameContact;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] FrameLocation;

        // Version 6
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
        public char[] FrameType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
        public char[] FrameVariant;

        // Version 7
        public UInt32 SubNetMask;

        public IdaArp(byte[] bytes)
        {
            if (bytes.Length >= 342)
            {
                // Version 1
                this.Version = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 0));
                this.Text = Encoding.UTF8.GetString(bytes, 4, 64).ToCharArray();
                this.Etaddr = new byte[6];
                Array.Copy(bytes, 68, this.Etaddr, 0, 6);
                this.Ipaddr = new byte[4];
                Array.Copy(bytes, 74, this.Ipaddr, 0, 4);
                this.TypeNo = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 78));
                this.Contact = Encoding.UTF8.GetString(bytes, 82, 64).ToCharArray();
                this.Location = Encoding.UTF8.GetString(bytes, 146, 64).ToCharArray();
                this.Connected = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 210));
                this.LastMachine = Encoding.UTF8.GetString(bytes, 214, 64).ToCharArray();
                this.LastUser = Encoding.UTF8.GetString(bytes, 278, 64).ToCharArray();
            }
            if (bytes.Length >= 358)
            {
                // Version 2
                this.Boot = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 342));
                this.ModuleSerialNo = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 346));
                this.FrameSerialNo = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 350));
                this.NoOfSlots = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 354));
                this.SlotNo = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 358));
            }
            if (bytes.Length >= 368)
            {
                // Version 3
                this.PCEtaddr = new byte[6];
                Array.Copy(bytes, 362, this.PCEtaddr, 0, 6);
            }
            if (bytes.Length >= 384)
            {
                // Version 4
                this.HostName = Encoding.UTF8.GetString(bytes, 368, 16).ToCharArray();
            }
            if (bytes.Length >= 530)
            {
                // Version 5
                this.Variant = Encoding.UTF8.GetString(bytes, 384, 18).ToCharArray();
                this.FrameContact = Encoding.UTF8.GetString(bytes, 402, 64).ToCharArray();
                this.FrameLocation = Encoding.UTF8.GetString(bytes, 466, 64).ToCharArray();
            }
            if (bytes.Length >= 566)
            {
                // Version 6
                this.FrameType = Encoding.UTF8.GetString(bytes, 530, 18).ToCharArray();
                this.FrameVariant = Encoding.UTF8.GetString(bytes, 548, 18).ToCharArray();
            }
            if (bytes.Length >= 602)
            {
                // Version 7
                this.SubNetMask = (UInt32)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(bytes, 566));
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            if (this.Version >= 1)
            {
                sb.AppendFormat("Version: {0}", this.Version);
                sb.AppendFormat("\nText: {0:s}", new string(this.Text));
                sb.AppendFormat("\nEtaddr: {0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", this.Etaddr[0], this.Etaddr[1], this.Etaddr[2], this.Etaddr[3], this.Etaddr[4], this.Etaddr[5]);
                sb.AppendFormat("\nIpaddr: {0}.{1}.{2}.{3}", Ipaddr[0], Ipaddr[1], Ipaddr[2], Ipaddr[3]);
                sb.AppendFormat("\nTypeNo: {0}", this.TypeNo);
                sb.AppendFormat("\nContact: {0}", new string(this.Contact));
                sb.AppendFormat("\nLocation: {0}", new string(this.Location));
                sb.AppendFormat("\nConnected: {0}", this.Connected);
                sb.AppendFormat("\nLastMachine: {0}", new string(this.LastMachine));
                sb.AppendFormat("\nLastUser: {0}", new string(this.LastUser));
            }
            if (this.Version >= 2)
            {
                sb.AppendFormat("\nBoot: {0}", this.Boot);
                sb.AppendFormat("\nModuleSerialNo: {0}", this.ModuleSerialNo);
                sb.AppendFormat("\nFrameSerialNo: {0}", this.FrameSerialNo);
                sb.AppendFormat("\nNoOfSlots: {0}", this.NoOfSlots);
                sb.AppendFormat("\nSlotNo: {0}", this.SlotNo);
            }
            if (this.Version >= 3)
            {
                sb.AppendFormat("\nPCEtaddr: {0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", this.PCEtaddr[0], this.PCEtaddr[1], this.PCEtaddr[2], this.PCEtaddr[3], this.PCEtaddr[4], this.PCEtaddr[5]);
            }
            if (this.Version >= 4)
            {
                sb.AppendFormat("\nHostName: {0}", new string(this.HostName));
            }
            if (this.Version >= 5)
            {
                sb.AppendFormat("\nVariant: {0}", new string(this.Variant));
                sb.AppendFormat("\nFrameContact: {0}", new string(this.FrameContact));
                sb.AppendFormat("\nFrameLocation: {0}", new string(this.FrameLocation));
            }
            if (this.Version >= 6)
            {
                sb.AppendFormat("\nFrameType: {0}", new string(this.FrameType));
                sb.AppendFormat("\nFrameVariant: {0}", new string(this.FrameVariant));
            }
            if (this.Version >= 7)
            {
                sb.AppendFormat("\nSubNetMask: {0:X8}", this.SubNetMask);
            }

            return sb.ToString();
        }
    }
}
