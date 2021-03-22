using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace Shared
{
    public class CTedsType
    {
        public string model { get; set; }
        public string number { get; set; }
        public string prefix { get; set; }
        public string variant { get; set; }
    }
    public class CTeds
    {
        public string direction { get; set; }
        public string requires200V { get; set; }
        public string requiresCcld { get; set; }
        public string sensitivity { get; set; }
        public string serialNumber { get; set; }
        public string teds { get; set; }
        public CTedsType type { get; set; }
        public string unit { get; set; }
    }
    public class LanXIRESTBoundary
    {
        string host { get; set; }

        public LanXIRESTBoundary(string host)
        {
            this.host = host;
        }

        /// <summary>
        /// Perform a HTTP request to the host with the body specified. Any response is parsed as JSON.
        /// </summary>
        /// <param name="path">Path to the request resource on the host.</param>
        /// <param name="method">Method to use, ie. GET, PUT, POST etc.</param>
        /// <param name="body">Body to send with the request. If no body is to be sent, set to null.</param>
        /// <returns>Dictionary containing key-value pairs corresponding to the JSON structure received from the host.</returns>
        public Dictionary<string, dynamic> RequestWithPath(string path, string method, string body, bool printout)
        {
            if (printout == true)
                Console.WriteLine("{0} {1} {2}", host, method, path);

            // Create a request to send to the host
            string uri = host + path;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + host + path);
            request.Method = method;
            request.ContentType = "application/json";

            if (body == null)
            {
                request.ContentLength = 0;
            }
            else
            {
                request.ContentLength = body.Length;

                // Send any request body to the host
                StreamWriter requestWriter = new StreamWriter(request.GetRequestStream(), System.Text.Encoding.ASCII);
                requestWriter.Write(body);
                requestWriter.Close();
            }

            try
            {
                // Get the response
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader responseReader = new StreamReader(response.GetResponseStream());
                string responseText = responseReader.ReadToEnd();
                responseReader.Close();
                response.Close();

                if (responseText == "")
                    responseText = "{\"response\": \"none\"}";  // Avoid returning null

                if (method == "POST" && response.ContentType == "text/plain")
                    return new Dictionary<string, dynamic>();

                // Deserialize JSON response into dictionary
                var serializer = new JavaScriptSerializer();
                Dictionary<string, dynamic> jsonResponse = serializer.Deserialize<Dictionary<string, object>>(responseText);
                return jsonResponse;
            }
            catch (WebException ex)
            {
                Console.WriteLine("WebException({0}), {1} (Status={2})", uri, ex.Message, ex.Status);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception({0}), {1}",uri, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Perform a HTTP request to the host with the body specified. Any response is parsed as JSON array.
        /// </summary>
        /// <param name="path">Path to the request resource on the host.</param>
        /// <param name="method">Method to use, ie. GET, PUT, POST etc.</param>
        /// <param name="body">Body to send with the request. If no body is to be sent, set to null.</param>
        /// <returns>list of CTeds containing key-value pairs corresponding to the JSON structure received from the host.</returns>
        public List<CTeds> RequestWithPathTeds(string path, string method, string body, bool printout)
        {
            if (printout == true)
                Console.WriteLine("{0} {1} {2}", host, method, path);

            // Create a request to send to the host
            string uri = host + path;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + host + path);
            request.Method = method;
            request.ContentType = "application/json";

            if (body == null)
            {
                request.ContentLength = 0;
            }
            else
            {
                request.ContentLength = body.Length;

                // Send any request body to the host
                StreamWriter requestWriter = new StreamWriter(request.GetRequestStream(), System.Text.Encoding.ASCII);
                requestWriter.Write(body);
                requestWriter.Close();
            }

            try
            {
                // Get the response
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader responseReader = new StreamReader(response.GetResponseStream());
                string responseText = responseReader.ReadToEnd();
                responseReader.Close();
                response.Close();

                if (responseText == "")
                    responseText = "{\"response\": \"none\"}";  // Avoid returning null


                // Deserialize JSON response into dictionary
                var serializer = new JavaScriptSerializer();
                List<CTeds> jsonResponse = serializer.Deserialize<List<CTeds>>(responseText);
                return jsonResponse;
            }
            catch (WebException ex)
            {
                Console.WriteLine("WebException({0}), {1} (Status={2})", uri, ex.Message, ex.Status);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception({0}), {1}", uri, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Perform a HTTP PUT request to the host with the body specified. Any response is parsed as JSON.
        /// </summary>
        /// <param name="path">Path to the request resource on the host.</param>
        /// <param name="body">Body to send with the request. If no body is to be sent, set to null.</param>
        /// <returns>Dictionary containing key-value pairs corresponding to the JSON structure received from the host.</returns>
        public Dictionary<string, dynamic> PutRequestWithPath(string path, string body)
        {
            return RequestWithPath(path, "PUT", body,true);
        }

        /// <summary>
        /// Perform a HTTP GET request to the host with the body specified. Any response is parsed as JSON.
        /// </summary>
        /// <param name="path">Path to the request resource on the host.</param>
        /// <returns>Dictionary containing key-value pairs corresponding to the JSON structure received from the host.</returns>
        public Dictionary<string, dynamic> GetRequestWithPath(String path)
        {
            return RequestWithPath(path, "GET", null,true);
        }

        /// <summary>
        /// Perform a HTTP POST request to the host with the body specified. Any response is parsed as JSON.
        /// </summary>
        /// <param name="path">Path to the request resource on the host.</param>
        /// <param name="body">Body to send with the request. If no body is to be sent, set to null.</param>
        /// <returns>Dictionary containing key-value pairs corresponding to the JSON structure received from the host.</returns>
        public Dictionary<string, dynamic> PostRequestWithPath(String path, String body)
        {
            return RequestWithPath(path, "POST", body,true);
        }

        /// <summary>
        /// Perform a HTTP DELETE request to the host with the body specified. Any response is parsed as JSON.
        /// </summary>
        /// <param name="path">Path to the request resource on the host.</param>
        /// <param name="body">Body to send with the request. If no body is to be sent, set to null.</param>
        /// <returns>Dictionary containing key-value pairs corresponding to the JSON structure received from the host.</returns>
        public Dictionary<string, dynamic> DeleteRequestWithPath(String path, String body)
        {
            return RequestWithPath(path, "DELETE", body,true);
        }

        /// <summary>
        /// Get for the recorder state.
        /// </summary>
        /// <returns>currentstate.</returns>
        public string GetRecorderState()
        {
            string currentState = "";

            // Get the module state
            Dictionary<string, dynamic> dict = GetRequestWithPath("/rest/rec/onchange");
            currentState = dict["moduleState"];
            return currentState;
        }

        /// <summary>
        /// Waits for the recorder to be in the specified state. The state is polled from the LAN-XI module at 1s interval.
        /// When the state anticipated is reached, the call returns. If the anticipated state is not reached after 255s, the call returns.
        /// </summary>
        /// <param name="state">The state anticipated</param>
        /// <returns>TRUE if the anticipated state was reached, FALSE if the max time has elapsed.</returns>
        public bool WaitForRecorderState(string state)
        {
            int seconds = 0;
            bool result = false;
            string currentState = "";
            uint sec = 0;

            for (; ; )
            {
                // Get the module state
                Dictionary<string, dynamic> dict = GetRequestWithPath("/rest/rec/onchange");
                currentState = dict["moduleState"];
                Console.WriteLine("{0} WaitForRecorderState: {1}, got {2}, ({3} sec)", host, state, currentState, sec++);

                // See if the state is the one anticipated
                if (state.Equals(currentState))
                {
                    // Return
                    result = true;
                    break;
                }

                if (currentState.Equals("PostFailed"))
                {
                    // Return
                    result = false;
                    break;
                }

                // See if max time has elapsed
                if (seconds > 255)
                {
                    result = false;
                    break;
                }

                // Wait and try again
                Thread.Sleep(1000);
                seconds++;
            }

            if (!result)
                throw new TimeoutException("WaitForRecorderState: " + state + " timed out. Last retrieved state: " + currentState + ".");
            return result;
        }
        /// <summary>
        /// Waits for inputStatus to be in the specified state. The state is polled from the LAN-XI module at 1s interval.
        /// When the state anticipated is reached, the call returns. If the anticipated state is not reached after 255s, the call returns.
        /// </summary>
        /// <param name="state">The state anticipated</param>
        /// <returns>TRUE if the anticipated state was reached, FALSE if the max time has elapsed.</returns>
        public bool WaitForInputState(string state)
        {
            int seconds = 0;
            bool result = false;
            string currentState = "";
            uint sec = 0;
            for (; ; )
            {
                // Get the module state
                Dictionary<string, dynamic> dict = GetRequestWithPath("/rest/rec/onchange");
                currentState = dict["inputStatus"];
                Console.WriteLine("WaitForInputStatus: {0}, got {1}, ({2} sec)", state, currentState,sec++);

                // See if the state is the one anticipated
                if (state.Equals(currentState))
                {
                    // Return
                    result = true;
                    break;
                }

                // See if max time has elapsed
                if (seconds > 255)
                    break;

                // Wait and try again
                Thread.Sleep(1000);
                seconds++;
            }

            if (!result)
                throw new TimeoutException("WaitForInputStatus: " + state + " timed out. Last retrieved state: " + currentState + ".");
            return result;
        }
        /// <summary>
        /// Waits for the PTP to be in the specified state. The state is polled from the LAN-XI module at 1s interval.
        /// When the state anticipated is reached, the call returns. If the anticipated state is not reached after 255s, the call returns.
        /// </summary>
        /// <param name="state">The state anticipated</param>
        /// <returns>TRUE if the anticipated state was reached, FALSE if the max time has elapsed.</returns>
        public bool WaitForPtpState(string state)
        {
            int seconds = 0;
            bool result = false;
            string currentState = "";

            for (; ; )
            {
                // Get the module state
                Dictionary<string, dynamic> dict = GetRequestWithPath("/rest/rec/onchange");
                currentState = dict["ptpStatus"];
                Console.WriteLine("WaitForPtpStatus: {0}, got {1}", state, currentState);

                // See if the state is the one anticipated
                if (state.Equals(currentState))
                {
                    // Return
                    result = true;
                    break;
                }

                // See if max time has elapsed
                if (seconds > 255)
                    break;

                // Wait and try again
                Thread.Sleep(1000);
                seconds++;
            }

            if (!result)
                throw new TimeoutException("WaitForPtpStatus: " + state + " timed out. Last retrieved state: " + currentState + ".");
            return result;
        }
    }
}
