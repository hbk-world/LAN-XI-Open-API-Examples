//
//  main.m
//  InputStreaming
//
//  Simple example streaming from all inputs of a 3160 module and storing a few samples from each channel in a file. The file may be read using e.g. LibreOffice.

#import <Foundation/Foundation.h>
#import <arpa/inet.h>
#import <sys/socket.h>
#import <dispatch/dispatch.h>
#import "LanXIRESTBoundary.h"
#import "StreamingHeaders.h"

#define LANXI_IP "169.254.16.0" // IP address (or hostname) of the LAN-XI module
#define OUTPUT_FILE "LANXI.out" // Path to the file where the samples received should be stored. Relative to the build/Debug folder
#define SAMPLES_TO_RECEIVE 4096*4 // Samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s

UInt32 samplesReceived[4] = { 0, 0, 0, 0 }; // Used to count the number of samples received - for demo purpose
dispatch_queue_t ioQueue;
dispatch_io_t io;
int sock;

SInt32 *outputSamples[4]; // Pointers to output buffers
NSString* outputFile = @OUTPUT_FILE; // The file to write samples to

#pragma mark -
#pragma mark Prototypes
void connectSocketAndStream(NSString* address, UInt16 port);
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
        
        for (int i = 0; i < sizeof(outputSamples); i++)
        {
            outputSamples[i] = malloc(SAMPLES_TO_RECEIVE * sizeof(SInt32));
        }
    
        // Initialize boundary objects
        LanXIRESTBoundary *rest = [[LanXIRESTBoundary alloc] initWithHostname:ipAddress];
        
        // Start measurement
        
        // Open the Recorder application on the LAN-XI module
        [rest putRequestWithPath:@"/rest/rec/open" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderOpened" error:&error];
        
        // Create Recorder configuration
        [rest putRequestWithPath:@"/rest/rec/create" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderConfiguring" error:&error];
        
        // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default
        // In this example a JSON file has been prepared with the desired config.
        NSData *inputChannelConfiguration = [[NSData alloc] initWithContentsOfFile:@"../../InputStreaming/InputChannelSetup.json"];
        [rest putRequestWithPath:@"/rest/rec/channels/input" body:inputChannelConfiguration error:&error];
        [rest waitForRecorderState:@"RecorderStreaming" error:&error];
        
        // Get the TCP port provided by the LAN-XI module for streaming samples
        dict = (id)[rest getRequestWithPath:@"/rest/rec/destination/socket" error:&error];
        UInt16 port = [dict[@"tcpPort"] unsignedShortValue];
        NSLog(@"Streaming TCP port: %d", port);
        
        // Let connectSocketAndStream() method handle socket connection
        connectSocketAndStream(ipAddress, port);
        
        // Start measuring
        [rest postRequestWithPath:@"/rest/rec/measurements" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderRecording" error:&error];
        
        // Streaming should now be running
        
        [NSThread sleepForTimeInterval:5];
        
        // Stop measuring and close socket
        [rest putRequestWithPath:@"/rest/rec/measurements/stop" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderStreaming" error:&error];
        close(sock);
        
        // Finish recording
        [rest putRequestWithPath:@"/rest/rec/finish" body:nil error:&error];
        [rest waitForRecorderState:@"RecorderOpened" error:&error];
        
        // Close Recorder application
        [rest putRequestWithPath:@"/rest/rec/close" body:nil error:&error];
        [rest waitForRecorderState:@"Idle" error:&error];
        
        // Module should now be back in the Idle state without the Recorder application running
        
        // Save samples in file
        // Empty output file
        [@"" writeToFile:outputFile atomically:NO encoding:NSUTF8StringEncoding error:&error];
        NSFileHandle *fileHandle = [NSFileHandle fileHandleForWritingAtPath:outputFile];
        // Write each sample from all channels as a single line in the output file
        for (int i = 0; i < SAMPLES_TO_RECEIVE; i++)
        {
            [fileHandle writeData:[[NSString stringWithFormat:@"%d\t%d\t%d\t%d\n", outputSamples[0][i], outputSamples[1][i], outputSamples[2][i], outputSamples[3][i]] dataUsingEncoding:NSUTF8StringEncoding]];
        }
        [fileHandle closeFile];
        
        // Free allocated resources
        for (int i = 0; i < 4; i++)
        {
            free(outputSamples[i]);
        }
        
        NSLog(@"Finished");
    }
    
    return 0;
}

#pragma mark -
#pragma mark Socket streaming

// Sets up socket communication
void connectSocketAndStream(NSString* address, UInt16 port)
{
    // Set up and connect socket
    sock = socket(AF_INET, SOCK_STREAM, 0);
    
    struct sockaddr_in addr;
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = inet_addr([address UTF8String]);
    
    connect(sock, (struct sockaddr *)&addr, sizeof(addr));
    
    // Set up dispatch queue and IO
    ioQueue = dispatch_queue_create("com.bksv.LANXI.inputStreaming", DISPATCH_QUEUE_SERIAL);
    io = dispatch_io_create(DISPATCH_IO_STREAM, sock, ioQueue, ^(int error) {});
    
    // Socket is ready for receiving data. Hand over control to readHeader().
    readHeader();
}

// Reads the StreamingHeader from the socket data stream.
void readHeader()
{
    dispatch_io_read(io, 0, sizeof(StreamingHeader), ioQueue, ^(bool done, dispatch_data_t data, int error) {
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

// Read the message following the StreamingHeader from the socket data stream.
void readMessage(UInt32 dataLength, UInt16 messageType)
{
    dispatch_io_read(io, 0, dataLength, ioQueue, ^(bool done, dispatch_data_t data, int error) {
        dispatch_data_apply(data, (dispatch_data_applier_t)^(dispatch_data_t region, size_t offset, const void *buffer, size_t size) {
            
            // Simple example - we only care about signal data.
            if (messageType == 1)
            {
                // Make a pointer to the SignalData header.
                SignalDataMessage *signalHeader = (SignalDataMessage*)buffer;
                
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
                    SInt32 sample = ((high << 24) + (mid << 16) + (low << 8)) >> 8;
                    
                    // Store sample in output array
                    outputSamples[signalHeader->signalId-1][samplesReceived[signalHeader->signalId-1]] = sample;
                    
                    // Increment the number of samples gathered for this signal ID
                    samplesReceived[signalHeader->signalId-1]++;
                }
                NSLog(@"Samples received: %d %d %d %d", samplesReceived[0], samplesReceived[1], samplesReceived[2], samplesReceived[3]);
            }
            
            // If we still desire receiving more samples, hand control back to readHeader().
            if (samplesReceived[0] < SAMPLES_TO_RECEIVE || samplesReceived[1] < SAMPLES_TO_RECEIVE || samplesReceived[2] < SAMPLES_TO_RECEIVE || samplesReceived[3] < SAMPLES_TO_RECEIVE)
                readHeader();
        });
    });
}
