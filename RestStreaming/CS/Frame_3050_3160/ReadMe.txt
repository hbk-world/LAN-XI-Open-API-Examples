MultiModuleInputStreamingOutputGenerator in a frame

This sample shows basic use of input streaming combined with output generator in a single frame. 
For use out-of-the-box, the master module is a 3050 and the slave is a 3160
The modules are configured for streaming using REST commands, and a socket connection is made to receive the samples measured. 
The samples from all modules are assembled into 24-bit integers and stored in a simple file where they may be treated further.

The purpose of the sample is to illustrate what is needed to make frame synchronization work with input streaming, and a sine output
and thus there has not been put much emphasis on error handling etc.
The sync mode is set to "stand-alone".
Teds detect are enabled, some values of the detected are printet on the console
The example may also only store a small number of samples, due to the way buffering and file writing was constructed.

To run this example, change the values of the constants (LANXI_IP - OUTPUT_FILE and SAMPLES_TO_RECEIVE) to match the desired setup:
LANXI_IP: Array of IP Addresses of the LAN-XI modules to configure and stream from. The first entry will be the Frame master, the rest will be Frame slaves.
OUTPUT_FILE: Path to the file where the samples received should be stored. Samples are output in plain text with one value for each channel (tab-separated)
per line. The first four columns contain values from the master module, slave modules are represented in the following columns.
SAMPLES_TO_RECEIVE: Sets the number of samples to gather for each channel - and store in the output file.

Furthermore, the
Frame_3050_3160__OpenParameters.json,
Frame_3050_3160__MasterInputChannelSetup.json,
Frame_3050_3160__SlaveInputChannelSetup.json,
Frame_3050_3160__OutputChannelSetup.json and
Frame_3050_3160__OutputChannelStart.json files may be altered to change the configuration of the channels.