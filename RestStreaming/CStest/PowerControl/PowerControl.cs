using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BK.Lanxi.REST.Test.PowerControl
{
    public class Actions
    {
        public static void PowerCycle()
        {
            PowerCycle(new string[] { });
        }

        public static void PowerCycle(string[] args)
        {
            string powerOffURL = "";
            string powerOnURL = "";
            string[] lanxiIps = new string[] { "", "" };

            try
            {
                string[] powerControlURLs = File.ReadAllLines(@"..\..\..\PowerControlURLs.txt");
                powerOffURL = powerControlURLs[0];
                powerOnURL = powerControlURLs[1];
            }
            catch (Exception) { }
            try
            {
                lanxiIps = File.ReadAllLines(@"..\..\..\LanXIIPs.txt");
            }
            catch (Exception) { }

            if (args.Length >= 2)
            {
                powerOffURL = args[0];
                powerOnURL = args[1];
            }
            if (args.Length >= 4)
            {
                lanxiIps = new string[] { args[2], args[3] };
            }

            PowerCycle(powerOffURL, powerOnURL, lanxiIps);
        }

        public static void PowerCycle(string powerOffURL, string powerOnURL, string[] lanxiIps)
        {
            TestResult.Log("", "Using parameters {0} - {1} - {2}", powerOffURL, powerOnURL, lanxiIps);
            // Power off modules
            //((HttpWebRequest)WebRequest.Create("http://192.168.0.50/set.cmd?user=admin&pass=12345678&cmd=setpower+p62=0+p64=0")).GetResponse();
            //((HttpWebRequest)WebRequest.Create("http://10.116.121.196/SetPower.cgi?p1=0+p2=0")).GetResponse();
            try
            {
                TestResult.Log("", "Create request " + powerOffURL);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(powerOffURL);
                TestResult.Log("", "Get response");
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                TestResult.Log("", "Close response");
                response.Close();
                TestResult.Log("", "Response closed, report success");

                //((HttpWebRequest)WebRequest.Create(PowerControlURLs[0])).GetResponse();
                TestResult.Success("", "Sent power off request");
            }
            catch (Exception e)
            {
                TestResult.Log("", "Power off exception: " + e.Message);
                TestResult.Fail("", "Power off request error: " + e.Message);
            }
            Thread.Sleep(1000);
            TestResult.Success("", "Power off modules");

            // Verify that modules do not respond to ping
            TestResult.Log("", "Ping module 0, verify no response");
            TestResult.Log("", "Verify that module " + lanxiIps[0] + " does not reply to ping. " + ((new Ping()).Send(lanxiIps[0], 500).Status != IPStatus.Success));
            TestResult.Log("", "Ping module 1, verify no response");
            TestResult.Log("", "Verify that module " + lanxiIps[1] + " does not reply to ping. " + ((new Ping()).Send(lanxiIps[1], 500).Status != IPStatus.Success));
            TestResult.Log("", "No response verification finished");

            try
            {
                TestResult.Log("", "Create request " + powerOnURL);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(powerOnURL);
                TestResult.Log("", "Get response");
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                TestResult.Log("", "Close response");
                response.Close();
                TestResult.Log("", "Response closed, report success");

                //((HttpWebRequest)WebRequest.Create(PowerControlURLs[1])).GetResponse();
                TestResult.Success("", "Sent power on request");
            }
            catch (Exception e)
            {
                TestResult.Fail("", "Power on request error: " + e.Message);
            }
            TestResult.Success("", "Power on modules");

            // Verify that modules respond to ping
            TestResult.Log("", "Wait for modules to respond to ping");
            int pingTries = 0;
            bool pingSuccess = false;
            Ping pingClient = new Ping();
            while (!pingSuccess)
            {
                pingTries++;
                TestResult.Log("", "Ping attempt #" + pingTries + " module 0");
                PingReply reply = pingClient.Send(lanxiIps[0], 100);
                //TestResult.Success("", "Module " + MultiModuleIpAddresses[0] + " ping attempt #" + pingTries);
                if (reply.Status == IPStatus.Success)
                    pingSuccess = true;
                else if (pingTries > 1200)
                {
                    TestResult.Fail("", "Module " + lanxiIps[0] + " did not respond to ping after " + pingTries + " attempts / 100 ms.");
                    return;
                }
            }
            TestResult.Success("", "Module " + lanxiIps[0] + " responding to ping after " + pingTries + " attempts / 100 ms.");
            TestResult.Log("", "Module " + lanxiIps[0] + " responding to ping after " + pingTries + " attempts / 100 ms.");
            while (!pingSuccess)
            {
                pingTries++;
                TestResult.Log("", "Ping attempt #" + pingTries + " module 1");
                PingReply reply = pingClient.Send(lanxiIps[1], 100);
                //TestResult.Success("", "Module " + MultiModuleIpAddresses[1] + " ping attempt #" + pingTries);
                if (reply.Status == IPStatus.Success)
                    pingSuccess = true;
                else if (pingTries > 1200)
                {
                    TestResult.Fail("", "Module " + lanxiIps[1] + " did not respond to ping after " + pingTries + " attempts / 100 ms.");
                    return;
                }
            }
            TestResult.Success("", "Module " + lanxiIps[1] + " responding to ping after " + pingTries + " attempts / 100 ms.");
            TestResult.Log("", "Module " + lanxiIps[1] + " responding to ping after " + pingTries + " attempts / 100 ms.");
            //Rec.Report(((new Ping()).Send(lanxiIps[0], 500).Status == IPStatus.Success), "Verify that module " + lanxiIps[0] + " replies to ping.");
            //Rec.Report(((new Ping()).Send(lanxiIps[1], 500).Status == IPStatus.Success), "Verify that module " + lanxiIps[1] + " replies to ping.");

            // Wait for modules to respond to requests
            TestResult.Log("", "Verify that modules respond to HTTP connections");
            int connectTries = 0;
            bool connect = false;
            while (connect == false)
            {
                connectTries++;

                try
                {
                    HttpWebRequest request;
                    HttpWebResponse response;

                    TestResult.Log("", "Attempting to connect to " + lanxiIps[0]);
                    request = (HttpWebRequest)WebRequest.Create("http://" + lanxiIps[0].ToString());
                    request.Timeout = 10000;
                    request.ReadWriteTimeout = 10000;
                    request.KeepAlive = false;
                    response = (HttpWebResponse)request.GetResponse();
                    response.Close();
                    TestResult.Success("", "Module " + lanxiIps[0] + " responding to HTTP request.");
                    TestResult.Log("", "Module 0 responding");

                    TestResult.Log("", "Attempting to connect to " + lanxiIps[1]);
                    request = (HttpWebRequest)WebRequest.Create("http://" + lanxiIps[1].ToString());
                    request.Timeout = 10000;
                    request.ReadWriteTimeout = 10000;
                    request.KeepAlive = false;
                    response = (HttpWebResponse)request.GetResponse();
                    response.Close();
                    TestResult.Success("", "Module " + lanxiIps[1] + " responding to HTTP request.");
                    TestResult.Log("", "Module 1 also responding");

                    connect = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    if (connectTries > 120)
                    {
                        TestResult.Fail("", "Modules did not respond to HTTP requests after " + connectTries + " attempts.");
                        return;
                    }
                }
            }
            TestResult.Log("", "Modules respond to HTTP request: " + connect);
        }
    }
}
