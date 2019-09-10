using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net;
using System.Runtime.Serialization;
using Shared;
using System.Diagnostics;

namespace StateViewer
{
    [DataContract()]
    internal class ModuleStatusJson
    {
        [DataMember(Name = "moduleState")]
        public string ModuleState { get; set; }

        [DataMember(Name = "ptpStatus")]
        public string PtpStatus { get; set; }

        [DataMember(Name = "inputStatus")]
        public string InputStatus { get; set; }

        [DataMember(Name = "canStartStreaming")]
        public bool CanStartStreaming { get; set; }
    }
    public class Program_StateViwer
    {
        static string[] modules_ip = { "10.100.35.199" };
 /*       
                                                        "10.10.1.1", "10.10.1.2", "10.10.1.3", "10.10.1.4", "10.10.1.5",
                                                        "10.10.2.1", "10.10.2.2", "10.10.2.3", "10.10.2.4", "10.10.2.5",
                                                        "10.10.3.1", "10.10.3.2", "10.10.3.3", "10.10.3.4",
                                                        "10.10.4.1", "10.10.4.2", "10.10.4.3", "10.10.4.4" };
 */       
        private static readonly int numOfModules = modules_ip.Count();      // Number of modules under test
        private static LanXIRESTBoundary[] modules = new LanXIRESTBoundary[numOfModules];
        static bool stopThreads = false;
        static Stopwatch runTime;
        static void Main(string[] args)
        {
            if (numOfModules == 0)
            {
                Console.WriteLine("ERROR: Missing module IP(s).");
                goto DONE;
            }

            runTime = Stopwatch.StartNew();
            runTime.Start();

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
                        if (pIp[0] == 0 || (pIp[0] + pIp[1] + pIp[2] + pIp[3]) == 0)
                            success = false;
                    }
                    if (!success)
                    {
                        Console.WriteLine("ERROR: '{0}' is not a valid IP address", modules_ip[i]);
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
            StartThreads();
            DONE:
            Console.WriteLine("\r\nPress ENTER to terminate");
            Console.ReadLine();

            stopThreads = true;
            Console.WriteLine("Stopping");
        }

        /// <summary>
        /// Depending on data destination calls appropriate functions to start measurement.
        /// </summary>
        /// <param name="tasks">A list of Tasks</param>
        static void StartThreads()
        {
            var threads = new Thread[numOfModules];
            try
            {
                for (int i = 0; i < numOfModules; i++)
                {
                    threads[i] = new Thread((object idx) =>
                    {
                        WaitForStateChange((int)idx);
                    });
                    threads[i].Start(i);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GoToRecorderRecordingState(exception): {0}", ex.Message);
            }
        }

        static void Recurse(Dictionary<string, dynamic> dict, Dictionary<string, dynamic> prev, TimeSpan elapsed, string ip, string keyName = "")
        {
            foreach (string key in dict.Keys)
            {
                var sub = dict[key] as Dictionary<string, dynamic>;
                if (sub != null)
                {
                    var keyName2 = keyName;
                    if (keyName2.Length > 0)
                        keyName2 += ".";
                    keyName2 += key;
                    Recurse(sub, (prev != null && prev.ContainsKey(key)) ? prev[key] : null, elapsed, ip, keyName2);
                }
                else
                {
                    if (prev == null || !prev.ContainsKey(key) || dict[key] != prev[key])
                    {
                        var keyName2 = keyName;
                        if (keyName2.Length > 0)
                            keyName2 += ".";
                        keyName2 += key;
                        Console.WriteLine("{0} {1} {2}: {3}", elapsed, ip, keyName2, dict[key]);
                    }
                }
            }
        }
        static bool WaitForStateChange(int index)
        {
            bool result = false;
            LanXIRESTBoundary module = modules[index];
            int lastUpdate = 0;
            Dictionary<string, dynamic> prev = module.RequestWithPath("/rest/rec/onchange?last=0", "GET", null, false); 

            for (; ; )
            {
                // Get the module state
                Dictionary<string, dynamic> dict = module.RequestWithPath("/rest/rec/onchange?last=" + lastUpdate.ToString(), "GET", null, false);
                TimeSpan elapsed = runTime.Elapsed;      // Get elapsed time
                if (stopThreads == true)
                    break;
                lastUpdate = dict["lastUpdateTag"];
                dict.Remove("lastUpdateTag");
                dict.Remove("lastSdCardUpdateTag");
                dict.Remove("lastTransducerUpdateTag");
                if (dict.ContainsKey("recordingStatus"))
                    (dict["recordingStatus"] as Dictionary<string, dynamic>).Remove("channelStatus");

                Recurse(dict, prev, elapsed, modules_ip[index]);

                prev = dict;
            }
            return result;
        }
    }
}
