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

        public OperatonResult StartSession(SessionMeta meta)
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
            Directory.CreateDirectory(storage);
            lock (sessionsLock)
            {
                if (openSessions.ContainsKey(meta.sessionId))
                {
                    return new OperationResult { Success = false, Message = "Session already exists", Status = SessionStatus.IN_PROGRESS };
                }

                string sessionFolder = Path.Combine(storage, meta.SessionId);
                Directory.CreateDirectory(sessionFolder);

                string sessionFile = Path.Combine(sessionFolder, "measurements_session.csv");
                string rejectsFile = Path.Combine(sessionFolder, "rejects.csv");

                FileSessionWriter writer = new FileSessionWriter(sessionFile, rejectsFile);
                writer.WriteHeader();

                openSessions.Add(meta.SessionId, writer);
                AnalyticsEngine engine = new AnalyticsEngine(meta.SessionId);
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
                if (!openSessions.ContainsKey(sample.SessionId))
                    return new OperationResult { Success = false, Message = "Session not found", Status = SessionStatus.IN_PROGRESS };

                var writer = openSessions[sample.SessionId];
                var engine = analyticsForSession[sample.SessionId];
                try
                {
                    writer.AppendSample(sample);

                    var warnings = engine.ProcessSample(sample);
                    foreach (var w in warnings)
                    {
                        OnWarningRaised?.Invoke(sample.SessionId, w);
                    }
                    OnSampleReceived?.Invoke(sample.SessionId, sample);

                    return new OperationResult { Success = true, Message = "Sample accepted", Status = SessionStatus.IN_PROGRESS };
                }
                catch (Exception ex)
                {
                    writer.AppendReject(sample, ex.Message);
                    return new OperationResult { Success = false, Message = "Failed to store sample: " + ex.Message, Status = SessionStatus.IN_PROGRESS };
                }
            }
        }
        public OperationResult EndSession(string sessionId)
        {
            lock (sessionsLock)
            {
                if (!openSessions.ContainsKey(sessionId))
                    return new OperationResult { Success = false, Message = "Session not found", Status = SessionStatus.COMPLETED };

                var writer = openSessions[sessionId];
                writer.Dispose(); 

                openSessions.Remove(sessionId);

                if (analyticsForSession.ContainsKey(sessionId))
                {
                    analyticsForSession.Remove(sessionId);
                }

                OnTransferCompleted?.Invoke(sessionId);

                return new OperationResult { Success = true, Message = "Session completed", Status = SessionStatus.COMPLETED };
            }
        }
    }
}

    
    

