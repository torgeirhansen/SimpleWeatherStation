using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.System.Threading;
using Newtonsoft.Json;

namespace SimpleWeatherStationFrontend
{
    public sealed class DataFetcherTask
    {
        /// <summary>
        /// The update timer for updating the current values.
        /// </summary>
        private ThreadPoolTimer currentUpdateTimer { get; set; }

        /// <summary>
        /// The update timer for last day values, runs every 15 mins.
        /// </summary>
        private ThreadPoolTimer lastdayUpdateTimer { get; set; }

        public WeatherData WeatherData { get; } = new WeatherData();
        public TemperatureData TemperatureData  { get; } = new TemperatureData();

        public async Task StartAsync()
        {
            try
            {
                // Create a timer-initiated ThreadPool task to read data from weather device.
                this.currentUpdateTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
                {
                    //Debug.WriteLine(nameof(DataFetcherTask) + ": in timer @ " + DateTime.Now);

                    await FetchDataFromDeviceAsync();

                }, TimeSpan.FromSeconds(3));

                // Fetch the last day worth of data, so that we can use it for the graph page.
                await FetchLastDayDataFromDeviceAsync();

                // Create a timer-initiated ThreadPool task to read data from weather device.
                this.lastdayUpdateTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
                {
                    //Debug.WriteLine(nameof(DataFetcherTask) + ": in timer @ " + DateTime.Now);

                    await FetchLastDayDataFromDeviceAsync();

                }, TimeSpan.FromMinutes(15));
            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(StartAsync), ex);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                // If it goes to shit here, rethrow which will terminate the process - but at least we have it logged!
                throw;
            }
        }

        /// <summary>
        /// Fetches the last day worth of data from the device and pushes it into the LastDayValues list.
        /// </summary>
        /// <returns></returns>
        private async Task FetchLastDayDataFromDeviceAsync()
        {
            try
            {
                // Fetch data from weather-device.
                string weather1Url = "http://weather1:50001/LastDay";

                WebRequest req = WebRequest.Create(weather1Url);
                WebResponse res = await req.GetResponseAsync();

                StreamReader sr = new StreamReader(res.GetResponseStream());
                string lastDayData = await sr.ReadToEndAsync();

                lock (WeatherData.LastDayValues)
                {
                    this.WeatherData.LastDayValues.Clear();
                    List<WeatherRecord> records = JsonConvert.DeserializeObject<List<WeatherRecord>>(lastDayData);
                    foreach (WeatherRecord wr in records)
                    {
                        this.WeatherData.LastDayValues[wr.TimeStamp] = wr;
                    }
                }
            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(FetchLastDayDataFromDeviceAsync), ex);
            }
        }


        private async Task FetchDataFromDeviceAsync()
        {
            try
            {
                // Fetch data from weather-device.
                string weather1Url = "http://weather1:50001";
                WebRequest req = WebRequest.Create(weather1Url);
                WebResponse res = await req.GetResponseAsync();

                StreamReader sr = new StreamReader(res.GetResponseStream());
                string weatherData = await sr.ReadToEndAsync();

                this.WeatherData.SetCurrent(JsonConvert.DeserializeObject<WeatherRecord>(weatherData));

            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(FetchDataFromDeviceAsync), ex);
                // update screen, show error..
                this.WeatherData.SetCurrent(new WeatherRecord() { TimeStamp = DateTime.Now, ErrorMessage = ex.Message });
            }

            try
            {
                // Fetch data from weather-device.
                string weatherUrl = "http://weather:50001";
                WebRequest req = WebRequest.Create(weatherUrl);
                WebResponse res = await req.GetResponseAsync();

                StreamReader sr = new StreamReader(res.GetResponseStream());
                string temperatureData = await sr.ReadToEndAsync();

                this.TemperatureData.SetCurrent(JsonConvert.DeserializeObject<TemperatureRecord>(temperatureData));
            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(FetchDataFromDeviceAsync), ex);
                // update screen, show error..
                this.TemperatureData.SetCurrent(new TemperatureRecord() { TimeStamp = DateTime.Now, ErrorMessage = ex.Message });
            }


        }

        /// <summary>
        /// Stops the updating-timers
        /// </summary>
        internal void Stop()
        {
            currentUpdateTimer?.Cancel();
            currentUpdateTimer = null;

            lastdayUpdateTimer?.Cancel();
            lastdayUpdateTimer = null;
        }

        /// <summary>
        /// Log the exception to a logfile
        /// </summary>
        /// <param name="ex"></param>
        private async Task LogExceptionAsync(string methodName, Exception ex)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile exceptionFile = await localFolder.CreateFileAsync("ExceptionLog.Log", CreationCollisionOption.OpenIfExists);

            List<string> outData = new List<string>();
            outData.Add(string.Empty);
            outData.Add("----");
            outData.Add("Date: " + DateTime.Now);
            outData.Add("Exception, type: " + ex.GetType());
            outData.Add("In method: " + methodName);
            outData.Add("Message: " + ex.Message);
            outData.Add("Stacktrace: " + ex.StackTrace);

            await FileIO.AppendLinesAsync(exceptionFile, outData);
        }
    }
}