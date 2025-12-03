var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("multitenant-etl-postgres-data")
    .WithPgAdmin();

// Resource name must use hyphens (Aspire naming rules), but actual database name can use underscores
var postgresDb = postgres.AddDatabase("multitenant-etl-db", databaseName: "multitenant_etl");

// RabbitMQ message broker
var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume("multitenant-etl-rabbitmq-data")
    .WithManagementPlugin();

// API project - use WithReference with custom connection string name
var api = builder.AddProject<Projects.MultiTenantETL_API>("api")
    .WithReference(postgresDb, connectionName: "DefaultConnection")
    .WithReference(rabbitmq, connectionName: "RabbitMq")
    .WaitFor(postgresDb)
    .WaitFor(rabbitmq)
    .WithExternalHttpEndpoints();

// Worker project
builder.AddProject<Projects.MultiTenantETL_Worker>("worker")
    .WithReference(postgresDb, connectionName: "DefaultConnection")
    .WithReference(rabbitmq, connectionName: "RabbitMq")
    .WaitFor(postgresDb)
    .WaitFor(rabbitmq)
    .WaitFor(api);

builder.Build().Run();
