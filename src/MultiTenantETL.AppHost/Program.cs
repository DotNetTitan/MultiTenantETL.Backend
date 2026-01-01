var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("multitenant-etl-postgres-data")
    .WithPgAdmin();

// Resource name must use hyphens (Aspire naming rules), but actual database name can use underscores
var postgresDb = postgres.AddDatabase("multitenant-etl-db", databaseName: "multitenant_etl");

// Azure Service Bus message broker
// Note: For local development, you need to configure a connection string via user secrets or environment variables
// For production, use Azure Service Bus connection string
var serviceBus = builder.AddAzureServiceBus("messaging");

// API project - use WithReference with custom connection string name
var api = builder.AddProject<Projects.MultiTenantETL_API>("api")
    .WithReference(postgresDb, connectionName: "DefaultConnection")
    .WithReference(serviceBus, connectionName: "ServiceBus")
    .WaitFor(postgresDb)
    .WithExternalHttpEndpoints();

// Worker project
builder.AddProject<Projects.MultiTenantETL_Worker>("worker")
    .WithReference(postgresDb, connectionName: "DefaultConnection")
    .WithReference(serviceBus, connectionName: "ServiceBus")
    .WaitFor(postgresDb)
    .WaitFor(api);

builder.Build().Run();
