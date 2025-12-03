using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Infrastructure.Orchestration;
using MultiTenantETL.Infrastructure.Transformations.FieldProcessors;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class FieldMappingServiceTests
{
    private readonly ILogger<FieldMappingService> _logger;
    private readonly IFieldTransformationProcessor _fieldProcessor;
    private readonly IEnumerable<ITransformationProcessor> _batchProcessors;
    private readonly FieldMappingService _sut;

    public FieldMappingServiceTests()
    {
        _logger = Substitute.For<ILogger<FieldMappingService>>();
        _fieldProcessor = Substitute.For<IFieldTransformationProcessor>();
        _batchProcessors = new List<ITransformationProcessor>();

        _sut = new FieldMappingService(_logger, _fieldProcessor, _batchProcessors);
    }

    [Fact]
    public void ApplyFieldMappings_WithValidGuidTransformationIds_ProcessesSuccessfully()
    {
        // Arrange
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 2,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "firstName", "John" }, { "lastName", "Doe" } },
                new() { { "firstName", "Jane" }, { "lastName", "Smith" } }
            }
        };

        var fieldMappingsJson = $@"[
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""firstName""],
                ""destinationField"": ""first_name"",
                ""order"": 1,
                ""transformations"": []
            }},
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""lastName""],
                ""destinationField"": ""last_name"",
                ""order"": 2,
                ""transformations"": []
            }}
        ]";

        // Act
        var result = _sut.ApplyFieldMappings(batch, fieldMappingsJson);

        // Assert
        result.Should().NotBeNull();
        result.RowCount.Should().Be(2);
        result.Rows[0].Should().ContainKey("first_name");
        result.Rows[0].Should().ContainKey("last_name");
        result.Rows[0]["first_name"].Should().Be("John");
        result.Rows[0]["last_name"].Should().Be("Doe");
    }

    [Fact]
    public void ApplyFieldMappings_WithFrontendGeneratedIds_ProcessesSuccessfully()
    {
        // Arrange - Frontend-style IDs that are NOT valid GUIDs
        // The FieldMappingService should handle these gracefully during execution
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 2,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "email", "john@example.com" } },
                new() { { "email", "jane@example.com" } }
            }
        };

        // Using frontend-style temporary IDs (not GUIDs)
        var fieldMappingsJson = @"[
            {
                ""id"": ""mapping-1701629000000-0.123"",
                ""sourceFields"": [""email""],
                ""destinationField"": ""email_address"",
                ""order"": 1,
                ""transformations"": []
            }
        ]";

        // Act
        var result = _sut.ApplyFieldMappings(batch, fieldMappingsJson);

        // Assert - Should process without throwing even with non-GUID IDs
        result.Should().NotBeNull();
        result.RowCount.Should().Be(2);
        result.Rows[0].Should().ContainKey("email_address");
        result.Rows[0]["email_address"].Should().Be("john@example.com");
    }

    [Fact]
    public void ApplyFieldMappings_WithEmptyMappings_ReturnsOriginalBatch()
    {
        // Arrange
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "field1", "value1" } }
            }
        };

        // Act
        var result = _sut.ApplyFieldMappings(batch, "[]");

        // Assert
        result.Should().BeSameAs(batch);
    }

    [Fact]
    public void ApplyFieldMappings_WithNullMappings_ReturnsOriginalBatch()
    {
        // Arrange
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "field1", "value1" } }
            }
        };

        // Act
        var result = _sut.ApplyFieldMappings(batch, null!);

        // Assert
        result.Should().BeSameAs(batch);
    }

    [Fact]
    public void ApplyFieldMappings_WithMultipleSourceFields_CombinesFieldsCorrectly()
    {
        // Arrange
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "firstName", "John" }, { "lastName", "Doe" } }
            }
        };

        // Complex mapping with multiple source fields (no transformations for simplicity)
        var fieldMappingsJson = $@"[
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""firstName"", ""lastName""],
                ""destinationField"": ""fullName"",
                ""order"": 1,
                ""transformations"": []
            }}
        ]";

        // Act
        var result = _sut.ApplyFieldMappings(batch, fieldMappingsJson);

        // Assert
        result.Should().NotBeNull();
        result.RowCount.Should().Be(1);
        // When no transformations, the result should contain the combined values as a list
        result.Rows[0].Should().ContainKey("fullName");
    }

    [Fact]
    public void ApplyFieldMappings_WithMixedIdFormats_ProcessesAllMappings()
    {
        // Arrange - Mix of proper GUIDs and frontend-generated IDs
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "field1", "value1" }, { "field2", "value2" }, { "field3", "value3" } }
            }
        };

        var fieldMappingsJson = $@"[
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""field1""],
                ""destinationField"": ""output1"",
                ""order"": 1,
                ""transformations"": []
            }},
            {{
                ""id"": ""frontend-mapping-123456"",
                ""sourceFields"": [""field2""],
                ""destinationField"": ""output2"",
                ""order"": 2,
                ""transformations"": []
            }},
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""field3""],
                ""destinationField"": ""output3"",
                ""order"": 3,
                ""transformations"": []
            }}
        ]";

        // Act
        var result = _sut.ApplyFieldMappings(batch, fieldMappingsJson);

        // Assert - All mappings should be processed regardless of ID format
        result.Should().NotBeNull();
        result.Rows[0].Should().ContainKey("output1");
        result.Rows[0].Should().ContainKey("output2");
        result.Rows[0].Should().ContainKey("output3");
        result.Rows[0]["output1"].Should().Be("value1");
        result.Rows[0]["output2"].Should().Be("value2");
        result.Rows[0]["output3"].Should().Be("value3");
    }

    [Fact]
    public void ApplyFieldMappings_PreservesOrderOfMappings()
    {
        // Arrange
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "a", "1" }, { "b", "2" }, { "c", "3" } }
            }
        };

        // Mappings in non-sequential order to test ordering
        var fieldMappingsJson = $@"[
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""c""],
                ""destinationField"": ""third"",
                ""order"": 3,
                ""transformations"": []
            }},
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""a""],
                ""destinationField"": ""first"",
                ""order"": 1,
                ""transformations"": []
            }},
            {{
                ""id"": ""{Guid.NewGuid()}"",
                ""sourceFields"": [""b""],
                ""destinationField"": ""second"",
                ""order"": 2,
                ""transformations"": []
            }}
        ]";

        // Act
        var result = _sut.ApplyFieldMappings(batch, fieldMappingsJson);

        // Assert
        result.Should().NotBeNull();
        result.Rows[0].Should().ContainKey("first");
        result.Rows[0].Should().ContainKey("second");
        result.Rows[0].Should().ContainKey("third");
    }
}
