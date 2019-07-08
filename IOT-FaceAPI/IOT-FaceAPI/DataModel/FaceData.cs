using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IOT_FaceAPI.DataModel
{
    public class FaceData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public FaceData() {}

        public FaceData(FaceData faceData)
        {
            // Create a Deep Copy of FaceData instance
            _face.FaceId = faceData.Face.FaceId;
            _face.FaceAttributes = new FaceAttributes
            {
                Age = faceData.Face.FaceAttributes.Age,
                Emotion = new Emotion
                {
                    Anger = faceData.Face.FaceAttributes.Emotion.Anger,
                    Contempt = faceData.Face.FaceAttributes.Emotion.Contempt,
                    Disgust = faceData.Face.FaceAttributes.Emotion.Disgust,
                    Fear = faceData.Face.FaceAttributes.Emotion.Fear,
                    Happiness = faceData.Face.FaceAttributes.Emotion.Happiness,
                    Neutral = faceData.Face.FaceAttributes.Emotion.Neutral,
                    Sadness = faceData.Face.FaceAttributes.Emotion.Sadness,
                    Surprise = faceData.Face.FaceAttributes.Emotion.Surprise
                },
                FacialHair = new FacialHair
                {
                    Beard = faceData.Face.FaceAttributes.FacialHair.Beard,
                    Moustache = faceData.Face.FaceAttributes.FacialHair.Moustache,
                    Sideburns = faceData.Face.FaceAttributes.FacialHair.Sideburns
                },
                Gender = faceData.Face.FaceAttributes.Gender,
                Glasses = faceData.Face.FaceAttributes.Glasses,
                Hair = new Hair
                {
                    Bald = faceData.Face.FaceAttributes.Hair.Bald,
                    HairColor = faceData.Face.FaceAttributes.Hair.HairColor,
                    Invisible = faceData.Face.FaceAttributes.Hair.Invisible
                },
                Makeup = new Makeup
                {
                    EyeMakeup = faceData.Face.FaceAttributes.Makeup.EyeMakeup,
                    LipMakeup = faceData.Face.FaceAttributes.Makeup.LipMakeup
                },
                Smile = faceData.Face.FaceAttributes.Smile
            };

            _face.FaceRectangle = faceData.Face.FaceRectangle;

        }

        private DetectedFace _face = new DetectedFace();
        public DetectedFace Face
        {
            get => _face;

            set
            {
                if (_face.FaceId != value.FaceId)
                {
                    _face = value;
                    OnPropertyChanged();
                    OnPropertyChanged("FacialHair");
                    OnPropertyChanged("Makeup");
                    OnPropertyChanged("HairColor");
                }
            }
        }

        #region COMPUTED PROPERTIES
        public string FacialHair
        {
            get
            {
                if (null == Face.FaceAttributes)
                {
                    return String.Empty;
                }

                return $"beard-{(Face.FaceAttributes.FacialHair.Beard > 0.5 ? "Y" : "N")}" +
                    $" stache-{(Face.FaceAttributes.FacialHair.Moustache > 0.5 ? "Y" : "N")}" +
                    $" burns-{(Face.FaceAttributes.FacialHair.Sideburns > 0.5 ? "Y" : "N")}";
            }
        }

        public string Makeup
        {
            get
            {
                if (null == Face.FaceAttributes)
                {
                    return String.Empty;
                }

                return $"eye-{(Face.FaceAttributes.Makeup.EyeMakeup ? "Y" : "N")}" +
                    $" lip-{(Face.FaceAttributes.Makeup.LipMakeup ? "Y" : "N")}";
            }
        }

        public string HairColor
        {
            get
            {
                if (null == Face.FaceAttributes)
                {
                    return String.Empty;
                }

                if (Face.FaceAttributes.Hair.Bald > .7)
                {
                    return $"Bald ({Face.FaceAttributes.Hair.Bald.ToString("P2")})";
                }
                else
                {
                    HairColor hc = Face.FaceAttributes.Hair.HairColor.OrderByDescending(x => x.Confidence).FirstOrDefault();
                    return $"{hc.Color.ToString()} ({hc.Confidence.ToString("P2")})";
                }
            }
        }
        #endregion

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
