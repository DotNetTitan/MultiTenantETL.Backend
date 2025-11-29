using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Application.DataAccess;
using MultiTenantETL.Application.Orchestration;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Infrastructure.Configuration;
using MultiTenantETL.Infrastructure.DataAccess.Readers;
using MultiTenantETL.Infrastructure.DataAccess.Writers;
using MultiTenantETL.Infrastructure.Orchestration;
using MultiTenantETL.Infrastructure.Persistence;
using MultiTenantETL.Infrastructure.Transformations;
using MultiTenantETL.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Core services
builder.Services.AddScoped<IDataReaderFactory, DataReaderFactory>();
builder.Services.AddScoped<IDataWriterFactory, DataWriterFactory>();
builder.Services.AddScoped<ITransformationOrchestrator, TransformationOrchestrator>();
builder.Services.AddScoped<IPipelineOrchestrator, PipelineOrchestrator>();

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
