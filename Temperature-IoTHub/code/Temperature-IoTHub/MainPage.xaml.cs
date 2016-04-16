using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Temperature_IoTHub
{
    public sealed partial class MainPage : Page
    {
        //Create a constant for pressure at sea level. 
        //This is based on your local sea level pressure (Unit: Hectopascal)
        private const float CLT_SEA_LEVEL_PRESSURE = 1027.3f;
        private TimeSpan TIMER_TICK = TimeSpan.FromMilliseconds(1000);
        private static string CXN_STRING = "<REPLACE>";

        //A class which wraps the barometric sensor
        private BMP280 _ptSensor = null;

        // Callback timer for reading sensor
        private DispatcherTimer _timer = null;

        // Azure Device
        private DeviceClient _devClient = null;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs navArgs)
        {
            try
            {
                //Create a new object for our barometric sensor class
                _ptSensor = new BMP280();
               
                //Initialize the sensor
                await _ptSensor.Initialize();

                // Create Azure Device
                _devClient = DeviceClient.CreateFromConnectionString(CXN_STRING, TransportType.Http1);

                // Create Timer
                _timer = new DispatcherTimer();
                _timer.Interval = TIMER_TICK;
                _timer.Tick += OnTimerTick;
                _timer.Start();

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _timer.Stop();
        }

        private async void OnTimerTick(object sender, object e)
        {
            float temperature = await _ptSensor.ReadTemperature();
            float pressure = await _ptSensor.ReadPreasure();
            float altitude = await _ptSensor.ReadAltitude(CLT_SEA_LEVEL_PRESSURE);

            // Write the values to your debug console
            Debug.WriteLine($"Temperature: {temperature.ToString()} deg C");
            Debug.WriteLine($"Pressure: {pressure.ToString()} Pa");
            Debug.WriteLine($"Altitude: {altitude.ToString()} m");

            if (null != _devClient)
            {
                try
                {
                    // Send the values to Azure IoT Hub
                    var obj = new
                    {
                        time = DateTime.UtcNow.ToString("o"),
                        temperature = temperature,
                        pressure = pressure,
                        altitude = altitude,
                    };

                    string jsonText = JsonConvert.SerializeObject(obj);
                    Message msg = new Message(Encoding.UTF8.GetBytes(jsonText));
                    await _devClient.SendEventAsync(msg);

                    Debug.WriteLine($"AZURE: {jsonText}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception when sending message:" + ex.Message);
                }
            }
        }
    }
}
