using MultiTenantETL.Application.Connectors.DataReaders;

namespace MultiTenantETL.Infrastructure.Services;

public class DataReaderFactory
{
    private readonly IEnumerable<IDataReader> _readers;

    public DataReaderFactory(IEnumerable<IDataReader> readers)
    {
        _readers = readers;
    }

    public IDataReader GetReader(string connectorType)
    {
        var reader = _readers.FirstOrDefault(r => 
            r.ConnectorType.Equals(connectorType, StringComparison.OrdinalIgnoreCase));
        
        if (reader == null)
        {
            throw new NotSupportedException($"No data reader found for connector type: {connectorType}");
        }

        return reader;
    }
}
