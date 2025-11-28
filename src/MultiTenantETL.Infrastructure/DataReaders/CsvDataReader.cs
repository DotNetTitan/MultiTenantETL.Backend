using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using IDataReader = MultiTenantETL.Application.Connectors.DataReaders.IDataReader;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class CsvDataReader : IDataReader
{
    private readonly ILogger<CsvDataReader> _logger;

    public CsvDataReader(ILogger<CsvDataReader> logger)
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

        // Check if stream is provided in config (for S3/cloud storage)
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
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = config.HasHeader,
                Delimiter = config.Delimiter ?? ",",
                TrimOptions = TrimOptions.Trim
            };

            using var csv = new CsvReader(reader, csvConfig);

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        var batch = new ReadBatch { BatchId = Guid.NewGuid() };
        var rowsRead = 0;

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, object?>();

            for (int i = 0; i < headers.Length; i++)
            {
                var value = csv.GetField(i);
                row[headers[i]] = string.IsNullOrWhiteSpace(value) ? null : value;
            }

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
            _logger.LogError(ex, "CSV file test failed");
            return Task.FromResult(false);
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);

            using var reader = new StreamReader(config.FilePath);
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = config.HasHeader,
                Delimiter = config.Delimiter ?? ","
            };

            using var csv = new CsvReader(reader, csvConfig);

            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            var fields = headers.Select(h => new FieldDefinition
            {
                Name = h,
                DataType = "string",
                IsNullable = true,
                IsPrimaryKey = false
            }).ToList();

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
            _logger.LogError(ex, "Schema detection failed for CSV");
            return new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private CsvConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<CsvConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid CSV configuration");
    }

    private class CsvConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public bool HasHeader { get; set; } = true;
        public string? Delimiter { get; set; }
        public Stream? Stream { get; set; }
    }
}
