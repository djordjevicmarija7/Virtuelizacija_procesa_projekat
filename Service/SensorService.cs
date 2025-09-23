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

                // IMPORTANT: add analytics engine to dictionary
                var engine = new AnalyticsEngine(meta.SessionId);
                analyticsForSession.Add(meta.SessionId, engine);

                // notify subscribers
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
            if (sample.Pressure < 0)
            {
                throw new FaultException<ValidationFault>(new ValidationFault("Pressure must be > 0"));
            }

            lock (sessionsLock)
            {
                if (!openSessions.TryGetValue(sample.SessionId, out var writer))
                {
                    return new OperationResult { Success = false, Message = "Session not found", Status = SessionStatus.IN_PROGRESS };
                }

                // defensively get or create analytics engine
                if (!analyticsForSession.TryGetValue(sample.SessionId, out var engine))
                {
                    // Option A: create and store new engine
                    engine = new AnalyticsEngine(sample.SessionId);
                    analyticsForSession[sample.SessionId] = engine;

                    // Optionally log that engine was missing
                    // writer.AppendReject(sample, "Analytics engine was missing; created new one.");
                }

                try
                {
                    writer.AppendSample(sample);

                    var warnings = engine.ProcessSample(sample) ?? new List<string>();
                    foreach (var w in warnings)
                    {
                        OnWarningRaised?.Invoke(sample.SessionId, w);
                    }

                    OnSampleReceived?.Invoke(sample.SessionId, sample);

                    return new OperationResult { Success = true, Message = "Sample accepted", Status = SessionStatus.IN_PROGRESS };
                }
                catch (Exception ex)
                {
                    // make sure writer exists before using it in catch (we have it via TryGetValue)
                    try { writer.AppendReject(sample, ex.Message); } catch { /* ignore logging errors */ }
                    return new OperationResult { Success = false, Message = "Failed to store sample: " + ex.Message, Status = SessionStatus.IN_PROGRESS };
                }
            }
        }

        public OperationResult EndSession(string sessionId)
        {
            lock (sessionsLock)
            {
                if (!openSessions.TryGetValue(sessionId, out var writer))
                    return new OperationResult { Success = false, Message = "Session not found", Status = SessionStatus.COMPLETED };

                // dispose writer safely
                try { writer.Dispose(); } catch { /* ignore disposal exceptions */ }

                openSessions.Remove(sessionId);

                if (analyticsForSession.ContainsKey(sessionId))
                {
                    try { analyticsForSession[sessionId] = null; analyticsForSession.Remove(sessionId); }
                    catch { /* ignore */ }
                }

                OnTransferCompleted?.Invoke(sessionId);

                return new OperationResult { Success = true, Message = "Session completed", Status = SessionStatus.COMPLETED };
            }
        }

    }
}

    
    

