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

        // Check if a pre-opened stream was registered (e.g. from AzureBlobDataReader)
        if (config.StreamRegistryKey.HasValue)
        {
            stream = AzureBlobDataReader.GetStreamFromRegistry(config.StreamRegistryKey.Value)
                ?? throw new InvalidOperationException($"Stream registry key {config.StreamRegistryKey.Value} not found");
        }
        else
        {
            stream = File.OpenRead(config.FilePath);
            ownsStream = true;
        }

        try
        {
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = config.HasHeader,
                Delimiter = config.Delimiter ?? ",",
                TrimOptions = TrimOptions.Trim
            };

            string[] headers;
            CsvReader csvReader;

            if (config.HasHeader)
            {
                var reader = new StreamReader(stream, leaveOpen: !ownsStream);
                csvReader = new CsvReader(reader, csvConfig);
                await csvReader.ReadAsync();
                csvReader.ReadHeader();
                headers = csvReader.HeaderRecord ?? Array.Empty<string>();
            }
            else
            {
                // For headerless files, read the first line to determine column count
                using var tempReader = new StreamReader(stream, leaveOpen: true);
                var firstLine = await tempReader.ReadLineAsync();
                if (firstLine == null)
                {
                    headers = Array.Empty<string>();
                }
                else
                {
                    // Count columns by splitting on delimiter
                    var delimiter = config.Delimiter ?? ",";
                    var columns = firstLine.Split(delimiter);
                    headers = new string[columns.Length];
                    for (int i = 0; i < columns.Length; i++)
                    {
                        headers[i] = $"Column{i + 1}";
                    }
                }

                // Reset stream for actual reading
                stream.Position = 0;
                var reader = new StreamReader(stream, leaveOpen: !ownsStream);
                csvReader = new CsvReader(reader, csvConfig);
            }

            using (csvReader)
            {
                var batch = new ReadBatch { BatchId = Guid.NewGuid() };
                var rowsRead = 0;

                while (await csvReader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var value = csvReader.GetField(i);
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

            string[] headers;
            if (config.HasHeader)
            {
                using (var csv = new CsvReader(reader, csvConfig))
                {
                    await csv.ReadAsync();
                    csv.ReadHeader();
                    headers = csv.HeaderRecord ?? Array.Empty<string>();
                }
            }
            else
            {
                // For headerless files, read first line to determine column count
                var firstLine = await reader.ReadLineAsync();
                if (firstLine == null)
                {
                    headers = Array.Empty<string>();
                }
                else
                {
                    // Count columns by splitting on delimiter
                    var delimiter = config.Delimiter ?? ",";
                    var columns = firstLine.Split(delimiter);
                    headers = new string[columns.Length];
                    for (int i = 0; i < columns.Length; i++)
                    {
                        headers[i] = $"Column{i + 1}";
                    }
                }
            }

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
        /// <summary>Key into <see cref="AzureBlobDataReader._streamRegistry"/> for cloud-streamed blobs.</summary>
        public Guid? StreamRegistryKey { get; set; }
    }
}
