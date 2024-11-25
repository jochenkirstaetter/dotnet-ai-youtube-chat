#pragma warning disable
using System.Numerics.Tensors;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;
using YoutubeTranscriptApi;

public class TranscriptIngestor
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStoreRecordCollection<int, TranscriptChunk> _transcriptItems;

    public TranscriptIngestor(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStoreRecordCollection<int, TranscriptChunk> transcriptItems)
    {
        _embeddingGenerator = embeddingGenerator;
        _transcriptItems = transcriptItems;
    }

    public async Task RunAsync(IEnumerable<YoutubeTranscriptApi.TranscriptItem> transcript, string outputDir)
    {
        Console.WriteLine("Ingesting transcript...");

        // Load data
        // var chunks = new List<TranscriptChunk>();
        var chunkIndex = 0;

        // Create embeddings for the full transcript
        // There are about 3 seconds of audio per transcript item, so combine them into 30 second chunks
        var builder = new StringBuilder();
        var endOfCurrentChunk = 10;
        float startTimeForChunk = 0;
        float durationForChunk = 0;

        for (var i = 0; i < transcript.Count(); i++)
        {
            var element = transcript.ElementAt(i);

            // Track the start time for the first item in this 30 second clip
            if (i == (endOfCurrentChunk - 10))
            {
                startTimeForChunk = element.Start;
            }
            durationForChunk += element.Duration;

            // [1] Build up text to ingest
            builder.Append(element.Text);
            builder.Append(" "); // Account for transcripts not adding spacing between items

            // When we have about 30 seconds of audio, ingest it
            if (i == endOfCurrentChunk)
            {
                Console.WriteLine($"Writing chunk {chunkIndex}");
                var text = builder.ToString();

                // [2] Embed (string -> embedding)
                var embedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(text);

                // [3] Save
                _transcriptItems.UpsertAsync(new TranscriptChunk
                {
                    Id = ++chunkIndex,
                    StartTime = startTimeForChunk,
                    Duration = durationForChunk,
                    Text = text,
                    Embedding = embedding
                });

                builder.Clear();

                // Ensure the chunks overlap by ~3 seconds
                i -= 3;
                endOfCurrentChunk = i + 10;
                durationForChunk = 0;
            }
        }
    }
}

public class TranscriptChunk
{
    [VectorStoreRecordKey]
    public int Id { get; set; }
    [VectorStoreRecordData]
    public float StartTime { get; set; }
    [VectorStoreRecordData]
    public float Duration { get; set; }
    [VectorStoreRecordData]
    public required string Text { get; set; }
    [VectorStoreRecordVector]
    public required ReadOnlyMemory<float> Embedding { get; set; }
}