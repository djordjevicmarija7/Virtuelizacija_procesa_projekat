using System;
using System.Collections.Generic;
using System.Configuration;
using Common;

namespace Service
{
    public class AnalyticsEngine
    {
        private readonly string sessionId;
        private double lastPressure = double.NaN;
        private double lastC0 = double.NaN;
        private double lastN02 = double.NaN;
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

        public List<string> ProcessSample(SensorSample s)
        {
            var warnings = new List<string>();

            // 1) Pressure spike check (delta from last pressure) — isto kao pre
            if (!double.IsNaN(lastPressure))
            {
                double dP = s.Pressure - lastPressure;
                if (Math.Abs(dP) > P_threshold)
                {
                    string dir = dP > 0 ? "iznad očekivanog" : "ispod očekivanog";
                    warnings.Add($"PressureSpike: |ΔP|={Math.Abs(dP):F2} > {P_threshold} ({dir})");
                }
            }

            // 2) Out-of-band check: koristi PRETHODNI runningPressureMean (strogo prethodni)
            if (pressureCount > 0)
            {
                double lower = runningPressureMean * (1 - PercentDeviation / 100.0);
                double upper = runningPressureMean * (1 + PercentDeviation / 100.0);
                if (s.Pressure < lower)
                    warnings.Add($"OutOfBandWarning: Pressure {s.Pressure:F2} < lower {lower:F2} (session mean {runningPressureMean:F2})");
                else if (s.Pressure > upper)
                    warnings.Add($"OutOfBandWarning: Pressure {s.Pressure:F2} > upper {upper:F2} (session mean {runningPressureMean:F2})");
            }

            // 3) After checks, update running mean using the current sample
            pressureCount++;
            runningPressureMean = runningPressureMean + (s.Pressure - runningPressureMean) / pressureCount;

            // 4) CO spike (C0)
            if (!double.IsNaN(lastC0))
            {
                double dC0 = s.C0 - lastC0;
                if (Math.Abs(dC0) > C0_threshold)
                {
                    string dir = dC0 > 0 ? "iznad ocekivanog" : "ispod ocekivanog";
                    warnings.Add($"C0Spike: |ΔC0|={Math.Abs(dC0):F2} > {C0_threshold} ({dir})");
                }
            }

            // 5) NO2 spike
            if (!double.IsNaN(lastN02))
            {
                double dN02 = s.N02 - lastN02;
                if (Math.Abs(dN02) > N02_threshold)
                {
                    string dir = dN02 > 0 ? "iznad ocekivanog" : "ispod ocekivanog";
                    warnings.Add($"N02Spike: |ΔNO2|={Math.Abs(dN02):F2} > {N02_threshold} ({dir})");
                }
            }

            // 6) Update last values for next invocation
            lastPressure = s.Pressure;
            lastC0 = s.C0;
            lastN02 = s.N02;

            return warnings;
        }
    }
}
