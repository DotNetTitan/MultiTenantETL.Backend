using MultiTenantETL.Application.Connectors.DataReaders;

namespace MultiTenantETL.Application.Orchestration;

public interface IFieldMappingService
{
    ReadBatch ApplyFieldMappings(ReadBatch batch, string fieldMappingsJson);
}
