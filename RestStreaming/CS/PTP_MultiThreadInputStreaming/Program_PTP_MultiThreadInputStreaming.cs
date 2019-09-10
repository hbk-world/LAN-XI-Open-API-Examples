using System;
using System.Linq;
using System.Text;
using Shared;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections;

public struct ChannelInfo
{
    public string lable;           // A label containing channel info, which will be added in top of each file
    public int numOfactiveChannels;
    public ChannelInfo(string l, int n) { lable = l; numOfactiveChannels = n; }
}



namespace PTP_MultiThreadInputStreaming
{
    class Program
    {
        // ************************************** User can change the following as desired *********************************************

        // Note: The following two constants are measurement stop criteria, but only one of them is 
        //       used at a time. The MEASURE_TIME is used if destination is set to SD card, while
        //       the other one is used if scoket is destination. Mixed destination is not supported.
        private static readonly int MEASURE_TIME = 3;               // Measuremnet time in seconds
        private static readonly int SAMPLES_TO_RECEIVE = 4096 * 64; // Number of samples to receive. Must be a multiple of chunkSize (4096). 32*4096 = 131072 samples = 1s.


        // For convenience purpose the same setup (first setup in the channelSetupFiles[] array) can be used for all modules
        private static readonly bool useSameSetupForAllModules = false;
        private static readonly string inputFilePath = "..//..//";      // Setup file path (Same location as Program.cs)
        private static readonly string outputFilePath = "";             // Output file path (Same location as executable program)
        private static Barrier barrier;                                 //used for thread synchronization
        // The following two arrays are used to select modules and setups. The modules_ip[]
        // should contain modules IP and there must be one entery for each module, where the
        // first IP is selected as master module. 
        // The second arrray must contain setup of modules and should be in accordance with 
        // the 'modules_ip[]' array. However, if all modules should share same the setup 
        // (useSameSetupForAllModules = true), then the first one is used and the rest are ignored.
        //        static readonly string[] modules_ip = { "192.168.200.98", "192.168.200.95", "192.168.200.94", "192.168.200.93", "192.168.200.96" };
        static readonly string[] modules_ip = { "10.10.1.1", "10.10.1.2", "10.10.1.3", "10.10.1.4", "10.10.1.5",
                                                "10.10.2.1", "10.10.2.2", "10.10.2.3", "10.10.2.4", "10.10.2.5",
                                                "10.10.3.1", "10.10.3.2", "10.10.3.3", "10.10.3.4",
                                                "10.10.4.1", "10.10.4.2", "10.10.4.3", "10.10.4.4" };
        static readonly int[] modules_ip_json = { 0,0,0,0,0,
                                                  0,0,0,0,0,
                                                  2,1,1,1,
                                                  2,1,1,1 };
        static readonly string[] channelSetupFiles = { "PTPInputStreaming_InputChannelSetup-3050.json",  //0
                                                       "PTPInputStreaming_InputChannelSetup-3053.json",  //1
                                                       "PTPInputStreaming_InputChannelSetup-3160.json" };//2

        // **************************************************** End of section *********************************************************


        private static readonly int numOfModules = modules_ip.Count();      // Number of modules under test
        private static LanXIRESTBoundary[] modules = new LanXIRESTBoundary[numOfModules];
        private static ChannelInfo[] channelInfo = new ChannelInfo[numOfModules];
        private static bool destIsSDcard = false;
        

        static void Main(string[] args)
        {
            if (numOfModules == 0)
            {
                Console.WriteLine("ERROR: Missing module IP(s).");
                goto DONE;
            }

            Stopwatch runTime = Stopwatch.StartNew();
            runTime.Start();
            Stopwatch taskTime = Stopwatch.StartNew();
            taskTime.Start();

            for (int i = 0; i < numOfModules; i++)
            {
                try
                {
                    IPAddress ip;
                    bool success = IPAddress.TryParse(modules_ip[i], out ip);
                    if (success)
                    {
                        ip = IPAddress.Parse(modules_ip[i]);
                        byte[] pIp = ip.GetAddressBytes();
                        if (pIp[0] == 0 || (pIp[0]+ pIp[1]+ pIp[2]+ pIp[3])==0)
                            success = false;
                    }
                    if (!success)
                    {
                        Console.WriteLine("ERROR: '{0}' is not a valid IP address",modules_ip[i]);
                        goto DONE;
                    }

                    modules[i] = new LanXIRESTBoundary(modules_ip[i]);
                }
                catch (FormatException e)
                {
                    Console.WriteLine("Exception: {0}  ('{1}')", e.Message, modules_ip[i]);
                    goto DONE;
                }
            }

            List<Task<int>> tasks = new List<Task<int>>();

            GoToStartState(tasks);                                  // Make sure that all modules are in Idle state
            TimeSpan startStateTime = GetElapsedTime(taskTime);

            SetSychronizationMode(tasks);
            TimeSpan ptpLockingTime = GetElapsedTime(taskTime);

            OpenRecorder();
            GoToConfigurationRecordingState(tasks);
            TimeSpan cfgStateTime = GetElapsedTime(taskTime);

            RecorderSetup(tasks);                                   // Send channel setup(s) and wait for modules to settle
            TimeSpan settlingTime = GetElapsedTime(taskTime);

            GotToInputSynchronizeState(tasks);                      // Wait for modules to enter the Synchronized input state
            GoToRecorderStreamingState(tasks);                      // Change state to 'RecorderStreaming' and waits for PTP locked state

            GoToRecorderRecordingState();                      // Enters 'RecorderRecording' state and starts measurement
            TimeSpan measuringTime = GetElapsedTime(taskTime);

            GoToStartState(tasks);                                  // Stops measurement and gets modules into 'Idle' state
            TimeSpan restoreTime = GetElapsedTime(taskTime);

            taskTime.Stop();
            runTime.Stop();
            TimeSpan totalRunTime = runTime.Elapsed;

            Console.WriteLine();
            Console.WriteLine("**************************************************");
            Console.WriteLine("Get to Idle state:      {0}", startStateTime);   // Time to get moule into Idle state (if it is left in any other state)
            Console.WriteLine("PTP locking time:       {0}", ptpLockingTime);   // Time used to enter 'RecorderStreaming' state and PTP locked sate
            Console.WriteLine("Configuration time:     {0}", cfgStateTime);     // Time used to get module from 'Idle' to 'RecorderConfiguring' state
            Console.WriteLine("Setup + settling time:  {0}", settlingTime);     // Channel setup and waiting for settling
            Console.WriteLine("Measuring time:         {0}", measuringTime);    // Time used to enter 'RecorderRecording' state and measuring
            Console.WriteLine("Restore (to idle) time: {0}", restoreTime);      // Stop recording and getting module back to Idle state
            Console.WriteLine("Total run time:         {0}", totalRunTime);
            Console.WriteLine("**************************************************");

 DONE:
            Console.WriteLine("\r\nPress ENTER to terminate");
            Console.ReadLine();
        }

        static TimeSpan GetElapsedTime(Stopwatch stopwatch)
        {
            stopwatch.Stop();                           // Stop timer
            TimeSpan elapsed = stopwatch.Elapsed;      // Get elspsed time
            stopwatch.Restart();                        // Resume
            return elapsed;
        }

        /// <summary>
        /// Force all modules into Idle state.
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void GoToStartState(List<Task<int>> tasks)
        {
            try
            {
                for (int i = 0; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {
                        SetRecorderStateTo(modules[(int)idx], "Idle");
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("GoToStartState(exception): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Reads synchronization parameters from file and sends to modules. There are two
        /// type of sychronization paramters, where one is used for master module and the other
        /// one is used for all slave moduels.
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void SetSychronizationMode(List<Task<int>> tasks)
        {
            try
            {
                if (numOfModules == 1)  // Single module, sync command not required
                    return;

                for (int i = 0; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {
                        string syncParam;
                        if ((int)idx == 0)
                            syncParam = inputFilePath + "PTPInputStreaming_SyncParametersMaster.json";
                        else
                            syncParam = inputFilePath +  "PTPInputStreaming_SyncParametersSlave.json";

                        string syncParameters = File.ReadAllText(@syncParam);
                        modules[(int)idx].PutRequestWithPath("/rest/rec/syncmode", syncParameters);
                        // Wait until PTP is locked on all modules
                        modules[(int)idx].WaitForPtpState("Locked");
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("SetSychronizationMode(exception): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Opens the recorder application. This requires that module is not in use,
        /// which means that no application is connect to it.
        /// It use the 'PTPInputStreaming_OpenParameters.json' file to open recorder,
        /// which contains two parameters, one is used to select single or multiple
        /// module and the other one used for TEDS detection (auto transducer detection).
        /// Depending on number of input channels the tranducer detection might take up to 10 senconds.
        /// </summary>
        static void OpenRecorder()
        {
            var threads = new Thread[numOfModules];
            try
            {//send to slaves
                string openParameters;
                for (int i = 1; i < numOfModules; i++)
                {
                    threads[i] = new Thread((object idx) =>
                    {
                        //if (currentState == "Idle")
                        {
                            // Open the Recorder application on the module. The same body is sent to all module, and is prepared in PTPInputStreaming_OpenParameters.json
                            openParameters = File.ReadAllText(inputFilePath + "PTPInputStreaming_OpenParameters.json");
                            modules[(int)idx].PutRequestWithPath("/rest/rec/open", openParameters);

                        }
                    });
                    threads[i].Start(i);
                }
                for (int i = 1; i < numOfModules; i++)
                {
                    threads[i].Join();
                }
                //send to master
                // Open the Recorder application on the module. The same body is sent to all module, and is prepared in PTPInputStreaming_OpenParameters.json
                openParameters = File.ReadAllText(inputFilePath + "PTPInputStreaming_OpenParameters.json");
                modules[0].PutRequestWithPath("/rest/rec/open", openParameters);
                //wait for all modules
                for (int i = 0; i < numOfModules; i++)
                {
                    threads[i] = new Thread((object idx) =>
                    {
                        // Wait for module to be ready; Input in Sampling state, and module in the RecorderOpened state.
                        modules[(int)idx].WaitForInputState("Sampling");
                        modules[(int)idx].WaitForRecorderState("RecorderOpened");
                    });
                    threads[i].Start(i);
                }
                for (int i = 0; i < numOfModules; i++)
                {
                    threads[i].Join();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("OpenRecorder(exception): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Takes modules to 'RecorderConfiguring' state to receive channel setup.
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void GoToConfigurationRecordingState(List<Task<int>> tasks)
        {
            try
            {
                for (int i = 0; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {
                        modules[(int)idx].PutRequestWithPath("/rest/rec/create", null);
                        modules[(int)idx].WaitForRecorderState("RecorderConfiguring");
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("GoToConfigurationRecordingState(exception): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Sends channel setup to each module and waits for them to settle. If the same setup is to be
        /// used for all modules, then only the first setup-file from 'channelSetupFiles []' array is
        /// used otherwise the function expects to find a setup per module.
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void RecorderSetup(List<Task<int>> tasks)
        {
            try
            {
                for (int i = 0; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {
                        string line = "       ";
                        string setup;

                        channelInfo[(int)idx] = new ChannelInfo("", 0);

                        if (useSameSetupForAllModules)
                            setup = inputFilePath + channelSetupFiles[0];
                        else
                            setup = inputFilePath + channelSetupFiles[modules_ip_json[(int)idx]];

                        // Configuration for the input channels. Default configuration can be obtained by sending a GET request to /rest/rec/channels/input/default.
                        // The body has been prepared and stored in PTPInputStreaming_InputChannelSetup.json. 
                        string inputChannelConfiguration = File.ReadAllText(setup);
                        modules[(int)idx].PutRequestWithPath("/rest/rec/channels/input", inputChannelConfiguration);

                        // Find the number of active channels for each module. 
                        // Note: Normally configuration file contains one setup for each input channel, however inactive channels can be omitted.
                        var serializer = new JavaScriptSerializer();
                        Dictionary<string, dynamic> dict = serializer.Deserialize<Dictionary<string, object>>(inputChannelConfiguration);
                        var numberOfChannels = ((ArrayList)dict["channels"]).Count;
                        for (int n = 0; n < numberOfChannels; n++)
                        {   // Find number of active channels
                            if (dict["channels"][n]["enabled"] == true)     // If channel is enabled
                            {
                                channelInfo[(int)idx].numOfactiveChannels++;
                                // Create a lable containing channel info, which will be added at the top of each file 
                                string channelName = "Channel " + dict["channels"][n]["channel"];
                                channelInfo[(int)idx].lable += string.Format("{0,16}", channelName);
                                line += "---------------";

                                if (dict["channels"][n]["destinations"][0] == "sd")
                                    destIsSDcard = true;
                            }
                        }
                        channelInfo[(int)idx].lable += Environment.NewLine + line;

                        if (numOfModules == 1)  //  If  single module
                            modules[(int)idx].WaitForRecorderState("RecorderStreaming");
                        else
                            modules[(int)idx].WaitForInputState("Settled");
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("RecorderSetup(exception): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Waits for input-synchronization state to be reached by all modules
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void GotToInputSynchronizeState(List<Task<int>> tasks)
        {
            try
            {
                if (numOfModules == 1)  // Single module, sync command not required
                    return;

                for (int i = 1; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {   // Wait for module to enter the Synchronized input state
                        modules[(int)idx].PutRequestWithPath("/rest/rec/synchronize", null);
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
                tasks.Clear();
                modules[0].PutRequestWithPath("/rest/rec/synchronize", null);
                for (int i = 0; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {   // Wait for module to enter the Synchronized input state
                        modules[(int)idx].WaitForInputState("Synchronized");
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("GotToInputSynchronizeState(exception): {0}", ex.Message);
            }
        }


        /// <summary>
        /// Changes module's state to 'RecorderStreaming' and waits for PTP locked state.
        /// The very first time after power-up (and depending on ethernet swich used), PTP 
        /// locking might take long time (worse case severeal minutes)
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void GoToRecorderStreamingState(List<Task<int>> tasks)
        {
            try
            {
                if (numOfModules == 1)  // Single module
                    return;

                for (int i = 1; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {   // Wait for module to enter the Synchronized input state
                        modules[(int)idx].PutRequestWithPath("/rest/rec/startstreaming", null);
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
                tasks.Clear();
                modules[0].PutRequestWithPath("/rest/rec/startstreaming", null);
                for (int i = 0; i < numOfModules; i++)
                {
                    tasks.Add(Task.Factory.StartNew<int>((object idx) =>
                    {   // Wait for module to enter the Synchronized input state
                        modules[(int)idx].WaitForRecorderState("RecorderStreaming");
                        return 1;
                    }, i));
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("GoToRecorderStreamingState(exception): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Depending on data destination calls appropriate functions to start measurement.
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void GoToRecorderRecordingState()
        {
            var threads = new Thread[numOfModules];
            barrier     = new Barrier(numOfModules);
            try
            {
                for (int i = 0; i < numOfModules; i++)
                {
                    threads[i] = new Thread((object idx) =>
                    {
                        if (destIsSDcard)
                            StartSDcarMeasuremnet((int)idx);
                        else
                            StartSocketMeasuremnet((int)idx);
                    });
                    threads[i].Start(i);
                }
                for (int i = 0; i < numOfModules; i++)
                {
                    threads[i].Join();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GoToRecorderRecordingState(exception): {0}", ex.Message);
            }
            barrier.Dispose();
        }

        /// Changes module's state to 'RecorderRecording', starts measurement, receives samples and saves into file.
        /// </summary>
        /// <param name="index">Module index</param>
        static void StartSocketMeasuremnet(int index)
        {
            try
            {
                LanXIRESTBoundary module = modules[index];
                int numberOfActiveChannels = channelInfo[index].numOfactiveChannels;
                string columnLable = channelInfo[index].lable;
                string module_ip = modules_ip[index];


                if (index == 0)
                    Thread.CurrentThread.Name = outputFilePath + "Master(" + module_ip + ")";
                else
                    Thread.CurrentThread.Name = outputFilePath + "Slave(" + module_ip + ")";


                StreamWriter file = new StreamWriter(Thread.CurrentThread.Name + ".csv");
                file.WriteLine(columnLable);


                // Prepare buffer to receive samples from module
                int[] samplesReceived = new int[numberOfActiveChannels];    // Contains the number of accumulated samples received- for demo purpose. Array of one array per LAN-XI module, containing one integer per input channel.
                int[][] outputSamples = new int[numberOfActiveChannels][];  // Buffers for storing samples fetched from the LAN-XI module. Requires one array per active channel
                                                                            // Allocate buffer for samples
                for (int channel = 0; channel < numberOfActiveChannels; channel++)
                {
                    outputSamples[channel] = new int[4096];  // Buffer size will dynamically adjusted if required
                    samplesReceived[channel] = 0;
                }

                // Get the TCP ports provided by each LAN-XI module for streaming samples
                Dictionary<string, dynamic> dict = module.GetRequestWithPath("/rest/rec/destination/socket");
                UInt16 modulePort = (UInt16)dict["tcpPort"];

                // Connect the streaming socket to the LAN - XI module defined by address/ port.
                IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostAddresses(module_ip)[0], modulePort);
                // Socket used when fetching samples from the LAN-XI module
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(remoteEP);

                if (index != 0)
                {
                    // Start measuring on slaves, wait for trigger.
                    module.PostRequestWithPath("/rest/rec/measurements", null);
                    // Wait for module to enter RecorderRecording state
                    string recorderRecording = "RecorderRecording";
                    module.WaitForRecorderState(recorderRecording);
                    barrier.SignalAndWait();
                }
                else
                {
                    // Start measuring on master and send trigger
                    barrier.SignalAndWait();
                    Thread.Sleep(200);
                    module.PostRequestWithPath("/rest/rec/measurements", null);
                    // Wait for module to enter RecorderRecording state
                    string recorderRecording = "RecorderRecording";
                    module.WaitForRecorderState(recorderRecording);
                }

                // Handle streaming
                StreamingHeader streamingHeader;
                int channelSamples = 0;
                bool waitingForSamples;
                do
                {
                    waitingForSamples = false;

                    for (int channel = 0; channel < numberOfActiveChannels; channel++)
                    {
                        if (samplesReceived[channel] < SAMPLES_TO_RECEIVE) // If received samples for current channel is incomplete
                        {
                            waitingForSamples = true;
                            streamingHeader = ReadHeader(sock);
                            channelSamples = ReadMessage(sock, (int)streamingHeader.dataLength, streamingHeader.messageType, ref outputSamples);
                            samplesReceived[channel] += channelSamples;  // Accumulate number of samples gathered for current channel
                        }
                    }

                    if (waitingForSamples)
                    {
                        Console.WriteLine("{0} Samples received:  {1}", Thread.CurrentThread.Name, string.Join(", ", samplesReceived));

                        // Write samples to file. Each line contains one sample from each active channel
                        for (int i = 0; i < channelSamples; i++)
                        {
                            for (int channel = 0; channel < numberOfActiveChannels; channel++)
                            {
                                file.Write(string.Format("{0,15:d8},", outputSamples[channel][i])); // Add sample from current channel
                            }
                            file.WriteLine();  // Change line
                        }

                    }
                } while (waitingForSamples);

                file.Close();
                // Stop streaming
                module.PutRequestWithPath("/rest/rec/measurements/stop", null);
                module.WaitForRecorderState("RecorderStreaming");

                // Close sockets
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();
                // Close streaming session
                module.PutRequestWithPath("/rest/rec/finish", null);
                module.WaitForRecorderState("RecorderOpened");

                // Close recorder application
                module.PutRequestWithPath("/rest/rec/close", null);
                module.WaitForRecorderState("Idle");
            }
            catch (Exception ex)
            {
                Console.WriteLine("StartMeasuremnet(exception): {0}", ex.Message);
            }
        }


        /// <summary>
        /// Changes module's state to 'RecorderRecording' and starts measurement.
        /// </summary>
        /// <param name="index">Module index</param>
        static void StartSDcarMeasuremnet(int index)
        {
            try
            {
                LanXIRESTBoundary module = modules[index];
                int numberOfActiveChannels = channelInfo[index].numOfactiveChannels;
                string columnLable = channelInfo[index].lable;
                string module_ip = modules_ip[index];

                // Start measuring.
                module.PostRequestWithPath("/rest/rec/measurements", null);

                // Wait for module to enter RecorderRecording state
                string recorderRecording = "RecorderRecording";
                module.WaitForRecorderState(recorderRecording);

                Thread.Sleep(MEASURE_TIME * 1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine("StartMeasuremnet(exception): {0}", ex.Message);
            }
        }



        /// <summary>
        /// Read and returns message header from socket. The socket must be initialized prior to call.
        /// </summary>
        /// <param name="sock">Socked to read data form.</param>
        public static StreamingHeader ReadHeader(Socket sock)
        {
            // Initialize StreamingHeader struct and temporary buffer for processing
            StreamingHeader streamingHeader;
            byte[] inputBuffer = new byte[28];
            int bytesReceived = 0;

            // Receive the header
            while (bytesReceived < inputBuffer.Length)
                bytesReceived += sock.Receive(inputBuffer, bytesReceived, inputBuffer.Length - bytesReceived, SocketFlags.None);

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

            //Console.WriteLine("Module {0} time: 0x{1:x} 0x{2:x}", moduleNo, streamingHeader.timestampFamily, streamingHeader.timestamp);
            return streamingHeader;
        }


        /// <summary>
        /// Read message body from socket and copies data to output buffer.
        /// </summary>
        /// <param name="sock">Socket to read data from</param>
        /// <param name="dataLength">Number of bytes to read and parse.</param>
        /// <param name="messageType">Type of message to parse. 1 means SignalData, and is the only one supported. Other types are ignored.</param>
        public static int ReadMessage(Socket sock, int dataLength, UInt16 messageType, ref int[][] outputSamples)
        {
            // Initialize a temporary buffer
            byte[] inputBuffer = new byte[dataLength];
            int bytesReceived = 0;

            // Read the full message contents into the temporary buffer
            while (bytesReceived < dataLength)
                bytesReceived += sock.Receive(inputBuffer, bytesReceived, dataLength - bytesReceived, SocketFlags.None);

            // Simple examples - we only care about signal data.
            if (messageType == 1)
            {
                // Populate a header struct
                SignalDataMessage signalDataMessage;
                signalDataMessage.numberOfSignals = BitConverter.ToUInt16(inputBuffer, 0);
                signalDataMessage.reserved1 = BitConverter.ToInt16(inputBuffer, 2);
                signalDataMessage.signalId = BitConverter.ToUInt16(inputBuffer, 4);
                signalDataMessage.numberOfValues = BitConverter.ToUInt16(inputBuffer, 6);

                int channel = signalDataMessage.signalId - 1;  // Channel index 

                // Check for enough storage space in the buffer 
                if (signalDataMessage.numberOfValues > outputSamples[channel].Length)
                {
                    //Console.WriteLine("{0} Increasing channel {1} sample-buffer size from {2} to {3}", Thread.CurrentThread.Name, channel + 1, outputSamples[channel].Length, signalDataMessage.numberOfValues);

                    // Resize outputSamples buffer
                    var t = outputSamples[channel];
                    Array.Resize(ref t, signalDataMessage.numberOfValues);
                    outputSamples[channel] = t;
                }

                // Maintain an offset in the buffer and parse through each sample.
                int offset = 8;
                for (int i = 0; i < signalDataMessage.numberOfValues; i++)
                {
                    // Collect 3 bytes for a sample.
                    Byte low = inputBuffer[offset++];
                    Byte mid = inputBuffer[offset++];
                    Byte high = inputBuffer[offset++];

                    // Assemble the bytes into a 24-bit sample
                    Int32 sample = ((high << 24) + (mid << 16) + (low << 8)) >> 8;

                    // Store sample in output ArrayList
                    outputSamples[channel][i] = sample;
                }
                return (signalDataMessage.numberOfValues);
            }
            return 0;
        }

        /// <summary>
        /// Changes the recorder state to desired one. It can be changed from any state, but in one direction as shown:
        ///     RecorderRecording-->RecorderStreaming-->RecorderConfiguring-->RecorderOpened-->Idle
        /// </summary>
        /// <param name="module">LANXI module</param>
        /// <param name="state">Desired recorder state to achieve</param>
        /// <returns>
        ///     0   Successful
        ///     1   Unable to change to desired state
        ///    -1   Any error
        /// </returns>
        static public int SetRecorderStateTo(LanXIRESTBoundary module, string state)
        {
            Dictionary<string, dynamic> dict;
            bool success = false;
            bool wait = false;

            do
            {
                dict = module.GetRequestWithPath("/rest/rec/onchange");
                if (dict == null)
                    return -1;

                if (dict["moduleState"] == state)
                    return 0;

                if (dict["moduleState"] == "RecorderRecording")
                {
                    module.PutRequestWithPath("/rest/rec/measurements/stop", null);
                    success = module.WaitForRecorderState("RecorderStreaming");
                    wait = true;
                }
                else if (dict["moduleState"] == "RecorderStreaming")
                {   // Check for backward state change request
                    if (state == "RecorderRecording")
                        return 1;

                    module.PutRequestWithPath("/rest/rec/finish", null);
                    success = module.WaitForRecorderState("RecorderOpened");
                    wait = true;
                }
                else if (dict["moduleState"] == "RecorderConfiguring")
                {   // Check for backward state change request
                    if (state == "RecorderRecording" || state == "RecorderStreaming")
                        return 1;

                    module.PutRequestWithPath("/rest/rec/cancel", null);
                    success = module.WaitForRecorderState("RecorderOpened");
                    wait = true;
                }
                else if (dict["moduleState"] == "RecorderOpened")
                {   // Check for backward state change request
                    if (state == "RecorderRecording" || state == "RecorderStreaming" || state == "RecorderConfiguring")
                        return 1;

                    module.PutRequestWithPath("/rest/rec/close", null);
                    success = module.WaitForRecorderState("Idle");
                    wait = true;
                }
                else if (dict["moduleState"] == "Idle")
                {   // Check for backward state change request
                    if (state == "RecorderRecording" || state == "RecorderStreaming" || state == "RecorderConfiguring" || state == "RecorderOpened")
                        return 1;

                    if (wait)
                        Thread.Sleep(1000);
                    return 0;
                }
                else
                {
                    Console.WriteLine("ERROR: Unknown module state {0}", dict["moduleState"]);
                    break;
                }

            } while (success);      // Loop until desired state is reached

            Console.WriteLine("ERROR: {0}, Cannot change module state from {1} to {2}", Thread.CurrentThread.Name, dict["moduleState"], state);
            return -1;
        }
    }
}
