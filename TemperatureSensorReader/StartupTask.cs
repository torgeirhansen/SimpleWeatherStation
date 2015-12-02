using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;
using Windows.Storage;
using Windows.System.Threading;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

//#error se her for info http://www.analog.com/media/en/technical-documentation/data-sheets/TMP35_36_37.pdf

namespace Weatherstation.TemperatureReader {
    public sealed class StartupTask: IBackgroundTask {
        private BackgroundTaskDeferral taskDeferral;
        private ThreadPoolTimer i2cTimer;
        private readonly int i2cReadIntervalSeconds = 2;
        private SpiDevice spiReader;
        private readonly TemperatureData temperatureData = new TemperatureData();
        private HttpServer server;
        private readonly int port = 50001;

        // RaspBerry Pi2  Parameters
        private const string SPI_CONTROLLER_NAME = "SPI0";  /* For Raspberry Pi 2, use SPI0                             */
        private const Int32 SPI_CHIP_SELECT_LINE = 0;       /* Line 0 maps to physical pin number 24 on the Rpi2        */

        // Uncomment if you are using mcp3002
        byte[] readBuffer = new byte[3]; // this is defined to hold the output data
        byte[] writeBuffer = new byte[3] { 0x68, 0x00, 0x00 };//01101000 00;  It is SPI port serial input pin, and is used to load channel configuration data into the device

        /// <summary>
        /// Used to lock() while reading from the device
        /// </summary>
        private object readerLock = new object();

        public async void Run(IBackgroundTaskInstance taskInstance) {
            await LogToFile("Run()");
            try {
                // Ensure our background task remains running
                taskDeferral = taskInstance.GetDeferral();

                // Initiate the SPI
                InitSPI();

                // test nå..
                //ReadTemperature(null);

                // Create a timer-initiated ThreadPool task to read data from I2C
                i2cTimer = ThreadPoolTimer.CreatePeriodicTimer(ReadTemperature, TimeSpan.FromSeconds(i2cReadIntervalSeconds));

                //            // Start the server
                server = new HttpServer(port);
                var asyncAction = ThreadPool.RunAsync(w => { server.StartServerAsync(temperatureData); });

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
            await LogToFile("Run() done");
        }

        private async void InitSPI() {
            try {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 500000; // 10000000;
                settings.Mode = SpiMode.Mode0; //Mode3;

                string spiAqs = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);
                var deviceInfo = await DeviceInformation.FindAllAsync(spiAqs);
                spiReader = await SpiDevice.FromIdAsync(deviceInfo[0].Id, settings);
            }

            /* If initialization fails, display the exception and stop running */
            catch(Exception ex) {
                await LogExceptionAsync(nameof(InitSPI), ex);
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }

                // If it goes to shit here, rethrow which will terminate the process - but at least we have it logged!
                throw new Exception("SPI Initialization Failed", ex);
            }
        }

        private int convertToInt(byte[] data) {
            /*Uncomment if you are using mcp3208/3008 which is 12 bits output */
            /*
             int result = data[1] & 0x0F;
             result <<= 8;
             result += data[2];
             return result;
             */

            /*Uncomment if you are using mcp3002*/
            int result = data[0] & 0x03;
            result <<= 8;
            result += data[1];
            return result;
        }


        private async void ReadTemperature(ThreadPoolTimer timer)
        {
            try
            {
                TemperatureRecord tr = new TemperatureRecord();
                lock (readerLock)
                {
                    spiReader.TransferFullDuplex(writeBuffer, readBuffer);

                    //voltage = ADC_value / 1024 * 3.3 = 0.621 V 
                    //TMP36 is 0.5V at 0 C and 10 mV per degree
                    //Temp_in_C = (voltage - 0.5) / 0.01
                    
                    int adcData = convertToInt(readBuffer);
                    double inputVolt = 3.3;
                    double sensorVolt = adcData/(double)1024*inputVolt;

                    tr.CelsiusTemperature = Convert.ToSingle((sensorVolt - 0.5)/0.01);
                    tr.TimeStamp = DateTime.Now.ToLocalTime();

                    temperatureData.AddRecord(tr);
                }
                await LogToFile("Date: " + DateTime.Now + ", temp: " + tr.CelsiusTemperature);
            }
            catch (Exception ex)
            {
                LogExceptionAsync(nameof(ReadTemperature), ex).Wait();
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                // If it goes to shit here, rethrow which will terminate the process - but at least we have it logged!
                throw ex;
            }
        }

        private static async Task LogToFile(string logMsg) {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile exceptionFile =
                await localFolder.CreateFileAsync("TemperatureLog.Log", CreationCollisionOption.OpenIfExists);

            await FileIO.AppendLinesAsync(exceptionFile, new List<string>() { logMsg });
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
