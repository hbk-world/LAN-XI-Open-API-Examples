MultiModuleInputStreamingOutputGenerator in two frames

This sample shows basic use of input streaming combined with output generator in a two frames. 
For use out-of-the-box, the master module in master frame is a 3050 and the master module in slave frame is a 3160,
 the slave module in the master frame can be a 3050 or a 3160
The modules are configured for streaming using REST commands, and a socket connection is made to receive the samples measured. 
The samples from all modules are assembled into 24-bit integers and stored in a simple file where they may be treated further.
For simplicity only 2 channels on evry module are used.

The purpose of the sample is to illustrate what is needed to make PTP synchronizatoin work with input streaming, and a sine output
and thus there has not been put much emphasis on error handling etc. 
The example may also only store a small number of samples, due to the way buffering and file writing was constructed.

To run this example, change the values of the constants (LANXI_IP - OUTPUT_FILE and SAMPLES_TO_RECEIVE) to match the desired setup:
LANXI_IP: Array of IP Addresses of the LAN-XI modules to configure and stream from. The first entry will be the Frame master, the rest will be Frame slaves.
OUTPUT_FILE: Path to the file where the samples received should be stored. Samples are output in plain text with one value for each channel (tab-separated)
per line. The first four columns contain values from the master module, slave modules are represented in the following columns.
SAMPLES_TO_RECEIVE: Sets the number of samples to gather for each channel - and store in the output file.

Furthermore, the
PTP_MultiFrame_OpenParameters.json,
PTP_MultiFrame_SyncParametersMaster.json,
PTP_MultiFrame_SyncParametersSlave.json,
PTP_MultiFrame_InputChannelSetup.json,
PTP_MultiFrame_OutputChannelSetup.json and
PTP_MultiFrame_OutputChannelStart.json files may be altered to change the configuration of the channels.