using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.System.Threading;
using Newtonsoft.Json;
using SimpleWeatherStationBackgroundApp.Sparkfun;

namespace SimpleWeatherStationBackgroundApp {
    public sealed class StartupTask: IBackgroundTask {
        private BackgroundTaskDeferral taskDeferral;
        private ThreadPoolTimer i2cTimer;
        private HttpServer server;
        private readonly int port = 50001;
        private readonly WeatherData weatherData = new WeatherData();
        private readonly int i2cReadIntervalSeconds = 2;
        private readonly WeatherShield shield = new WeatherShield();
        private Mutex mutex;
        private readonly string mutexId = "WeatherStation";

        public async void Run(IBackgroundTaskInstance taskInstance) {
            try {
                // Ensure our background task remains running
                taskDeferral = taskInstance.GetDeferral();

                // Read any weather data that might have been persisted to disk in a previous run.
                await ReadPersistedWeatherDataAsync();

                // Mutex will be used to ensure only one thread at a time is talking to the shield / isolated storage
                mutex = new Mutex(false, mutexId);

                // Initialize WeatherShield
                await shield.BeginAsync();

                // Create a timer-initiated ThreadPool task to read data from I2C
                i2cTimer = ThreadPoolTimer.CreatePeriodicTimer(PopulateWeatherData, TimeSpan.FromSeconds(i2cReadIntervalSeconds));

                // Start the server
                server = new HttpServer(port);
                var asyncAction = ThreadPool.RunAsync(w => { server.StartServerAsync(weatherData); });

                // Task cancellation handler, release our deferral there 
                taskInstance.Canceled += OnCanceled;
            } catch(Exception ex) {
                await LogExceptionAsync(nameof(Run), ex);
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }

                // If it goes to shit here, rethrow which will terminate the process - but at least we have it logged!
                throw;
            }
        }

        /// <summary>
        /// Reads the json files if existing, and populates the WeatherData with that data.
        /// </summary>
        private async Task ReadPersistedWeatherDataAsync() {
            try {
                List<WeatherRecord> data = await GetFileDataOrEmptyListAsync("CurrentMonthRecords.Json");
                lock (weatherData.CurrentMonthRecords)
                {
                    weatherData.CurrentMonthRecords.Clear();

                    // If app crashed or stopped we might get duplicates here. Ensure we don't.
                    List<DateTime> foundDates = new List<DateTime>();
                    foreach (WeatherRecord wr in data)
                    {
                        if (!foundDates.Contains(wr.TimeStamp))
                        {
                            foundDates.Add(wr.TimeStamp);
                            weatherData.CurrentMonthRecords.Add(wr);
                        }
                    }
                }

                data = await GetFileDataOrEmptyListAsync("CurrentDayRecords.Json");
                lock (weatherData.CurrentDayRecords)
                {
                    weatherData.CurrentDayRecords.Clear();

                    // If app crashed or stopped we might get duplicates here. Ensure we don't.
                    List<DateTime> foundDates = new List<DateTime>();
                    foreach (WeatherRecord wr in data)
                    {
                        if (!foundDates.Contains(wr.TimeStamp))
                        {
                            foundDates.Add(wr.TimeStamp);
                            weatherData.CurrentDayRecords.Add(wr);
                        }
                    }
                }

                data = await GetFileDataOrEmptyListAsync("CurrentHourRecords.Json");
                lock (weatherData.CurrentHourRecords) {
                    weatherData.CurrentHourRecords.Clear();

                    // If app crashed or stopped we might get duplicates here. Ensure we don't.
                    List<DateTime> foundDates = new List<DateTime>();
                    foreach(WeatherRecord wr in data) {
                        if(!foundDates.Contains(wr.TimeStamp)) {
                            foundDates.Add(wr.TimeStamp);
                            weatherData.CurrentHourRecords.Add(wr);
                        }
                    }
                }
            } catch(Exception ex) {
                await LogExceptionAsync(nameof(ReadPersistedWeatherDataAsync), ex);
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }
            }
        }

        private async Task<List<WeatherRecord>> GetFileDataOrEmptyListAsync(string filename) {
            try {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;

                if(await localFolder.TryGetItemAsync(filename) != null) {
                    StorageFile monthFile = await localFolder.GetFileAsync(filename);
                    if(monthFile.IsAvailable) {
                        using(var stream = await monthFile.OpenStreamForReadAsync()) {
                            StreamReader reader = new StreamReader(stream);
                            return JsonConvert.DeserializeObject<List<WeatherRecord>>(reader.ReadToEnd());
                        }
                    }
                }
            } catch(Exception ex) {
                await LogExceptionAsync(nameof(GetFileDataOrEmptyListAsync) + "(" + filename + ")", ex);
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }
            }

            // In case of none-existing file or exception:
            return new List<WeatherRecord>();
        }

        private void PopulateWeatherData(ThreadPoolTimer timer) {
            bool hasMutex = false;

            try {
                hasMutex = mutex.WaitOne(1000);
                if(hasMutex) {
                    WeatherRecord record = new WeatherRecord();
                    record.TimeStamp = DateTime.Now.ToLocalTime();

                    // TOHA: når ferdig med å teste på soverom må dette aktiveres igjen..
                    ////shield.BlueLEDPin.Write(GpioPinValue.High);
                    shield.GreenLEDPin.Write(GpioPinValue.High);

                    record.Altitude = shield.Altitude;
                    record.BarometricPressure = shield.Pressure;
                    record.CelsiusTemperature = shield.Temperature;
                    record.Humidity = shield.Humidity;
                    record.AmbientLight = shield.AmbientLight;

                    weatherData.AddRecord(record);

                    // TOHA: når ferdig med å teste på soverom må dette aktiveres igjen..
                    ////shield.BlueLEDPin.Write(GpioPinValue.Low);
                    shield.GreenLEDPin.Write(GpioPinValue.Low);

                    // Write the data locally so that the http server can serve it.
                    WriteDataToIsolatedStorageAsync();
                }
            } finally {
                if(hasMutex) {
                    mutex.ReleaseMutex();
                }
            }

        }

        private async void WriteDataToIsolatedStorageAsync() {
            try {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile transferFile = await localFolder.CreateFileAsync("DataFile.json", CreationCollisionOption.ReplaceExisting);

                using(var stream = await transferFile.OpenStreamForWriteAsync()) {
                    StreamWriter writer = new StreamWriter(stream);
                    string jsonData;
                    lock (weatherData.Current) {
                        jsonData = JsonConvert.SerializeObject(weatherData.Current);
                    }
                    await writer.WriteAsync(jsonData);

                    writer.Flush();
                }

                long tsCurrent = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                int hourRecords = await WriteDataListToIsolatedStorageAsync(weatherData.CurrentHourRecords, "CurrentHourRecords.json");
                long tsHour = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                int dayRecords = await WriteDataListToIsolatedStorageAsync(weatherData.CurrentDayRecords, "CurrentDayRecords.json");
                long tsDay = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                int monthRecords = await WriteDataListToIsolatedStorageAsync(weatherData.CurrentMonthRecords, "CurrentMonthRecords.json");
                long tsMonth = stopwatch.ElapsedMilliseconds;

                // IO Performance logging
                StorageFile logFile = await localFolder.CreateFileAsync("WritePerformance.Log", CreationCollisionOption.OpenIfExists);
                using(var stream = await logFile.OpenStreamForWriteAsync()) {
                    StreamWriter writer = new StreamWriter(stream);
                    await
                        writer.WriteLineAsync(
                            $"{DateTime.Now.ToLocalTime()} - Current: {tsCurrent}ms, Hour: {tsHour}ms (count: {hourRecords}), Day: {tsDay}ms (count: {dayRecords}), Month: {tsMonth}ms (count: {monthRecords})");

                    writer.Flush();
                }
            } catch(Exception ex) {
                await LogExceptionAsync(nameof(WriteDataToIsolatedStorageAsync), ex);
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }
            }
        }

        private async Task<int> WriteDataListToIsolatedStorageAsync(List<WeatherRecord> data, string filename) {
            try {
                // local copy of the data to avoid problems with concurrency.
                List<WeatherRecord> myDataCopy = null;
                lock (data) {
                    myDataCopy = new List<WeatherRecord>(data);
                }

                // We have exlusive access to the mutex so can safely wipe the transfer file
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile transferFile = await localFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);

                using(var stream = await transferFile.OpenStreamForWriteAsync()) {
                    StreamWriter writer = new StreamWriter(stream);
                    string jsonData = JsonConvert.SerializeObject(myDataCopy);
                    await writer.WriteAsync(jsonData);

                    writer.Flush();
                }

                return myDataCopy.Count;

            } catch(Exception ex) {
                await LogExceptionAsync(nameof(WriteDataListToIsolatedStorageAsync), ex);
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }
            }

            // zero items if we fail..
            return 0;
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason) {
            // Relinquish our task deferral
            taskDeferral.Complete();
        }

        /// <summary>
        /// Log the exception to a logfile
        /// </summary>
        /// <param name="ex"></param>
        private async Task LogExceptionAsync(string methodName, Exception ex) {
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