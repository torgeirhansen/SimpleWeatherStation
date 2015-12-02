using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Weatherstation.TemperatureReader
{

    public sealed class HttpServer : IDisposable
    {
        private const uint bufLen = 8192;
        private readonly int defaultPort;
        private readonly StreamSocketListener sock;
        private TemperatureData temperatureData;

        public HttpServer(int serverPort)
        {
            sock = new StreamSocketListener();
            defaultPort = serverPort;
            sock.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }

        public async void StartServerAsync(TemperatureData td)
        {
            try
            {
                await sock.BindServiceNameAsync(defaultPort.ToString());
                temperatureData = td;
            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(StartServerAsync), ex);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            try
            {
                // Read in the HTTP request, we only care about type 'GET'
                StringBuilder request = new StringBuilder();
                using (IInputStream input = socket.InputStream)
                {
                    byte[] data = new byte[bufLen];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = bufLen;
                    while (dataRead == bufLen)
                    {
                        await input.ReadAsync(buffer, bufLen, InputStreamOptions.Partial);
                        request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                        dataRead = buffer.Length;
                    }
                }

                using (IOutputStream output = socket.OutputStream)
                {
                    string requestMethod = request.ToString().Split('\n')[0];
                    string[] requestParts = requestMethod.Split(' ');
                    await WriteResponseAsync(requestParts, output);
                }
            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(ProcessRequestAsync), ex);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        private async Task WriteResponseAsync(string[] requestTokens, IOutputStream outstream)
        {
            string respBody = string.Empty;
            try
            {
                string urlPath = requestTokens.Length > 1 ? requestTokens[1] : string.Empty;

                if (urlPath.Equals("/LastHour", StringComparison.OrdinalIgnoreCase))
                {
                    respBody = temperatureData.JsonLastHour;
                }
                else if (urlPath.Equals("/LastDay", StringComparison.OrdinalIgnoreCase))
                {
                    respBody = temperatureData.JsonLast24Hours;
                }
                else if (urlPath.Equals("/LastMonth", StringComparison.OrdinalIgnoreCase))
                {
                    respBody = temperatureData.JsonLastMonth;
                }
                else
                {
                    respBody = temperatureData.JsonCurrent;
                }
            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(WriteResponseAsync) + "(part 1: getting content)", ex);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }

            try
            {
                string htmlCode = "200 OK";

                using (Stream resp = outstream.AsStreamForWrite())
                {
                    byte[] bodyArray = Encoding.UTF8.GetBytes(respBody);
                    MemoryStream stream = new MemoryStream(bodyArray);

                    // NOTE: If you change the respBody format (above), change the Content-Type accordingly
                    string header = $"HTTP/1.1 {htmlCode}\r\n" +
                                    "Content-Type: text/json\r\n" +
                                    $"Content-Length: {stream.Length}\r\n" +
                                    "Connection: close\r\n\r\n";

                    byte[] headerArray = Encoding.UTF8.GetBytes(header);
                    await resp.WriteAsync(headerArray, 0, headerArray.Length);
                    await stream.CopyToAsync(resp);
                    await resp.FlushAsync();
                }

            }
            catch (Exception ex)
            {
                await LogExceptionAsync(nameof(WriteResponseAsync) + "(part) 2: sending results)", ex);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                sock.Dispose();
            }
            catch (Exception ex)
            {
                LogExceptionAsync("Dispose()", ex).Wait();
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        /// <summary>
        /// Log the exception to a logfile
        /// </summary>
        /// <param name="ex"></param>
        private async Task LogExceptionAsync(string methodName, Exception ex)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile exceptionFile = await localFolder.CreateFileAsync("ExceptionLogHttpServer.Log", CreationCollisionOption.OpenIfExists);

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