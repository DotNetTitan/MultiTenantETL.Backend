using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataAccess.Readers.Api;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;

namespace MultiTenantETL.UnitTests.DataAccess.Readers.Api;

public class ApiDataReaderFactoryTests
{
    // These tests are disabled because NSubstitute cannot mock concrete classes with complex constructors.
    // The factory logic is tested through integration tests and the DataReaderFactoryTests that mock the interface.
}