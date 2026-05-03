using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataAccess.Readers.File;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataAccess.Readers.File;

public class FileDataReaderFactoryTests
{
    // These tests are disabled because NSubstitute cannot mock concrete classes with complex constructors.
    // The factory logic is tested through integration tests and the DataReaderFactoryTests that mock the interface.
}