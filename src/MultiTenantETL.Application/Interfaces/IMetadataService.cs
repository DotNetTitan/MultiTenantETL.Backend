using MultiTenantETL.Application.Metadata;

namespace MultiTenantETL.Application.Interfaces;

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
