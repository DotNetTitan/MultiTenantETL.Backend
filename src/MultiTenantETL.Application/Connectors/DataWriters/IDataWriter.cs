using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;

namespace MultiTenantETL.Application.Connectors.DataWriters;

/// <summary>
/// Defines operations for writing data to various connector types.
/// </summary>
public interface IDataWriter : IAsyncDisposable
{
    /// <summary>
    /// Writes a batch of data to the connector
    /// </summary>
    Task<DataWriteResult> WriteBatchAsync(
        Connector connector, 
        ReadBatch batch, 
        WriteOptions options,
        CancellationToken cancellationToken);
}
