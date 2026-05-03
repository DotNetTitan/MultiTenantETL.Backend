using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.Orchestration;
using MultiTenantETL.Infrastructure.Persistence;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class PipelineOrchestratorTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IDataReaderFactory _readerFactory;
    private readonly IDataWriterFactory _writerFactory;
    private readonly IFieldMappingService _fieldMappingService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PipelineOrchestrator> _logger;
    private readonly ITenantProvider _tenantProvider;
    private readonly PipelineOrchestrator _sut;

    public PipelineOrchestratorTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = Substitute.For<ITenantProvider>();
        _context = new ApplicationDbContext(options, _tenantProvider);
        _readerFactory = Substitute.For<IDataReaderFactory>();
        _writerFactory = Substitute.For<IDataWriterFactory>();
        _fieldMappingService = Substitute.For<IFieldMappingService>();
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<PipelineOrchestrator>>();

        _sut = new PipelineOrchestrator(
            _context,
            _readerFactory,
            _writerFactory,
            _fieldMappingService,
            _emailService,
            Options.Create(new AzureCommunicationSettings { FrontendUrl = "https://frontend.example" }),
            _logger);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ExecutePipelineAsync_BatchException_FailsExecutionAndPersistsCounters()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantProvider.TenantId.Returns(tenantId);

        var sourceConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Source",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        var destinationConnector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Destination",
            Type = "Database",
            Provider = "PostgreSQL",
            Direction = "Destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Pipeline",
            SourceConnectorId = sourceConnector.Id,
            DestinationConnectorId = destinationConnector.Id,
            Status = "Idle",
            FieldMappingsJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            SourceConnector = sourceConnector,
            DestinationConnector = destinationConnector
        };

        var execution = new PipelineExecution
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            TenantId = tenantId,
            Status = ExecutionStatus.Queued,
            StartTime = DateTimeOffset.UtcNow,
            TriggeredBy = "Manual",
            TriggeredByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            Pipeline = pipeline
        };

        _context.Connectors.AddRange(sourceConnector, destinationConnector);
        _context.Pipelines.Add(pipeline);
        _context.PipelineExecutions.Add(execution);
        await _context.SaveChangesAsync();

        var reader = Substitute.For<IDataReader>();
        var writer = Substitute.For<IDataWriter>();
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            RowCount = 2,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["id"] = 1 },
                new() { ["id"] = 2 }
            }
        };

        _readerFactory.CreateReader(Arg.Any<Connector>()).Returns(reader);
        _writerFactory.CreateWriter(Arg.Any<Connector>()).Returns(writer);
        _fieldMappingService.ApplyFieldMappings(Arg.Any<ReadBatch>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(call => call.Arg<ReadBatch>());
        reader.ReadAsync(Arg.Any<Connector>(), Arg.Any<ReadOptions>(), Arg.Any<CancellationToken>())
            .Returns(GetBatches(batch));
        writer.WriteBatchAsync(Arg.Any<Connector>(), Arg.Any<ReadBatch>(), Arg.Any<WriteOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DataWriteResult>(new InvalidOperationException("write failed")));

        // Act
        await _sut.ExecutePipelineAsync(execution.Id);

        // Assert
        var updatedExecution = await _context.PipelineExecutions.FirstAsync(e => e.Id == execution.Id);
        updatedExecution.Status.Should().Be(ExecutionStatus.Failed);
        updatedExecution.RecordsProcessed.Should().Be(2);
        updatedExecution.RecordsSucceeded.Should().Be(0);
        updatedExecution.RecordsFailed.Should().Be(2);
        updatedExecution.BatchCount.Should().Be(1);
        updatedExecution.ErrorMessage.Should().Contain("write failed");

        var updatedPipeline = await _context.Pipelines.FirstAsync(p => p.Id == pipeline.Id);
        updatedPipeline.LastRunStatus.Should().Be("Failed");
        updatedPipeline.LastRunRecordsProcessed.Should().Be(2);
    }

    private static async IAsyncEnumerable<ReadBatch> GetBatches(params ReadBatch[] batches)
    {
        foreach (var batch in batches)
        {
            yield return batch;
            await Task.Yield();
        }
    }
}
