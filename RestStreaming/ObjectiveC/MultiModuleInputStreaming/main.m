//
//  main.m
//  MultiModuleInputStreaming
//
//  Simple example streaming from two or more LAN-XI modules (4+ input channels, capable of doing 51.2 kHz bandwidth) such as 3160 or 3050 and storing a few samples in a file. The file may be read using e.g. Matlab, Microsoft Excel or LibreOffice Calc.

#import <Foundation/Foundation.h>
#import <arpa/inet.h>
#import <sys/socket.h>
#import <dispatch/dispatch.h>
#import "LanXIRESTBoundary.h"
#import "StreamingHeaders.h"

#define MASTER_IP "169.254.16.0" // IP address (or hostname) of the first LAN-XI module (PTP master)
#define SLAVE_IP "169.254.171.21" // IP address (or hostname) of the second LAN-XI module (PTP slave)
#define OUTPUT_FILE "LANXI.out" // Path to the file where the samples received should be stored. Relative to the build/Debug folder
#define SAMPLES_TO_RECEIVE 4096*8 // Samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s

UInt32 samplesReceived[2][4] = { { 0, 0, 0, 0 },{ 0, 0, 0, 0 } }; // Used to count the number of samples received - for demo purpose
dispatch_queue_t ioQueue; // IO dispatches may be done in a single queue
dispatch_io_t io[2];
int sock[2]; // Sockets for streaming

SInt32 *outputSamples[2][4]; // Pointers to output buffers. 4 channels, 2 modules.
NSString* outputFile = @OUTPUT_FILE; // The file to write samples to

#pragma mark -
#pragma mark Prototypes
void connectSocketAndStream(NSString* address, UInt16 port, int index);
void readHeader(int index);
void readMessage(UInt32 dataLength, UInt16 messageType, int index);

#pragma mark -
#pragma mark Main method
int main(int argc, const char * argv[])
{
    
    @autoreleasepool {
        
        NSError *error;
        NSDictionary *dict;
        
        // Initialize boundary objects
        LanXIRESTBoundary *master = [[LanXIRESTBoundary alloc] initWithHostname:@MASTER_IP];
        LanXIRESTBoundary *slave = [[LanXIRESTBoundary alloc] initWithHostname:@SLAVE_IP];
        
        // Initialize input buffers. Allocate memory for each of the 2x4 channels retrieved.
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < sizeof(outputSamples[i]); j++)
            {
                outputSamples[i][j] = malloc(SAMPLES_TO_RECEIVE * sizeof(SInt32));
            }
        }
        
        // Start measurement
        // During this process commands are generally performed on SLAVEs first, finished with MASTER
        
        // Set synchronization mode on the LAN-XI modules. The body tells which module is master and which is slave. Body for each module is prepared in SyncParametersMaster.json and SyncParametersSlave.json
        NSData *syncParametersSlave = [[NSData alloc] initWithContentsOfFile:@"../../MultiModuleInputStreaming/SyncParametersSlave.json"];
        [slave putRequestWithPath:@"/rest/rec/syncmode" body:syncParametersSlave error:&error];
        NSData *syncParametersMaster = [[NSData alloc] initWithContentsOfFile:@"../../MultiModuleInputStreaming/SyncParametersMaster.json"];
        [master putRequestWithPath:@"/rest/rec/syncmode" body:syncParametersMaster error:&error];
        
        // Wait until PTP is locked on all modules
        [slave waitForPtpState:@"Locked" error:&error];
        [master waitForPtpState:@"Locked" error:&error];
        
        // Open the Recorder application on the modules. The same body is sent to both modules, and is prepared in OpenParameters.json
        NSData *openParameters = [[NSData alloc] initWithContentsOfFile:@"../../MultiModuleInputStreaming/OpenParameters.json"];
        [slave putRequestWithPath:@"/rest/rec/open" body:openParameters error:&error];
        [master putRequestWithPath:@"/rest/rec/open" body:openParameters error:&error];
        
        // Wait for all modules to be ready; Input in Sampling state, and module in the RecorderOpened state.
        [slave waitForInputState:@"Sampling" error:&error];
        [master waitForInputState:@"Sampling" error:&error];
        [slave waitForRecorderState:@"RecorderOpened" error:&error];
        [master waitForRecorderState:@"RecorderOpened" error:&error];
        
        // Create Recorder configuration on all modules
        [slave putRequestWithPath:@"/rest/rec/create" body:nil error:&error];
        [master putRequestWithPath:@"/rest/rec/create" body:nil error:&error];
        
        // Wait for all modules to be in the RecorderConfiguring state.
        [slave waitForRecorderState:@"RecorderConfiguring" error:&error];
        [master waitForRecorderState:@"RecorderConfiguring" error:&error];
        
        // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default.
        // The body has been prepared and stored in InputChannelSetup.json. In this example the setup is identical for the two modules, but it may differ as needed.
        NSData *inputChannelConfiguration = [[NSData alloc] initWithContentsOfFile:@"../../MultiModuleInputStreaming/InputChannelSetup.json"];
        [slave putRequestWithPath:@"/rest/rec/channels/input" body:inputChannelConfiguration error:&error];
        [master putRequestWithPath:@"/rest/rec/channels/input" body:inputChannelConfiguration error:&error];
        
        // Wait until modules enter the Settled input state
        [slave waitForInputState:@"Settled" error:&error];
        [master waitForInputState:@"Settled" error:&error];
        
        // Synchronize modules.
        [slave putRequestWithPath:@"/rest/rec/synchronize" body:nil error:&error];
        [master putRequestWithPath:@"/rest/rec/synchronize" body:nil error:&error];
        
        // Wait for all modules to enter the Synchronized input state
        [slave waitForInputState:@"Synchronized" error:&error];
        [master waitForInputState:@"Synchronized" error:&error];
        
        // Start streaming internally in the LAN-XI modules.
        [slave putRequestWithPath:@"/rest/rec/startstreaming" body:nil error:&error];
        [master putRequestWithPath:@"/rest/rec/startstreaming" body:nil error:&error];
        
        // Wait for all modules to enter the RecorderStreaming state
        [slave waitForRecorderState:@"RecorderStreaming" error:&error];
        [master waitForRecorderState:@"RecorderStreaming" error:&error];
        
        // Get the TCP ports provided by each LAN-XI module for streaming samples
        dict = (id)[slave getRequestWithPath:@"/rest/rec/destination/socket" error:&error];
        UInt16 slavePort = [dict[@"tcpPort"] unsignedShortValue];
        dict = (id)[master getRequestWithPath:@"/rest/rec/destination/socket" error:&error];
        UInt16 masterPort = [dict[@"tcpPort"] unsignedShortValue];
        
        // Let connectSocketAndStream() method handle socket connection
        connectSocketAndStream(@SLAVE_IP, slavePort, 1);
        connectSocketAndStream(@MASTER_IP, masterPort, 0);
        
        // Start measuring.
        [slave postRequestWithPath:@"/rest/rec/measurements" body:nil error:&error];
        [master postRequestWithPath:@"/rest/rec/measurements" body:nil error:&error];
        
        // Wait for modules to enter RecorderRecording state
        [slave waitForRecorderState:@"RecorderRecording" error:&error];
        [master waitForRecorderState:@"RecorderRecording" error:&error];
        
        // Streaming should now be running.
        // For this example, streaming will be allowed to run for a predefined amount of time before it is ended.
        [NSThread sleepForTimeInterval:5];
        
        // Stop measuring and close sockets for all modules.
        // During this process commands are generally performed on MASTER module first, then on SLAVEs
        
        // Stop measurement on modules
        [master putRequestWithPath:@"/rest/rec/measurements/stop" body:nil error:&error];
        [slave putRequestWithPath:@"/rest/rec/measurements/stop" body:nil error:&error];
        
        // Wait for modules to enter RecorderStreaming state
        [master waitForRecorderState:@"RecorderStreaming" error:&error];
        [slave waitForRecorderState:@"RecorderStreaming" error:&error];
        
        // Close sockets
        close(sock[0]);
        close(sock[1]);
        
        // Finish recording
        [master putRequestWithPath:@"/rest/rec/finish" body:nil error:&error];
        [slave putRequestWithPath:@"/rest/rec/finish" body:nil error:&error];
        
        // Wait for modules to enter the RecorderOpened state
        [master waitForRecorderState:@"RecorderOpened" error:&error];
        [slave waitForRecorderState:@"RecorderOpened" error:&error];
        
        // Close Recorder application on all modules
        [master putRequestWithPath:@"/rest/rec/close" body:nil error:&error];
        [slave putRequestWithPath:@"/rest/rec/close" body:nil error:&error];
        
        // Wait for modules to enter the Idle state
        [master waitForRecorderState:@"Idle" error:&error];
        [slave waitForRecorderState:@"Idle" error:&error];
        
        // Modules should now be back in the Idle state without the Recorder application running
        
        // Save samples in file
        // Empty output file
        [@"" writeToFile:outputFile atomically:NO encoding:NSUTF8StringEncoding error:&error];
        NSFileHandle *fileHandle = [NSFileHandle fileHandleForWritingAtPath:outputFile];
        // Write each sample from all channels as a single line in the output file
        for (int i = 0; i < SAMPLES_TO_RECEIVE; i++)
        {
            [fileHandle writeData:[[NSString stringWithFormat:@"%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\n", outputSamples[0][0][i], outputSamples[0][1][i], outputSamples[0][2][i], outputSamples[0][3][i], outputSamples[1][0][i], outputSamples[1][1][i], outputSamples[1][2][i], outputSamples[1][3][i]] dataUsingEncoding:NSUTF8StringEncoding]];
        }
        [fileHandle closeFile];
        
        // Free allocated resources
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                free(outputSamples[i][j]);
            }
        }
        
        NSLog(@"Finished");
    }
    
    return 0;
}

#pragma mark -
#pragma mark Socket streaming

// Sets up socket communication
void connectSocketAndStream(NSString* address, UInt16 port, int index)
{
    // Set up and connect socket
    sock[index] = socket(AF_INET, SOCK_STREAM, 0);
    
    struct sockaddr_in addr;
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = inet_addr([address UTF8String]);
    
    connect(sock[index], (struct sockaddr *)&addr, sizeof(addr));
    
    // Set up dispatch queue and IO
    ioQueue = dispatch_queue_create("com.bksv.LANXI.inputStreaming", DISPATCH_QUEUE_SERIAL);
    io[index] = dispatch_io_create(DISPATCH_IO_STREAM, sock[index], ioQueue, ^(int error) {});
    
    // Socket is ready for receiving data. Hand over control to readHeader().
    readHeader(index);
}

// Reads the StreamingHeader from the socket data stream.
void readHeader(int index)
{
    dispatch_io_read(io[index], 0, sizeof(StreamingHeader), ioQueue, ^(bool done, dispatch_data_t data, int error) {
        dispatch_data_apply(data, (dispatch_data_applier_t)^(dispatch_data_t region, size_t offset, const void *buffer, size_t size) {
            
            // Make a pointer to the header and extract necessary information from it.
            StreamingHeader* header = (StreamingHeader*)buffer;
            UInt32 dataLength = header->dataLength;
            UInt16 messageType = header->messageType;

            // Pass control on to the message parsing method.
            readMessage(dataLength, messageType, index);
            
        });
    });
}

// Read the message following the StreamingHeader from the socket data stream.
void readMessage(UInt32 dataLength, UInt16 messageType, int index)
{
    dispatch_io_read(io[index], 0, dataLength, ioQueue, ^(bool done, dispatch_data_t data, int error) {
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
                    outputSamples[index][signalHeader->signalId-1][samplesReceived[index][signalHeader->signalId-1]] = sample;
                    
                    // Increment the number of samples gathered for this signal ID
                    samplesReceived[index][signalHeader->signalId-1]++;
                }
                NSLog(@"Samples received %d: %d %d %d %d", index, samplesReceived[index][0], samplesReceived[index][1], samplesReceived[index][2], samplesReceived[index][3]);
            }
            
            // If we still desire receiving more samples from the module, hand control back to readHeader().
            if (samplesReceived[index][0] < SAMPLES_TO_RECEIVE || samplesReceived[index][1] < SAMPLES_TO_RECEIVE || samplesReceived[index][2] < SAMPLES_TO_RECEIVE || samplesReceived[index][3] < SAMPLES_TO_RECEIVE)
                readHeader(index);
        });
    });
}
