using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;
using OllamaSharp;
using YoutubeTranscriptApi;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.Extensions.VectorData;
using OpenAI.VectorStores;

// Setup the connection to OpenAI
var ollamaEndpoint = "http://localhost:11434/";
IChatClient client = new OllamaApiClient(ollamaEndpoint, "llama3.2");
// IChatClient client = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
//     .AsChatClient("gpt-4o-mini");

IEmbeddingGenerator<string, Embedding<float>> generator =
    new OllamaApiClient(ollamaEndpoint, "all-minilm");
// new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
//     .AsEmbeddingGenerator("text-embedding-3-small");

IEnumerable<YoutubeTranscriptApi.TranscriptItem> transcript = null;

if (File.Exists("transcript.json"))
{
    transcript = JsonSerializer.Deserialize<IEnumerable<YoutubeTranscriptApi.TranscriptItem>>(File.ReadAllText("transcript.json"));
}
else
{

    var youtubeTranscriptApi = new YouTubeTranscriptApi();

    // https://www.youtube.com/watch?v=7Rw_ciSh2Wk
    transcript = youtubeTranscriptApi.GetTranscript("7Rw_ciSh2Wk");

    // Save JSON version of transcript to the disk
    File.WriteAllText("transcript.json", JsonSerializer.Serialize(transcript));
}

#pragma warning disable SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
IVectorStoreRecordCollection<int, TranscriptChunk> transcriptItems;

// if (File.Exists("data/transcript-chunks.json"))
// {
//     transcriptItems = JsonSerializer.Deserialize<IVectorStoreRecordCollection<int, TranscriptChunk>>(
//         File.ReadAllText("data/transcript-chunks.json"));
// }
// else
// {
    var vectorStore = new InMemoryVectorStore();
    transcriptItems = vectorStore.GetCollection<int, TranscriptChunk>("transcriptItems");
    await transcriptItems.CreateCollectionIfNotExistsAsync();

    TranscriptIngestor ingestor = new TranscriptIngestor(generator, transcriptItems);
    await ingestor.RunAsync(transcript, "./data");

    // var outputOptions = new JsonSerializerOptions { WriteIndented = true };
    // await vectorStore.SerializeCollectionAsJsonAsync<int, TranscriptChunk>("transcriptItems",
    //     File.Create("data/transcript-chunks.json"), outputOptions);
    
    // var content = JsonSerializer.Serialize(vectorStore, outputOptions);
    // await File.WriteAllTextAsync(Path.Combine("data", "transcript-chunks.json"), content);
    Console.WriteLine($"Wrote transcript chunks");
// }

do
{
    string prompt = Console.ReadLine();

    if (prompt.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
    {
        break;
    }

    var queryEmbedding = await generator.GenerateEmbeddingVectorAsync(prompt);
    
    var searchOptions = new VectorSearchOptions()
    {
        Top = 3,
        VectorPropertyName = "Embedding"
    };

    var results = await transcriptItems.VectorizedSearchAsync(queryEmbedding, searchOptions);

    await foreach (var result in results.Results)
    {
        Console.WriteLine($"StartTime: {result.Record.StartTime}");
        Console.WriteLine($"Duration: {result.Record.Duration}");
        Console.WriteLine($"Text: {result.Record.Text}");
        Console.WriteLine($"Score: {result.Score}");
        Console.WriteLine();
    }
} while (true);



// // Create embeddings for the full transcript
// // There are about 3 seconds of audio per transcript item, so combine them into 30 second chunks
// for (var i = 0; i < transcript.Count(); i += 10)
// {
//     var chunk = transcript.Skip(i).Take(10);
//     var embeddings = generator.Generate(chunk.Select(x => x.Text));
//     var response = client.SendMessage(embeddings);
//     Console.WriteLine(response);
// }