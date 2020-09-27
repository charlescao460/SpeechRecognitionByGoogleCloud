using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Google.Cloud.Speech.V1;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Compression;

namespace SpeechRecognitionByGoogleCloud
{
    internal class Program
    {
        private class Options
        {
            [Option(shortName: 'r', longName: "SampleRate", Required = false, Default = InfiniteSpeechStream.DefaultReSampleRate, HelpText = "Sample Rate of PCM stream that will send to Google Cloud.")]
            public int SampleRate { get; set; }

            [Option(shortName: 'v', longName: "video", Required = false, Default = false, HelpText = "Specify if use the video model, which does better for high sample rate audio.")]
            public bool VideoMode { get; set; }

        }

        private static int Main(string[] args)
        {
            int ret = CommandLine.Parser.Default.ParseArguments<Options>(args).MapResult(RunAndReturn, OnParseError);
            return ret;
        }

        private static int OnParseError(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(Enum.GetName(typeof(ErrorType), error.Tag));
            }
            return -1;
        }

        /// <summary>
        /// Main logic here
        /// </summary>
        private static int RunAndReturn(Options options)
        {
            using (WasapiCapture capture = new WasapiLoopbackCapture())
            {
                InfiniteSpeechStream speechStream = new InfiniteSpeechStream(capture, options.VideoMode, options.SampleRate);
                speechStream.ResultArrive += (s, e) =>
                {
                    foreach (var result in e.Results)
                    {
                        Console.WriteLine(result.Alternatives[0].Transcript);
                    }
                };
                speechStream.Run(CancellationToken.None).Wait();
            }
            return 0;
        }
    }
}
