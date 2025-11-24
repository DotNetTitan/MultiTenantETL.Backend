using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Metadata;
using MultiTenantETL.Domain.Constants;

namespace MultiTenantETL.Infrastructure.Services;

public class MetadataService : IMetadataService
{
    public MetadataDto GetAllMetadata()
    {
        return new MetadataDto
        {
            ConnectorConfig = GetConnectorConfig(),
            TransformationTypes = GetTransformationTypes(),
            DataTypes = GetDataTypes(),
            ScheduleFrequencies = GetScheduleFrequencies(),
            DaysOfWeek = GetDaysOfWeek()
        };
    }

    public ConnectorConfigDto GetConnectorConfig()
    {
        return new ConnectorConfigDto
        {
            Types = MetadataConstants.ConnectorTypes.Types
                .Select(t => new ConnectorTypeDto { Value = t.Value, LabelKey = t.LabelKey, Icon = t.Icon })
                .ToList(),
            
            Providers = MetadataConstants.ConnectorProviders.ProvidersByType
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList()),
            
            Directions = MetadataConstants.ConnectorDirections.Directions
                .Select(d => new DirectionDto { Value = d.Value, LabelKey = d.LabelKey, Icon = d.Icon })
                .ToList(),
            
            AuthTypes = MetadataConstants.AuthTypes.Types
                .Select(a => new AuthTypeDto { Value = a.Value, LabelKey = a.LabelKey })
                .ToList(),
            
            FileFormats = MetadataConstants.FileFormats.Formats
                .Select(f => new FileFormatDto { Value = f.Value, LabelKey = f.LabelKey, Extension = f.Extension })
                .ToList(),
            
            WriteOperations = MetadataConstants.WriteOperations.Operations
                .Select(w => new WriteOperationDto 
                { 
                    Value = w.Value, 
                    LabelKey = w.LabelKey, 
                    DescriptionKey = w.DescriptionKey, 
                    RequiresPrimaryKey = w.RequiresPrimaryKey 
                })
                .ToList(),
            
            HttpMethods = MetadataConstants.HttpMethods.Methods
                .Select(h => new HttpMethodDto { Value = h.Value, LabelKey = h.LabelKey, Color = h.Color })
                .ToList()
        };
    }

    public List<TransformationTypeDto> GetTransformationTypes()
    {
        return MetadataConstants.TransformationTypes.Types
            .Select(t => new TransformationTypeDto
            {
                Value = t.Value,
                LabelKey = t.LabelKey,
                Icon = t.Icon,
                CategoryKey = t.CategoryKey,
                DescriptionKey = t.DescriptionKey
            })
            .ToList();
    }

    public List<DataTypeDto> GetDataTypes()
    {
        return MetadataConstants.DataTypes.Types
            .Select(d => new DataTypeDto { Value = d.Value, LabelKey = d.LabelKey, Icon = d.Icon })
            .ToList();
    }

    public List<ScheduleFrequencyDto> GetScheduleFrequencies()
    {
        return MetadataConstants.ScheduleFrequencies.Frequencies
            .Select(f => new ScheduleFrequencyDto { Value = f.Value, LabelKey = f.LabelKey, Icon = f.Icon })
            .ToList();
    }

    public List<DayOfWeekDto> GetDaysOfWeek()
    {
        return MetadataConstants.DaysOfWeek.Days
            .Select(d => new DayOfWeekDto { Value = d.Value, LabelKey = d.LabelKey, ShortKey = d.ShortKey })
            .ToList();
    }
}
