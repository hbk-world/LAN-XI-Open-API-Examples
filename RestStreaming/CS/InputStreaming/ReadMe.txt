InputStreaming

This sample shows a very basic use of input streaming over network on a B&K LAN-XI 3160 module.
The module is configured for streaming using REST commands, and a socket connection is made to receive the samples measured. The samples are assembled into 24-bit integers and stored in a simple file where they may be treated further.

The purpose of the sample is to illustrate what is needed to make input streaming work, and thus there has not been put much emphasis on error handling etc. The example may also only store a small number of samples, due to the way buffering and file writing was constructed.

To run this example, change the values of the constants (LANXI_IP - OUTPUT_FILE and SAMPLES_TO_RECEIVE) to match the desired setup:
LANXI_IP: IP Address of the 3160 module to stream from.
OUTPUT_FILE: Path to the file where the samples received should be stored. Samples are output in plain text with one value for each channel (tab-separated) per line.
SAMPLES_TO_RECEIVE: Sets the number of samples to gather for each channel - and store in the output file.

Furthermore, the InputChannel_InputChannelSetup.json file may be altered to change the configuration of the channels.