IdaArp examples

These small examples should give a basic insight into how the IdaArp protocol can be used to get information on LAN-XI modules connected to the local network, and change their IP addresses - even without being able to access them normally through IP.

Requirements

The examples habe been developed using Microsoft Visual Studio 2010 on Windows 7. One or more LAN-XI modules are needed.
The examples must be able to bind to UDP port 1024.


Changes

2013-10-21
First version. Examples include getting information on modules discoverable from the computer, and setting the IP of a module. Both examples use the serial no of the module to determine the target.