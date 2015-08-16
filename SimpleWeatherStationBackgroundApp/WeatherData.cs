using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SimpleWeatherStationBackgroundApp
{

    /// <summary>
    /// One weather data record
    /// </summary>
    public sealed class WeatherRecord
    {
        internal DateTime TimeStamp { get; set; } = DateTime.Now;

        /// <summary>
        /// WinRT compability property (DateTime is not supported..)
        /// </summary>
        public DateTimeOffset TimeStampOffset => TimeStamp.ToUniversalTime();

        public float Altitude { get; set; }
        public float CelsiusTemperature { get; set; }
        public float Humidity { get; set; }
        public float BarometricPressure { get; set; }
        public float AmbientLight { get; set; }
    }

    public sealed class WeatherData
    {
        public WeatherRecord Current { get; set; } = new WeatherRecord();

        /// <summary>
        /// Helper for CurrentHourRecords housekeeping, to help w/performance.
        /// </summary>
        private DateTime currentHour = DateTime.Now;

        /// <summary>
        /// This holds a list of the datapoints that are all from this current hour.
        /// </summary>
        internal List<WeatherRecord> CurrentHourRecords { get; } = new List<WeatherRecord>();

        /// <summary>
        /// This holds a list of averaged hours, to represent the last 24hours worth of data.
        /// </summary>
        internal List<WeatherRecord> Last24HourRecords { get; } = new List<WeatherRecord>();

        /// <summary>
        /// This holds a list of averaged days, to represent the last month worth of data.
        /// </summary>
        internal List<WeatherRecord> CurrentMonthRecords { get; } = new List<WeatherRecord>();

        /// <summary>
        /// Gives the current record in JSON format.
        /// </summary>
        public string JsonCurrent => JsonConvert.SerializeObject(Current);

        /// <summary>
        /// Gives the last hour of stored values in JSON format.
        /// </summary>
        public string JsonLastHour
        {
            get
            {
                lock (CurrentHourRecords)
                {
                    return JsonConvert.SerializeObject(CurrentHourRecords);
                }
            }
        }

        /// <summary>
        /// Gives the last 24 hours of stored values in JSON format.
        /// </summary>
        public string JsonLast24Hours
        {
            get
            {
                lock (Last24HourRecords)
                {
                    return JsonConvert.SerializeObject(Last24HourRecords);
                }
            }
        }

        /// <summary>
        /// Gives the last month of stored values in JSON format.
        /// </summary>
        public string JsonLastMonth
        {
            get
            {
                lock (CurrentMonthRecords)
                {
                    return JsonConvert.SerializeObject(CurrentMonthRecords);
                }
            }
        }

        /// <summary>
        /// Adds the specified WeatherRecord, pointint the Current property to it and adding to the data list(s).
        /// </summary>
        /// <param name="record"></param>
        public void AddRecord(WeatherRecord record)
        {
            lock (Current)
            {
                Current = record;

                // Also, do housekeeping and aggregation if appropriate.
                AggregateData();

                // After aggregating, insert to the CurrentHourRecords and set currentHour.
                // If we're in a new hour, record will become the first entry in the list. Otherwise it'll just add to the list of entries.
                lock (CurrentHourRecords)
                {
                    CurrentHourRecords.Add(record);
                }
                currentHour = Current.TimeStamp;
            }
        }

        /// <summary>
        /// Aggregates data if appropriate. This happenes within a lock(Current) to 
        /// </summary>
        private void AggregateData()
        {
            // Still in the same hour? do nothing
            if (currentHour.Date == Current.TimeStamp.Date && currentHour.Hour == Current.TimeStamp.Hour)
            {
                return;
            }

            WeatherRecord lastHour = new WeatherRecord();
            lastHour.TimeStamp = new DateTime(Current.TimeStamp.Year, Current.TimeStamp.Month, Current.TimeStamp.Day, Current.TimeStamp.Hour, 0, 0);
            lock (CurrentHourRecords)
            {
                lastHour.Altitude = CurrentHourRecords.Average(wr => wr.Altitude);
                lastHour.AmbientLight = CurrentHourRecords.Average(wr => wr.AmbientLight);
                lastHour.BarometricPressure = CurrentHourRecords.Average(wr => wr.BarometricPressure);
                lastHour.CelsiusTemperature = CurrentHourRecords.Average(wr => wr.CelsiusTemperature);
                lastHour.Humidity = CurrentHourRecords.Average(wr => wr.Humidity);
                CurrentHourRecords.Clear();
            }

            // Add to our list of last 24hours, and remove any entries that are too old.
            lock (Last24HourRecords)
            {
                Last24HourRecords.Add(lastHour);
                Last24HourRecords.RemoveAll(wr => wr.TimeStamp < lastHour.TimeStamp.AddDays(-1));
            }

            // If we're not on a new day, do not continue (as we will add a days average to the 30days list).
            if (currentHour.Date == Current.TimeStamp.Date)
            {
                return;
            }
            WeatherRecord lastDay = new WeatherRecord();
            lastDay.TimeStamp = new DateTime(currentHour.Year, currentHour.Month, currentHour.Day, 0, 0, 0);
            lock (Last24HourRecords)
            {
                var currentDayRecords = Last24HourRecords.Where(wr => wr.TimeStamp.Day == currentHour.Day).ToList();
                lastDay.Altitude = currentDayRecords.Average(wr => wr.Altitude);
                lastDay.AmbientLight = currentDayRecords.Average(wr => wr.AmbientLight);
                lastDay.BarometricPressure = currentDayRecords.Average(wr => wr.BarometricPressure);
                lastDay.CelsiusTemperature = currentDayRecords.Average(wr => wr.CelsiusTemperature);
                lastDay.Humidity = currentDayRecords.Average(wr => wr.Humidity);
            }

            // Add to our list of last 30 days, and remove any entries that are too old.
            lock (CurrentMonthRecords)
            {
                CurrentMonthRecords.Add(lastHour);
                CurrentMonthRecords.RemoveAll(wr => wr.TimeStamp < lastHour.TimeStamp.AddMonths(-1));
            }
        }
    }
}