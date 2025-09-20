using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class SessionMeta
    {
        [DataMember] public string SessionId { get; set; }
        [DataMember] public string Volume { get; set; }
        [DataMember] public string C0 { get; set; }
        [DataMember] public string N02 { get; set; }
        [DataMember] public string Pressure { get; set; }
        [DataMember] public DateTime StartTime { get; set; }
    }
    [DataContract]
    public class SensorSample
    {
        [DataMember] public string SensorId { get; set; }
        [DataMember] public double Volume { get; set; }
        [DataMember] public double C0 { get; set; }
        [DataMember] public double N02 { get; set; }
        [DataMember] public double Pressure { get; set; }
        [DataMember] public double Temperature { get; set; }
        [DataMember] public double Humidity { get; set; }
        [DataMember] public DateTime TimeStamp { get; set; }
    }
    [DataContract]
    public enum SessionStatus { [EnumMember] IN_PROGRESS, [EnumMember] COMPLETED }

    [DataContract]
    public class OperationResult
    {
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public SessionStatus Status { get; set; }

    }

    [DataContract]
    public class DataFormatFault
    {
        public DataFormatFault(string message) { Message = message; }
        [DataMember] public string Message { get; set; }
    }

    [DataContract]
    public class ValidationFault
    {
        public ValidationFault(string message) { Message = message; }
        [DataMember] public string Message { get; set; }
    }
}
