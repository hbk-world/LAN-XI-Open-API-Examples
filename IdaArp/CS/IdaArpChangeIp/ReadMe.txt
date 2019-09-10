IdaArpChangeIp

This sample finds a LAN-XI module with a specific serial no on the network and changes the IP configuration of it. Any module connected to the same network - even in different subnets - should be possible to handle.
The module is discovered using IdaArp, the same method as in the IdaArpGetInfo example.

The purpose of the sample is to illustrate what is needed to change the IP configuration of a (possibly physically unreachable) LAN-XI module through the IdaArp protocol, and thus there has not been put much emphasis on error handling etc. As a consequence, if the desired module is not found, the program will stall.

To run this example, change the value of the serialNo variable to match the serial no of the LAN-XI module to configure. Furthermore, set the desired IP configuration in the new_ip, new_gw and new_subnet arrays. The format for thos is { a, b, c, d } where a-d construct the IP address (a.b.c.d).
Setting new_ip to { 0, 0, 0, 0 } tells the module to get an IP address through DHCP - and if no DHCP is available, use a link-local (169.254.x.y) address.