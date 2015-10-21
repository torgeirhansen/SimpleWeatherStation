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


//#1: Fiks grafikken til separate filer og dra inn som assets
//#2: Finn pent/yr/geowhatever xml'en og sjekk formatet.
//#3: Tenk litt på hvordan det skal se ut (to ikoner som er nå+12?)
//#4: Lag timer oppdatering av skjerm, som da henter xml og viser korrekt bilde.
//#5: Fikse så nodenavn blir appconfig param (appsetting..)


namespace SimpleWeatherStationFrontend
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ThreadPoolTimer updateTimer;

        private WeatherData weatherData { get; set; }

        private TemperatureData temperatureData { get; set; }

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Tuple<WeatherData, TemperatureData> param = (Tuple<WeatherData, TemperatureData>) e.Parameter;
            weatherData = param.Item1;
            temperatureData = param.Item2;
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
                DegreesOutdoor.Text = string.Empty;
            }
            else
            {
                DegreesOutdoor.Text = string.Format("{0:N1}C", weatherRecord.CelsiusTemperature);
                LastUpdate.Text = "Last update: " + DateTime.Now;
            }

            DegreesIndoor.Text = string.Empty;
            TemperatureRecord temperatureRecord = temperatureData.Current;
            if (temperatureRecord == null)
            {
                DegreesIndoor.Text = string.Empty;
            }
            else
            {
                DegreesIndoor.Text = string.Format("{0:N1}C", temperatureRecord.CelsiusTemperature);
            }

            // Testing w/o the annoying replacement of temp with temporary error..
            //if (!string.IsNullOrEmpty(weatherRecord.ErrorMessage))
            //{
            //    DegreesOutdoor.Text = "Error: " + weatherRecord.ErrorMessage;
            //}
            //else
            //{
            //    DegreesOutdoor.Text = string.Format("{0:N1}C", weatherRecord.CelsiusTemperature);
            //}
        }

        /// <summary>
        /// When clicked, show historic values
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClick(object sender, object e)
        {
            Tuple<WeatherData, TemperatureData> param = new Tuple<WeatherData, TemperatureData>(weatherData, temperatureData);

            this.Frame.Navigate(typeof (HistoricValuesPage), param);
        }
    }
}