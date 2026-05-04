using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.DataWriters;

/// <summary>
/// Unit tests for <see cref="EmailDataWriter"/>.
/// Verifies buffer-and-flush pattern: rows are buffered in WriteBatchAsync,
/// exactly one email is sent in DisposeAsync.
/// </summary>
public class EmailDataWriterTests
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailDataWriter> _logger;
    private readonly EmailDataWriter _sut;

    public EmailDataWriterTests()
    {
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<EmailDataWriter>>();
        _sut = new EmailDataWriter(_emailService, _logger);

        _emailService.SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>())
            .Returns(true);
    }

    [Fact]
    public void Constructor_WithNullEmailService_ShouldThrowArgumentNullException()
    {
        var act = () => new EmailDataWriter(null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("emailService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new EmailDataWriter(_emailService, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldBufferRows_WithoutSendingEmail()
    {
        // Arrange
        var connector = CreateConnector();
        var batch = CreateBatch(3);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(3);
        result.RowsFailed.Should().Be(0);

        await _emailService.DidNotReceive().SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    [Fact]
    public async Task DisposeAsync_WithBufferedCsvData_ShouldSendOneEmailWithCsvAttachment()
    {
        // Arrange
        var connector = CreateConnector(attachmentFormat: "CSV");
        var batch = CreateBatch(2);
        await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _emailService.Received(1).SendDataExportEmailAsync(
            Arg.Is<List<string>>(r => r.Contains("user@example.com")),
            Arg.Any<List<string>?>(),
            Arg.Is("Data Export Test"),
            Arg.Any<string>(),
            Arg.Is<string>(f => f.EndsWith(".csv")),
            Arg.Is("text/csv"),
            Arg.Is<byte[]>(b => b.Length > 0));
    }

    [Fact]
    public async Task DisposeAsync_WithBufferedJsonData_ShouldSendOneEmailWithJsonAttachment()
    {
        // Arrange
        var connector = CreateConnector(attachmentFormat: "JSON");
        var batch = CreateBatch(2);
        await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _emailService.Received(1).SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(f => f.EndsWith(".json")),
            Arg.Is("application/json"),
            Arg.Is<byte[]>(b => b.Length > 0));
    }

    [Fact]
    public async Task DisposeAsync_WithBufferedExcelData_ShouldSendOneEmailWithExcelAttachment()
    {
        // Arrange
        var connector = CreateConnector(attachmentFormat: "Excel");
        var batch = CreateBatch(2);
        await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _emailService.Received(1).SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(f => f.EndsWith(".xlsx")),
            Arg.Is("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            Arg.Is<byte[]>(b => b.Length > 0));
    }

    [Fact]
    public async Task DisposeAsync_WithNoRowsAndSendEmptyReportFalse_ShouldNotSendEmail()
    {
        // Arrange - no batches written, sendEmptyReport = false (default)
        var connector = CreateConnector(sendEmptyReport: false);
        var emptyBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>(),
            RowCount = 0
        };
        await _sut.WriteBatchAsync(connector, emptyBatch, new WriteOptions(), CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _emailService.DidNotReceive().SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    [Fact]
    public async Task DisposeAsync_WithNoRowsAndSendEmptyReportTrue_ShouldSendEmail()
    {
        // Arrange
        var connector = CreateConnector(sendEmptyReport: true);
        var emptyBatch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>(),
            RowCount = 0
        };
        await _sut.WriteBatchAsync(connector, emptyBatch, new WriteOptions(), CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _emailService.Received(1).SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    [Fact]
    public async Task MultipleBatches_ShouldAccumulateAllRows_BeforeSendingOneEmail()
    {
        // Arrange
        var connector = CreateConnector();
        var batch1 = CreateBatch(3);
        var batch2 = CreateBatch(5);
        var batch3 = CreateBatch(2);

        // Act
        var result1 = await _sut.WriteBatchAsync(connector, batch1, new WriteOptions(), CancellationToken.None);
        var result2 = await _sut.WriteBatchAsync(connector, batch2, new WriteOptions(), CancellationToken.None);
        var result3 = await _sut.WriteBatchAsync(connector, batch3, new WriteOptions(), CancellationToken.None);

        // Assert - no email yet
        await _emailService.DidNotReceive().SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>());

        result1.RowsWritten.Should().Be(3);
        result2.RowsWritten.Should().Be(5);
        result3.RowsWritten.Should().Be(2);

        // Act - dispose sends exactly one email
        await _sut.DisposeAsync();

        // Assert - exactly one email sent
        await _emailService.Received(1).SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    [Fact]
    public async Task DisposeAsync_WithEmailSendFailure_ShouldNotThrow()
    {
        // Arrange
        _emailService.SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>())
            .Returns(false);

        var connector = CreateConnector();
        var batch = CreateBatch(1);
        await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Act - should not throw even when email send fails
        var act = async () => await _sut.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WithEmailServiceException_ShouldNotThrow()
    {
        // Arrange
        _emailService.SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>())
            .Returns<bool>(_ => throw new InvalidOperationException("SMTP error"));

        var connector = CreateConnector();
        var batch = CreateBatch(1);
        await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Act
        var act = async () => await _sut.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WithNoConnectorSet_ShouldNotSendEmail()
    {
        // Arrange - no WriteBatchAsync called at all

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _emailService.DidNotReceive().SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    [Fact]
    public async Task DisposeAsync_ShouldIncludeCustomBodyMessage_InEmailBody()
    {
        // Arrange
        var connector = CreateConnector(bodyMessage: "Monthly report for Q1");
        var batch = CreateBatch(1);
        await _sut.WriteBatchAsync(connector, batch, new WriteOptions(), CancellationToken.None);

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _emailService.Received(1).SendDataExportEmailAsync(
            Arg.Any<List<string>>(),
            Arg.Any<List<string>?>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains("Monthly report for Q1")),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    #region Helper Methods

    private static Connector CreateConnector(
        string attachmentFormat = "CSV",
        bool sendEmptyReport = false,
        string? bodyMessage = null)
    {
        var config = new
        {
            recipients = new[] { "user@example.com", "admin@example.com" },
            ccRecipients = new[] { "manager@example.com" },
            subject = "Data Export Test",
            bodyMessage = bodyMessage ?? "",
            attachmentFormat,
            attachmentFileName = "test-export",
            writeConfig = new
            {
                sendEmptyReport
            }
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Email Connector",
            Type = "Email",
            Provider = "Email",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static ReadBatch CreateBatch(int rowCount)
    {
        var rows = new List<Dictionary<string, object?>>();
        for (var i = 0; i < rowCount; i++)
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = i + 1,
                ["name"] = $"Item {i + 1}",
                ["value"] = (i + 1) * 10.5
            });
        }

        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = rows,
            RowCount = rowCount
        };
    }

    #endregion
}
