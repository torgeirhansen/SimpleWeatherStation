using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Weatherstation.AzureConnection
{
    /// <summary>
    /// One weather data record
    /// </summary>
    public sealed class WeatherRecord
    {
        public DateTime TimeStamp { get; set; } = DateTime.Now;

        public float Altitude { get; set; }
        public float CelsiusTemperature { get; set; }
        public float Humidity { get; set; }
        public float BarometricPressure { get; set; }
        public float AmbientLight { get; set; }
    }
}