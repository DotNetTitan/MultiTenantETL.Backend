using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataAccess.Readers;
using MultiTenantETL.Infrastructure.DataAccess.Writers;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Orchestration;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Transformations;
using MultiTenantETL.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<EtlSettings>(builder.Configuration.GetSection(EtlSettings.SectionName));

// Tenant context provider (scoped per job)
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

// CurrentUserService (needed by various services, uses TenantProvider in worker context)
builder.Services.AddHttpContextAccessor(); // Will be null in worker, but required by CurrentUserService
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Encryption Service (needed for decrypting connector credentials)
builder.Services.AddSingleton<IEncryptionService, MultiTenantETL.Infrastructure.Security.EncryptionService>();

// Audit Service - use null implementation for worker (actions already audited at API level)
builder.Services.AddScoped<MultiTenantETL.Application.Interfaces.IAuditService,
    MultiTenantETL.Infrastructure.Services.NullAuditService>();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Transformation Services
builder.Services.AddScoped<MultiTenantETL.Application.Transformations.ITransformationService,
    MultiTenantETL.Infrastructure.Services.TransformationService>();
builder.Services.AddScoped<ITransformationOrchestrator, TransformationOrchestrator>();

// Transformation Processors
builder.Services.AddScoped<MultiTenantETL.Application.Transformations.ITransformationProcessor,
    MultiTenantETL.Infrastructure.Transformations.Processors.FilterProcessor>();
builder.Services.AddScoped<MultiTenantETL.Application.Transformations.ITransformationProcessor,
    MultiTenantETL.Infrastructure.Transformations.Processors.MapProcessor>();
builder.Services.AddScoped<MultiTenantETL.Application.Transformations.ITransformationProcessor,
    MultiTenantETL.Infrastructure.Transformations.Processors.StringProcessor>();
builder.Services.AddScoped<MultiTenantETL.Application.Transformations.ITransformationProcessor,
    MultiTenantETL.Infrastructure.Transformations.Processors.ScriptProcessor>();

// Field Transformation Processor (for complex field mappings)
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Transformations.FieldProcessors.IFieldTransformationProcessor,
    MultiTenantETL.Infrastructure.Transformations.FieldProcessors.FieldTransformationProcessor>();

// Data Reader Factories
builder.Services.AddScoped<IDataReaderFactory, DataReaderFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Readers.Database.IDatabaseDataReaderFactory,
    MultiTenantETL.Infrastructure.DataAccess.Readers.Database.DatabaseDataReaderFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Readers.File.IFileDataReaderFactory,
    MultiTenantETL.Infrastructure.DataAccess.Readers.File.FileDataReaderFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Readers.Api.IApiDataReaderFactory,
    MultiTenantETL.Infrastructure.DataAccess.Readers.Api.ApiDataReaderFactory>();

// Data Writer Factories
builder.Services.AddScoped<IDataWriterFactory, DataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Writers.Database.IDatabaseDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.Database.DatabaseDataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Writers.File.IFileDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.File.FileDataWriterFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataAccess.Writers.Api.IApiDataWriterFactory,
    MultiTenantETL.Infrastructure.DataAccess.Writers.Api.ApiDataWriterFactory>();

// Data Readers
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.SqlServerDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.PostgreSqlDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.MySqlDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.CsvDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.JsonDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.JsonLinesDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.RestApiDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.S3DataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.AzureBlobDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.SftpDataReader>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataReaders.FtpDataReader>();

// Data Writers
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.SqlServerDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.PostgreSqlDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.MySqlDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.CsvDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.JsonDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.JsonLinesDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.RestApiDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.S3DataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.AzureBlobDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.SftpDataWriter>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.DataWriters.FtpDataWriter>();

// Supporting Services
builder.Services.AddHttpClient(); // For API connectors
builder.Services.AddSingleton<MultiTenantETL.Infrastructure.Services.Database.IDatabaseConnectionStringBuilder,
    MultiTenantETL.Infrastructure.Services.Database.DatabaseConnectionStringBuilder>();
builder.Services.AddSingleton<MultiTenantETL.Infrastructure.Services.Storage.IStorageClientFactory,
    MultiTenantETL.Infrastructure.Services.Storage.StorageClientFactory>();
builder.Services.AddScoped<MultiTenantETL.Infrastructure.Services.Http.IHttpClientAuthenticator,
    MultiTenantETL.Infrastructure.Services.Http.HttpClientAuthenticator>();
builder.Services.AddScoped<MultiTenantETL.Application.Connectors.DataWriters.IFormatValidator,
    MultiTenantETL.Infrastructure.DataWriters.FormatValidator>();

// Orchestration Services
builder.Services.AddScoped<IPipelineOrchestrator, PipelineOrchestrator>();
builder.Services.AddScoped<MultiTenantETL.Application.Orchestration.IFieldMappingService,
    MultiTenantETL.Infrastructure.Orchestration.FieldMappingService>();

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
