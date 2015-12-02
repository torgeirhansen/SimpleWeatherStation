using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.System.Threading;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Weatherstation.AzureConnection {
    public class AzureConnector {
        // TODO: move to a better place (Windows.Storage.ApplicationData.Current.LocalSettings?)
        private readonly string connectionString = "DefaultEndpointsProtocol=https;AccountName=INSERT_ACCOUNTNAME_HERE;AccountKey=INSERT_KEY_HERE";
        // remember, all lowercase to avoid issues with Azure queue:
        private readonly string queueName = "currentweather";

        private CloudQueue queue;
        // TODO: kjør queue
        //private CloudTable table;

        public event Action<WeatherRecord> OnMessageReceived;
 
        /// <summary>
        /// Initialize the connection to Azure Storage.
        /// If initReader is set to true, a timer will start that polls the queue every readerPollInterval, or by default 250ms.
        /// </summary>
        public async Task InitAsync(bool initReader = false, TimeSpan? readerPollInterval = null) {
            CloudStorageAccount sta = CloudStorageAccount.Parse(connectionString);
            var queueClient = sta.CreateCloudQueueClient();
            queue = queueClient.GetQueueReference(queueName);

            await queue.CreateIfNotExistsAsync();

            if (initReader) {
                await StartReaderAsync(readerPollInterval);
            }
        }

        /// <summary>
        /// Initialize the queue reader.
        /// This starts a timer that pulls the queue every 250ms, or whatever the poll interval is set to.
        /// </summary>
        /// <returns></returns>
        public async Task StartReaderAsync(TimeSpan? pollInterval = null) {

            TimeSpan interval = pollInterval ?? TimeSpan.FromMilliseconds(250);

            ThreadPoolTimer.CreatePeriodicTimer(async (source) => {
                
                try {
                    // Peek?
                    var message = await queue.GetMessageAsync();
                    if (message != null) {
                        WeatherRecord weatherRecord = JsonConvert.DeserializeObject<WeatherRecord>(message.AsString);
                        if (weatherRecord != null) {
                            await queue.DeleteMessageAsync(message);
                            OnMessageReceived?.Invoke(weatherRecord);
                        }
                    }
                }
                catch (Exception ex) {
                    if (Debugger.IsAttached) {
                        Debugger.Break();
                    }
                }

            }, interval);
        }

        /// <summary>
        /// Push record to cloud queue
        /// </summary>
        public async Task SendMessageAsync(WeatherRecord weatherRecord) {
            var json = JsonConvert.SerializeObject(weatherRecord);
            var cloudMessage = new CloudQueueMessage(json);
            await queue.AddMessageAsync(cloudMessage);
        }
    }
}