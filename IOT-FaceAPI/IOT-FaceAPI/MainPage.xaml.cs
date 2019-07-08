using IOT_FaceAPI.ViewModel;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IOT_FaceAPI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public FaceViewModel ViewModel { get; set; } = new FaceViewModel();

        private MediaCapture _cameraCapture = null;
        private readonly DisplayRequest _displayRequest = new DisplayRequest();
        private bool _isPreviewing = false;

        private int _bmiWidth;
        private int _bmiHeight;

        public MainPage()
        {
            this.InitializeComponent();

            ViewModel.RequestStateChanged += ViewModel_RequestStateChanged;
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Initialize Face API Client
                ViewModel.Initialize();

                // Setup Camera
                _cameraCapture = new MediaCapture();
                await _cameraCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview,
                    AudioDeviceId = String.Empty
                });

                _displayRequest.RequestActive();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
                cameraPreview.Source = _cameraCapture;

                await _cameraCapture.StartPreviewAsync();
                _isPreviewing = true;

            }
            catch(Exception ex)
            {
                Debug.WriteLine($"0x{ex.HResult}: {ex.Message}");
            }
        }

        private async void CmdTakePicture_Click(object sender, RoutedEventArgs e)
        {
            ToggleCameraStateAsync();

            await TakeAndProcessPictureAsync();
            ProcessFaceRect();
        }

        private void CmdReset_Click(object sender, RoutedEventArgs e)
        {
            imgPhoto.Source = null;
            shpFrame.Points.Clear();
            ViewModel.ClearData();
            ToggleCameraStateAsync();
        }

        private async Task TakeAndProcessPictureAsync()
        {
            try
            {
                InMemoryRandomAccessStream memStream = new InMemoryRandomAccessStream();
                await _cameraCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), memStream);

                memStream.Seek(0);

                BitmapImage bmpImage = new BitmapImage();
                bmpImage.SetSource(memStream);
                imgPhoto.Source = bmpImage;

                _bmiHeight = bmpImage.PixelHeight;
                _bmiWidth = bmpImage.PixelWidth;

                memStream.Seek(0);
                await ViewModel.ProcessPictureAsync(memStream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"0x{ex.HResult}: {ex.Message}");
            }
        }

        private void ProcessFaceRect()
        {
            if (ViewModel.RequestState == REQUEST_STATE.SUCCESS)
            {
                //
                // Unless the image control is at the same resolution as the captured image
                // need to apply a scale factor so that the rectangle draws in the proper place
                //
                double scaleHeight = imgPhoto.ActualHeight / _bmiHeight;
                double scaleWidth = imgPhoto.ActualWidth / _bmiWidth;

                FaceRectangle faceRect = ViewModel.CurrentFace.Face.FaceRectangle;
                FaceRectangle faceRectScaled = new FaceRectangle((int)(faceRect.Width * scaleWidth), 
                                                                 (int)(faceRect.Height * scaleHeight),
                                                                 (int)(faceRect.Left * scaleWidth), 
                                                                 (int)(faceRect.Top * scaleHeight));

                shpFrame.Points.Add(new Point(faceRectScaled.Left, faceRectScaled.Top));
                shpFrame.Points.Add(new Point(faceRectScaled.Left + faceRectScaled.Width, faceRectScaled.Top));
                shpFrame.Points.Add(new Point(faceRectScaled.Left + faceRectScaled.Width, faceRectScaled.Top + faceRectScaled.Height));
                shpFrame.Points.Add(new Point(faceRectScaled.Left, faceRectScaled.Top + faceRectScaled.Height));
                shpFrame.Points.Add(new Point(faceRectScaled.Left, faceRectScaled.Top));
            }
        }

        private void ViewModel_RequestStateChanged(object sender, REQUEST_STATE e)
        {
            cmdTakePicture.IsEnabled = (((int)e > 1) ? true : false);
            cmdReset.IsEnabled = (((int)e > 1) ? true : false);
        }

        private async void ToggleCameraStateAsync()
        {
            _isPreviewing = !_isPreviewing;

            cameraPreview.Visibility = (_isPreviewing ? Visibility.Visible : Visibility.Collapsed);
            imgPhoto.Visibility = (_isPreviewing ? Visibility.Collapsed : Visibility.Visible);
            cmdTakePicture.Visibility = (_isPreviewing ? Visibility.Visible : Visibility.Collapsed);
            cmdReset.Visibility = (_isPreviewing ? Visibility.Collapsed : Visibility.Visible);

            if (_isPreviewing)
            {
                await _cameraCapture.StartPreviewAsync();
            }
            else
            {
                await _cameraCapture.StopPreviewAsync();
            }
        }
    }
}
