using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;

namespace IoT_DataSimulator
{
    class Program
    {
        // IoT Hub endpoint, device id, key
        static IoT _device0001 = new IoT("Workshop-IoT.azure-devices.net", "IoT_DataSimulator", "");
        private static string _openWeatherAuthorization = "";



        public class WeatherApi
        {
            public class Main
            {
                public double temp { get; set; }
                public double temp_min { get; set; }
                public double temp_max { get; set; }
                public int pressure { get; set; }
                public int humidity { get; set; }
            }

            public class Wind
            {
                public int speed { get; set; }
                public int deg { get; set; }
            }

            public class Clouds
            {
                public int all { get; set; }
            }

            public class Weather
            {
                public int id { get; set; }
                public string main { get; set; }
                public string description { get; set; }
                public string icon { get; set; }
            }
            public class Rain
            {
                public double __invalid_name__3h { get; set; }
            }
            public class RootObject
            {
                public int city_id { get; set; }
                public Main main { get; set; }
                public Wind wind { get; set; }
                public Clouds clouds { get; set; }
                public List<Weather> weather { get; set; }
                public int dt { get; set; }
                public string dt_iso { get; set; }
                public Rain rain { get; set; }
            }
        }
        static void Main(string[] args)
        {
            //Prepare timer to send data each n seconds
            Timer timerUpdateData = new Timer();
            timerUpdateData.Interval = 10*1000;
            timerUpdateData.Elapsed += new ElapsedEventHandler(UpdateData);
            timerUpdateData.Start();

            Console.ReadLine();
        }

        private static void UpdateData(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            Console.WriteLine("Sending Data");
            _device0001.SendData(GetWeather("Leipzig", _device0001.DeviceId, "WeatherStation"));
        }
        
        static object GetWeather(string city, string deviceId, string subDeviceId)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.openweathermap.org/data/2.5/weather?q="+city+"&APPID="+ _openWeatherAuthorization);
            request.Headers.Add("Authorization", _openWeatherAuthorization);

            try
            {
                WebResponse response = request.GetResponse();
                using (Stream responseStream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, System.Text.Encoding.UTF8);
                    var message= reader.ReadToEnd().ToString();
                    JObject obj = JObject.Parse(message);
                    var temp = Math.Round(Convert.ToDouble(((Newtonsoft.Json.Linq.JValue) obj["main"]["temp"]).Value) - 273.15,2);
                    var humidity= Convert.ToDouble(((Newtonsoft.Json.Linq.JValue) obj["main"]["humidity"]).Value);
                    var location = Convert.ToString(((Newtonsoft.Json.Linq.JValue)obj["name"]).Value);
                    var pressure= Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)obj["main"]["pressure"]).Value);
                    var telemetryDataPoint = new
                    {
                        deviceId = deviceId,
                        deviceType = "API",
                        dateTime=DateTime.UtcNow,
                        locationReported = city,
                        temperature = temp,
                        humidity = humidity,
                        pressure= pressure,
                        location=location

                    };
                    return telemetryDataPoint;
                }
            }
            catch (WebException ex)
            {
                WebResponse errorResponse = ex.Response;
                using (Stream responseStream = errorResponse.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, System.Text.Encoding.GetEncoding("utf-8"));
                    String errorText = reader.ReadToEnd();
                }
                return "";
            }
        }
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
    }

    class IoT
    {
        public string DeviceKey;
        public string DeviceId;
        public string IoTHubHostName;

        public IoT(string iotHub, string deviceId, string deviceKey)
        {
            DeviceId = deviceId;
            DeviceKey = deviceKey;
            IoTHubHostName = iotHub;
            ReadDeviceTwinData();
            SendDeviceTwinData();
        }
        private async void SendDeviceTwinData()
        {
            try
            {
                Console.WriteLine("Sending twin data as reported property");
                var client = DeviceClient.CreateFromConnectionString($"HostName={IoTHubHostName};DeviceId={DeviceId};SharedAccessKey={DeviceKey}");
                TwinCollection reportedProperties, windows;
                reportedProperties = new TwinCollection();
                windows= new TwinCollection();
                windows["Computername"] = System.Environment.MachineName;
                windows["Username"] = System.Environment.UserName;
                reportedProperties["Windows"] = windows;
                await client.UpdateReportedPropertiesAsync(reportedProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in updating device twin: {0}", ex.Message);
            }
        }
        private async void ReadDeviceTwinData()
        {
            try
            {
                Console.WriteLine("Reading twin data as reported property");
                var client = DeviceClient.CreateFromConnectionString($"HostName={IoTHubHostName};DeviceId={DeviceId};SharedAccessKey={DeviceKey}");
                var twinData=client.GetTwinAsync().Result;
                Console.WriteLine("Latest twin data:\n"+twinData.ToJson(Formatting.Indented));
                
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in reading device twin: {0}", ex.Message);
            }
        }

        public async void SendData(object dataGram)
        {
            if (dataGram == null)
            {
                Console.WriteLine("Skipping empty datagram");
            }
            else
            {
                var deviceAuthentication = new DeviceAuthenticationWithRegistrySymmetricKey(DeviceId, DeviceKey);
                DeviceClient deviceClient = DeviceClient.Create(IoTHubHostName, deviceAuthentication, TransportType.Http1);
                string messageString = JsonConvert.SerializeObject(dataGram);
                Message message = new Message(Encoding.ASCII.GetBytes(messageString));
                //  message.Properties.Add("temperatureAlert", (currentTemperature > 30) ? "true" : "false");
                deviceClient.SendEventAsync(message).Wait();
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);
            }
        }
    }
}
