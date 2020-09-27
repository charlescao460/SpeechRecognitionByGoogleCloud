using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Cloud.Speech.V1;
using Google.Protobuf.Collections;

namespace SpeechRecognitionByGoogleCloud
{
    public class ResultArriveEventArgs : EventArgs
    {
        /// <summary>
        /// Results from speech recognition.
        /// <see cref="StreamingRecognitionResult"/>
        /// </summary>
        public RepeatedField<StreamingRecognitionResult> Results { get; }

        public ResultArriveEventArgs(RepeatedField<StreamingRecognitionResult> results)
        {
            Results = results;
        }
    }
}
