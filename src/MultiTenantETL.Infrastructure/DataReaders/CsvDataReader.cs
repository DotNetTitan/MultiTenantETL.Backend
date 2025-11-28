using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.DataReaders;

public class CsvDataReader : IDataReader
{
    private readonly EtlSettings _settings;

    public CsvDataReader(IOptions<EtlSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ConnectorType => "CSV";

    public async Task<DataReadResult> ReadAsync(Connector connector, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var result = new DataReadResult();

        using var reader = new StreamReader(config.FilePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = config.Delimiter,
            HasHeaderRecord = config.HasHeader
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        result.Schema = BuildSchema(headers);

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            foreach (var header in headers)
            {
                row[header] = csv.GetField(header);
            }
            result.Rows.Add(row);
        }

        result.TotalRows = result.Rows.Count;
        return result;
    }

    public async IAsyncEnumerable<List<Dictionary<string, object?>>> ReadBatchesAsync(
        Connector connector,
        int batchSize = 0,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(connector.ConfigJson);
        var effectiveBatchSize = batchSize > 0 ? batchSize : _settings.FileBatchSize;

        using var reader = new StreamReader(config.FilePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = config.Delimiter,
            HasHeaderRecord = config.HasHeader
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var batch = new List<Dictionary<string, object?>>(effectiveBatchSize);

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            foreach (var header in headers)
            {
                row[header] = csv.GetField(header);
            }
            batch.Add(row);

            if (batch.Count >= effectiveBatchSize)
            {
                yield return batch;
                batch = new List<Dictionary<string, object?>>(effectiveBatchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    public Task<ConnectionTestResult> TestConnectionAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            if (!File.Exists(config.FilePath))
            {
                return Task.FromResult(new ConnectionTestResult
                {
                    IsSuccessful = false,
                    Message = "File not found",
                    ErrorDetails = $"The file '{config.FilePath}' does not exist"
                });
            }

            return Task.FromResult(new ConnectionTestResult
            {
                IsSuccessful = true,
                Message = "File found and accessible"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ConnectionTestResult
            {
                IsSuccessful = false,
                Message = "File access failed",
                ErrorDetails = ex.Message
            });
        }
    }

    public async Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector)
    {
        try
        {
            var config = ParseConfig(connector.ConfigJson);
            
            using var reader = new StreamReader(config.FilePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = config.Delimiter,
                HasHeaderRecord = config.HasHeader
            });

            await csv.ReadAsync();
            csv.ReadHeader();
            
            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            
            return new SchemaDetectionResult
            {
                IsSuccessful = true,
                Schema = BuildSchema(headers)
            };
        }
        catch (Exception ex)
        {
            return new SchemaDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private CsvConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<CsvConfig>(configJson) 
            ?? throw new InvalidOperationException("Invalid CSV configuration");
    }

    private SchemaInfo BuildSchema(string[] headers)
    {
        var schema = new SchemaInfo();
        
        foreach (var header in headers)
        {
            schema.Fields.Add(new FieldDefinition
            {
                Name = header,
                DataType = "string",
                IsNullable = true
            });
        }

        return schema;
    }

    private class CsvConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public string Delimiter { get; set; } = ",";
        public bool HasHeader { get; set; } = true;
    }
}
