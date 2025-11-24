namespace MultiTenantETL.Application.Metadata;

public class MetadataDto
{
    public ConnectorConfigDto ConnectorConfig { get; set; } = new();
    public List<TransformationTypeDto> TransformationTypes { get; set; } = new();
    public List<DataTypeDto> DataTypes { get; set; } = new();
    public List<ScheduleFrequencyDto> ScheduleFrequencies { get; set; } = new();
    public List<DayOfWeekDto> DaysOfWeek { get; set; } = new();
}

public class ConnectorConfigDto
{
    public List<ConnectorTypeDto> Types { get; set; } = new();
    public Dictionary<string, List<string>> Providers { get; set; } = new();
    public List<DirectionDto> Directions { get; set; } = new();
    public List<AuthTypeDto> AuthTypes { get; set; } = new();
    public List<FileFormatDto> FileFormats { get; set; } = new();
    public List<WriteOperationDto> WriteOperations { get; set; } = new();
    public List<HttpMethodDto> HttpMethods { get; set; } = new();
}

public class ConnectorTypeDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class DirectionDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class AuthTypeDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
}

public class FileFormatDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
}

public class WriteOperationDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public bool RequiresPrimaryKey { get; set; }
}

public class HttpMethodDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

public class TransformationTypeDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
}

public class DataTypeDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class ScheduleFrequencyDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class DayOfWeekDto
{
    public string Value { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string ShortKey { get; set; } = string.Empty;
}
