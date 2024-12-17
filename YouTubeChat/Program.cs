using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Mscc.GenerativeAI.Microsoft;
using OllamaSharp;
using OpenAI;
using YoutubeTranscriptApi;

AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", false);

var usingOpenAI = true;
var usingGemini = true;
IChatClient chatClient = null;
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = null;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (apiKey is null) usingOpenAI = false;

apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
if (apiKey is null) usingGemini = false;

// Setup the connection to OpenAI
if (usingOpenAI)
{
    OpenAIClient client = new OpenAIClient(apiKey);
    chatClient = client.AsChatClient("gpt-4o-mini");
    embeddingGenerator = client.AsEmbeddingGenerator("text-embedding-3-small");
}
// Setup the connection to Gemini
else if (usingGemini)
{
    chatClient = new GeminiChatClient(apiKey, 
        Environment.GetEnvironmentVariable("GOOGLE_AI_MODEL") ?? "gemini-1.5-flash-latest");
    embeddingGenerator = new GeminiEmbeddingGenerator(apiKey, "text-embedding-004");
}
else
{
    var ollamaEndpoint = "http://localhost:11434/";
    chatClient = new OllamaApiClient(ollamaEndpoint, "llama3.2");
    embeddingGenerator = new OllamaApiClient(ollamaEndpoint, "all-minilm");
}

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
var vectorStore = new InMemoryVectorStore();
IVectorStoreRecordCollection<int, TranscriptChunk> transcriptItems = 
    vectorStore.GetCollection<int, TranscriptChunk>("transcriptItems");
await transcriptItems.CreateCollectionIfNotExistsAsync();

// Load the transcript chunks from disk, if they exist
if (File.Exists(Path.Combine("data", "transcript-chunks.json")))
{
    List <TranscriptChunk> chunks = JsonSerializer.Deserialize<List<TranscriptChunk>>(
         File.ReadAllText("data/transcript-chunks.json"));
    foreach (var chunk in chunks)
    {
        await transcriptItems.UpsertAsync(chunk);
    }
}
else
{
    TranscriptIngestor ingestor = new TranscriptIngestor(embeddingGenerator, transcriptItems);

    // Maintain a copy of the list of chunks, for serialization
    List<TranscriptChunk> chunks = new List<TranscriptChunk>();
    ingestor.ChunkUpserted += (object? sender, TranscriptChunk chunk) =>
    {
        Console.WriteLine($"Chunk upserted: {chunk}");
        chunks.Add(chunk);
    };

    Console.WriteLine("Ingesting transcript...");
    await ingestor.RunAsync(transcript);

    // When finished, serialize the chunks to disk
    var outputOptions = new JsonSerializerOptions { WriteIndented = true };
    
    var content = JsonSerializer.Serialize(chunks, outputOptions);
    await File.WriteAllTextAsync(Path.Combine("data", "transcript-chunks.json"), content);
}

do
{
    Console.WriteLine("Ask a question, or type 'exit' to quit.");
    string prompt = Console.ReadLine();

    if (prompt.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
    {
        break;
    }

    var queryEmbedding = await embeddingGenerator.GenerateEmbeddingVectorAsync(prompt);

    var searchOptions = new VectorSearchOptions()
    {
        Top = 3,
        VectorPropertyName = "Embedding"
    };

    var results = await transcriptItems.VectorizedSearchAsync(queryEmbedding, searchOptions);
    StringBuilder builder = new StringBuilder();

    await foreach (var result in results.Results)
    {
        // Console.WriteLine($"StartTime: {result.Record.StartTime}");
        // Console.WriteLine($"Duration: {result.Record.Duration}");
        // Console.WriteLine($"Text: {result.Record.Text}");
        // Console.WriteLine($"Score: {result.Score}");
        // Console.WriteLine();
        builder.AppendLine(result.Record.Text);
    }

    var systemPrompt = $@"You're an expert at developing software using .NET and Microsoft.Extensions.AI.
                        When you answer questions from developers, you should provide detailed explanations and examples.
                        You should also provide links to documentation and other resources that can help developers learn more.
                        Use the context below to help answer questions, limit responses to use only the provided context.
                        Respond in 4 paragraphs or less:
                        
                        <context>
                        {builder.ToString()}
                        </context>
                        
                        Question: {prompt}";
    
    Console.ForegroundColor = ConsoleColor.Green; // Set console text color to green
    IAsyncEnumerable<StreamingChatCompletionUpdate> responseChunk = chatClient.CompleteStreamingAsync(systemPrompt);
    await foreach (var update in responseChunk)
    {
        Console.Write(update.Text);
    }
    Console.WriteLine();
    Console.ResetColor(); // Reset console text color to default
} while (true);