//
//  main.m
//  OutputStreaming
//
//  Simple example outputting samples generated on the PC through a 3160 module.

#import <Foundation/Foundation.h>
#import <arpa/inet.h>
#import <sys/socket.h>
#import <dispatch/dispatch.h>
#import "LanXIRESTBoundary.h"

#define LANXI_IP "10.116.125.11" // IP address (or hostname) of the LAN-XI module
#define FREQ1 784 // Frequency to generate for channel 1
#define FREQ2 659 // Frequency to generate for channel 2
#define SAMPLES_TO_PRIME 3*6000 // Number of samples to prime into the generator buffers before starting the output

uint primed = 0; // Number of channels primed and ready to receive start command

#pragma mark -
#pragma mark Prototypes
void streamToChannel(NSString* host, int port, int freq);

#pragma mark -
#pragma mark Main method
int main(int argc, const char * argv[])
{
    
    @autoreleasepool {
        
        NSString *ipAddress = @LANXI_IP;
        NSError *error;
        
        LanXIRESTBoundary *rest = [[LanXIRESTBoundary alloc] initWithHostname:ipAddress];
        
        // Open recorder application
        [rest putRequestWithPath:@"/rest/rec/open" body:nil error:&error];
        
        // Prepare generator
        NSData *outputChannelStart = [[NSData alloc] initWithContentsOfFile:@"../../OutputStreaming/OutputChannelStartStreaming.json"];
        [rest putRequestWithPath:@"/rest/rec/generator/prepare" body:outputChannelStart error:&error];
        
        // Configure generator channels
        NSData *outputChannelConfiguration = [[NSData alloc] initWithContentsOfFile:@"../../OutputStreaming/OutputChannelSetupStreaming.json"];
        [rest putRequestWithPath:@"/rest/rec/generator/output" body:outputChannelConfiguration error:&error];
        
        // Get port numbers to send samples to
        NSDictionary *dict = (id)[rest getRequestWithPath:@"/rest/rec/generator/output" error:&error];
        UInt16 port1 = [dict[@"outputs"][0][@"inputs"][0][@"port"] unsignedShortValue];
        UInt16 port2 = [dict[@"outputs"][1][@"inputs"][0][@"port"] unsignedShortValue];
        NSLog(@"Output streaming TCP ports: %d - %d", port1, port2);
        
        // Connect to sockets and send data using streamToChannel(...)
        streamToChannel(ipAddress, port1, FREQ1);
        streamToChannel(ipAddress, port2, FREQ2);
        
        // Wait until channels are primed
        while (primed < 2);
        NSLog(@"Channels primed");
        
        // Start output
        [rest putRequestWithPath:@"/rest/rec/generator/start" body:outputChannelStart error:&error];
        
        // Wait for signal to finish playing
        [NSThread sleepForTimeInterval:11];
        
        // Stop output
        [rest putRequestWithPath:@"/rest/rec/generator/stop" body:outputChannelStart error:&error];
        
        // Close recorder application
        [rest putRequestWithPath:@"/rest/rec/close" body:nil error:&error];
        
    }
    return 0;
}

#pragma mark -
#pragma mark Socket streaming
/**
 Connects to the specified host/port, generates a tone of specified frequency and streams this to the connected host.
 @param host Host name or IP address of host to send samples to
 @param port TCP port number to connect to
 @param freq Frequency of tone to generate
 */
void streamToChannel(NSString* host, int port, int freq)
{
    // Create socket
    int sock = socket(AF_INET, SOCK_STREAM, 0);
    
    struct sockaddr_in addr;
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = inet_addr([host UTF8String]);
    
    // Connect to socket
    connect(sock, (struct sockaddr *)&addr, sizeof(addr));
    NSLog(@"OutputStreaming Connected to %@:%d. Generating %d Hz signal", host, port, freq);
    
    // Create dispatch queue and IO for channel
    NSMutableString *queueName = [[NSMutableString alloc] initWithFormat:@"com.bksv.SimpleExampleOutputStreaming.%@.%d", host, port];
    dispatch_queue_t queue = dispatch_queue_create([queueName UTF8String], DISPATCH_QUEUE_SERIAL);
    dispatch_io_t io = dispatch_io_create(DISPATCH_IO_STREAM, sock, queue, ^(int error) {} );
    
    // Allocate buffer for samples to stream
    UInt32 numberOfSamples = 131072*10; // Number of samples to generate. 131072 samples/sec
    void *sampleBuf = malloc(numberOfSamples * sizeof(SInt32));
    SInt32 *sample = sampleBuf;
    
    // Generate tone in the buffer
    for (int i = 0; i < numberOfSamples; i++)
    {
        // Calculate next floating-point sample value
        double val = sin(i*2*M_PI*freq/131072);
        // Scale to 24-bit signed integer and store in 32-bit signed integer (lower bytes)
        *sample = (SInt32)(val*8372224);
        sample++;
    }
    
    // Create dispatch data object with generated samples
    dispatch_data_t data = dispatch_data_create(sampleBuf, numberOfSamples*sizeof(SInt32), queue, ^{});
    
    // Split data object into 2; a part for priming the buffers on the LAN-XI module and a part containing the remaining samples
    dispatch_data_t primeData = dispatch_data_create_subrange(data, 0, SAMPLES_TO_PRIME*sizeof(SInt32));
    dispatch_data_t streamData = dispatch_data_create_subrange(data, SAMPLES_TO_PRIME*sizeof(SInt32), (numberOfSamples-SAMPLES_TO_PRIME)*sizeof(SInt32));
    
    // Prime buffers on the module
    dispatch_io_write(io, 0, primeData, queue, ^(bool done, dispatch_data_t data, int error) {
        if (done)
            primed++;
    });
    
    // Send samples. The signal will be split automatically
    dispatch_io_write(io, 0, streamData, queue, ^(bool done, dispatch_data_t data, int error) {
        if (done)
        {
            // Finished sending data for this channel. Clean up.
            close(sock);
            free(sampleBuf);
        }
    });
}
