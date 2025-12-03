var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("multitenant-etl-postgres-data")
    .WithPgAdmin();

var postgresDb = postgres.AddDatabase("multitenant-etl");

// RabbitMQ message broker
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume("multitenant-etl-rabbitmq-data")
    .WithManagementPlugin();

// API project
var api = builder.AddProject<Projects.MultiTenantETL_API>("api")
    .WithReference(postgresDb)
    .WithReference(rabbitmq)
    .WaitFor(postgresDb)
    .WaitFor(rabbitmq)
    .WithExternalHttpEndpoints();

// Worker project
builder.AddProject<Projects.MultiTenantETL_Worker>("worker")
    .WithReference(postgresDb)
    .WithReference(rabbitmq)
    .WaitFor(postgresDb)
    .WaitFor(rabbitmq)
    .WaitFor(api);

builder.Build().Run();
