//
//  main.m
//  OutputGenerator
//
//  Simple example using the built-in generators of a 3160 module for outputting a signal.

#import <Foundation/Foundation.h>
#import <dispatch/dispatch.h>
#import "LanXIRESTBoundary.h"

#define LANXI_IP "10.116.125.11" // IP address (or hostname) of the LAN-XI module

#pragma mark -
#pragma mark Main method
int main(int argc, const char * argv[])
{
    
    @autoreleasepool
    {
        
        NSString *ipAddress = @LANXI_IP;
        NSError *error;
        
        LanXIRESTBoundary *rest = [[LanXIRESTBoundary alloc] initWithHostname:ipAddress];
        
        // Open recorder application
        [rest putRequestWithPath:@"/rest/rec/open" body:nil error:&error];
        
        // Prepare generator
        NSData *outputChannelStart = [[NSData alloc] initWithContentsOfFile:@"../../OutputGenerator/OutputChannelStart.json"];
        [rest putRequestWithPath:@"/rest/rec/generator/prepare" body:outputChannelStart error:&error];
        
        // Configure generator channels
        NSData *outputChannelConfiguration = [[NSData alloc] initWithContentsOfFile:@"../../OutputGenerator/OutputChannelSetup.json"];
        [rest putRequestWithPath:@"/rest/rec/generator/output" body:outputChannelConfiguration error:&error];
        
        // Start output
        [rest putRequestWithPath:@"/rest/rec/generator/start" body:outputChannelStart error:&error];
        
        [NSThread sleepForTimeInterval:5];
        
        // Change generator configuration (frequencies and amplitudes)
        outputChannelConfiguration = [[NSData alloc] initWithContentsOfFile:@"../../OutputGenerator/OutputChannelSetup2.json"];
        [rest putRequestWithPath:@"/rest/rec/generator/output" body:outputChannelConfiguration error:&error];
        
        [NSThread sleepForTimeInterval:5];
        
        // Stop output
        [rest putRequestWithPath:@"/rest/rec/generator/stop" body:nil error:&error];
        
        // Close recorder application
        [rest putRequestWithPath:@"/rest/rec/close" body:nil error:&error];
        
    }
    return 0;
}
