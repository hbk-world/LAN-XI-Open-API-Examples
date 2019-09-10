OutputGenerator

This sample shows a very basic use of controlling generator output over network on a B&K LAN-XI 3160 module.
The module is configured using REST commands.

The purpose of the sample is to illustrate what is needed to make generator output work, and thus there has not been put much emphasis on error handling etc.

To run this example, change the IP address in the LANXI_IP constant.

The OutputGenerator_OutputChannelStart.json file may be altered to change which channels to start.
    OutputGenerator_OutputChannelSetup.json and 
	OutputGenerator_OutputChannelSetup2.json may be changed to configure the generators - frequencies etc.