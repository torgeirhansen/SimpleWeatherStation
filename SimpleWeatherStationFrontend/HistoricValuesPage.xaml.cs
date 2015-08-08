using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

namespace SimpleWeatherStationFrontend {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HistoricValuesPage: Page {

        private WeatherData weatherData { get; set; }

        public PlotModel PlotModel { get; } = new PlotModel();

        public string SomeText { get; } = "MEGA UKEBLA";

        public HistoricValuesPage() {
            this.DataContext = this;

            // Set up the PlotModel with some dummy data
            InitPlotModel();

            this.InitializeComponent();
        }

        private void InitPlotModel() {
            try {
                OxyColor bgColor = OxyColors.Black;
                OxyColor axisColor = OxyColors.AntiqueWhite;
                OxyColor graphPlusColor = OxyColors.Tomato;
                OxyColor graphMinusColor = OxyColors.LightBlue;

                PlotModel.Series.Clear();
                PlotModel.Axes.Clear();

                //PlotModel = new PlotModel {Title = "Siste døgn", LegendSymbolLength = 24};
                PlotModel.Title = "Siste døgn";
                PlotModel.LegendSymbolLength = 24;

                PlotModel.Axes.Add(new LinearAxis {
                    TextColor = axisColor,
                    AxislineColor = axisColor,
                    MajorGridlineColor = axisColor,
                    MinorGridlineColor = axisColor,
                    TicklineColor = axisColor,
                    TitleColor = axisColor,
                    Position = AxisPosition.Left,
                    Title = "Temperatur",
                    Unit = "°C",
                    ExtraGridlines = new[] { 0.0 },
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot
                    //Maximum = 35,
                    //Minimum = -25
                });

                var hourAxis = new CategoryAxis {
                    AxislineStyle = LineStyle.Solid,
                    TextColor = axisColor,
                    AxislineColor = axisColor,
                    TitleColor = axisColor,
                    Position = AxisPosition.Bottom,
                    Title = "Klokken"
                };

                for(DateTime dt = DateTime.Now.AddDays(-1);dt < DateTime.Now;dt = dt.AddHours(1)) {
                    hourAxis.Labels.Add(dt.Hour.ToString());
                }

                PlotModel.Axes.Add(hourAxis);

                PlotModel.InvalidatePlot(false);

            } catch(Exception ex) {
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }
                // TODO: be better at this..
            }
        }

        private void RepopulatePlotModel() {
            try {
                // TOHA: må reaktiveres for å oppdatere akse-informasjonen.. eller er det ikke nødvendig
                //  siden jeg tenker å automatisk hoppe tilbake til MainPage etter ett minutt elns?!
                //InitPlotModel();

                OxyColor bgColor = OxyColors.Black;
                OxyColor axisColor = OxyColors.AntiqueWhite;
                OxyColor graphPlusColor = OxyColors.Tomato;
                OxyColor graphMinusColor = OxyColors.LightBlue;

                PlotModel.Series.Clear();

                //PlotModel = new PlotModel {Title = "Siste døgn", LegendSymbolLength = 24};
                PlotModel.Title = "Siste døgn";
                PlotModel.LegendSymbolLength = 24;
                var s1 = new TwoColorAreaSeries {
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

                DateTime? startDt = null;
                List<TimeSpan> tss = new List<TimeSpan>();

                DateTime dtMin = DateTime.Now.AddDays(-1);
                for (int i=0;i<24;i++)
                {
                    DateTime dtMax = dtMin.AddHours(1);
                    double xPoint = i;

                    var min = dtMin;
                    lock (weatherData.LastDayValues)
                    {
                        var valuesForPeriod = weatherData.LastDayValues.Where(kvp => kvp.Key >= min && kvp.Key < dtMax).Select(kvp => kvp.Value).ToList();
                        if (valuesForPeriod.Any())
                        {
                            double average = valuesForPeriod.Average(v => v.CelsiusTemperature);
                            DataPoint dp = new DataPoint(xPoint, average);
                            s1.Points.Add(dp);
                        }
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

            } catch(Exception ex) {
                if(Debugger.IsAttached) {
                    Debugger.Break();
                }
                // TODO: be better at this..
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            weatherData = e.Parameter as WeatherData;
            RepopulatePlotModel();

            base.OnNavigatedTo(e);
        }

        /// <summary>
        /// When clicked, return to main page
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_Tapped(object sender, TappedRoutedEventArgs e) {
            this.Frame.Navigate(typeof(MainPage), weatherData);
        }
    }
}
