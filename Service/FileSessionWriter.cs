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

        /// <summary>
        /// Konstruktor otvara (ili kreira) session i rejects fajlove. 
        /// Fajlovi se otvaraju za append tako da je bezbedno ponovo pokretati servis.
        /// </summary>
        public FileSessionWriter(string sessionFilePath, string rejectsFilePath)
        {
            if (string.IsNullOrWhiteSpace(sessionFilePath)) throw new ArgumentNullException(nameof(sessionFilePath));
            if (string.IsNullOrWhiteSpace(rejectsFilePath)) throw new ArgumentNullException(nameof(rejectsFilePath));

            SessionFilePath = sessionFilePath;

            // Ensure directories exist
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sessionFilePath)) ?? ".");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rejectsFilePath)) ?? ".");

            // Open or create session file and position to end for append
            sessionFs = new FileStream(sessionFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            sessionFs.Seek(0, SeekOrigin.End);
            sessionWriter = new StreamWriter(sessionFs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };

            // Open or create rejects file and position to end
            rejectsFs = new FileStream(rejectsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            rejectsFs.Seek(0, SeekOrigin.End);
            rejectsWriter = new StreamWriter(rejectsFs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        }

        /// <summary>
        /// Zapiše header samo ako je fajl prazan (sprečava višestruko pisanje headera).
        /// </summary>
        public void WriteHeader()
        {
            lock (writeLock)
            {
                try
                {
                    // Ako je fajl prazan (length == 0) napiši header
                    if (sessionFs.Length == 0)
                    {
                        sessionWriter.WriteLine("SessionId,Timestamp,Volume,LightLevel,TempDHT,Pressure,TempBMP,Humidity,AirQuality,CO,NO2");
                        sessionWriter.Flush();
                    }
                }
                catch (ObjectDisposedException) { /* ignore if disposed */ }
            }
        }

        /// <summary>
        /// Dodaje jedan sample u session CSV (redosled kolona mora odgovarati specifikaciji).
        /// </summary>
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
                s.CO.ToString(CultureInfo.InvariantCulture),
                s.NO2.ToString(CultureInfo.InvariantCulture)
            );

            lock (writeLock)
            {
                try
                {
                    sessionWriter.WriteLine(line);
                    sessionWriter.Flush();
                }
                catch (ObjectDisposedException) { /* if disposing/closed, ignore or rethrow depending on policy */ }
            }
        }

        /// <summary>
        /// Zapisuje odbaceni sample u rejects CSV zajedno sa razlogom.
        /// Ako sample == null, zapisuje minimalne informacije (sessionId i reason).
        /// </summary>
        public void AppendReject(SensorSample s, string reason)
        {
            lock (writeLock)
            {
                try
                {
                    string safeReason = (reason ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");

                    if (s != null)
                    {
                        string line = string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},{2}",
                            EscapeForCsv(s.SessionId ?? string.Empty),
                            s.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                            EscapeForCsv(safeReason));
                        rejectsWriter.WriteLine(line);
                    }
                    else
                    {
                        // fallback: only reason
                        string line = string.Format(CultureInfo.InvariantCulture,
                            "{0},{1}",
                            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                            EscapeForCsv(safeReason));
                        rejectsWriter.WriteLine(line);
                    }

                    rejectsWriter.Flush();
                }
                catch (ObjectDisposedException) { /* ignore */ }
            }
        }

        // Simple CSV escaping for fields that may contain commas/newlines.
        private static string EscapeForCsv(string field)
        {
            if (field == null) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                // Double quotes inside field must be doubled according to CSV rules
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
                    // managed resources
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
