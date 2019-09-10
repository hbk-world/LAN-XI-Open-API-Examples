PTP_MultiThreadInputStreaming

This example illustrates how to configure several modules for basic use of input streaming using 
PTP-synchronization. There has not been put much emphasis on error handling etc. 

For convenience, the modules can share the same setup, but it is possible to have a separate configuration.
In that case, a separate setup file is required for each module. Through setup file modules can be  
configured to stream data into either SD card or using socket connection to receive measured samples. 
All setup files must use either 'sd' or 'socket' as data destination, mixed configuration is not supported.

Received samples from each module are assembled into 24-bit integers and stored in a separate file in csv 
format. Module's IP number is used to name output files. Each column in the output file represent samples 
from a channel and is labeled with channel number. 
 

To run this example, change the values of the following parameters to match the desired setup:

modules_ip:                 Array of IP Addresses of the LAN-XI modules to configure and stream from.
                            The first entry will be the PTP master, the rest will be PTP slaves.
channelSetupFiles:          Array of setup files that must be in accordance with module IP order. 
inputFilePath:              Path to setup files. Change the configuration file path to desired location
outputFilePath:             Path to output files

MEASURE_TIME:               Measurement time in seconds if destination in the setup file configured to 
                            be 'sd' (SD card). Ignored otherwise.
SAMPLES_TO_RECEIVE:         Number of samples to gather for each channel if destination in the setup file 
                            configured to be 'socket'. Ignored otherwise.
useSameSetupForAllModules:  For convenience this boolean can be set to true in order to share the same 
                            setup (first file in the channelSetupFiles array) for all modules.


