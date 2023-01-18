MultiModuleInputStreamingOutputGenerator in a frame

This sample shows basic use of input streaming combined with GPS a single frame. 
For use out-of-the-box, the master module is a 3050 
The modules are configured for streaming using REST commands, and a socket connection is made to receive the samples measured. 
The samples from one channel are assembled into 24-bit integers and stored in a simple file where they may be treated further.

In this setup we have opened the right side of the LAN-XI frame soldered a wire on the pps line and connected the wire to 
channel 1 of the 3050. This is NOT recomended for other users, use a PPS from a external GPS.
When streaming the code will look for the first rising edge of the input, and print out the time for this edge.
The purpuse is to show how good the timestamping calculation is.
The time printout is a 64bit hex number: SSSSSSSS:PPPPPPPP where S is seconds sins 1970 and P is fraction of a second 1 LSB = 1/(2^32)
We found that the P in the printout is zero, meaning the GPS-PPS, the sample clock and the calculation works perfect.
When using a extern GPS-PPS P should be a small number, showing the difference between the two GPS's.

The purpose of the sample is to illustrate what is needed to make frame synchronization work with input streaming and GPS
The sync mode is set to "stand-alone".
The example may also only store a small number of samples, due to the way buffering and file writing was constructed.

To run this example, change the values of the constants (LANXI_IP - OUTPUT_FILE and SAMPLES_TO_RECEIVE) to match the desired setup:
LANXI_IP: Array of IP Addresses of the LAN-XI modules to configure and stream from. The first entry will be the Frame master, the rest will be Frame slaves.
OUTPUT_FILE: Path to the file where the samples received should be stored. Samples are output in plain text with one value for each channel (tab-separated)
per line. The first four columns contain values from the master module, slave modules are represented in the following columns.
SAMPLES_TO_RECEIVE: Sets the number of samples to gather for each channel - and store in the output file.

Furthermore, the
Frame_3050_GPS__OpenParameters.json,
Frame_3050_GPS__MasterInputChannelSetup.json,
files may be altered to change the configuration of the channels.