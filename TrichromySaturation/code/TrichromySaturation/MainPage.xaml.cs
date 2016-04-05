using System;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.Devices.Gpio;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;


namespace TrichromySaturation
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Constant pin assignments
        private const int PUSH_BUTTON_PIN = 4;
        private const int RED_LED_PIN = 17;
        private const int GREEN_LED_PIN = 27;
        private const int BLUE_LED_PIN = 22;
        private const int RGB_LED_PIN = 5;

        // Class wrapper for the color sensor
        private TCS34725 _colorSensor = null;

        // GPIO pins for the pushbutton and LEDs
        private GpioPin _gpioPushbutton = null;
        private GpioPin _gpioRedLED = null;
        private GpioPin _gpioGreenLED = null;
        private GpioPin _gpioBlueLED = null;
        private GpioPinValue _pinValRed;
        private GpioPinValue _pinValGreen;
        private GpioPinValue _pinValBlue;

        // Other variables
        private readonly TimeSpan LED_OFF_DURATION_MSEC = TimeSpan.FromMilliseconds(100); // 100 msec
        private TimeSpan _redLEDDurationMsec;
        private TimeSpan _greenLEDDurationMsec;
        private TimeSpan _blueLEDDurationMsec;

        // Timers for blinking LEDs
        private DispatcherTimer _timerRed;
        private DispatcherTimer _timerGreen;
        private DispatcherTimer _timerBlue;

        private bool _isInitialized = false;

        public MainPage()
        {
            this.InitializeComponent();

            InitializeGPIO();
        }

        private async void InitializeGPIO()
        {
            try
            {
                GpioController controller = GpioController.GetDefault();

                if (null != controller)
                {
                    // Create and initialize color sensor instance
                    _colorSensor = new TCS34725(RGB_LED_PIN);
                    await _colorSensor.Initialize();
                    _colorSensor.LedState = TCS34725.eLedState.Off;

                    // Setup button pin
                    _gpioPushbutton = controller.OpenPin(PUSH_BUTTON_PIN);
                    _gpioPushbutton.DebounceTimeout = TimeSpan.FromMilliseconds(100); // 100 ms
                    _gpioPushbutton.SetDriveMode(GpioPinDriveMode.Input);
                    _gpioPushbutton.ValueChanged += gpioPushbutton_ValueChanged;

                    // Setup LEDs
                    // Red
                    _gpioRedLED = controller.OpenPin(RED_LED_PIN);
                    _gpioRedLED.SetDriveMode(GpioPinDriveMode.Output);
                    _pinValRed = SetGpioPinState(_gpioRedLED, GpioPinValue.High); // HIGH == ON

                    _timerRed = new DispatcherTimer();
                    _timerRed.Tick += timerRed_Tick;

                    // Green
                    _gpioGreenLED = controller.OpenPin(GREEN_LED_PIN);
                    _gpioGreenLED.SetDriveMode(GpioPinDriveMode.Output);
                    _pinValGreen = SetGpioPinState(_gpioGreenLED, GpioPinValue.High); // HIGH == ON

                    _timerGreen = new DispatcherTimer();
                    _timerGreen.Tick += timerGreen_Tick;

                    // Blue
                    _gpioBlueLED = controller.OpenPin(BLUE_LED_PIN);
                    _gpioBlueLED.SetDriveMode(GpioPinDriveMode.Output);
                    _pinValBlue = SetGpioPinState(_gpioBlueLED, GpioPinValue.High); // HIGH == ON

                    _timerBlue = new DispatcherTimer();
                    _timerBlue.Tick += timerBlue_Tick;

                    _isInitialized = true;
                    Debug.WriteLine("All pins initialized");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        // Sets the pin to the new pinValue and returns the new value;
        private GpioPinValue SetGpioPinState(GpioPin pin, GpioPinValue newValue)
        {
            pin.Write(newValue);
            return newValue;
        }

        private void ResetLEDsAndTimers(RGBData color)
        {
            _timerRed.Stop();
            _timerGreen.Stop();
            _timerBlue.Stop();

            _pinValRed = SetGpioPinState(_gpioRedLED, GpioPinValue.Low); 
            _pinValGreen = SetGpioPinState(_gpioGreenLED, GpioPinValue.Low);
            _pinValBlue = SetGpioPinState(_gpioBlueLED, GpioPinValue.Low); 

            _timerRed.Interval = LED_OFF_DURATION_MSEC;
            _timerGreen.Interval = LED_OFF_DURATION_MSEC;
            _timerBlue.Interval = LED_OFF_DURATION_MSEC;

            _timerRed.Start();
            _timerGreen.Start();
            _timerBlue.Start();
        }

        private TimeSpan GetLEDDurationForValue(int colorValue)
        {
            if ((colorValue < 0) || (colorValue > 255))
            {
                throw new ArgumentOutOfRangeException("Invalid Value");
            }
            
            // This sample only has 5 blink rates.  Subtract out the off duration
            // so that the blink will remain in sync across all possible values
            if(colorValue < 51) { return (TimeSpan.FromMilliseconds(1600) - LED_OFF_DURATION_MSEC); }
            else if (colorValue < 102) { return (TimeSpan.FromMilliseconds(1300) - LED_OFF_DURATION_MSEC); }
            else if (colorValue < 153) { return (TimeSpan.FromMilliseconds(1000) - LED_OFF_DURATION_MSEC); }
            else if (colorValue < 204) { return (TimeSpan.FromMilliseconds(700) - LED_OFF_DURATION_MSEC); }
            else { return (TimeSpan.FromMilliseconds(400) - LED_OFF_DURATION_MSEC); }
        }

        // Updates the sample square in headed mode
        private async void UpdateSampleColor(RGBData color)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                rectSample.Fill = new SolidColorBrush(Color.FromArgb(255, (byte)color.Red, (byte)color.Green, (byte)color.Blue));
                sldRed.Value = color.Red;
                sldGreen.Value = color.Green;
                sldBlue.Value = color.Blue;

                if (_isInitialized)
                {
                    _redLEDDurationMsec = GetLEDDurationForValue(color.Red);
                    _greenLEDDurationMsec = GetLEDDurationForValue(color.Green);
                    _blueLEDDurationMsec = GetLEDDurationForValue(color.Blue);

                    ResetLEDsAndTimers(color);
                }
            });
        }

        private async void gpioPushbutton_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            //Only read the sensor value when the button is released
            if (args.Edge == GpioPinEdge.RisingEdge)
            {
                //Read the approximate color from the sensor
                _colorSensor.LedState = TCS34725.eLedState.On;
                await System.Threading.Tasks.Task.Delay(1000);

                RGBData colorData = await _colorSensor.GetRgbData();
                _colorSensor.LedState = TCS34725.eLedState.Off;

                UpdateSampleColor(colorData);
            }
        }

        private void sld_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            Slider sld = (Slider)sender;

            switch(sld.Name)
            {
                case "sldRed":
                    txtRed.Text = sldRed.Value.ToString();
                    break;

                case "sldGreen":
                    txtGreen.Text = sldGreen.Value.ToString();
                    break;

                case "sldBlue":
                    txtBlue.Text = sldBlue.Value.ToString();
                    break;
            }
        }

        private void cmdSet_Click(object sender, RoutedEventArgs e)
        {
            RGBData colorData = new RGBData();

            colorData.Red = Convert.ToInt16(sldRed.Value);
            colorData.Green = Convert.ToInt16(sldGreen.Value);
            colorData.Blue = Convert.ToInt16(sldBlue.Value);

            UpdateSampleColor(colorData);
        }

        private void timerRed_Tick(object sender, object e)
        {
            _timerRed.Stop();
            _pinValRed = SetGpioPinState(_gpioRedLED, (_pinValRed == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High));
            Debug.WriteLine(String.Format("{0}: Red Pin Val: {1}", DateTime.Now.Ticks, _pinValRed));

            _timerRed.Interval = (_pinValRed == GpioPinValue.Low ? LED_OFF_DURATION_MSEC : _redLEDDurationMsec);
            _timerRed.Start();
        }

        private void timerGreen_Tick(object sender, object e)
        {
            _timerGreen.Stop();
            _pinValGreen = SetGpioPinState(_gpioGreenLED, (_pinValGreen == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High));
            Debug.WriteLine(String.Format("{0}: Green Pin Val: {1}", DateTime.Now.Ticks, _pinValGreen));

            _timerGreen.Interval = (_pinValGreen == GpioPinValue.Low ? LED_OFF_DURATION_MSEC : _greenLEDDurationMsec);
            _timerGreen.Start();
        }

        private void timerBlue_Tick(object sender, object e)
        {
            _timerBlue.Stop();
            _pinValBlue = SetGpioPinState(_gpioBlueLED, (_pinValBlue == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High));
            Debug.WriteLine(String.Format("{0}: Blue Pin Val: {1}", DateTime.Now.Ticks, _pinValBlue));

            _timerBlue.Interval = (_pinValBlue == GpioPinValue.Low ? LED_OFF_DURATION_MSEC : _blueLEDDurationMsec);
            _timerBlue.Start();
        }
    }
}
