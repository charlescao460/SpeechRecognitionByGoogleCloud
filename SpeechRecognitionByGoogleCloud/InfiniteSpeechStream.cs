using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Speech.V1;
using JetBrains.Annotations;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SpeechRecognitionByGoogleCloud
{
    public class InfiniteSpeechStream : IDisposable
    {
        /// <summary>
        /// Path to google cloud credentials json file.
        /// </summary>
        private const string GoogleCloudKeys = "GoogleCloudKey.json";

        /// <summary>
        /// Environment variable name for google cloud api credentials
        /// </summary>
        private const string GoogleCloudEnvironment = "GOOGLE_APPLICATION_CREDENTIALS";

        /// <summary>
        /// Rpc stream allowed continue time in seconds.
        /// </summary>
        private const int RpcStreamLife = 290;

        /// <summary>
        /// The default sample rate that will be sent to google cloud
        /// </summary>
        public const int DefaultReSampleRate = 44100;

        static InfiniteSpeechStream()
        {
            SetUpEnvironmentVariables();
        }

        /// <summary>
        /// Invoked when there's new result returned from Google Speech API.
        /// </summary>
        public event EventHandler<ResultArriveEventArgs> ResultArrive;

        /// <summary>
        /// Google cloud speech client
        /// </summary>
        public SpeechClient GoogleCloudSpeechClient { get; }

        private bool _disposed = false;
        private readonly WasapiCapture _wasapiCapture;
        private readonly BufferedWaveProvider _reSamplerWaveProvider;
        private readonly MediaFoundationResampler _reSampler;
        private DateTime _rpcStreamDeadline;
        private readonly object _recognizeStreamLock = new object();
        private SpeechClient.StreamingRecognizeStream _recognizeStream;
        private readonly StreamingRecognitionConfig _recognitionConfig;
        private readonly byte[] _convertedBuffer;

        /// <summary>
        /// Construct an InfiniteSpeechStream without starting it.
        /// </summary>
        /// <param name="wasapiCapture">A WasapiCapture that must be in stopped state. It will be disposed with this stream.</param>
        /// <param name="video">If use video enhanced model.</param>
        /// <param name="sampleRate">The sample rate of data sent to google.</param>
        public InfiniteSpeechStream([NotNull] WasapiCapture wasapiCapture, bool video = false, [NonNegativeValue] int sampleRate = DefaultReSampleRate)
        {
            _wasapiCapture = wasapiCapture;
            GoogleCloudSpeechClient = SpeechClient.Create();
            if (_wasapiCapture.CaptureState != CaptureState.Stopped)
            {
                throw new ArgumentException("WasapiCapture should not in capture state.", nameof(wasapiCapture));
            }
            _reSamplerWaveProvider = new BufferedWaveProvider(wasapiCapture.WaveFormat) { ReadFully = false };
            _reSampler = new MediaFoundationResampler(_reSamplerWaveProvider, new WaveFormat(sampleRate, 16, 1));
            _convertedBuffer = new byte[_reSampler.WaveFormat.AverageBytesPerSecond * 10];
            _recognitionConfig = new StreamingRecognitionConfig()
            {
                Config = new RecognitionConfig()
                {
                    Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                    SampleRateHertz = sampleRate,
                    LanguageCode = LanguageCodes.English.UnitedStates,
                    UseEnhanced = true,
                    EnableAutomaticPunctuation = true,
                    Model = video ? "video" : "phone_call"
                },
                InterimResults = true,
            };
        }

        /// <summary>
        /// Start the loop of continuous reading result.
        /// </summary>
        public async Task Run(CancellationToken cancellationToken)
        {
            await CreateNewStreamIfNeeded();
            _wasapiCapture.StartRecording();
            while (!cancellationToken.IsCancellationRequested)
            {
                AsyncResponseStream<StreamingRecognizeResponse> responseStream;
                lock (_recognizeStreamLock)
                {
                    responseStream = _recognizeStream.GetResponseStream();
                }
                try
                {
                    await responseStream.MoveNextAsync(cancellationToken);
                }catch(System.AggregateException)
                {
                    Console.WriteLine("System.AggregateException");
                }
                var results = responseStream.Current.Results;
                ResultArrive?.Invoke(this, new ResultArriveEventArgs(results));
            }
        }


        /// <summary>
        /// Check if current prc stream exceed its life. If so end current one and start a new one.
        /// </summary>
        private async Task CreateNewStreamIfNeeded()
        {
            if (_recognizeStream == null) // The first one
            {
                lock (_recognizeStreamLock)
                {
                    _rpcStreamDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(RpcStreamLife);
                    _recognizeStream = GoogleCloudSpeechClient.StreamingRecognize();
                }
                await _recognizeStream.WriteAsync(new StreamingRecognizeRequest()
                { StreamingConfig = _recognitionConfig });//Configured
                _wasapiCapture.DataAvailable += OnWaveInDataAvailable;
            }
            else if (DateTime.UtcNow >= _rpcStreamDeadline) // Expiring, switch to new
            {
                _wasapiCapture.DataAvailable -= OnWaveInDataAvailable; // Stop sending new bytes
                SpeechClient.StreamingRecognizeStream oldStream;
                lock (_recognizeStreamLock)
                {
                    oldStream = _recognizeStream;
                    _rpcStreamDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(RpcStreamLife);
                    _recognizeStream = GoogleCloudSpeechClient.StreamingRecognize(); // Create new one
                }
                await _recognizeStream.WriteAsync(new StreamingRecognizeRequest()
                { StreamingConfig = _recognitionConfig }); //Configure new one
                _wasapiCapture.DataAvailable += OnWaveInDataAvailable; // Start sending to new stream

                await oldStream.WriteCompleteAsync(); // Complete old one
                oldStream.GrpcCall.Dispose();
            }
        }

        private async void OnWaveInDataAvailable(object s, WaveInEventArgs e)
        {
            _reSamplerWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
            int convertedBytes = _reSampler.Read(_convertedBuffer, 0, _convertedBuffer.Length);
            Task writeTask;
            lock (_recognizeStreamLock)
            {
                writeTask = _recognizeStream.WriteAsync(new StreamingRecognizeRequest()
                {
                    AudioContent = Google.Protobuf.ByteString.CopyFrom(_convertedBuffer, 0, convertedBytes)
                });
            }
            await writeTask;
            await CreateNewStreamIfNeeded();
        }

        /// <summary>
        /// <inheritdoc cref="IDisposable.Dispose()"/>
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _wasapiCapture?.Dispose();
                _reSampler?.Dispose();
                _disposed = true;
            }
        }

        ~InfiniteSpeechStream()
        {
            Dispose();
        }

        private static void SetUpEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(GoogleCloudEnvironment, GoogleCloudKeys);
        }
    }
}
