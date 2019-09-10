// Simple example streaming from all inputs of a 3160 module and storing a few samples in a file. The file may be read using e.g. Matlab, Microsoft Excel or LibreOffice Calc.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Shared;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Web.Script.Serialization;



namespace CanStreaming
{
    class Program
    {
        static readonly string LANXI_IP = "10.100.35.79";   // IP address (or hostname) of the LAN-XI module
        static readonly string OUTPUT_FILE = "LANXI.out";   // Path to the file where the samples received should be stored
        static readonly int SAMPLES_TO_RECEIVE  = 4096 * 4; // Number of samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s
        static int testTime = 20;                           // Test time in seconds (default value)
        static int numberOfTests = 1;                       // Number of tests
        static bool stopOnError = false;
        static bool obd2Enabled = true;
        static bool VerifyData = false;                  // To verify received OBD data. Requires that obd2Enabled is true
        static int  sendMessage = 0;                        // Number of "SendMessage" command to be send
        static bool testBaudRateDetectCommand = false;

        static int[] samplesReceived = { 0, 0, 0, 0, 0, 0, 0, 0 }; // Used to count the number of samples received - for demo purpose
        static Socket sock;

        static ArrayList[] outputSamples = {new ArrayList(), new ArrayList(), new ArrayList(), new ArrayList(),
                                            new ArrayList(), new ArrayList(), new ArrayList(), new ArrayList()}; // Pointers to output buffers
//        static ArrayList socketBuffer = new ArrayList(); // Buffer for incoming bytes from module.

        static int samples_to_receive = SAMPLES_TO_RECEIVE;
        static UInt64 totalReceivedCanPackages = 0;
        static UInt64 idCounter = 0;
        static int speed = 0, rpm = 0;
        static byte[] dataToTest = new byte[8];
        static UInt16 obdChannel = 0;

//        static int totalErrors = 0;
        static UInt64[] canChanError = {0,0,0};
        static bool dataError = false;


        static int armed = 0;
        static int currentDataSign = 1;
        static int lastDataSign = 1;
        static int[] enabledCanChannels = new int[2];

        static UInt64 anaTime=0, canTime = 0;



        
        static void Main(string[] args)
        {
            // Use program arguments if specified, otherwise use constants.
            // Arguments are in the sequence [[[<lanxi_ip>] <output_file>] <samples_to_receive>]
            string lanxi_ip = LANXI_IP;
            string output_file = OUTPUT_FILE;
            Dictionary<string, dynamic> dict;

            if (args.Length == 0)
            {
                string fileName = @"CanStreaming_testInfo.json";
                if (File.Exists(fileName))
                {
                    string[] testInfo = File.ReadAllLines(fileName);
                    foreach (string line in testInfo)
                    {
                        string[] col = line.Split(',');
                        if (string.Compare(col[0], "ModuleIP") == 0)
                            lanxi_ip = col[1].Trim();
                        
                        if (string.Compare(col[0], "TestTime") == 0)
                            testTime = Convert.ToInt32(col[1]);
                        
                        if (string.Compare(col[0], "NumberOfLoops") == 0)
                            numberOfTests = Convert.ToInt32(col[1]);
                        
                        if (string.Compare(col[0], "StopOnError") == 0)
                            stopOnError = Convert.ToBoolean(col[1]);
                        
                        if (string.Compare(col[0], "VerifyData") == 0)
                            VerifyData = Convert.ToBoolean(col[1]);
                        
                        if (string.Compare(col[0], "EnableObd2") == 0)
                            obd2Enabled = Convert.ToBoolean(col[1]);


                        if (string.Compare(col[0], "TestData") == 0)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                dataToTest[i] = Convert.ToByte(col[i+1]);
                            }
                        }
                    }
                }
            }
            else
            {
                if (args.Length >= 1)
                {
                    lanxi_ip = args[0];
                }
                if (args.Length >= 2)
                {
                    output_file = args[1];
                }
                if (args.Length >= 3)
                {
                    samples_to_receive = Convert.ToInt32(args[2]);
                }
            }

            testTime *= 1000;                   // Conver it to millisconds.

            // Initialize boundary objects
            LanXIRESTBoundary rest = new LanXIRESTBoundary(lanxi_ip);



            // Start measurement
            if (SetModuleToIdle(rest) < 0)
                return;

            // Connect to CAN-IB module. Use the IPs from the Json file
//            string canibIPs = File.ReadAllText(@"CanStreaming_CanIbIPs.json");
//            if (rest.PutRequestWithPath("/rest/rec/can/connect", canibIPs) == null)
//                goto EXIT_STREAMING;


            // Open the Recorder application on the LAN-XI module
            if (rest.PutRequestWithPath("/rest/rec/open", null) == null)
                goto EXIT_STREAMING;
            rest.WaitForRecorderState("RecorderOpened");


            for (int loop = 0; loop < numberOfTests; loop++)
            {
                idCounter = 0;

                Console.WriteLine("\r\n\r\n************************** Test No: {0} **************************", loop + 1);



                //************************* Auto baud rate detection *************************
                // Note: Baud rate detection requires that CAN channel is connected to a CAN-bus
                //       with CAN activity.
                if (testBaudRateDetectCommand)
                {
                    string detectBaudrate = File.ReadAllText(@"CanStreaming_detectBaudRate.json");
                    dict = rest.PutRequestWithPath("/rest/rec/can/detectBaudRate", detectBaudrate);
                    if (dict == null)
                        goto EXIT_STREAMING;

                    var channels = dict["channels"] as ArrayList;
                    foreach (Dictionary<string, object> channel in channels)
                    {
                        var channelId = channel["channel"];
                        var baudRate = channel["baudRate"];
                        Console.WriteLine("channel={0} baudRate={1}", channelId, baudRate);
                    }
                    //goto EXIT_STREAMING;
                }
                //****************************************************************************


                // Create Recorder configuration
                if (rest.PutRequestWithPath("/rest/rec/create", null)==null)
                    goto EXIT_STREAMING;
                rest.WaitForRecorderState("RecorderConfiguring");


                // Set a configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default
                // In this example a JSON file has been prepared with the desired configuration.
                string inputChannelConfiguration = File.ReadAllText(@"CanStreaming_ChannelSetup.json");
                if (inputChannelConfiguration.Length > 0)
                {
                    int j = 0;
                    for (int i = 0; i < 2; i++)
                    {
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        dynamic d = js.Deserialize<dynamic>(inputChannelConfiguration);
                        bool enabled = Convert.ToBoolean(d["canChannels"][i]["enabled"]);
                        if (enabled == true)
                            enabledCanChannels[j++] = i+1;
                    }
                    //Console.WriteLine("Number enabled CAN channels = {0}.", enabledCanChannels);
                }
                
                
/*
                var serializer = new JavaScriptSerializer();
                Dictionary<string, dynamic> json = serializer.Deserialize<Dictionary<string, object>>(inputChannelConfiguration);
                
*/

                if (rest.PutRequestWithPath("/rest/rec/channels/input", inputChannelConfiguration)==null)
                    goto EXIT_STREAMING;
                rest.WaitForRecorderState("RecorderStreaming");



                //******************************** OBD-II ***********************************
                // First delete all messages
                string delMsg = File.ReadAllText(@"CanStreaming_DeleteAllObd2.json");
                if (rest.DeleteRequestWithPath("/rest/rec/can/obd2", delMsg)==null)
                    goto EXIT_STREAMING;


                // We assume that CAN channel 1 is looped back to channel 2 
                if (obd2Enabled)
                {
                    string obd2Msg = File.ReadAllText(@"CanStreaming_AddCanObd2.json");
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    dynamic d = js.Deserialize<dynamic>(obd2Msg);
                    dynamic dataArr = d["Obd2Messages"][0]["data"];
                    var len = dataArr.Length;                    
                    UInt16 chan = Convert.ToUInt16(d["Obd2Messages"][0]["channel"]);
                    if (chan == 1) obdChannel = 102;        // Channel 1 sending OBD2 message and channel 2 receiving data
                    else if (chan == 2) obdChannel = 101;   // Channel 2 sending OBD2 message and channel 1 receiving data
                    else
                    {
                        Console.WriteLine("**************** ERROR **********************");
                        Console.WriteLine("ERROR: Invalid OBD channel number in the OBD-steup (channel={0}). Discarding OBD-setup.", chan);
                        obd2Enabled = false;
                    }

                    // Overwrite test data with data from first OBD2-message in the CanStreaming_AddCanObd2.json file
                    if (VerifyData)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            if (i < len)
                                dataToTest[i] = Convert.ToByte(dataArr[i]);
                            else
                                dataToTest[i] = 0;
                        }
                    }

                    if (rest.PutRequestWithPath("/rest/rec/can/obd2", obd2Msg)==null)
                        goto EXIT_STREAMING;
                }
                //string delMsg = File.ReadAllText(@"CanStreaming_DeleteAllObd2.json");
                //if (rest.DeleteRequestWithPath("/rest/rec/can/obd2", delMsg)==null)
                //  goto EXIT_STREAMING;
                //****************************************************************************


                // Get the TCP port provided by the LAN-XI module for streaming samples
                dict = rest.GetRequestWithPath("/rest/rec/destination/socket");
                if (dict == null)
                    goto EXIT_STREAMING;

                UInt16 port = (UInt16)dict["tcpPort"];
                Console.WriteLine("Streaming TCP port: {0}", port);

                // Start measuring
                if (rest.PostRequestWithPath("/rest/rec/measurements", null)==null)
                    goto EXIT_STREAMING;
                rest.WaitForRecorderState("RecorderRecording");


                //***************************** Send Message ********************************
                if (sendMessage > 0)
                {
                    string sendMessageCommand = File.ReadAllText(@"CanStreaming_SendMessages.json");
                    for (int i = 0; i < sendMessage; i++)
                    {
                        if (rest.PutRequestWithPath("/rest/rec/can/sendMessages", sendMessageCommand)==null)
                            goto EXIT_STREAMING;
                        Thread.Sleep(10);
                    }
                }
                //****************************************************************************




                // Streaming should now be running

                // Let connectSocketAndStream() method handle socket connection
                // The socket connection may be established while the Recorder was in the "RecorderStreaming" state
                ConnectSocketAndStream(lanxi_ip, port);
//                Console.WriteLine("");

                // Stop measuring and close socket
                if (rest.PutRequestWithPath("/rest/rec/measurements/stop", null)==null)
                    goto EXIT_STREAMING;
                rest.WaitForRecorderState("RecorderStreaming");
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();


                // Free memory used for streaming
                //for (int i = 0; i < outputSamples.Count(); i++)
                //{
                //    outputSamples[i].Clear();
                //}

                
                if (obd2Enabled)
                {
                    string delAllObd2Msg = File.ReadAllText(@"CanStreaming_DeleteAllObd2.json");
                    if (rest.DeleteRequestWithPath("/rest/rec/can/obd2", delAllObd2Msg)==null)
                        goto EXIT_STREAMING;
                }


                if (rest.PutRequestWithPath("/rest/rec/finish", null) == null)
                    goto EXIT_STREAMING;
                rest.WaitForRecorderState("RecorderOpened");
//                Console.WriteLine("*** Number of: messages={0} errors={1} ***", idCounter, totalErrors);
                if (stopOnError && dataError) break;
                idCounter = 0;
                totalReceivedCanPackages = 0;
                dataError = false;

            }
            
            // Close Recorder application
            rest.PutRequestWithPath("/rest/rec/close", null);
            rest.WaitForRecorderState("Idle");
            return;

            EXIT_STREAMING:
                SetModuleToIdle(rest);
                return;

                //StreamWriter file = new StreamWriter(output_file);
                //for (int i = 0; i < outputSamples[0].Count; i++)
                //{
                //    finle.WriteLine("{0}\t{1}\t{2}\t{3}", outputSamples[0][i], outputSamples[1][i], outputSamples[2][i], /*outputSamples[3][i]*/0);
                //}
                //file.Close();


        }
        /// <summary>
        /// Sets up socket communication
        /// </summary>
        public static int SetModuleToIdle(LanXIRESTBoundary rest)
        {
            Dictionary<string, dynamic> dict;

            // Bring recorder back from any state to Idle state
            dict = rest.GetRequestWithPath("/rest/rec/onchange");
            if (dict == null)
                return -1;
            if (dict["moduleState"] == "RecorderRecording")
            {
                rest.PutRequestWithPath("/rest/rec/measurements/stop", null);
                rest.WaitForRecorderState("RecorderStreaming");
                rest.PutRequestWithPath("/rest/rec/finish", null);
                rest.WaitForRecorderState("RecorderOpened");
                rest.PutRequestWithPath("/rest/rec/close", null);
                rest.WaitForRecorderState("Idle");
            }
            else if (dict["moduleState"] == "RecorderStreaming")
            {
                rest.PutRequestWithPath("/rest/rec/finish", null);
                rest.WaitForRecorderState("RecorderOpened");
                rest.PutRequestWithPath("/rest/rec/close", null);
                rest.WaitForRecorderState("Idle");
            }
            else if (dict["moduleState"] == "RecorderConfiguring")
            {
                rest.PutRequestWithPath("/rest/rec/cancel", null);
                rest.WaitForRecorderState("RecorderOpened");
                rest.PutRequestWithPath("/rest/rec/close", null);
                rest.WaitForRecorderState("Idle");
            }
            else if (dict["moduleState"] == "RecorderOpened")
            {
                rest.PutRequestWithPath("/rest/rec/close", null);
                rest.WaitForRecorderState("Idle");
            }
            return 0;
        }


        /// <summary>
        /// Sets up socket communication
        /// </summary>
        public static void ConnectSocketAndStream(string address, UInt16 port)
        {
            // Connect to the streaming port on the LAN-XI module
            IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostAddresses(address)[0], port);
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Connect(remoteEP);
            sock.ReceiveTimeout = testTime;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            long elapsedTime = 0;
            // Socket is connected and everything is ready to go. Run until the desired number of samples have been received.
//            while (samplesReceived[0] < samples_to_receive || samplesReceived[1] < samples_to_receive || samplesReceived[2] < samples_to_receive /*|| samplesReceived[3] < samples_to_receive*/)
            while (elapsedTime<(testTime)) {
                StreamingHeader streamingHeader = ReadHeader();
                if (streamingHeader.messageType == 11)
                {
                    //CheckCanDataTimeStamp(streamingHeader.timestamp, streamingHeader.timestampFamily);

                    ReadCanMessage((int)streamingHeader.dataLength, streamingHeader.messageType);
                } else {
                    ReadMessage((int)streamingHeader.dataLength, streamingHeader.messageType, streamingHeader.timestamp);
                }
                elapsedTime = stopwatch.ElapsedMilliseconds;

                for (int i = 0; i < outputSamples.Count(); i++)
                {
                    outputSamples[i].Clear();
                }
            }
            stopwatch.Stop();
        }

        /// <summary>
        /// Reads the StreamingHeader from the socket data stream.
        /// </summary>
        /// <returns>StreamingHeader struct describing the following message.</returns>
        public static StreamingHeader ReadHeader()
        {
            // Initialize StreamingHeader struct and temporary buffer for processing
            StreamingHeader streamingHeader;
            byte[] inputBuffer = new byte[28];
            int bytesReceived = 0;

            try
            {
                // Receive the header
                while (bytesReceived < inputBuffer.Length)
                    bytesReceived += sock.Receive(inputBuffer, bytesReceived, inputBuffer.Length - bytesReceived, SocketFlags.None);
            }
            catch  (SocketException e) {
                if (e.SocketErrorCode == SocketError.TimedOut)
                {
                    Console.WriteLine("No Data Receviced");
                }
            }

            // Populate the StreamingHeader struct from the contents of the temporary buffer
            streamingHeader.magic = new Byte[2];
            for (int i = 0; i < 2; i++)
                streamingHeader.magic[i] = (Byte)BitConverter.ToChar(inputBuffer, i);

            streamingHeader.headerLength = BitConverter.ToUInt16(inputBuffer, 2);
            streamingHeader.messageType = BitConverter.ToUInt16(inputBuffer, 4);
            streamingHeader.reserved1 = BitConverter.ToInt16(inputBuffer, 6);
            streamingHeader.reserved2 = BitConverter.ToInt32(inputBuffer, 8);
            streamingHeader.timestampFamily = BitConverter.ToUInt32(inputBuffer, 12);
            streamingHeader.timestamp = BitConverter.ToUInt64(inputBuffer, 16);
            streamingHeader.dataLength = BitConverter.ToUInt32(inputBuffer, 24);
            //Console.WriteLine("messageType={0:d2}  timestamp={1}  timestampFamily={2}", streamingHeader.messageType, streamingHeader.timestamp, streamingHeader.timestampFamily);
            return streamingHeader;
        }

        /// <summary>
        /// Read the message following the StreamingHeader from the socket data stream.
        /// </summary>
        /// <param name="dataLength">Number of bytes to read and parse.</param>
        /// <param name="messageType">Type of message to parse. 1 means SignalData, and is the only one supported. Other types are ignored.</param>
        public static void ReadMessage(int dataLength, UInt16 messageType,  UInt64 timestamp)
        {
            // Initialize a temporary buffer½
            byte[] inputBuffer = new byte[dataLength];
            int bytesReceived = 0;

            // Read the full message contents into the temporary buffer
            while (bytesReceived < dataLength)
                bytesReceived += sock.Receive(inputBuffer, bytesReceived, dataLength-bytesReceived, SocketFlags.None);

            // Simple examples - we only care about signal data.
            if (messageType == 1)
            {
                // Populate a header struct
                SignalDataMessage signalDataMessage;
                signalDataMessage.numberOfSignals = BitConverter.ToUInt16(inputBuffer, 0);
                signalDataMessage.reserved1 = BitConverter.ToInt16(inputBuffer, 2);
                signalDataMessage.signalId = BitConverter.ToUInt16(inputBuffer, 4);
                signalDataMessage.numberOfValues = BitConverter.ToUInt16(inputBuffer, 6);

                //Console.WriteLine("signalId:  {0:x}  ", signalDataMessage.signalId);

                // Maintain an offset in the buffer and parse through each sample.
                int offset = 8;
                
                for (int i = 0; i < signalDataMessage.numberOfValues; i++)
                {
                    // Collect 3 bytes for a sample.
                    Byte low = inputBuffer[offset++];
                    Byte mid = inputBuffer[offset++];
                    Byte high = inputBuffer[offset++];

                    // Assemble the bytes into a 24-bit sample
                    Int32 sample = (((high << 24) + (mid << 16) + (low << 8)) >> 8);

                    // Store sample in output ArrayList
                    outputSamples[signalDataMessage.signalId - 1].Add(sample);

                    //CheckAnalogDataTimeStamp(signalDataMessage.signalId, sample, timestamp, offset, signalDataMessage.numberOfValues);

                    // Increment the number of samples gathered for this signal ID.
                    samplesReceived[signalDataMessage.signalId - 1]++;
                }
                //Console.WriteLine("Samples received: {0} {1} {2} {3}", samplesReceived[0], samplesReceived[1], samplesReceived[2], samplesReceived[3]);
            }
        }


        /// <summary>
        /// Reads CAN packages send in WebXi message from the socket data stream.
        /// </summary>
        /// <param name="dataLength">Number of bytes to read and parse.</param>
        /// <param name="messageType">Type of message to parse. 1 means SignalData, and is the only one supported. Other types are ignored.</param>
        public static void ReadCanMessage(int dataLength, UInt16 messageType)
        {
            // Initialize a temporary buffer
            byte[] inputBuffer = new byte[dataLength];
            int bytesReceived = 0;

            // Read the full message contents into the temporary buffer
            while (bytesReceived < dataLength)
                bytesReceived += sock.Receive(inputBuffer, bytesReceived, dataLength - bytesReceived, SocketFlags.None);

            // Simple examples - we only care about signal data.

            // Populate a header struct
            AuxSequenceDataHeader auxSequenceHeader;
            auxSequenceHeader.numberOfSequence = BitConverter.ToUInt16(inputBuffer, 0);
            auxSequenceHeader.sequenceId = BitConverter.ToUInt16(inputBuffer, 4);
            auxSequenceHeader.numberOfValues = BitConverter.ToUInt16(inputBuffer, 6);

            // Total number of packaged received so far
            totalReceivedCanPackages += auxSequenceHeader.numberOfValues;

            AuxSequenceData auxSequenceData = new AuxSequenceData();
            auxSequenceData.canData = new byte[8];

            int canChanIndex = auxSequenceHeader.sequenceId - 101;
            
//            if (canChannel>2) canChannel = 0;
            dataError = false;


            int offset = 8;
            for (int i = 0; i < auxSequenceHeader.numberOfValues; i++)
            {
                auxSequenceData.relativeOffsetTime = BitConverter.ToUInt32(inputBuffer, offset);
                auxSequenceData.status = (Byte)BitConverter.ToChar(inputBuffer, offset + 4);
                auxSequenceData.canMessageInfo = (Byte)BitConverter.ToChar(inputBuffer, offset + 5);
                auxSequenceData.canDataSize = (Byte)BitConverter.ToChar(inputBuffer, offset + 6);
                auxSequenceData.canMessageID = BitConverter.ToUInt32(inputBuffer, offset + 8);
                Array.Copy(inputBuffer, (offset + 12), auxSequenceData.canData, 0, 8);
                idCounter++;
                //if (auxSequenceData.canMessageID == 0xcfe6c17)
                {

                    //Console.WriteLine("CAN Message:  ID=0x{0:x}  Data={1:x} counter={2} channel={3}", auxSequenceData.canMessageID, BitConverter.ToString(auxSequenceData.canData), idCounter, auxSequenceHeader.sequenceId);
                    
                }

               // Console.WriteLine("Info: {0}", auxSequenceData.canMessageInfo);
                //Console.WriteLine("RelTime: {0}", auxSequenceData.relativeOffsetTime);
                    
//                if (obd2Enabled)
//                {
//                    if (auxSequenceData.canData[2] == 0x0c) rpm = ((auxSequenceData.canData[3] << 8) + auxSequenceData.canData[4]) >> 2;
//                    if (auxSequenceData.canData[2] == 0x0d) speed = auxSequenceData.canData[3];
//                    Console.WriteLine("Speed: {0} km/h    RPM: {1}", speed, rpm);
//                }


                if (VerifyData)
                {
                    dataError = false;
                    for (byte n = 0; n < 8; n++)
                    {
                        if (auxSequenceData.canData[n] != dataToTest[n])
                        {
//                                Console.WriteLine("ERROR on: CAN-message={0} byte[{1}] expected={2} received={4})", idCounter, n, dataToTest[n], auxSequenceData.canData[n]);
                            dataError = true;
                        }
                    }

                    if (dataError)
                        canChanError[canChanIndex+1]++;

                    Console.WriteLine("CAN message:{0}  Channel={1}  errors={2}  ID={3:x}  Data={4:x}\r", idCounter, enabledCanChannels[canChanIndex], canChanError[canChanIndex+1], auxSequenceData.canMessageID, BitConverter.ToString(auxSequenceData.canData));
                }
                else
                {
                    Console.WriteLine("Message={0} Channel={1} SeqID={2} [CAN: ID={3:x}  Data={4:x}]\r", idCounter, enabledCanChannels[canChanIndex], auxSequenceHeader.sequenceId, auxSequenceData.canMessageID, BitConverter.ToString(auxSequenceData.canData));
                }

                offset += 20;
            }
            //Console.WriteLine("Speed: {0} km/h    RPM: {1}", speed, rpm);
        }

        public static void CheckCanDataTimeStamp(UInt64 timestamp, UInt32 timestampFamily)
        {
            Console.WriteLine("timestamp={0}  timestampFamily={1}", timestamp, timestampFamily);
            canTime = timestamp;

            if (anaTime != 0)
            {
                double diff;

                if (canTime > anaTime)
                    diff = (double)(canTime - anaTime) / 4294967296.0;
                else
                    diff = (double)(anaTime - canTime) / 4294967296.0;

                canTime = 0;
                anaTime = 0;

                Console.WriteLine("1-times diff={0}", diff);
            }
        }

        public static void CheckAnalogDataTimeStamp(UInt16 signalId, Int32 sample, UInt64 timestamp, int offset, UInt16 numberOfValues)
        {
            if (signalId == 1)                      // If this is Channel 1 (We test channel 1 only)
            {
                Int32 absSampVal = Math.Abs(sample & ~0xff);

                if (absSampVal <= 2000000)          // If signal value is less than 2 volt (trigg high->low edge)
                {
                    if (armed == 1)                 // If the signal level has been high
                    {
                        anaTime = timestamp + (UInt32)(((offset - 7) / 3) << 24);
                        Console.WriteLine("Data sign changed sample={0}  armed={1} offset={2} time={3} Nsamples={4}", absSampVal, armed, (offset - 7) / 3, anaTime, numberOfValues);

                        // If CAN-data time stamp is ready, then caluculate time diff between analog and CAN
                        if (canTime != 0)           
                        {
                            double diff;

                            if (canTime > anaTime)
                                diff = (double)(canTime - anaTime) / 4294967296.0;
                            else
                                diff = (double)(anaTime - canTime) / 4294967296.0;

                            canTime = 0;
                            anaTime = 0;
                            Console.WriteLine("Analog: Times diff={0}", diff);
                        }

                        lastDataSign = currentDataSign;
                        armed = 0;
                    }
                }
                else
                {
                    if (absSampVal > 2100000)
                        armed = 1;
                    //Console.WriteLine("Data sign changed sample={0}  armed={1} offset={2}", absSampVal, armed, offset);
                }
                //Console.WriteLine("Data sign changed sample={0}  armed={1} offset={2}", absSampVal, armed, offset);
            }
        }
    }
}
