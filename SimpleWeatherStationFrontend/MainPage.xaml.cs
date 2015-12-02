using System;
using System.Threading.Tasks;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Weatherstation.AzureConnection;

//#1: Fiks grafikken til separate filer og dra inn som assets
//#2: Finn pent/yr/geowhatever xml'en og sjekk formatet.
//#3: Tenk litt på hvordan det skal se ut (to ikoner som er nå+12?)
//#4: Lag timer oppdatering av skjerm, som da henter xml og viser korrekt bilde.
//#5: Fikse så nodenavn blir appconfig param (appsetting..)


namespace Weatherstation.Frontend
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page {
        private AzureConnector azureConnector;
        private WeatherRecord weatherRecord = null;

        public MainPage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            azureConnector = new AzureConnector();
            await azureConnector.InitAsync(true);
            azureConnector.OnMessageReceived += AzureConnector_OnMessageReceived;
        }

        private void AzureConnector_OnMessageReceived(WeatherRecord weatherRecord) {
            this.weatherRecord = weatherRecord;
            Task updateScreen = Task.Run(async () => await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, UpdateScreen));
            updateScreen.Wait();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Init();

            //// Update UI every 2 seconds while screen is active.
            //this.updateTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            //{
            //    // Notify the UI to do an update.
            //    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, UpdateScreen);

            //}, TimeSpan.FromSeconds(2));

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            azureConnector.OnMessageReceived -= AzureConnector_OnMessageReceived;
            //this.updateTimer?.Cancel();

            base.OnNavigatingFrom(e);
        }

        private void Init()
        {
            // Do initial update
            UpdateScreen();
        }

        private void UpdateScreen()
        {
            if (weatherRecord == null)
            {
                LastUpdate.Text = "Please wait for initial values..";
                DegreesOutdoor.Text = string.Empty;
            }
            else
            {
                DegreesOutdoor.Text = string.Format("{0:N1}C", weatherRecord.CelsiusTemperature);
                LastUpdate.Text = "Last update: " + weatherRecord.TimeStamp;
            }

            //DegreesIndoor.Text = string.Empty;
            //TemperatureRecord temperatureRecord = temperatureData.Current;
            //if (temperatureRecord == null)
            //{
            //    DegreesIndoor.Text = string.Empty;
            //}
            //else
            //{
            //    DegreesIndoor.Text = string.Format("{0:N1}C", temperatureRecord.CelsiusTemperature);
            //}

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
    }
}