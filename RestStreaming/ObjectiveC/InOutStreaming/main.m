//
//  main.m
//  InOutStreaming
//
//  Created by Per Boye Clausen on 02/10/13.
//
//  Simple example streaming from all inputs of a 3160 module and echoing input 1 and 2 back through the PC to output 1 and 2.

#import <Foundation/Foundation.h>
#import <arpa/inet.h>
#import <sys/socket.h>
#import <dispatch/dispatch.h>
#import "LanXIRESTBoundary.h"
#import "StreamingHeaders.h"

#define LANXI_IP "169.254.16.0" // IP address (or hostname) of the LAN-XI module
#define RUN_TIME 10 // Number of seconds to run
#define SAMPLES_TO_RECEIVE 4096*32*RUN_TIME // Samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s
#define SAMPLES_TO_PRIME 3*6000 // Number of samples to prime into the generator buffers before starting the output

UInt32 samplesReceived[4] = { 0, 0, 0, 0 }; // Used to count the number of samples received - for demo purpose
UInt32 samplesSent[2] = { 0, 0 }; // Used to count the number of samples sent - for priming and demo purposes

// Queues etc. used for socket communication
dispatch_queue_t inputQueue;
dispatch_io_t inputIO;
dispatch_queue_t outputQueue[2];
dispatch_io_t outputIO[2];
// Sockets
int inputSocket;
int outputSockets[2];

#pragma mark -
#pragma mark Prototypes
void connectSocketsAndStream(NSString* address, UInt16 inputPort, UInt16 outputPorts[]);
void readHeader();
void readMessage(UInt32 dataLength, UInt16 messageType);

#pragma mark -
#pragma mark Main method
int main(int argc, const char * argv[])
{
    
    @autoreleasepool {
        
        NSError *error;
        NSDictionary *dict;
        
        NSString* ipAddress = @LANXI_IP;
        
        // Initialize boundary objects
        LanXIRESTBoundary *rest = [[LanXIRESTBoundary alloc] initWithHostname:ipAddress];
        
        // Start measurement
        
        // Open the Recorder application on the LAN-XI module
        [rest putRequestWithPath:@"/rest/rec/open" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderOpened" error:&error];
        
        // Prepare generator
        NSData *outputChannelStart = [[NSData alloc] initWithContentsOfFile:@"../../InOutStreaming/OutputChannelStartStreaming.json"];
        [rest putRequestWithPath:@"/rest/rec/generator/prepare" body:outputChannelStart error:&error];
        
        // Configure generator channels
        NSData *outputChannelConfiguration = [[NSData alloc] initWithContentsOfFile:@"../../InOutStreaming/OutputChannelSetupStreaming.json"];
        [rest putRequestWithPath:@"/rest/rec/generator/output" body:outputChannelConfiguration error:&error];
        
        // Get port numbers to send samples to
        dict = (id)[rest getRequestWithPath:@"/rest/rec/generator/output" error:&error];
        UInt16 outputPorts[2];
        outputPorts[0] = [dict[@"outputs"][0][@"inputs"][0][@"port"] unsignedShortValue];
        outputPorts[1] = [dict[@"outputs"][1][@"inputs"][0][@"port"] unsignedShortValue];
        NSLog(@"Output streaming TCP ports: %d - %d", outputPorts[0], outputPorts[1]);
        
        // Create Recorder configuration
        [rest putRequestWithPath:@"/rest/rec/create" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderConfiguring" error:&error];
        
        // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default
        // In this example a JSON file has been prepared with the desired config.
        NSData *inputChannelConfiguration = [[NSData alloc] initWithContentsOfFile:@"../../InOutStreaming/InputChannelSetup.json"];
        [rest putRequestWithPath:@"/rest/rec/channels/input" body:inputChannelConfiguration error:&error];
        [rest waitForRecorderState:@"RecorderStreaming" error:&error];
        
        // Get the TCP port provided by the LAN-XI module for streaming samples
        dict = (id)[rest getRequestWithPath:@"/rest/rec/destination/socket" error:&error];
        UInt16 inputPort = [dict[@"tcpPort"] unsignedShortValue];
        NSLog(@"Input streaming TCP port: %d", inputPort);
        
        // Let connectSocketAndStream() method handle socket connection
        connectSocketsAndStream(ipAddress, inputPort, outputPorts);
        
        // Start measuring
        [rest postRequestWithPath:@"/rest/rec/measurements" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderRecording" error:&error];
        
        // Streaming should now be running
        
        // Wait for a while to prime the generators
        while (samplesSent[0] < SAMPLES_TO_PRIME && samplesSent[1] < SAMPLES_TO_PRIME);
        NSLog(@"Channels primed");
        
        // Start output
        [rest putRequestWithPath:@"/rest/rec/generator/start" body:outputChannelStart error:&error];
        
        [NSThread sleepForTimeInterval:RUN_TIME+2];
        
        // Stop output
        [rest putRequestWithPath:@"/rest/rec/generator/stop" body:outputChannelStart error:&error];
        
        // Stop measuring and close sockets
        [rest putRequestWithPath:@"/rest/rec/measurements/stop" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderStreaming" error:&error];
        close(inputSocket);
        for (uint i = 0; i < 2; i++)
            close(outputSockets[i]);
        
        // Finish recording
        [rest putRequestWithPath:@"/rest/rec/finish" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderOpened" error:&error];
        
        // Close Recorder application
        [rest putRequestWithPath:@"/rest/rec/close" body:nil error:&error];
        [rest waitForRecorderState:@"Idle" error:&error];
        
        // Module should now be back in the Idle state without the Recorder application running
        
        NSLog(@"Finished");
    }
    
    return 0;
}

#pragma mark -
#pragma mark Socket streaming

// Sets up socket communication
void connectSocketsAndStream(NSString* address, UInt16 inputPort, UInt16 outputPorts[])
{
    // Set up and connect input socket
    inputSocket = socket(AF_INET, SOCK_STREAM, 0);
    
    struct sockaddr_in addr;
    addr.sin_family = AF_INET;
    addr.sin_port = htons(inputPort);
    addr.sin_addr.s_addr = inet_addr([address UTF8String]);
    
    connect(inputSocket, (struct sockaddr *)&addr, sizeof(addr));
    
    // Set up dispatch queue and IO
    inputQueue = dispatch_queue_create("com.bksv.LANXI.inputStreaming", DISPATCH_QUEUE_SERIAL);
    inputIO = dispatch_io_create(DISPATCH_IO_STREAM, inputSocket, inputQueue, ^(int error) {});
    
    // Set up and connect output sockets (1 per channel)
    for (int i = 0; i < 2; i++)
    {
        outputSockets[i] = socket(AF_INET, SOCK_STREAM, 0);
        
        struct sockaddr_in addr;
        addr.sin_family = AF_INET;
        addr.sin_port = htons(outputPorts[i]);
        addr.sin_addr.s_addr = inet_addr([address UTF8String]);
        
        int result = connect(outputSockets[i], (struct sockaddr *)&addr, sizeof(addr)) != 0;
        if (result != 0)
            NSLog(@"Connect failed? %d", result);
        
        // Set up dispatch queue and IO
        NSMutableString *queueName = [[NSMutableString alloc] initWithFormat:@"com.bksv.SimpleExampleOutputStreaming.%@.%d", address, outputPorts[i]];
        outputQueue[i] = dispatch_queue_create([queueName UTF8String], DISPATCH_QUEUE_SERIAL);
        outputIO[i]  = dispatch_io_create(DISPATCH_IO_STREAM, outputSockets[i], outputQueue[i], ^(int error) {} );
    }
    
    // Sockets are ready for transmitting data. Hand over control to readHeader().
    readHeader();
}

// Reads the StreamingHeader from the input socket data stream.
void readHeader()
{
    dispatch_io_read(inputIO, 0, sizeof(StreamingHeader), inputQueue, ^(bool done, dispatch_data_t data, int error) {
        dispatch_data_apply(data, (dispatch_data_applier_t)^(dispatch_data_t region, size_t offset, const void *buffer, size_t size) {
            
            // Make a pointer to the header and extract necessary information from it.
            StreamingHeader* header = (StreamingHeader*)buffer;
            UInt32 dataLength = header->dataLength;
            UInt16 messageType = header->messageType;
            
            // Pass control on to the message parsing method.
            readMessage(dataLength, messageType);
            
        });
    });
}

// Read the message following the StreamingHeader from the input socket data stream.
void readMessage(UInt32 dataLength, UInt16 messageType)
{
    dispatch_io_read(inputIO, 0, dataLength, inputQueue, ^(bool done, dispatch_data_t data, int error) {
        dispatch_data_apply(data, (dispatch_data_applier_t)^(dispatch_data_t region, size_t offset, const void *buffer, size_t size) {
            
            // Simple example - we only care about signal data.
            if (messageType == 1)
            {
                // Make a pointer to the SignalData header.
                SignalDataMessage *signalHeader = (SignalDataMessage*)buffer;
                
                if (signalHeader->signalId <= 2)
                {
                    __block bool finished = NO;
                    // Initialize temporary output buffer
                    SInt32 outputBuffer[signalHeader->numberOfValues];
                    
                    // Make a pointer to the first signal byte.
                    UInt8* bytes = (UInt8*)(buffer+sizeof(SignalDataMessage));
                    // Iterate through each value (sample) in the message.
                    for (uint i = 0; i < signalHeader->numberOfValues; i++)
                    {
                        // Collect 3 bytes for a sample.
                        UInt8 low = *bytes++;
                        UInt8 mid = *bytes++;
                        UInt8 high = *bytes++;
                        
                        // Assemble the bytes into a 24-bit sample
                        outputBuffer[i] = ((high << 24) + (mid << 16) + (low << 8)) >> 8;
                        
                    }
                    
                    // Create a data object with the samples for the output channel
                    dispatch_data_t data = dispatch_data_create(outputBuffer, signalHeader->numberOfValues * sizeof(SInt32), outputQueue[signalHeader->signalId-1], ^{});
                    
                    // Send data
                    dispatch_io_write(outputIO[signalHeader->signalId-1], 0, data, outputQueue[signalHeader->signalId-1], ^(bool done, dispatch_data_t data, int error) {
                        // Mark when the chunk has been transmitted
                        if (done)
                        {
                            samplesSent[signalHeader->signalId-1] += signalHeader->numberOfValues;
                            finished = YES;
                        }
                    });
                    
                    // Busy-wait to make sure the chunk is sent before next iteration is started
                    while (!finished)
                    {
                        [NSThread sleepForTimeInterval:0.001];
                    }
                }
                
                // Increment the number of samples gathered for this signal ID
                samplesReceived[signalHeader->signalId-1] += signalHeader->numberOfValues;
                
                NSLog(@"Samples received: %d %d %d %d", samplesReceived[0], samplesReceived[1], samplesReceived[2], samplesReceived[3]);
                
            }
            
            // Continue until the desired number of samples have been handled for all channels
            if (samplesReceived[0] < SAMPLES_TO_RECEIVE || samplesReceived[1] < SAMPLES_TO_RECEIVE || samplesReceived[2] < SAMPLES_TO_RECEIVE || samplesReceived[3] < SAMPLES_TO_RECEIVE)
                readHeader();
        });
    });
}
