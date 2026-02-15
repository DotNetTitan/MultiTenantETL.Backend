# Multi-stage Dockerfile for MultiTenantETL.Worker
# Build context: repository root (run with: docker build -f src/MultiTenantETL.Worker/Dockerfile .)

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY MultiTenantETL.slnx ./
COPY src/MultiTenantETL.Worker/MultiTenantETL.Worker.csproj src/MultiTenantETL.Worker/
COPY src/MultiTenantETL.Application/MultiTenantETL.Application.csproj src/MultiTenantETL.Application/
COPY src/MultiTenantETL.Domain/MultiTenantETL.Domain.csproj src/MultiTenantETL.Domain/
COPY src/MultiTenantETL.Infrastructure/MultiTenantETL.Infrastructure.csproj src/MultiTenantETL.Infrastructure/
COPY src/MultiTenantETL.ServiceDefaults/MultiTenantETL.ServiceDefaults.csproj src/MultiTenantETL.ServiceDefaults/
COPY src/MultiTenantETL.API/MultiTenantETL.API.csproj src/MultiTenantETL.API/
COPY src/MultiTenantETL.AppHost/MultiTenantETL.AppHost.csproj src/MultiTenantETL.AppHost/
COPY tests/MultiTenantETL.IntegrationTests/MultiTenantETL.IntegrationTests.csproj tests/MultiTenantETL.IntegrationTests/
COPY tests/MultiTenantETL.UnitTests/MultiTenantETL.UnitTests.csproj tests/MultiTenantETL.UnitTests/

# Restore dependencies
RUN dotnet restore src/MultiTenantETL.Worker/MultiTenantETL.Worker.csproj

# Copy all source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/MultiTenantETL.Worker/MultiTenantETL.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Security: run as non-root
RUN groupadd --gid 1000 appgroup && \
    useradd --uid 1000 --gid appgroup --shell /bin/bash --create-home appuser

ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

# Switch to non-root user
USER appuser

ENTRYPOINT ["dotnet", "MultiTenantETL.Worker.dll"]
