using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

/// <summary>
/// Reads JSONL (JSON Lines / newline-delimited JSON) files - one JSON object per line
/// This format is ideal for streaming large datasets without loading entire file into memory
/// </summary>
public class JsonLinesDataReader : IDataReader
{
    private readonly ILogger<JsonLinesDataReader> _logger;

    public JsonLinesDataReader(ILogger<JsonLinesDataReader> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<ReadBatch> ReadAsync(
        Connector connector,
        ReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = ParseConfig(connector.ConfigJson);

        Stream stream;
        bool ownsStream = false;

        if (config.Stream != null)
        {
            stream = config.Stream;
        }
        else
        {
            stream = File.OpenRead(config.FilePath);
            ownsStream = true;
        }

        try
        {
            using var reader = new StreamReader(stream, leaveOpen: !ownsStream);

            var batch = new ReadBatch { BatchId = Guid.NewGuid() };
            var rowsRead = 0;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var row = JsonSerializer.Deserialize<Dictionary<string, object?>>(line);
                if (row == null)
                    continue;

                batch.Rows.Add(row);
                batch.RowCount++;
                rowsRead++;

                if (batch.RowCount >= options.BatchSize)
                {
                    yield return batch;
                    batch = new ReadBatch { BatchId = Guid.NewGuid() };
                }

                if (options.MaxRows.HasValue && rowsRead >= options.MaxRows.Value)
                {
                    break;
                }
            }

            if (batch.RowCount > 0)
            {
                yield return batch;
            }
        }
        finally
        {
            if (ownsStream)
            {
                stream.Dispose();
            }
        }
    }

    public Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            return Task.FromResult(File.Exists(config.FilePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSONL file test failed");
            return Task.FromResult(false);
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);

            using var reader = new StreamReader(config.FilePath);
            var firstLine = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(firstLine))
            {
                return new SchemaDetectionResult
                {
                    Success = false,
                    ErrorMessage = "File is empty",
                    DetectedAt = DateTimeOffset.UtcNow
                };
            }

            var firstRow = JsonSerializer.Deserialize<Dictionary<string, object?>>(firstLine);

            var fields = firstRow?.Keys.Select(k => new FieldDefinition
            {
                Name = k,
                DataType = "string",
                IsNullable = true,
                IsPrimaryKey = false
            }).ToList() ?? new List<FieldDefinition>();

            return new SchemaDetectionResult
            {
                Fields = fields,
                Version = 1,
                DetectedAt = DateTimeOffset.UtcNow,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection failed for JSONL");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private JsonLinesConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<JsonLinesConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid JSONL configuration");
    }

    private class JsonLinesConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public Stream? Stream { get; set; }
    }
}
