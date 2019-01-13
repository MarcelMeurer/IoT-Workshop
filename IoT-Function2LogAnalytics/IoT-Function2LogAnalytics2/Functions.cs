
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;



namespace IoT_Function2LogAnalytics
{
    public static class Fuction2LogAnalytics
    {
        private static string _workspaceKey = "";
        private static string _workspaceId = "";
        private static string _logTypeName = "IoT";
        private static string _dateField = "dateTime";
        private static string _jsonMustContain = "\"deviceid\"".ToLower();
        private static LogAnalytics _logAnalytics = new LogAnalytics(_workspaceId, _workspaceKey);

        [FunctionName("Send2LogAnalytics")]
        public static async Task<HttpResponseMessage> Send2LogAnalytics([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("Function invoked: " + "Send2LogAnalytics");
            var debugMessage = Environment.GetEnvironmentVariable("DebugMessage");


            //var debugMessage = config["DebugMessage"];
            if (debugMessage!=null)
            {
                log.Info("Debug message: "+debugMessage);
            }
            
            
            log.Info(req.ToString());
            string requestBody = await req.Content.ReadAsStringAsync();
            log.Info("Request body:\n" + requestBody);
            if (requestBody.ToLower().Contains(_jsonMustContain))
            {
                _logAnalytics.SetLogInstance(log);
                _logAnalytics.PostData(_logTypeName, requestBody, _dateField);
                log.Info("Data sended to workspace");
            }
            else
            {
                log.Error("Invalid data. A mandatory string is missing: " + _jsonMustContain);
                return req.CreateResponse(HttpStatusCode.BadRequest, "Invalid data. A mandatory string is missing: " + _jsonMustContain);
            }
            return req.CreateResponse(HttpStatusCode.OK, "Data sent to LogAnalytics.");
        }
    }
    public class LogAnalytics
    {
        private readonly string _customerId;
        private readonly string _sharedKey;
        private TraceWriter _log;
        public LogAnalytics(string customerId, string sharedKey)
        {
            _customerId = customerId;
            _sharedKey = sharedKey;
        }
        public void SetLogInstance(TraceWriter log)
        {
            _log = log;
        }
        public void PostData(string logName, string json, string timeGenField)
        {
            var date = DateTime.UtcNow.ToString("r");
            if (logName.Contains("."))
            {
                logName = logName.Split('.')[1];        // todo: replace invalid chars like . or - ...
            }

            string stringToHash = "POST\n" + Encoding.UTF8.GetByteCount(json)+ "\napplication/json\n" + "x-ms-date:" + date + "\n/api/logs";
            string hashedString = LogAnalyticsBuildSignature(stringToHash, _sharedKey);
            string signature = "SharedKey " + _customerId + ":" + hashedString;

            string url = "https://" + _customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                    client.Headers.Add("Log-Type", logName);
                    client.Headers.Add("Authorization", signature);
                    client.Headers.Add("x-ms-date", date);
                    client.Headers.Add("time-generated-field", timeGenField);
                    client.Encoding = System.Text.Encoding.UTF8;
                    client.UploadString(new Uri(url), "POST", json);
                }
            }
            catch (Exception ex)
            {
                _log.Error("LogAnalytics: Cannot send data\nError Message:\n" + ex.Message);
                throw ex;
            }
        }
        public static string LogAnalyticsBuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);

            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }
        private class NewWebClient : WebClient
        {
            public int Timeout = 5000;
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                w.Timeout = Timeout;
                return w;
            }
        }
    }
}
