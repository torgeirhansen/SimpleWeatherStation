using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWeatherStationFrontend {
    public sealed class WeatherData {
        private WeatherRecord currentRecord;

        public WeatherRecord Current
        {
            get
            {
                lock (this)
                {
                    return currentRecord;
                }
            }
            private set
            {
                lock (this)
                {
                    currentRecord = value;
                }
            }
        }

        public Dictionary<DateTime, WeatherRecord> LastDayValues { get; private set; }

        public WeatherData() {
            LastDayValues = new Dictionary<DateTime, WeatherRecord>();
        }

        /// <summary>
        /// Points the Current property to the new value.
        /// </summary>
        /// <param name="weatherRecord"></param>
        public void SetCurrent(WeatherRecord weatherRecord) {
            // Sanity, in case JsonConvert fails.
            if(weatherRecord == null) {
                return;
            }
            
            // The current property does locking.
            Current = weatherRecord;
        }
    }

    public sealed class WeatherRecord {
        /// <summary>
        /// ErrorMessage is populated when we're unable to fetch or decode the message received.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// When the reading occured (source-device time), or when the error occured (gathering-device time) - in UTC time.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// The background process on the sensor is limited to WinRT types - so no DateTime there.. accept here so that JsonConvert automagically fixes a proper TimeStamp for us.
        /// </summary>
        public DateTimeOffset TimeStampOffset
        {
            get { return TimeStamp.ToUniversalTime(); }
            set { TimeStamp = value.ToLocalTime().DateTime; }
        }

        public double Altitude { get; set; }
        public double AmbientLight { get; set; }
        public double BarometricPressure { get; set; }
        public double CelsiusTemperature { get; set; }
        public double Humidity { get; set; }
    }
}
