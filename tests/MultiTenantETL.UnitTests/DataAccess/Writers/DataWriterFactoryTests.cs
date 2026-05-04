using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataAccess.Writers;
using MultiTenantETL.Infrastructure.DataAccess.Writers.Api;
using MultiTenantETL.Infrastructure.DataAccess.Writers.Database;
using MultiTenantETL.Infrastructure.DataAccess.Writers.Email;
using MultiTenantETL.Infrastructure.DataAccess.Writers.File;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataAccess.Writers;

public class DataWriterFactoryTests
{
    private readonly IDatabaseDataWriterFactory _databaseFactory;
    private readonly IFileDataWriterFactory _fileFactory;
    private readonly IApiDataWriterFactory _apiFactory;
    private readonly IEmailDataWriterFactory _emailFactory;
    private readonly ILogger<DataWriterFactory> _logger;
    private readonly DataWriterFactory _sut;

    public DataWriterFactoryTests()
    {
        _databaseFactory = Substitute.For<IDatabaseDataWriterFactory>();
        _fileFactory = Substitute.For<IFileDataWriterFactory>();
        _apiFactory = Substitute.For<IApiDataWriterFactory>();
        _emailFactory = Substitute.For<IEmailDataWriterFactory>();
        _logger = Substitute.For<ILogger<DataWriterFactory>>();

        _sut = new DataWriterFactory(
            _databaseFactory,
            _fileFactory,
            _apiFactory,
            _emailFactory,
            _logger);
    }

    [Fact]
    public void CreateWriter_DatabaseType_ShouldCallDatabaseFactory()
    {
        // Arrange
        var connector = CreateConnector("Database", "SqlServer");
        var expectedWriter = Substitute.For<IDataWriter>();
        _databaseFactory.CreateWriter(connector).Returns(expectedWriter);

        // Act
        var result = _sut.CreateWriter(connector);

        // Assert
        result.Should().Be(expectedWriter);
        _databaseFactory.Received(1).CreateWriter(connector);
    }

    [Fact]
    public void CreateWriter_FileType_ShouldCallFileFactory()
    {
        // Arrange
        var connector = CreateConnector("File", "Local");
        var expectedWriter = Substitute.For<IDataWriter>();
        _fileFactory.CreateWriter(connector).Returns(expectedWriter);

        // Act
        var result = _sut.CreateWriter(connector);

        // Assert
        result.Should().Be(expectedWriter);
        _fileFactory.Received(1).CreateWriter(connector);
    }

    [Fact]
    public void CreateWriter_ApiType_ShouldCallApiFactory()
    {
        // Arrange
        var connector = CreateConnector("API", "REST");
        var expectedWriter = Substitute.For<IDataWriter>();
        _apiFactory.CreateWriter(connector).Returns(expectedWriter);

        // Act
        var result = _sut.CreateWriter(connector);

        // Assert
        result.Should().Be(expectedWriter);
        _apiFactory.Received(1).CreateWriter(connector);
    }

    [Fact]
    public void CreateWriter_UnsupportedType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var connector = CreateConnector("Unsupported", "Provider");

        // Act
        var act = () => _sut.CreateWriter(connector);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("Connector type 'Unsupported' is not supported");
    }

    [Theory]
    [InlineData("database")]
    [InlineData("DATABASE")]
    [InlineData("Database")]
    public void CreateWriter_DatabaseType_CaseInsensitive_ShouldWork(string type)
    {
        // Arrange
        var connector = CreateConnector(type, "SqlServer");
        var expectedWriter = Substitute.For<IDataWriter>();
        _databaseFactory.CreateWriter(connector).Returns(expectedWriter);

        // Act
        var result = _sut.CreateWriter(connector);

        // Assert
        result.Should().Be(expectedWriter);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("FILE")]
    [InlineData("File")]
    public void CreateWriter_FileType_CaseInsensitive_ShouldWork(string type)
    {
        // Arrange
        var connector = CreateConnector(type, "Local");
        var expectedWriter = Substitute.For<IDataWriter>();
        _fileFactory.CreateWriter(connector).Returns(expectedWriter);

        // Act
        var result = _sut.CreateWriter(connector);

        // Assert
        result.Should().Be(expectedWriter);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("API")]
    [InlineData("Api")]
    public void CreateWriter_ApiType_CaseInsensitive_ShouldWork(string type)
    {
        // Arrange
        var connector = CreateConnector(type, "REST");
        var expectedWriter = Substitute.For<IDataWriter>();
        _apiFactory.CreateWriter(connector).Returns(expectedWriter);

        // Act
        var result = _sut.CreateWriter(connector);

        // Assert
        result.Should().Be(expectedWriter);
    }

    private static Connector CreateConnector(string type, string provider)
    {
        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test Connector",
            Type = type,
            Provider = provider,
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}