using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml.Media.Imaging;


namespace BT_BeaconApp
{
    public static class DeviceProperties
    {
        public static List<string> AssociationEndpointProperties
        {
            get
            {
                List<string> properties = new List<string>();
                properties.Add("System.Devices.Aep.SignalStrength");

                return properties;
            }
        }
    }

    public class DeviceInformationDisplay : INotifyPropertyChanged
    {
        private DeviceInformation deviceInfo;

        public DeviceInformationDisplay(DeviceInformation deviceInfoIn)
        {
            deviceInfo = deviceInfoIn;
            UpdateGlyphBitmapImage();
        }

        public DeviceInformationKind Kind
        {
            get { return deviceInfo.Kind; }
        }

        public string Id
        {
            get { return deviceInfo.Id; }
        }

        public string Name
        {
            get { return deviceInfo.Name; }
        }

        public BitmapImage GlyphBitmapImage
        {
            get;
            private set;
        }

        public bool IsPairing
        {
            get;
            set;
        }

        public bool CanPair
        {
            get { return deviceInfo.Pairing.CanPair; }
        }

        public bool IsPaired
        {
            get { return deviceInfo.Pairing.IsPaired; }
        }

        public int SignalStrength
        {
            get
            {
                int val = int.MinValue;
                int.TryParse(deviceInfo.Properties["System.Devices.Aep.SignalStrength"]
                    .ToString(), out val);

                return val;
            }
        }

        public IReadOnlyDictionary<string, object> Properties
        {
            get { return deviceInfo.Properties; }
        }

        public DeviceInformation DeviceInformation
        {
            get { return deviceInfo; }

            private set { deviceInfo = value; }
        }

        public void Update(DeviceInformationUpdate deviceInfoUpdate)
        {
            deviceInfo.Update(deviceInfoUpdate);

            OnPropertyChanged("Kind");
            OnPropertyChanged("Id");
            OnPropertyChanged("Name");
            OnPropertyChanged("DeviceInformation");
            OnPropertyChanged("CanPair");
            OnPropertyChanged("IsPaired");
            OnPropertyChanged("SignalStrength");

            UpdateGlyphBitmapImage();
        }

        private async void UpdateGlyphBitmapImage()
        {
            DeviceThumbnail deviceThumbnail = await deviceInfo.GetGlyphThumbnailAsync();
            BitmapImage glyphBitmapImage = new BitmapImage();
            await glyphBitmapImage.SetSourceAsync(deviceThumbnail);
            GlyphBitmapImage = glyphBitmapImage;
            OnPropertyChanged("GlyphBitmapImage");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public string ToJson()
        {
            var obj = new
            {
                time = DateTime.UtcNow.ToString("o"),
                deviceName = this.Name,
                signalStrength = this.SignalStrength,
            };

            return JsonConvert.SerializeObject(obj);
        }
    }
}
