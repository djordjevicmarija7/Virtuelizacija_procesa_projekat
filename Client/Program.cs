using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using System.IO;
using System.Configuration;
using System.Globalization;
using System.Security.Authentication.ExtendedProtection;
using System.Xml.Schema;
using System.Diagnostics;
using System.Threading;
using System.ServiceModel;




namespace Client
{
    public class Program
    {
        static void Main(string[] args)
        {
            var datasetPath = ConfigurationManager.AppSettings["DatasetPath"];
            var rowsToSend = int.Parse(ConfigurationManager.AppSettings["RowsToSend"] ?? "100");

            string sessionId = ConfigurationManager.AppSettings["SessionId"];
            if(!File.Exists(datasetPath))
            {
                Console.WriteLine("Dataset not found:" + datasetPath);
                return;
            }

            ChannelFactory<ISensorService> factory = new ChannelFactory<ISensorService>("SensorServiceEndpoint");
            ISensorService proxy=factory.CreateChannel();

            SessionMeta meta = SessionMeta{
                sessionId = sessionId,
                StartTime=DateTime.UtcNow,
                Volume=0,
                C0=0,
                No2=0,
                Pressure=0
            };

            var startRes = proxy.StartSession(meta);
            Console.WriteLine($"StartSession:{startRes.Message} Status={startRes.Status}");

            int sent = 0;
            using(StreamReader reader=new StreamReader(datasetPath))
            {
                string header=reader.ReadLine();
                while(!reader.EndOfStream && sent < rowsToSend)
                {
                    string line=reader.ReadLine();
                    if(string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    if(!TryParseLine(line, sessionId, out SensorSample sample, out string error))
                    {
                        File.AppendAllText(Path.Combine("ClientLogs", "client_rejects.csv"), $"{sessionId},{DateTime.UtcNow:o}, {error}\n");
                        continue;
                    }
                    var res = proxy.PushSample(sample);
                    Console.WriteLine($"PushSample[{sent + 1}]:{res.Message}");
                    sent++;

                    Thread.Sleep(50);
                }
            }
            var endRes=proxy.EndSession(sessionId);
            Console.WriteLine($"EndSession: {endRes.Message} Status={endRes.Status}");

            ((IClientChannel)proxy).Close();
            factory.Close();
        }
        static bool TryParseLine(string line, string sessionId, out SensorSample sample, out string error)
        {
            sample = null;
            error = null;
            try
            {
                string[] kolone = line.Split(',');
                if (kolone.Length < 7)
                {
                    error = "Insufficient columns";
                    return false;
                }
                DateTime timestamp = DateTime.Parse(kolone[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                double volume = double.Parse(kolone[1], CultureInfo.InvariantCulture);
                double lightLevel = double.Parse(kolone[2], CultureInfo.InvariantCulture);
                double tempDHT = double.Parse(kolone[3], CultureInfo.InvariantCulture);
                double pressure = double.Parse(kolone[4], CultureInfo.InvariantCulture);
                double tempBMP = double.Parse(kolone[5], CultureInfo.InvariantCulture);
                double humidity = double.Parse(kolone[6], CultureInfo.InvariantCulture);
                double airQuality = double.Parse(kolone[7], CultureInfo.InvariantCulture);
                double co = double.Parse(kolone[8], CultureInfo.InvariantCulture);
                double no2 = double.Parse(kolone[9], CultureInfo.InvariantCulture);


                sample = new SensorSample
                {
                    sessionId = sessionId,
                    Timestamp = timestamp,
                    volume = volume,
                    lightLevel = lightLevel,
                    TemperatureDHT = tempDHT,
                    Pressure = pressure,
                    Humidity = humidity,
                    AirQuality = airQuality,
                    C0 = co,
                    N02 = no2

                };
                return true;
            }
            catch(Exception ex)
            {
                error = "Parse error:" + ex.Message;
                return false;
            }
        }
    }
}
