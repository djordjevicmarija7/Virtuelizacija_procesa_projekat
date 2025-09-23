using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Common;

namespace Service
{
    public class FileSessionWriter : IDisposable
    {
        private readonly FileStream sessionFs;
        private readonly StreamWriter sessionWriter;
        private readonly FileStream rejectsFs;
        private readonly StreamWriter rejectsWriter;
        private readonly object writeLock = new object();
        private bool disposed = false;

        public string SessionFilePath { get; }

        public FileSessionWriter(string sessionFilePath, string rejectsFilePath)
        {
            if (string.IsNullOrWhiteSpace(sessionFilePath)) throw new ArgumentNullException(nameof(sessionFilePath));
            if (string.IsNullOrWhiteSpace(rejectsFilePath)) throw new ArgumentNullException(nameof(rejectsFilePath));

            SessionFilePath = sessionFilePath;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sessionFilePath)) ?? ".");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rejectsFilePath)) ?? ".");

            sessionFs = new FileStream(sessionFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            sessionFs.Seek(0, SeekOrigin.End);
            sessionWriter = new StreamWriter(sessionFs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };

            rejectsFs = new FileStream(rejectsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            rejectsFs.Seek(0, SeekOrigin.End);
            rejectsWriter = new StreamWriter(rejectsFs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        }

        public void WriteHeader()
        {
            lock (writeLock)
            {
                try
                {
                    if (sessionFs.Length == 0)
                    {
                        sessionWriter.WriteLine("SessionId,Timestamp,Volume,LightLevel,TempDHT,Pressure,TempBMP,Humidity,AirQuality,CO,NO2");
                        sessionWriter.Flush();
                    }

                    if (rejectsFs.Length == 0)
                    {
                        rejectsWriter.WriteLine("SessionId,Timestamp,Volume,LightLevel,TempDHT,Pressure,TempBMP,Humidity,AirQuality,CO,NO2,Warnings");
                        rejectsWriter.Flush();
                    }
                }
                catch (ObjectDisposedException) {  }
            }
        }

        public void AppendSample(SensorSample s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            string line = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                EscapeForCsv(s.SessionId ?? string.Empty),
                s.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                s.Volume.ToString(CultureInfo.InvariantCulture),
                s.LightLevel.ToString(CultureInfo.InvariantCulture),
                s.TempDHT.ToString(CultureInfo.InvariantCulture),
                s.Pressure.ToString(CultureInfo.InvariantCulture),
                s.TempBMP.ToString(CultureInfo.InvariantCulture),
                s.Humidity.ToString(CultureInfo.InvariantCulture),
                s.AirQuality.ToString(CultureInfo.InvariantCulture),
                s.C0.ToString(CultureInfo.InvariantCulture),
                s.N02.ToString(CultureInfo.InvariantCulture)
            );

            lock (writeLock)
            {
                try
                {
                    sessionWriter.WriteLine(line);
                    sessionWriter.Flush();
                }
                catch (ObjectDisposedException) { }
            }
        }

        public void AppendReject(SensorSample s, IEnumerable<string> warnings)
        {
            if (warnings == null) warnings = Enumerable.Empty<string>();

            lock (writeLock)
            {
                try
                {
                    string warnJoined = string.Join(" | ", warnings).Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");

                    if (s != null)
                    {
                        string line = string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},\"{11}\"",
                            EscapeForCsv(s.SessionId ?? string.Empty),
                            s.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                            s.Volume.ToString(CultureInfo.InvariantCulture),
                            s.LightLevel.ToString(CultureInfo.InvariantCulture),
                            s.TempDHT.ToString(CultureInfo.InvariantCulture),
                            s.Pressure.ToString(CultureInfo.InvariantCulture),
                            s.TempBMP.ToString(CultureInfo.InvariantCulture),
                            s.Humidity.ToString(CultureInfo.InvariantCulture),
                            s.AirQuality.ToString(CultureInfo.InvariantCulture),
                            s.C0.ToString(CultureInfo.InvariantCulture),
                            s.N02.ToString(CultureInfo.InvariantCulture),
                            warnJoined
                        );

                        rejectsWriter.WriteLine(line);
                    }
                    else
                    {
                        string line = string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},,,,,,,,,,,\"{2}\"",
                            EscapeForCsv(string.Empty),
                            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                            warnJoined);
                        rejectsWriter.WriteLine(line);
                    }

                    rejectsWriter.Flush();
                }
                catch (ObjectDisposedException) { }
            }
        }

        private static string EscapeForCsv(string field)
        {
            if (field == null) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                string doubled = field.Replace("\"", "\"\"");
                return $"\"{doubled}\"";
            }
            return field;
        }

        #region Dispose pattern
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    try { sessionWriter?.Flush(); } catch { }
                    try { sessionWriter?.Dispose(); } catch { }
                    try { sessionFs?.Dispose(); } catch { }

                    try { rejectsWriter?.Flush(); } catch { }
                    try { rejectsWriter?.Dispose(); } catch { }
                    try { rejectsFs?.Dispose(); } catch { }
                }
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FileSessionWriter()
        {
            Dispose(false);
        }
        #endregion
    }
}
