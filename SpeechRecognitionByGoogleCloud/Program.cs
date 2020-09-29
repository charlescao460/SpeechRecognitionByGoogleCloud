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

                int lastStableLength = 0;
                int lastSnippetLength = 0;
                FileStream testFileStream = new FileStream("debug.txt", FileMode.Create);
                StreamWriter testWriter = new StreamWriter(testFileStream);

                speechStream.ResultArrive += (s, e) =>
                {
                    if (e.Results.Count == 0)
                    {
                        return;
                    }
                    else if (e.Results[0].IsFinal)
                    {
                        lastStableLength = 0;
                        lastSnippetLength = 0;
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine(e.Results[0].Alternatives[0].Transcript);
                        Console.WriteLine();
                    }
                    else if (e.Results.Count >= 1) // In a well connected network, at most 2 results in single response
                    {
                        string stable = e.Results[0].Alternatives[0].Transcript;
                        if (stable.Length <= lastStableLength)
                        {
                            CleanCurrentConsoleLine(lastStableLength - stable.Length + lastSnippetLength);
                        }
                        else
                        {
                            CleanCurrentConsoleLine(lastSnippetLength);
                            Console.Write(stable.Substring(lastStableLength));
                        }
                        lastStableLength = stable.Length;
                        lastSnippetLength = 0;
                        testWriter.Write($"[0] {stable.Length}");
                        testWriter.WriteLine(stable);

                        if (e.Results.Count == 2)
                        {
                            string snippet = e.Results[1].Alternatives[0].Transcript;
                            Console.Write(snippet);
                            lastSnippetLength = snippet.Length;
                            testWriter.Write($"[1] {snippet.Length}");
                            testWriter.WriteLine(snippet);
                        }

                        if (e.Results.Count > 2)
                        {
                            Console.Error.WriteLine("More than 2 results in single responses!");
                        }

                        testWriter.Flush();
                        testFileStream.Flush();
                    }
                };
                speechStream.Run(CancellationToken.None).Wait();
            }
            return 0;
        }

        private static void CleanCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            for (int i = 0; i < Console.WindowWidth; i++)
            {
                Console.Write(" ");
            }
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private static void CleanCurrentConsoleLine(int length)
        {
            int currentLineCursor = Console.CursorTop;
            int leftCursor = Console.CursorLeft - length;
            if (leftCursor < 0 || length <= 0)
            {
                return;
            }
            Console.SetCursorPosition(leftCursor, currentLineCursor);
            for (int i = 0; i < length; i++)
            {
                Console.Write(" ");
            }
            Console.SetCursorPosition(leftCursor, currentLineCursor);
        }

    }
}
