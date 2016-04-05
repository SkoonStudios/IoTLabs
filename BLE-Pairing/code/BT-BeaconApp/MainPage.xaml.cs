using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.Devices.Enumeration;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BT_BeaconApp
{
    public sealed partial class MainPage : Page
    {
        private DeviceWatcher _deviceWatcher = null;        // Device watcher for new BLE devices
        private bool _restartWatcher = false;               // Restart watcher upon completion

        SpeechSynthesizer _speechSynth = null;              // Text to speech engine
        MediaElement _audio = null;                         // Audio Renderer


        // Collect of devices bound to the UI
        public ObservableCollection<DeviceInformationDisplay> ResultCollection
        {
            get;
            private set;
        }

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Initialize the objects
            ResultCollection = new ObservableCollection<DeviceInformationDisplay>();
            DataContext = this;

            _speechSynth = new SpeechSynthesizer();
            _speechSynth.Voice = SpeechSynthesizer.AllVoices.DefaultIfEmpty(SpeechSynthesizer.DefaultVoice).FirstOrDefault(p => p.DisplayName.Contains("Mark"));
            _audio = new MediaElement();

            StartDeviceWatcher();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            StopDeviceWatcher();
        }

        private void StartDeviceWatcher()
        {
            ResultCollection.Clear();

            // Currently Bluetooth APIs don't provide a selector to get ALL devices that are both paired and non-paired.  Typically you wouldn't need this for common scenarios, 
            // but it's convenient to demonstrate the various sample scenarios. 
            string selector = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")" + 
                              " AND (System.Devices.Aep.CanPair:=System.StructuredQueryType.Boolean#True OR System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True)";

            // Kind is specified in the selector info
            _deviceWatcher = DeviceInformation.CreateWatcher(
                selector,
                DeviceProperties.AssociationEndpointProperties, 
                DeviceInformationKind.AssociationEndpoint);

            // Hook up events for the watcher
            _deviceWatcher.Added += OnDeviceAdded;
            _deviceWatcher.Updated += OnDeviceUpdated;
            _deviceWatcher.Removed += OnDeviceRemoved;
            _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
            _deviceWatcher.Stopped += OnStopped;

            _deviceWatcher.Start();
        }

        private void StopDeviceWatcher()
        {
            if (null != _deviceWatcher)
            {
                // First unhook all event handlers except the stopped handler. This ensures our
                // event handlers don't get called after stop, as stop won't block for any "in flight" 
                // event handler calls.  We leave the stopped handler as it's guaranteed to only be called
                // once and we'll use it to know when the query is completely stopped. 
                _deviceWatcher.Added -= OnDeviceAdded;
                _deviceWatcher.Updated -= OnDeviceUpdated;
                _deviceWatcher.Removed -= OnDeviceRemoved;
                _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;

                if (DeviceWatcherStatus.Started == _deviceWatcher.Status ||
                    DeviceWatcherStatus.EnumerationCompleted == _deviceWatcher.Status)
                {
                    _deviceWatcher.Stop();
                }
            }
        }

        private async void CheckPairing(DeviceInformationDisplay deviceInfoDisplay)
        {
            Debug.WriteLine("{0}- '{1}' Signal Strength - {2}", DateTime.Now.Ticks, deviceInfoDisplay.Name, deviceInfoDisplay.SignalStrength);

            if (!deviceInfoDisplay.IsPairing)
            {
                // Pair device only if it is close
                if (deviceInfoDisplay.Name.StartsWith("XY") &&
                    !deviceInfoDisplay.IsPaired &&
                    (deviceInfoDisplay.SignalStrength > -50))
                {
                    // Set a flag on the device so that once it begins to pair, it doesn't constantly try to pair
                    deviceInfoDisplay.IsPairing = true;

                    // This is just basic pairing (No PIN or confirmation)
                    DevicePairingKinds ceremonies = DevicePairingKinds.ConfirmOnly;
                    DevicePairingProtectionLevel protectionLevel = DevicePairingProtectionLevel.Default;
                    DeviceInformationCustomPairing customPairing = deviceInfoDisplay.DeviceInformation.Pairing.Custom;

                    // In the cases where more complex pairing is required, user interaction happens in the Pairing Requested event
                    customPairing.PairingRequested += OnPairingRequested;
                    DevicePairingResult result = await customPairing.PairAsync(ceremonies, protectionLevel);
                    customPairing.PairingRequested -= OnPairingRequested;

                    Debug.WriteLine("{0} pair result - {1}", deviceInfoDisplay.Name, result.Status);

                    // If the pair was successful, act based on the beacon ID
                    if(DevicePairingResultStatus.Paired == result.Status)
                    {
                        var stream = await _speechSynth.SynthesizeTextToStreamAsync("Paired beacon ID " + deviceInfoDisplay.Name);

                        // Use a dispatcher to play the audio
                        var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            //Set the souce of the MediaElement to the SpeechSynthesisStream
                            _audio.SetSource(stream, stream.ContentType);

                            //Play the stream
                            _audio.Play();
                        });

                    }

                    deviceInfoDisplay.IsPairing = false;
                }
                else if (deviceInfoDisplay.Name.StartsWith("XY") &&
                        deviceInfoDisplay.IsPaired &&
                        (deviceInfoDisplay.SignalStrength < -50))
                {
                    // Set a flag on the device so that once it begins to pair, it doesn't constantly try to unpair
                    deviceInfoDisplay.IsPairing = true;

                    // Unpair the beacon
                    DeviceUnpairingResult result = await deviceInfoDisplay.DeviceInformation.Pairing.UnpairAsync();
                    Debug.WriteLine("{0} unpair result - {1}", deviceInfoDisplay.Name, result.Status);

                    deviceInfoDisplay.IsPairing = false;
                }
            }
            
        }

        private async void OnDeviceAdded(DeviceWatcher watcher, DeviceInformation deviceInfoAdded)
        {
            // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                DeviceInformationDisplay deviceInfoDisplay = new DeviceInformationDisplay(deviceInfoAdded);

                if (!ResultCollection.Any(p => p.Name == deviceInfoAdded.Name))
                {
                    ResultCollection.Add(deviceInfoDisplay);
                    Debug.WriteLine("{0} devices found.", ResultCollection.Count);
                }

                CheckPairing(deviceInfoDisplay);
            });
        }

        private async void OnDeviceUpdated(DeviceWatcher watcher, DeviceInformationUpdate deviceInfoUpdate)
        {
            // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Find the corresponding updated DeviceInformation in the collection and pass the update object
                // to the Update method of the existing DeviceInformation. This automatically updates the object
                // for us.
                foreach (DeviceInformationDisplay deviceInfoDisplay in ResultCollection)
                {
                    if (deviceInfoDisplay.Id == deviceInfoUpdate.Id)
                    {
                        CheckPairing(deviceInfoDisplay);
                        deviceInfoDisplay.Update(deviceInfoUpdate);
                        break;
                    }
                }
            });
        }

        private async void OnDeviceRemoved(DeviceWatcher watcher, DeviceInformationUpdate deviceInfoRemoved)
        {
            // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Find the corresponding DeviceInformation in the collection and remove it
                foreach (DeviceInformationDisplay deviceInfoDisplay in ResultCollection)
                {
                    if (deviceInfoDisplay.Id == deviceInfoRemoved.Id)
                    {
                        CheckPairing(deviceInfoDisplay);                    
                        ResultCollection.Remove(deviceInfoDisplay);
                        break;
                    }
                }

                Debug.WriteLine("{0} devices found.", ResultCollection.Count);
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher watcher, object args)
        {
            Debug.WriteLine("{0} devices found. Enumeration completed. Watching for updates...", ResultCollection.Count);
            _restartWatcher = true;
            _deviceWatcher.Stop();
        }

        private async void OnStopped(DeviceWatcher watcher, object args)
        {
            Debug.WriteLine("{0} devices found. Watcher {1}.",
                ResultCollection.Count,
                DeviceWatcherStatus.Aborted == watcher.Status ? "aborted" : "stopped");

            if (_restartWatcher)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    _restartWatcher = false;
                    _deviceWatcher.Start();
                });
            }
        }

        private async void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            switch (args.PairingKind)
            {
                case DevicePairingKinds.ConfirmOnly:
                    // Windows itself will pop the confirmation dialog as part of "consent" if this is running on Desktop or Mobile
                    // If this is an App for 'Windows IoT Core' where there is no Windows Consent UX, you may want to provide your own confirmation.
                    args.Accept();
                    break;

                case DevicePairingKinds.DisplayPin:
                    // We just show the PIN on this side. The ceremony is actually completed when the user enters the PIN
                    // on the target device. We automatically except here since we can't really "cancel" the operation
                    // from this side.
                    args.Accept();

                    // No need for a deferral since we don't need any decision from the user
                    Debug.WriteLine("Please enter this PIN on the device you are pairing with: {0}", args.Pin);
                    break;

                case DevicePairingKinds.ProvidePin:
                    // A PIN may be shown on the target device and the user needs to enter the matching PIN on 
                    // this Windows device. Get a deferral so we can perform the async request to the user.
                    var collectPinDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        // NOT IMPLEMENTED - Make ASYNC
                        // GET PIN FROM USER

                        collectPinDeferral.Complete();
                    });
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    // We show the PIN here and the user responds with whether the PIN matches what they see
                    // on the target device. Response comes back and we set it on the PinComparePairingRequestedData
                    // then complete the deferral.
                    var displayMessageDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        // NOT IMPLEMENTED - Make ASYNC
                        // CONFIRM PIN MATCH

                        displayMessageDeferral.Complete();
                    });
                    break;
            }
        }
    }
}
