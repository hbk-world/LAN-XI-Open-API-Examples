IdaArpGetInfo

This samples broadcasts a discovery message on the network, and receives responses from any LAN-XI modules within reach. Any modules connected to the same network - even in different subnets - should be discovered.
As responses are received, the samples looks for a module with the serial no. given, and when (if) this is found, information about it is printed in the console.

The purpose of the sample is to illustrate what is needed to get information about LAN-XI modules through the IdaArp protocol, and thus there has not been put much emphasis on error handling etc. As a consequence, if the desired module is not found, the program will stall.

To run this example, change the value of the serialNo variable to match the serial no of the LAN-XI module to get information about.