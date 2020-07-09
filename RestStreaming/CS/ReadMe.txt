Simple streaming examples using the REST protocol and sockets

These small examples should give an idea of how the REST protocol may be used to control the LAN-XI modules (type 3160). The focus is on displaying the REST commands and their sequences in certain scenarios. The examples do thus not have much in terms of fault tolerance built in.


Requirements

The examples have been developed using Microsoft Visual Studio 2010 on Windows 7. A LAN-XI type 3160 module is needed.


Changes

2013-10-11
Output timing has been optimized and reflects the changes made in firmware ver. 2.3.0.8.
OutputGenerator example now sends a configuration change while the output is on. This demonstrates the generator's ability to change configuration while output is on.
Output streaming samples make sure the generator buffers are primed before the start command is issued.

2013-10-14
Input sequence changed slightly. Tested with firmware ver. 2.3.0.9.
Minor updates to priming methods for output streaming, and comments etc.
ReadMe updates.

2013-12-04
MultiModuleInputStreaming project added. This project sets up PTP and uses input streaming with two modules.
No effort is made to make sure samples from the modules are aligned - usually they are not - but the PTP setup is described.
Future firmwares and versions of these samples should address the remaining issues.
Tested with LAN-XI firmware ver. 2.3.0.51

2013-12-15
PTP-synchronized sample using 2 modules finished. Samples are aligned.

2019
OutputGenerator3677 added, showing all build-in generator waveforms.
InputStream3677 added, showing how to convert to volt.
PTP_3050_3161 added, showing a 3050 master and a 3160 slave, recording 10 channels and outputing to sine waves with 180 phase.
Frame_3050_3160 added, showing a single frame with two modules.
PTP_MultiFrame added, showing three modules in two frames.
Reboot_html and Reboot_rest, showing two methods to reboot a module.
PTP_MultiThreadInputStreaming added. It demos how to setup and configure several modules simultaneously. 

2020
CIC_MultiModule added, showing how to prepare module for CIC measurement. The same princip must be used for single module