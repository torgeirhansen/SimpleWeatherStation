using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace SimpleWeatherStationFrontend
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HistoricValuesPage : Page
    {

        private WeatherData weatherData { get; set; }
        private TemperatureData temperatureData { get; set; }

        public PlotModel PlotModel { get; } = new PlotModel();

        /// <summary>
        /// Timer that triggers the main-screen again after a couple of seconds.
        /// </summary>
        private ThreadPoolTimer gobackTimer;

        public HistoricValuesPage()
        {
            this.DataContext = this;

            // Set up the PlotModel with some dummy data
            InitPlotModel(0, 20);

            this.InitializeComponent();
        }

        private void InitPlotModel(int graphMinValue, int graphMaxValue)
        {
            try
            {
                OxyColor bgColor = OxyColors.Black;
                OxyColor axisColor = OxyColors.AntiqueWhite;
                OxyColor graphPlusColor = OxyColors.Tomato;
                OxyColor graphMinusColor = OxyColors.LightBlue;

                PlotModel.Series.Clear();
                PlotModel.Axes.Clear();

                //PlotModel = new PlotModel {Title = "Siste døgn", LegendSymbolLength = 24};
                PlotModel.Title = "Siste døgn";
                PlotModel.LegendSymbolLength = 24;

                PlotModel.Axes.Add(new LinearAxis
                {
                    TextColor = axisColor,
                    AxislineColor = axisColor,
                    MajorGridlineColor = axisColor,
                    MinorGridlineColor = axisColor,
                    TicklineColor = axisColor,
                    TitleColor = axisColor,
                    Position = AxisPosition.Left,
                    Title = "Temperatur",
                    Unit = "°C",
                    ExtraGridlines = new[] {0.0},
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    Maximum = graphMaxValue + 5,
                    Minimum = graphMinValue - 5
                });

                var hourAxis = new CategoryAxis
                {
                    AxislineStyle = LineStyle.Solid,
                    TextColor = axisColor,
                    AxislineColor = axisColor,
                    TitleColor = axisColor,
                    Position = AxisPosition.Bottom,
                    Title = "Klokken"
                };

                for (DateTime dt = DateTime.Now.AddDays(-1); dt < DateTime.Now; dt = dt.AddHours(1))
                {
                    hourAxis.Labels.Add(dt.Hour.ToString());
                }

                PlotModel.Axes.Add(hourAxis);

                PlotModel.InvalidatePlot(false);

            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                // TODO: be better at this..
            }
        }

        private void RepopulatePlotModel()
        {
            try
            {

                DateTime dtMin = DateTime.Now.AddDays(-1);
                Dictionary<DateTime, WeatherRecord> lastDayValues = null;
                int minValue = 0, maxValue = 20;
                lock (weatherData.LastDayValues)
                {
                    lastDayValues =
                        weatherData.LastDayValues.Where(kvp => kvp.Key >= dtMin && kvp.Key < DateTime.Now)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    if (lastDayValues.Any())
                    {
                        minValue = (int) lastDayValues.Min(k => k.Value.CelsiusTemperature);
                        maxValue = (int) lastDayValues.Max(k => k.Value.CelsiusTemperature);
                    }
                }

                InitPlotModel(minValue, maxValue);

                OxyColor bgColor = OxyColors.Black;
                OxyColor axisColor = OxyColors.AntiqueWhite;
                OxyColor graphPlusColor = OxyColors.Tomato;
                OxyColor graphMinusColor = OxyColors.LightBlue;

                // Now done in InitPlotModel()
                //PlotModel.Series.Clear();

                //PlotModel = new PlotModel {Title = "Siste døgn", LegendSymbolLength = 24};
                PlotModel.Title = "Siste døgn";
                PlotModel.LegendSymbolLength = 24;
                var s1 = new TwoColorAreaSeries
                {
                    Background = bgColor,
                    //TrackerFormatString = "December {2:0}: {4:0.0} °C",

                    Color = graphPlusColor,
                    Fill = graphPlusColor,

                    Color2 = graphMinusColor,
                    Fill2 = graphMinusColor,

                    StrokeThickness = 1,
                    Limit = 0,
                    Smooth = true,
                };

                for (int i = 0; i < 24; i++)
                {
                    DateTime dtMax = dtMin.AddHours(1);
                    double xPoint = i;

                    var min = dtMin;
                    var valuesForPeriod =
                        lastDayValues.Where(kvp => kvp.Key >= min && kvp.Key < dtMax).Select(kvp => kvp.Value).ToList();
                    if (valuesForPeriod.Any())
                    {
                        double average = valuesForPeriod.Average(v => v.CelsiusTemperature);
                        DataPoint dp = new DataPoint(xPoint, average);
                        s1.Points.Add(dp);
                    }

                    dtMin = dtMax;
                }

                //foreach(WeatherRecord wr in this.weatherData.LastDayValues.Values.OrderBy(ws => ws.TimeStamp)) {
                //    if(!startDt.HasValue) {
                //        startDt = wr.TimeStamp;
                //    }

                //    TimeSpan ts = wr.TimeStamp.Subtract(startDt.Value);
                //    tss.Add(ts);

                //    double xPoint = wr.TimeStamp.Subtract(startDt.Value).TotalSeconds / (60); // endre til 24*60*30 når vi har /2 sampling.
                //    DataPoint dp = new DataPoint(xPoint, wr.CelsiusTemperature);
                //    s1.Points.Add(dp);
                //}

                PlotModel.Series.Add(s1);
                PlotModel.InvalidatePlot(true);

            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                // TODO: be better at this..
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Tuple<WeatherData, TemperatureData> param = (Tuple<WeatherData, TemperatureData>)e.Parameter;
            weatherData = param.Item1;
            temperatureData = param.Item2;

            RepopulatePlotModel();

            // Create a timer that takes us back to the main page in 10 seconds.
            this.gobackTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                // Goto mainpage, done in a UI safe thread.
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, GotoMainPage);

            }, TimeSpan.FromSeconds(10));

            base.OnNavigatedTo(e);
        }

        /// <summary>
        /// When navigating away, ensure the goto-main timer is stopped.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // cancel the timer that sends us back to the main-screen.
            gobackTimer?.Cancel();
            gobackTimer = null;

            base.OnNavigatingFrom(e);
        }

        /// <summary>
        /// When clicked, return to main page
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_PointerPressed(object sender, object e)
        {
            GotoMainPage();
        }

        /// <summary>
        /// Jump back to the main page.
        /// </summary>
        private void GotoMainPage()
        {
            Tuple<WeatherData, TemperatureData> param = new Tuple<WeatherData, TemperatureData>(weatherData, temperatureData);

            this.Frame.Navigate(typeof (MainPage), param);
        }
    }
}