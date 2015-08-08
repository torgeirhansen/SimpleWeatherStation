using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading;
using Windows.ApplicationModel.Store;
using Windows.Devices.Sensors;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace SimpleWeatherStationFrontend
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ThreadPoolTimer updateTimer;

        private WeatherData weatherData { get; set; }

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            weatherData = e.Parameter as WeatherData;
            Init();

            // Update UI every 2 seconds while screen is active.
            this.updateTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                // Notify the UI to do an update.
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, UpdateScreen);

            }, TimeSpan.FromSeconds(2));

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            this.updateTimer?.Cancel();

            base.OnNavigatingFrom(e);
        }

        private void Init()
        {
            // Do initial update
            UpdateScreen();
        }

        private void UpdateScreen()
        {
            WeatherRecord weatherRecord = weatherData.Current;
            if (weatherRecord == null)
            {
                LastUpdate.Text = "Please wait for initial values..";
                Degrees.Text = string.Empty;
                return;
            }

            LastUpdate.Text = "Last update: " + DateTime.Now;

            if (!string.IsNullOrEmpty(weatherRecord.ErrorMessage))
            {
                Degrees.Text = "Error: " + weatherRecord.ErrorMessage;
            }
            else
            {
                Degrees.Text = string.Format("{0:N2}C", weatherRecord.CelsiusTemperature);
            }
        }

        /// <summary>
        /// When clicked, show historic values
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClick(object sender, object e)
        {
            this.Frame.Navigate(typeof (HistoricValuesPage), weatherData);
        }
    }
}