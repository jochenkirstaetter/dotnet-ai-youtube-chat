# Chat with YouTube videos using .NET and C#

This is a sample demonstrating how to ingest a transcript for a YouTube video and ask questions about it using either OpenAI, Gemini, or local AI models, and .NET.

## Running the sample
To run the sample:
1. Clone the app locally
2. Get an API key for OpenAI and set a local environment variable named OPENAI_API_KEY with that key
3. Alternatively, get an API key for [Gemini](https://aistudio.google.com/app/apikey) and set a local environment variable named GOOGLE_API_KEY with that key
4. Run the project using your favorite editor, or calling `dotnet run` from a Terminal in the root of your project directory

You could also run this sample using a local AI model, downloaded using [Ollama](https://ollama.com):
1. Install [Ollama](https://ollama.com)
2. Once installed, open a Terminal window and install the following models:
  * `ollama pull llama3.2`
  * `ollama pull all-minilm`
3. Run the project using your favorite editor, or calling `dotnet run` from a Terminal in the root of your project directory

## Architectural details
This sample is built using the following technology:
* [.NET 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
* [Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/)
* The [OpenAI library for .NET](https://www.nuget.org/packages/OpenAI)
* The [Gemini SDK for .NET using Microsoft.Extension.AI](https://www.nuget.org/packages/Mscc.GenerativeAI.Microsoft/2.0.1-preview)
* [OllamaSharp](https://www.nuget.org/packages/OllamaSharp)
* A [YouTubeTranscriptAPI](https://www.nuget.org/packages/Lofcz.Forks.YoutubeTranscriptApi) package, which is a .NET implementation of the Python [youtube-transcript-api](https://github.com/jdepoix/youtube-transcript-api) module
