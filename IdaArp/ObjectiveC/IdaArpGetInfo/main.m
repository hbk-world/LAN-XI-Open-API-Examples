//  Uses IdaArp to find any reachable LAN-XI modules on the network and display information about the one with the serial no given.
//  Using IdaArp, modules in a different subnet (but still physically reachable from the PC) may be found.
//  This simple example does not handle errors - and will not terminate if the module requested is never found.

#import <Foundation/Foundation.h>
#import <arpa/inet.h>
#import <sys/socket.h>
#import "IdaArp.h"

#define SN 100402 // Serial no of the LAN-XI module to find

int main(int argc, const char * argv[])
{

    @autoreleasepool {
        
        // Create a socket. This machine will broadcast the discovery request, and any LAN-XI modules within the subnet will reply to that request - by unicast if possible, otherwise using a broadcast message.
        int sock = socket(PF_INET, SOCK_DGRAM, IPPROTO_UDP);
        // Enable broadcast
        int broadcastEnable = 1;
        setsockopt(sock, SOL_SOCKET, SO_BROADCAST, &broadcastEnable, sizeof(broadcastEnable));
        
        // Set socket options for sending broadcast messages. Use UDP port 1024, and the 255.255.255.255 broadcast address.
        struct sockaddr_in sendAddr;
        sendAddr.sin_family = AF_INET;
        sendAddr.sin_port = htons( 1024 );
        sendAddr.sin_addr.s_addr = inet_addr( "255.255.255.255" );
        memset(sendAddr.sin_zero, '\0', sizeof(sendAddr.sin_zero));
        
        // Set socket options for receiving. Use UDP port 1024.
        struct sockaddr_in bindAddr;
        bindAddr.sin_family = AF_INET;
        bindAddr.sin_port = htons( 1024 );
        bindAddr.sin_addr.s_addr = htonl( INADDR_ANY );
        memset(bindAddr.sin_zero, '\0', sizeof(bindAddr.sin_zero));
        
        // Bind the socket.
        bind(sock, (struct sockaddr *) &bindAddr, sizeof(bindAddr));
        
        // Construct the request for module discovery.
        char requestText[ 50 ];
        memset( requestText, '\0', 50 );
        strcpy( requestText, "Request for IP address on B&K IDA frame" );
        
        // Send (broadcast) the discovery request.
        sendto ( sock, (char*) &requestText, sizeof(requestText), 0, (struct sockaddr*) &sendAddr, sizeof( sendAddr ) );
        
        // Variables to monitor whether the specified module has been found, and to hold information gathered about that module.
        bool found = NO;
        // Create a static buffer and an IdaArp pointer to it.
        char buf[1024];
        IdaArp_t* idaArp = (IdaArp_t*)buf;
        
        // Run while the requested module has not been found.
        while (!found)
        {
            // Get a chunk of data from the socket
            ssize_t bytesread = recvfrom(sock, buf, sizeof(buf), 0, NULL, NULL);
            
            if ( bytesread > 50)
            {
                // Check if the serial no matches the one we seek.
                if ( ntohl(idaArp->ModuleSerialNo) == SN )
                {
                    // Module has been found. Terminate loop.
                    found = YES;
                }
                else
                {
                    // This was not the module requested.
                    NSLog(@"IP: %d.%d.%d.%d", idaArp->Ipaddr[0], idaArp->Ipaddr[1], idaArp->Ipaddr[2], idaArp->Ipaddr[3]);
                }
            }
        }
        
        // Print information about the module found.
        NSLog(@"-----------------------------------------------------");
        uint version = ntohl(idaArp->Version);
        NSLog(@"Version: %u", version); // Values are represented in big-endian
        NSLog(@"MAC Address: %x.%x.%x.%x.%x.%x", idaArp->Etaddr[0], idaArp->Etaddr[1], idaArp->Etaddr[2], idaArp->Etaddr[3], idaArp->Etaddr[4], idaArp->Etaddr[5]);
        NSLog(@"IP: %d.%d.%d.%d", idaArp->Ipaddr[0], idaArp->Ipaddr[1], idaArp->Ipaddr[2], idaArp->Ipaddr[3]);
        NSLog(@"Text: %s", idaArp->Text);
        NSLog(@"Type: %d", ntohl(idaArp->TypeNo));
        NSLog(@"Contact: %s", idaArp->Contact);
        NSLog(@"Location: %s", idaArp->Location);
        NSLog(@"Connected: %d", ntohl(idaArp->Connected));
        NSLog(@"Last Machine: %s", idaArp->LastMachine);
        NSLog(@"Last User: %s", idaArp->LastUser);
        
        if (version >= 2)
        {
            NSLog(@"Boot: %d", ntohl(idaArp->Boot));
            NSLog(@"ModuleSerialNo: %d", ntohl(idaArp->ModuleSerialNo));
            NSLog(@"FrameSerialNo: %d", ntohl(idaArp->FrameSerialNo));
            NSLog(@"NoOfSlots: %d", ntohl(idaArp->NoOfSlots));
            NSLog(@"SlotNo: %d", ntohl(idaArp->SlotNo));
        }
        
        if (version >= 3)
        {
            NSLog(@"PC MAC Address: %x.%x.%x.%x.%x.%x", idaArp->PCEtaddr[0], idaArp->PCEtaddr[1], idaArp->PCEtaddr[2], idaArp->PCEtaddr[3], idaArp->PCEtaddr[4], idaArp->PCEtaddr[5]);
        }
        
        if (version >= 4)
        {
            NSLog(@"Hostname: %s", idaArp->HostName);
        }
        
        if (version >= 5)
        {
            NSLog(@"Variant: %s", idaArp->Variant);
            NSLog(@"Frame Contact: %s", idaArp->FrameContact);
            NSLog(@"Frame Location: %s", idaArp->FrameLocation);
        }
        
        if (version >= 6)
        {
            NSLog(@"Frame Type: %s", idaArp->FrameType);
            NSLog(@"Frame Variant: %s", idaArp->FrameVariant);
        }
        
        if (version >= 7)
        {
            NSLog(@"Subnet mask: %x", ntohl(idaArp->SubNetMask));
        }
        
        // Close socket.
        close(sock);
        
    }
    return 0;
}

