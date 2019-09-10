OutputGenerator

This sample shows a very basic use of controlling generator output over network on a B&K LAN-XI 3677 module.
3677 has:
four input channels, fs (sampling frequency) = 65536 Hz
one output channel, fs = 131072 Hz

This sample will also work on a 3160 module, with above limitations

The module is configured using REST commands.

The purpose of the sample is to illustrate what is needed to make generator output work, 
and thus there has not been put much emphasis on error handling etc.

To run this example, change the IP address in the LANXI_IP constant.

The OutputGenerator_OutputChannelStart.json file show how the channel is started.
The other json-files may be changed to configure the generator - frequencies etc.
OutputGenerator_OutputChannelSetupDC.json           a DC generator
OutputGenerator_OutputChannelSetupSine              a Sine generator.
OutputGenerator_OutputChannelSetupLinSweep.json     a Linar sine sweep generator
OutputGenerator_OutputChannelSetupLogSweep.json     a Logaritme sine sweep generator
OutputGenerator_OutputChannelSetupRandom.json       a Random generator
OutputGenerator_OutputChannelSetupPseudoRandom.json a Pseudo random generator
OutputGenerator_OutputChannelSetupSquare.json       a Square wave generator