using System;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class SessionMeta
    {
        [DataMember] public string SessionId { get; set; }
        [DataMember] public DateTime StartTime { get; set; }
        [DataMember] public double Volume { get; set; }
        [DataMember] public double Pressure { get; set; }
        [DataMember] public double CO { get; set; }    
        [DataMember] public double NO2 { get; set; }    
        [DataMember] public double LightLevel { get; set; }
        [DataMember] public double TempDHT { get; set; }
        [DataMember] public double TempBMP { get; set; }
        [DataMember] public double Humidity { get; set; }
        [DataMember] public double AirQuality { get; set; }
    }

    [DataContract]
    public class SensorSample
    {
        [DataMember] public string SessionId { get; set; }
        [DataMember] public DateTime Timestamp { get; set; } 
        [DataMember] public double Volume { get; set; }    
        [DataMember] public double LightLevel { get; set; }    
        [DataMember] public double TempDHT { get; set; }      
        [DataMember] public double Pressure { get; set; }      
        [DataMember] public double TempBMP { get; set; }       
        [DataMember] public double Humidity { get; set; }     
        [DataMember] public double AirQuality { get; set; }   
        [DataMember] public double CO { get; set; }           
        [DataMember] public double NO2 { get; set; }         
    }

    [DataContract]
    public enum SessionStatus
    {
        [EnumMember] IN_PROGRESS,
        [EnumMember] COMPLETED
    }

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
