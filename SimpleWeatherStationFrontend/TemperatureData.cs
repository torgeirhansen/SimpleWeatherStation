using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

// TODO: shared class would be nicer
namespace SimpleWeatherStationFrontend
{

    /// <summary>
    /// One weather data record
    /// </summary>
    public sealed class TemperatureRecord
    {
        /// <summary>
        /// ErrorMessage is populated when we're unable to fetch or decode the message received.
        /// </summary>
        public string ErrorMessage { get; set; }

        internal DateTime TimeStamp { get; set; } = DateTime.Now;

        /// <summary>
        /// The background process on the sensor is limited to WinRT types - so no DateTime there.. accept here so that JsonConvert automagically fixes a proper TimeStamp for us.
        /// </summary>
        public DateTimeOffset TimeStampOffset
        {
            get { return TimeStamp.ToUniversalTime(); }
            set { TimeStamp = value.ToLocalTime().DateTime; }
        }

        public float CelsiusTemperature { get; set; }
    }

    public sealed class TemperatureData
    {
        public TemperatureRecord Current { get; set; }

        /// <summary>
        /// Adds the specified WeatherRecord, pointint the Current property to it and adding to the data list(s).
        /// </summary>
        /// <param name="record"></param>
        public void SetCurrent(TemperatureRecord record)
        {
            // Sanity, in case JsonConvert fails.
            if (record == null)
            {
                return;
            }

            Current = record;
        }
    }
}