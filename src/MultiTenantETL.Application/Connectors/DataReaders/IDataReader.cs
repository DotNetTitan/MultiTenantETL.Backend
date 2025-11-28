namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Interface for reading data from various connector types
/// </summary>
public interface IDataReader
{
    /// <summary>
    /// Gets the connector type this reader supports
    /// </summary>
    string ConnectorType { get; }

    /// <summary>
    /// Reads data from the connector (loads all data into memory - use for small datasets)
    /// </summary>
    Task<DataReadResult> ReadAsync(Domain.Entities.Connector connector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams data from the connector in batches (recommended for large datasets)
    /// </summary>
    IAsyncEnumerable<List<Dictionary<string, object?>>> ReadBatchesAsync(
        Domain.Entities.Connector connector, 
        int batchSize = 5000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests if the connection to the data source is valid
    /// </summary>
    Task<ConnectionTestResult> TestConnectionAsync(Domain.Entities.Connector connector);

    /// <summary>
    /// Detects the schema of the data source
    /// </summary>
    Task<SchemaDetectionResult> DetectSchemaAsync(Domain.Entities.Connector connector);
}
