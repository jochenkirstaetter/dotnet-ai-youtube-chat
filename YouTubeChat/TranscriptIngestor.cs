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

    // Add the event
    public event EventHandler<TranscriptChunk>? ChunkUpserted;

    public async Task RunAsync(IEnumerable<YoutubeTranscriptApi.TranscriptItem> transcript)
    {
        var lengthOfChunksInSeconds = 120;
        var lengthPerTranscriptItem = 3;
        var overlapOfChunksInSeconds = 3;

        // Load data
        var chunkIndex = 0;

        // Create embeddings for the full transcript
        // There are about 3 seconds of audio per transcript item, so combine them into larger chunks (based on length defined earlier)
        var builder = new StringBuilder();
        float startTimeForChunk = 0;
        float durationForChunk = 0;

        for (var i = 0; i < transcript.Count(); i++)
        {
            var element = transcript.ElementAt(i);

            // [1] Build up text to ingest
            builder.Append(element.Text);
            builder.Append(" "); // Account for transcripts not adding spacing between items

            durationForChunk += element.Duration;

            // Track the start time for the first item in this chunk
            if (durationForChunk >= lengthOfChunksInSeconds)
            {
                var text = builder.ToString();

                // [2] Embed (string -> embedding)
                var embedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(text);

                // Create the chunk object
                var chunk = new TranscriptChunk
                {
                    Id = ++chunkIndex,
                    StartTime = startTimeForChunk,
                    Duration = durationForChunk,
                    Text = text,
                    Embedding = embedding
                };

                // [3] Save the chunk
                await _transcriptItems.UpsertAsync(chunk);

                // Notify any listeners that a chunk has been upserted
                ChunkUpserted?.Invoke(this, chunk);

                builder.Clear();

                // Go back by 3 transcript items to make sure there's an overlap of text in each chunk
                i -= overlapOfChunksInSeconds;
                durationForChunk = 0;
                element = transcript.ElementAt(i);
                startTimeForChunk = element.Start;
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