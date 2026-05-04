using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Defines operations for reading data from various connector types.
/// </summary>
public interface IDataReader
{
    /// <summary>
    /// Streams data from the connector in batches
    /// </summary>
    IAsyncEnumerable<ReadBatch> ReadAsync(Connector connector, ReadOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Tests if the connector can establish a connection
    /// </summary>
    Task<bool> TestConnectionAsync(Connector connector, CancellationToken cancellationToken);

    /// <summary>
    /// Detects the schema from the connector
    /// </summary>
    Task<SchemaDetectionResult> DetectSchemaAsync(Connector connector, CancellationToken cancellationToken);
}
