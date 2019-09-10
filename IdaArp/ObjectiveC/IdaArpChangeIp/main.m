//  Uses IdaArp to find any reachable LAN-XI modules on the network and change the IP configuration of the one with the serial no given.
//  Using IdaArp, modules in a different subnet (but still physically reachable from the PC) may be controlled.
//  This simple example does not handle errors - and will not terminate if the module requested is never found.

#import <Foundation/Foundation.h>
#import <arpa/inet.h>
#import <sys/socket.h>
#import <dispatch/dispatch.h>
#import "IdaArp.h"

#define SN 100402 // Serial no of the LAN-XI module to control

int main(int argc, const char * argv[])
{
    char new_ip[] = { 192, 168, 0, 2 }; // New IP address of the LAN-XI module. Set to 0.0.0.0 to use DHCP
    char new_gw[] = { 192, 168, 0, 1 }; // New gateway configuration
    char new_subnet[] = { 255, 255, 255, 0 }; // New subnet configuration

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
                    NSLog(@"IP: %d.%d.%d.%d", idaArp->Ipaddr[0], idaArp->Ipaddr[1], idaArp->Ipaddr[2], idaArp->Ipaddr[3]);
                    NSLog(@"Module found, type: %d", ntohl(idaArp->TypeNo));
                    
                    found = YES;
                }
                else
                {
                    // This was not the module requested.
                    NSLog(@"IP: %d.%d.%d.%d", idaArp->Ipaddr[0], idaArp->Ipaddr[1], idaArp->Ipaddr[2], idaArp->Ipaddr[3]);
                }
            }
        }
        
        // Create a message for setting the IP of the module found.
        SetIP_t setIp;
        // Set version
        setIp.Version = htonl(2);
        // Set magic / request text
        memset(setIp.Text, '\0', 50);
        strcpy(setIp.Text, "Set IP address on B&K IDA frame");
        // Set ethernet / MAC address from saved IdaArp message
        memcpy(setIp.Etaddr, idaArp->Etaddr, 6);
        // Set IP/subnet/gateway settings from values set at the top of the example.
        setIp.Ipaddr[0] = new_ip[0];
        setIp.Ipaddr[1] = new_ip[1];
        setIp.Ipaddr[2] = new_ip[2];
        setIp.Ipaddr[3] = new_ip[3];
        setIp.NetMask[0] = new_subnet[0];
        setIp.NetMask[1] = new_subnet[1];
        setIp.NetMask[2] = new_subnet[2];
        setIp.NetMask[3] = new_subnet[3];
        setIp.GateWay[0] = new_gw[0];
        setIp.GateWay[1] = new_gw[1];
        setIp.GateWay[2] = new_gw[2];
        setIp.GateWay[3] = new_gw[3];
        
        // Send IP settings. Broadcast is used again.
        sendto (sock, (char*) &setIp, sizeof(setIp), 0, (struct sockaddr*) &sendAddr, sizeof(sendAddr));
        
        NSLog(@"IP Settings sent to module");
        
        // Close socket.
        close(sock);

    }
    return 0;
}
