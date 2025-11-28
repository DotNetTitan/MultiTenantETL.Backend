using MultiTenantETL.Application.Connectors.DataWriters;

namespace MultiTenantETL.Infrastructure.Services;

public class DataWriterFactory
{
    private readonly IEnumerable<IDataWriter> _writers;

    public DataWriterFactory(IEnumerable<IDataWriter> writers)
    {
        _writers = writers;
    }

    public IDataWriter GetWriter(string connectorType)
    {
        var writer = _writers.FirstOrDefault(w => 
            w.ConnectorType.Equals(connectorType, StringComparison.OrdinalIgnoreCase));
        
        if (writer == null)
        {
            throw new NotSupportedException($"No data writer found for connector type: {connectorType}");
        }

        return writer;
    }
}
