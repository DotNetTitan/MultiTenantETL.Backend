using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Transformations.Processors;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class TransformationProcessorTests
{
    [Fact]
    public async Task ScriptProcessor_WithCamelCaseConfig_ParsesScriptCorrectly()
    {
        // Arrange - Test that camelCase JSON properties (script, scriptLanguage, etc.) are correctly deserialized to PascalCase C# properties
        var logger = Substitute.For<ILogger<ScriptProcessor>>();
        var settings = Options.Create(new EtlSettings { EnableDetailedLogging = false });
        var processor = new ScriptProcessor(logger, settings);

        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "stock_quantity", 100 } }
            }
        };

        // Config with camelCase property names as would come from the frontend
        var transformation = new Transformation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Name = "Test Script",
            Type = "Script",
            ConfigJson = @"{
                ""script"": ""row"",
                ""scriptLanguage"": ""javascript"",
                ""maxRecursionDepth"": 100,
                ""timeoutMs"": 5000,
                ""maxStatements"": 10000
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // Act
        var result = await processor.ProcessBatchAsync(batch, transformation, CancellationToken.None);

        // Assert - The script should execute and return the row
        result.TransformedRows.Should().HaveCount(1);
        result.RowsFiltered.Should().Be(0);
        result.RowsWithErrors.Should().Be(0);
    }

    [Fact]
    public async Task ScriptProcessor_WithTransformFunction_ExecutesTransformation()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ScriptProcessor>>();
        var settings = Options.Create(new EtlSettings { EnableDetailedLogging = false });
        var processor = new ScriptProcessor(logger, settings);

        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "stock_quantity", 100 } }
            }
        };

        // Script that returns a modified object
        var transformation = new Transformation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Name = "Test Transform",
            Type = "Script",
            ConfigJson = @"{
                ""script"": ""({ stock_quantity: row.stock_quantity, stock_status: row.stock_quantity > 0 ? 'In Stock' : 'Out of Stock' })"",
                ""scriptLanguage"": ""javascript""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // Act
        var result = await processor.ProcessBatchAsync(batch, transformation, CancellationToken.None);

        // Assert
        result.TransformedRows.Should().HaveCount(1);
        result.TransformedRows[0].Should().ContainKey("stock_status");
        result.TransformedRows[0]["stock_status"].Should().Be("In Stock");
    }

    [Fact]
    public async Task StringProcessor_WithCamelCaseConfig_ParsesConfigCorrectly()
    {
        // Arrange
        var logger = Substitute.For<ILogger<StringProcessor>>();
        var processor = new StringProcessor(logger);

        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 1,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "name", "  hello world  " } }
            }
        };

        // Config with camelCase property names - StringProcessor uses 'fields' array
        var transformation = new Transformation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Name = "Test Trim",
            Type = "String",
            ConfigJson = @"{
                ""fields"": [""name""],
                ""operation"": ""Trim""
            }",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        // Act
        var result = await processor.ProcessBatchAsync(batch, transformation, CancellationToken.None);

        // Assert
        result.TransformedRows.Should().HaveCount(1);
        result.TransformedRows[0]["name"].Should().Be("hello world");
    }
}
