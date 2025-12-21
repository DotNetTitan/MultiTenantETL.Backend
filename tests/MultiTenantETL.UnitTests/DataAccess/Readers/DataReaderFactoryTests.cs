using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataAccess.Readers;
using MultiTenantETL.Infrastructure.DataAccess.Readers.Api;
using MultiTenantETL.Infrastructure.DataAccess.Readers.Database;
using MultiTenantETL.Infrastructure.DataAccess.Readers.File;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataAccess.Readers;

public class DataReaderFactoryTests
{
    private readonly IDatabaseDataReaderFactory _databaseFactory;
    private readonly IFileDataReaderFactory _fileFactory;
    private readonly IApiDataReaderFactory _apiFactory;
    private readonly ILogger<DataReaderFactory> _logger;
    private readonly DataReaderFactory _sut;

    public DataReaderFactoryTests()
    {
        _databaseFactory = Substitute.For<IDatabaseDataReaderFactory>();
        _fileFactory = Substitute.For<IFileDataReaderFactory>();
        _apiFactory = Substitute.For<IApiDataReaderFactory>();
        _logger = Substitute.For<ILogger<DataReaderFactory>>();

        _sut = new DataReaderFactory(
            _databaseFactory,
            _fileFactory,
            _apiFactory,
            _logger);
    }

    [Fact]
    public void CreateReader_DatabaseType_ShouldCallDatabaseFactory()
    {
        // Arrange
        var connector = CreateConnector("Database", "SqlServer");
        var expectedReader = Substitute.For<IDataReader>();
        _databaseFactory.CreateReader(connector).Returns(expectedReader);

        // Act
        var result = _sut.CreateReader(connector);

        // Assert
        result.Should().Be(expectedReader);
        _databaseFactory.Received(1).CreateReader(connector);
    }

    [Fact]
    public void CreateReader_FileType_ShouldCallFileFactory()
    {
        // Arrange
        var connector = CreateConnector("File", "Local");
        var expectedReader = Substitute.For<IDataReader>();
        _fileFactory.CreateReader(connector).Returns(expectedReader);

        // Act
        var result = _sut.CreateReader(connector);

        // Assert
        result.Should().Be(expectedReader);
        _fileFactory.Received(1).CreateReader(connector);
    }

    [Fact]
    public void CreateReader_ApiType_ShouldCallApiFactory()
    {
        // Arrange
        var connector = CreateConnector("API", "REST");
        var expectedReader = Substitute.For<IDataReader>();
        _apiFactory.CreateReader(connector).Returns(expectedReader);

        // Act
        var result = _sut.CreateReader(connector);

        // Assert
        result.Should().Be(expectedReader);
        _apiFactory.Received(1).CreateReader(connector);
    }

    [Fact]
    public void CreateReader_UnsupportedType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var connector = CreateConnector("Unsupported", "Provider");

        // Act
        var act = () => _sut.CreateReader(connector);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("Connector type 'Unsupported' is not supported");
    }

    [Theory]
    [InlineData("database")]
    [InlineData("DATABASE")]
    [InlineData("Database")]
    public void CreateReader_DatabaseType_CaseInsensitive_ShouldWork(string type)
    {
        // Arrange
        var connector = CreateConnector(type, "SqlServer");
        var expectedReader = Substitute.For<IDataReader>();
        _databaseFactory.CreateReader(connector).Returns(expectedReader);

        // Act
        var result = _sut.CreateReader(connector);

        // Assert
        result.Should().Be(expectedReader);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("FILE")]
    [InlineData("File")]
    public void CreateReader_FileType_CaseInsensitive_ShouldWork(string type)
    {
        // Arrange
        var connector = CreateConnector(type, "Local");
        var expectedReader = Substitute.For<IDataReader>();
        _fileFactory.CreateReader(connector).Returns(expectedReader);

        // Act
        var result = _sut.CreateReader(connector);

        // Assert
        result.Should().Be(expectedReader);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("API")]
    [InlineData("Api")]
    public void CreateReader_ApiType_CaseInsensitive_ShouldWork(string type)
    {
        // Arrange
        var connector = CreateConnector(type, "REST");
        var expectedReader = Substitute.For<IDataReader>();
        _apiFactory.CreateReader(connector).Returns(expectedReader);

        // Act
        var result = _sut.CreateReader(connector);

        // Assert
        result.Should().Be(expectedReader);
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
            Direction = "source",
            IsSource = true,
            IsDestination = false,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }
}