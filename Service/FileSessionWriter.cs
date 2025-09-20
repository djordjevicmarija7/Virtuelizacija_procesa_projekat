using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;

namespace Service
{
    public class FileSessionWriter : IDisposable
    {
        private readonly FileStream sessionFs;
        private readonly StreamWriter sessionWriter;
        private readonly FileStream rejectsFs;
        private readonly StreamWriter rejectsWriter;
        private bool disposed = false;

        public string SessionFilePath { get; }
        public FileSessionWriter(string sessionFilePath, string rejectsFilePath)
        {
            SessionFilePath = sessionFilePath;
            sessionFs = new FileStream(sessionFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            sessionWriter = new StreamWriter(sessionFs, Encoding.UTF8) { AutoFlush = true };

            rejectsFs = new FileStream(rejectsFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            rejectsWriter = new StreamWriter(rejectsFs, Encoding.UTF8) { AutoFlush = true };
        }
        public void WriteHeader()
        {
            sessionWriter.WriteLine("SessionId,Timestamp,Volume,C0,N02,Pressure,Temperature,Humidity");
        }
        public void AppendSample(SensorSample s)
        {
            string line = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7}",
                 s.SessionId,
                 s.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                 s.Volume.ToString(CultureInfo.InvariantCulture),
                 s.C0.ToString(CultureInfo.InvariantCulture),
                 s.N02.ToString(CultureInfo.InvariantCulture),
                 s.Pressure.ToString(CultureInfo.InvariantCulture),
                 s.Temperature.ToString(CultureInfo.InvariantCulture),
                 s.Humidity.ToString(CultureInfo.InvariantCulture)
                 );
            sessionWriter.WriteLine(line);
        }
        public void AppendReject(SensorSample s, string reason)
        {
            string line = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}",
                s.SessionId,
                s.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                reason.Replace(',', ';'));
            rejectsWriter.WriteLine(line);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    sessionWriter?.Dispose();
                    sessionFs?.Dispose();
                    rejectsWriter?.Dispose();
                    rejectsFs?.Dispose();
                }
                disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FileSessionWriter() => Dispose(false);
    }
}
