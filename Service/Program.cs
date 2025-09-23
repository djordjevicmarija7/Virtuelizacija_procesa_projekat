using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Service
{
    internal class Program
    {
        static void Main(string[] args)
        {
            SensorService.OnTransferStarted += id => Console.WriteLine($"[EVENT] Transfer started: {id}");
            SensorService.OnSampleReceived += (id, sample) => {
                var local = sample.Timestamp.ToLocalTime();
                Console.WriteLine($"[EVENT] Sample received: {id} @ {local.ToString("dd.MM.yyyy HH:mm:ss ")}");
            };
            SensorService.OnWarningRaised += (id, msg) => Console.WriteLine($"[WARNING] Session {id}: {msg}");
            SensorService.OnTransferCompleted += id => Console.WriteLine($"[EVENT] Transfer completed: {id}");

            using (ServiceHost host = new ServiceHost(typeof(SensorService)))
            {
                host.Open();
                Console.WriteLine("SensorService open. Press key to exit.");
                Console.ReadKey();
                host.Close();
            }
        }
    }
}
