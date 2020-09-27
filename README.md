# SpeechRecognitionByGoogleCloud
A .NET program that captures local audio and recognizes speech

## Usage
1. Go to Google Cloud's [Speech-to-Text](https://cloud.google.com/speech-to-text/docs/quickstart-client-libraries) console.
2. Enable Speech-to-Text API.
3. Create a role with access to API, download its private key as a json file
4. Copy the json file to `SpeechRecognitionByGoogleCloud.exe`'s folder, rename it as `GoogleCloudKey.json`
5. Run it from CMD or PowerShell, use `--help` to get help.

## Runtime Requirements
* Windows 10 Version 1803 or above
* **or** .NET Framework 4.7.2
* Google Cloud credentials with Speech-to-Text enabled.

## Build Requirements
* MSBuild 15.0 or above
* NuGet 3.4 or above
* **or** Visual Studio 2019
* .NET Framework 4.7.2 Targeting Pack
