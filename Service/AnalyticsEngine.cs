using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Common;

namespace Service
{
    public class AnalyticsEngine
    {
        private readonly string sessionId;
        private double lastPressure=double.NaN;
        private double lastC0= double.NaN;
        private double lastN02= double.NaN;
        private double runningPressureMean = 0;
        private int pressureCount = 0;

        private readonly double C0_threshold;
        private readonly double N02_threshold;
        private readonly double P_threshold;
        private readonly double PercentDeviation;

        public AnalyticsEngine(string sessionId)
        {
            this.sessionId = sessionId;
            C0_threshold = double.Parse(ConfigurationManager.AppSettings["C0_threshold"] ?? "0.5", System.Globalization.CultureInfo.InvariantCulture);
            N02_threshold = double.Parse(ConfigurationManager.AppSettings["N02_threshold"] ?? "0.5", System.Globalization.CultureInfo.InvariantCulture);
            P_threshold = double.Parse(ConfigurationManager.AppSettings["P_threshold"] ?? "2.0", System.Globalization.CultureInfo.InvariantCulture);
            PercentDeviation = double.Parse(ConfigurationManager.AppSettings["PercentDeviation"] ?? "25", System.Globalization.CultureInfo.InvariantCulture);
        }

     public List<String> ProcessSample(SensorSample s)
        {
            var warnings = new List<string>();
            if(!double.IsNaN(lastPressure))
            {
                double dP = s.Pressure - lastPressure;
                if (Math.Abs(dP) > P_threshold)
                {
                    string dir = dP > 0 ? "iznad očekivanog" : "ispod očekivanog";
                    warnings.Add($"PressureSpike: |ΔP|={Math.Abs(dP):F2} > {P_threshold} ({dir})");
                }
            }
            pressureCount++;
            runningPressureMean = runningPressureMean + (s.Pressure - runningPressureMean) / pressureCount;

            if (pressureCount > 0)
            {
                double lower = runningPressureMean * (1 - PercentDeviation / 100.0);
                double upper = runningPressureMean * (1 + PercentDeviation / 100.0);
                if (s.Pressure < lower)
                    warnings.Add($"OutOfBandWarning: Pressure {s.Pressure:F2} < lower {lower:F2} (session mean {runningPressureMean:F2})");
                else if (s.Pressure > upper)
                    warnings.Add($"OutOfBandWarning: Pressure {s.Pressure:F2} > upper {upper:F2} (session mean {runningPressureMean:F2})");
            }
            if (!double.IsNaN(lastC0))
            {
                double dC0 = s.CO- lastC0;
                if (Math.Abs(dC0) > C0_threshold)
                {
                    string dir = dC0 > 0 ? "iznad očekivanog" : "ispod očekivanog";
                    warnings.Add($"C0Spike: |ΔC0|={Math.Abs(dC0):F2} > {C0_threshold} ({dir})");
                }
            }
            if (!double.IsNaN(lastN02))
            {
                double dN02 = s.NO2 - lastN02;
                if (Math.Abs(dN02) > N02_threshold)
                {
                    string dir = dN02 > 0 ? "iznad očekivanog" : "ispod očekivanog";
                    warnings.Add($"N02Spike: |ΔNO2|={Math.Abs(dN02):F2} > {N02_threshold} ({dir})");
                }
            }
            lastPressure = s.Pressure;
            lastC0 = s.CO;
            lastN02 = s.NO2;

            return warnings;
        }

    }
}
