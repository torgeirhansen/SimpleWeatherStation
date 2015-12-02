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
using Weatherstation.AzureConnection;
using Weatherstation.WeatherShieldReader.Sparkfun;

namespace Weatherstation.WeatherShieldReader {
    public sealed class StartupTask: IBackgroundTask {
        private BackgroundTaskDeferral taskDeferral;
        private readonly WeatherShield shield = new WeatherShield();
        private AzureConnector azureConnector = null;

        // How often to pull data from the weather shield
        private readonly int weatherShieldReadInterval = 2;

        public async void Run(IBackgroundTaskInstance taskInstance) {
            try {
                // Ensure our background task remains running
                taskDeferral = taskInstance.GetDeferral();

                // Initialize cloud connection
                azureConnector = new AzureConnector();
                await azureConnector.InitAsync();

                // Initialize WeatherShield
                await shield.BeginAsync();

                // Create a timer-initiated ThreadPool task to read data from I2C
                ThreadPoolTimer.CreatePeriodicTimer(async (source) => {
                    await readAndSendWeatherRecord();
                }, TimeSpan.FromSeconds(weatherShieldReadInterval));

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
        /// Reads current values from the weather shield and pushes them to Azure storage.
        /// </summary>
        /// <param name="timer"></param>
        private async Task readAndSendWeatherRecord() {

            WeatherRecord record = new WeatherRecord();
            record.TimeStamp = DateTime.Now.ToLocalTime();

            try {
                // Green led indicates that we're currently reading from the weathershield.
                shield.BlueLEDPin.Write(GpioPinValue.Low);
                shield.GreenLEDPin.Write(GpioPinValue.High);
                record.Altitude = shield.Altitude;
                record.BarometricPressure = shield.Pressure;
                record.CelsiusTemperature = shield.Temperature;
                record.Humidity = shield.Humidity;
                record.AmbientLight = shield.AmbientLight;
                shield.GreenLEDPin.Write(GpioPinValue.Low);

                // Blue led indicates that we're currently pushing data to Azure.
                shield.BlueLEDPin.Write(GpioPinValue.High);
                await azureConnector.SendMessageAsync(record);
                shield.BlueLEDPin.Write(GpioPinValue.Low);
            }
            catch (Exception ex) {
                // To give some feedback to user
                shield.BlueLEDPin.Write(GpioPinValue.High);
                shield.GreenLEDPin.Write(GpioPinValue.High);

                await LogExceptionAsync(nameof(readAndSendWeatherRecord), ex);
            }
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason) {

            // Relinquish our task deferral
            taskDeferral.Complete();

            Task t = new Task(async () => {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile exceptionFile = await localFolder.CreateFileAsync("TemperatureLog.Log", CreationCollisionOption.OpenIfExists);

                List<string> outData = new List<string>();
                outData.Add(string.Empty);
                outData.Add("OnCanceled(sender: " + sender + ", reason: " + reason);
                await FileIO.AppendLinesAsync(exceptionFile, outData);
            });
            t.Wait();
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