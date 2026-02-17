using MultiTenantETL.Application.Connectors.DataReaders;

namespace MultiTenantETL.Application.Orchestration;

/// <summary>
/// Applies field mappings and transformations to data batches during pipeline execution.
/// </summary>
public interface IFieldMappingService
{
    /// <summary>
    /// Applies field mappings and transformations to a batch.
    /// </summary>
    /// <param name="batch">The source data batch.</param>
    /// <param name="fieldMappingsJson">JSON array of field mapping definitions.</param>
    /// <param name="stripUnmappedFields">
    /// When true, removes any fields from each row that are not present in the
    /// mapping destination fields. Useful for destinations like Email where the
    /// writer exports all dictionary keys as column headers.
    /// </param>
    ReadBatch ApplyFieldMappings(ReadBatch batch, string fieldMappingsJson, bool stripUnmappedFields = false);
}
