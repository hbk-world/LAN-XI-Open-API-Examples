//
//  IdaArp.h
//  LANXIIdaArpExamples
//
//  Created by Per Boye Clausen on 16/10/13.
//
//

#ifndef LANXIIdaArpExamples_IdaArp_h
#define LANXIIdaArpExamples_IdaArp_h

typedef struct
{
    u_int32_t   Version;
    char        Text[64];           //magic text, 64 chars incl. null at the end.
    u_int8_t    Etaddr[6];          //my ethernet address
    u_int8_t    Ipaddr[4];          //my ip address
    u_int32_t   TypeNo;
    char        Contact[64];        //Module name, 64 chars incl. null at the end.
    char        Location[64];       //Module location, 64 chars incl. null at the end.
    u_int32_t   Connected;          //0=not connected
    char        LastMachine[64];    //last pc-id the frame was connected to, 64 chars incl. null at the end.
    char        LastUser[64];       //last pc-user_id the frame was connected to, 64 chars incl. null at the end.
    
    //--------Version 2
    u_int32_t   Boot;               //1="Cold boot"=boadcast from module, 0=responce to request from PC
    u_int32_t   ModuleSerialNo;     //Serial number of LanModule(Ida)/ Module(LanXI)
    u_int32_t   FrameSerialNo;      //Serial number of Power Supply Unit(ida)/FrameControler(LanXI)
    u_int32_t   NoOfSlots;          //Number of slots in frame. (ida + FrameControler(LanXI))/ Module(LANXI)=0
    u_int32_t   SlotNo;             //Slot number in frame.(ida=31)/FrameControler(LanXI)=0 / Module(LANXI)=0 or slotno if in frame
    
    //--------Version 3
    u_int8_t    PCEtaddr[6];        //PC ethernet address, used to see what interface-card on the pc send the request, else 0
    
    //--------Version 4
    char        HostName[16];       //Module host name 16 chars incl. null at the end.
    
    //--------Version 5
    char        Variant[18];        //Module variant 17 chars incl. null at the end. [18] to please pack-2
    char        FrameContact[64];   //frame name, 64 chars incl. null at the end.
    char        FrameLocation[64];  //frame location, 64 chars incl. null at the end.
    
    //--------Version 6
    char        FrameType[18];      //Frame type    17 chars incl. null at the end. [18] to please pack-2
    char        FrameVariant[18];   //Frame variant 17 chars incl. null at the end. [18] to please pack-2
    
    //--------Version 7
    u_int32_t   SubNetMask;         //Module IP-netmask
} __attribute__((packed)) IdaArp_t;

typedef struct
{
    u_int32_t   Version;
    char        Text[32];       // Magic text
    char        Etaddr[6];      // Module ethernet address
    char        Ipaddr[4];      // New ip address
    char        NetMask[4];     // New netmask
    char        GateWay[4];     // New gateway
    char        DnsServer1[4];  // Unused
    char        DnsServer2[4];  // Unused
    char        Reserved[4];    // Unused
} __attribute__((packed)) SetIP_t;

#endif
