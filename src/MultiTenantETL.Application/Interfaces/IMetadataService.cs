using MultiTenantETL.Application.Metadata;

namespace MultiTenantETL.Application.Interfaces;

/// <summary>
/// Service for retrieving application metadata, configuration, and lookup values.
/// </summary>
public interface IMetadataService
{
    MetadataDto GetAllMetadata();
    ConnectorConfigDto GetConnectorConfig();
    List<TransformationTypeDto> GetTransformationTypes();
    List<DataTypeDto> GetDataTypes();
    List<ScheduleFrequencyDto> GetScheduleFrequencies();
    List<DayOfWeekDto> GetDaysOfWeek();
    AppConstantsDto GetAppConstants();
}
