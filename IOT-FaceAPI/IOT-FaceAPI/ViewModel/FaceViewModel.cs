using IOT_FaceAPI.DataModel;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace IOT_FaceAPI.ViewModel
{
    public enum REQUEST_STATE
    {
        UNINITIALIZED,
        FAILED,
        WAITING,
        PROCESSING,
        NORESULTS,
        SUCCESS
    }

    public class FaceViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<REQUEST_STATE> RequestStateChanged;

        //
        // Utilize your Key and Endpoint from the Azure Portal
        // The API_URI will be the of the form https://{region}.api.cognitive.microsoft.com, even though
        // the endpoint in the portal may show something slightly different
        //
        private const string FACE_API_KEY = "{ENTER API KEY FROM AZURE PORTAL}";
        private const string FACE_API_URI = "https://eastus.api.cognitive.microsoft.com";   // Example: Resource created in EASTUS region
        private IFaceClient _faceServiceClient = null;

        // Select the data to be returned from the API
        private IList<FaceAttributeType> FACE_ATTR_TYPES = new FaceAttributeType[]
        {
            FaceAttributeType.Accessories,
            FaceAttributeType.Age,
            FaceAttributeType.Emotion,
            FaceAttributeType.FacialHair,
            FaceAttributeType.Gender,
            FaceAttributeType.Glasses,
            FaceAttributeType.Hair,
            FaceAttributeType.Makeup,
            FaceAttributeType.Smile
        };
       
        private ObservableCollection<FaceData> _faces = new ObservableCollection<FaceData>();
        private int _nSelectedIdx = -1;

        #region PROPERTIES
        private FaceData _currentFace;
        public FaceData CurrentFace
        {
            get => _currentFace;

            private set
            {
                if ((null == _currentFace) || (null == value) || (_currentFace.Face.FaceId != value.Face.FaceId))
                {
                    _currentFace = value;
                    OnPropertyChanged();
                }
            }
        }

        private REQUEST_STATE _requestState = REQUEST_STATE.UNINITIALIZED;
        public REQUEST_STATE RequestState
        {
            get => _requestState;

            private set
            {
                if (_requestState != value)
                {
                    _requestState = value;
                    OnPropertyChanged();

                    RequestStateChanged?.Invoke(this, _requestState);
                }
            }
        }

        private string _statusMessage = "Welcome... Please take a picture to get started";
        public string StatusMessage
        {
            get => _statusMessage;

            private set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        public void Initialize()
        {
            // Setup Face API
            _faceServiceClient = new FaceClient(
                new ApiKeyServiceClientCredentials(FACE_API_KEY),
                new System.Net.Http.DelegatingHandler[] { });

            if (Uri.IsWellFormedUriString(FACE_API_URI, UriKind.Absolute))
            {
                _faceServiceClient.Endpoint = FACE_API_URI;
            }
            else
            {
                RequestState = REQUEST_STATE.FAILED;
                throw new ArgumentException($"Invalid URI: {FACE_API_URI}");
            }

            RequestState = REQUEST_STATE.WAITING;
        }

        public async Task ProcessPictureAsync(InMemoryRandomAccessStream memStream)
        {
            RequestState = REQUEST_STATE.PROCESSING;

            try
            {
                // Submit the photo to the REST Endpoint
                IList<DetectedFace> faceList = await _faceServiceClient.Face.DetectWithStreamAsync(memStream.AsStream(), true, false, FACE_ATTR_TYPES);

                if (faceList.Count == 0)
                {
                    StatusMessage = "No face detected...";
                    RequestState = REQUEST_STATE.NORESULTS;
                    _nSelectedIdx = -1;
                    return;
                }
                else
                {
                    foreach (DetectedFace d in faceList)
                    {
                        FaceData fd = new FaceData();
                        fd.Face = d;
                        _faces.Add(fd);
                    }

                    //
                    // For demo purposes, we are only going to look at the first face
                    // Since we do have a collection of faces though, it is possible to set the
                    // CurrentFace to whatever face we want to look at
                    //
                    _nSelectedIdx = 0;
                    CurrentFace = new FaceData(_faces[_nSelectedIdx]);
                    StatusMessage = $"Face ID: {_faces[_nSelectedIdx].Face.FaceId}";
                }
            }
            catch (APIErrorException f)
            {
                StatusMessage = $"0x{f.HResult}: {f.Message}";
                RequestState = REQUEST_STATE.FAILED;

                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"0x{ex.HResult}: {ex.Message}";
                RequestState = REQUEST_STATE.FAILED;

                return;
            }

            RequestState = REQUEST_STATE.SUCCESS;
        }

        public void ClearData()
        {
            CurrentFace = new FaceData();
            _faces.Clear();
            _nSelectedIdx = -1;
            RequestState = REQUEST_STATE.WAITING;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }
}
