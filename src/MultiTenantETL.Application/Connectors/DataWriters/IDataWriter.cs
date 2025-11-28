namespace MultiTenantETL.Application.Connectors.DataWriters;

/// <summary>
/// Interface for writing data to various connector types
/// </summary>
public interface IDataWriter
{
    /// <summary>
    /// Gets the connector type this writer supports
    /// </summary>
    string ConnectorType { get; }

    /// <summary>
    /// Writes data to the connector (all at once - use for small datasets)
    /// </summary>
    Task<DataWriteResult> WriteAsync(
        Domain.Entities.Connector connector,
        List<Dictionary<string, object?>> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data in batches (recommended for large datasets)
    /// </summary>
    Task<DataWriteResult> WriteBatchesAsync(
        Domain.Entities.Connector connector,
        IAsyncEnumerable<List<Dictionary<string, object?>>> batches,
        CancellationToken cancellationToken = default);
}
