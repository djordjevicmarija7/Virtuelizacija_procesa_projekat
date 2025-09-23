using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using System.Globalization;
using System.Threading;
using System.Diagnostics.Eventing.Reader;
using System.Diagnostics;
using System.ServiceModel;
using System.Collections;
using Common;

namespace Service
{
    public delegate void TransferEventHandler(string sessionId);
    public delegate void SampleEventHandler(string sessionId, SensorSample sample);
    public delegate void WarningEventHandler(string sessionId, string message);
    public class SensorService : ISensorService
    {
        public static event TransferEventHandler OnTransferStarted;
        public static event SampleEventHandler OnSampleReceived;
        public static event TransferEventHandler OnTransferCompleted;
        public static event WarningEventHandler OnWarningRaised;

        private static readonly object sessionsLock = new object();
        private static readonly Dictionary<string, FileSessionWriter> openSessions = new Dictionary<string, FileSessionWriter>();
        private static readonly Dictionary<string, AnalyticsEngine> analyticsForSession = new Dictionary<string, AnalyticsEngine>();

        public OperationResult StartSession(SessionMeta meta)
        {
            if (meta == null)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault("Meta is null"));
            }
            if (string.IsNullOrWhiteSpace(meta.SessionId))
            {
                throw new FaultException<ValidationFault>(new ValidationFault("SessionId required"));
            }

            var storage = ConfigurationManager.AppSettings["StorageRoot"];
            if (string.IsNullOrWhiteSpace(storage))
                throw new FaultException<ValidationFault>(new ValidationFault("StorageRoot not configured"));

            Directory.CreateDirectory(storage);

            lock (sessionsLock)
            {
                if (openSessions.ContainsKey(meta.SessionId))
                {
                    return new OperationResult { Success = false, Message = "Session already exists", Status = SessionStatus.IN_PROGRESS };
                }

                string sessionFolder = Path.Combine(storage, meta.SessionId);
                Directory.CreateDirectory(sessionFolder);

                string sessionFile = Path.Combine(sessionFolder, "measurements_session.csv");
                string rejectsFile = Path.Combine(sessionFolder, "rejects.csv");

                var writer = new FileSessionWriter(sessionFile, rejectsFile);
                writer.WriteHeader();

                openSessions.Add(meta.SessionId, writer);

                var engine = new AnalyticsEngine(meta.SessionId);
                analyticsForSession.Add(meta.SessionId, engine);
                OnTransferStarted?.Invoke(meta.SessionId);

                return new OperationResult { Success = true, Message = "Session started", Status = SessionStatus.IN_PROGRESS };
            }
        }

        public OperationResult PushSample(SensorSample sample)
        {

            if (sample == null)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault("Sample is null"));
            }

            if (string.IsNullOrWhiteSpace(sample.SessionId))
            {
                throw new FaultException<ValidationFault>(new ValidationFault("SessionId is required on sample"));
            }

            if (sample.Timestamp == default(DateTime))
            {
                throw new FaultException<ValidationFault>(new ValidationFault("Timestamp is missing or invalid"));
            }

            Action<double, string> assertFinite = (v, name) =>
            {
                if (double.IsNaN(v) || double.IsInfinity(v))
                    throw new FaultException<ValidationFault>(new ValidationFault($"{name} is not a finite number"));
            };

            assertFinite(sample.Volume, "Volume");
            assertFinite(sample.LightLevel, "LightLevel");
            assertFinite(sample.TempDHT, "TempDHT");
            assertFinite(sample.Pressure, "Pressure");
            assertFinite(sample.TempBMP, "TempBMP");
            assertFinite(sample.Humidity, "Humidity");
            assertFinite(sample.AirQuality, "AirQuality");
            assertFinite(sample.C0, "CO");
            assertFinite(sample.N02, "NO2");

            if (sample.Pressure <= 0.0)
                throw new FaultException<ValidationFault>(new ValidationFault("Pressure must be > 0"));
            if (sample.Humidity < 0.0 || sample.Humidity > 100.0)
                throw new FaultException<ValidationFault>(new ValidationFault("Humidity must be in [0,100] %"));
            if (sample.C0 < 0.0)
                throw new FaultException<ValidationFault>(new ValidationFault("CO must be >= 0 (Ohm)"));
            if (sample.N02 < 0.0)
                throw new FaultException<ValidationFault>(new ValidationFault("NO2 must be >= 0 (Ohm)"));
            if (sample.Volume < 0.0)
                throw new FaultException<ValidationFault>(new ValidationFault("Volume must be >= 0"));
            if (sample.LightLevel < 0.0)
                throw new FaultException<ValidationFault>(new ValidationFault("LightLevel must be >= 0"));
            if (sample.TempDHT < -50.0 || sample.TempDHT > 100.0)
                throw new FaultException<ValidationFault>(new ValidationFault("TempDHT out of plausible range"));
            if (sample.TempBMP < -50.0 || sample.TempBMP > 100.0)
                throw new FaultException<ValidationFault>(new ValidationFault("TempBMP out of plausible range"));
            if (sample.Timestamp > DateTime.UtcNow.AddDays(1))
                throw new FaultException<ValidationFault>(new ValidationFault("Timestamp is in the (unreasonable) future"));

            lock (sessionsLock)
            {
                if (!openSessions.TryGetValue(sample.SessionId, out var writer))
                {
                    return new OperationResult { Success = false, Message = "Session not found", Status = SessionStatus.IN_PROGRESS };
                }

                if (!analyticsForSession.TryGetValue(sample.SessionId, out var engine))
                {
                    engine = new AnalyticsEngine(sample.SessionId);
                    analyticsForSession[sample.SessionId] = engine;
                }

                try
                { 
                    var warnings = engine.ProcessSample(sample) ?? new List<string>();
                    if (warnings.Count > 0)
                    {
                        foreach (var w in warnings) OnWarningRaised?.Invoke(sample.SessionId, w);

                        try
                        {
                            writer.AppendReject(sample, warnings);
                        }
                        catch (Exception ex)
                        {
                            try { OnWarningRaised?.Invoke(sample.SessionId, $"Failed to write reject: {ex.Message}"); } catch { }
                        }
                        return new OperationResult { Success = false, Message = "Sample rejected: " + string.Join(" | ", warnings), Status = SessionStatus.IN_PROGRESS };
                    }

                    writer.AppendSample(sample);
                    OnSampleReceived?.Invoke(sample.SessionId, sample);
                    return new OperationResult { Success = true, Message = "Sample accepted", Status = SessionStatus.IN_PROGRESS };
                }
                catch (FaultException) 
                {
                    throw;
                }
                catch (Exception ex)
                {
                    try { writer.AppendReject(sample, new[] { "Exception: " + ex.Message }); } catch {  }
                    return new OperationResult { Success = false, Message = "Failed to process sample: " + ex.Message, Status = SessionStatus.IN_PROGRESS };
                }
            }
        }


        public OperationResult EndSession(string sessionId)
        {
            lock (sessionsLock)
            {
                if (!openSessions.TryGetValue(sessionId, out var writer))
                    return new OperationResult { Success = false, Message = "Session not found", Status = SessionStatus.COMPLETED };

                try { writer.Dispose(); } catch {  }

                openSessions.Remove(sessionId);

                if (analyticsForSession.ContainsKey(sessionId))
                {
                    try { analyticsForSession[sessionId] = null; analyticsForSession.Remove(sessionId); }
                    catch { }
                }

                OnTransferCompleted?.Invoke(sessionId);

                return new OperationResult { Success = true, Message = "Session completed", Status = SessionStatus.COMPLETED };
            }
        }

    }
}

    
    

