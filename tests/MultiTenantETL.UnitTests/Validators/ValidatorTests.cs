using FluentValidation.TestHelper;
using MultiTenantETL.Application.Authentication.Models;
using MultiTenantETL.Application.Authentication.Validators;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Connectors.Validators;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Application.Pipelines.Validators;
using MultiTenantETL.Application.Tenants.Models;
using MultiTenantETL.Application.Tenants.Validators;
using MultiTenantETL.Application.Users.Models;
using MultiTenantETL.Application.Users.Validators;
using System.Text.Json;

namespace MultiTenantETL.UnitTests.Validators;

public class ValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly CreateTenantRequestValidator _createTenantValidator = new();
    private readonly UpdateUserRequestValidator _updateUserValidator = new();
    private readonly CreateConnectorRequestValidator _createConnectorValidator = new();
    private readonly CreatePipelineRequestValidator _createPipelineValidator = new();

    [Fact]
    public void RegisterRequest_ShouldHaveError_WhenEmailIsInvalid()
    {
        var model = new RegisterRequest { Email = "invalid-email", Password = "Password123!", FirstName = "John", LastName = "Doe" };
        var result = _registerValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterRequest_ShouldHaveError_WhenPasswordIsTooShort()
    {
        var model = new RegisterRequest { Email = "test@example.com", Password = "Short1!", FirstName = "John", LastName = "Doe" };
        var result = _registerValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void RegisterRequest_ShouldHaveError_WhenPasswordMissingSpecialChar()
    {
        var model = new RegisterRequest { Email = "test@example.com", Password = "Password123", FirstName = "John", LastName = "Doe" };
        var result = _registerValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void RegisterRequest_ShouldNotHaveError_WhenValid()
    {
        var model = new RegisterRequest { Email = "test@example.com", Password = "Password123!", FirstName = "John", LastName = "Doe" };
        var result = _registerValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateTenantRequest_ShouldHaveError_WhenSlugIsInvalid()
    {
        var model = new CreateTenantRequest { Name = "Valid Name", Slug = "Invalid Slug" };
        var result = _createTenantValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void CreateTenantRequest_ShouldNotHaveError_WhenValid()
    {
        var model = new CreateTenantRequest { Name = "Valid Name", Slug = "valid-slug-123" };
        var result = _createTenantValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateUserRequest_ShouldHaveError_WhenNamesAreTooShort()
    {
        var model = new UpdateUserRequest { Email = "test@example.com", FirstName = "J", LastName = "D" };
        var result = _updateUserValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void CreateConnectorRequest_ShouldHaveError_WhenNameIsTooShort()
    {
        var model = new CreateConnectorRequest
        {
            Name = "A",
            Type = "Database",
            Provider = "SqlServer",
            Direction = "source",
            Config = JsonDocument.Parse("{}").RootElement
        };
        var result = _createConnectorValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateConnectorRequest_ShouldHaveError_WhenTypeIsInvalid()
    {
        var model = new CreateConnectorRequest
        {
            Name = "Valid Name",
            Type = "InvalidType",
            Provider = "SqlServer",
            Direction = "source",
            Config = JsonDocument.Parse("{}").RootElement
        };
        var result = _createConnectorValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void CreatePipelineRequest_ShouldHaveError_WhenConnectorsAreMissing()
    {
        var model = new CreatePipelineRequest
        {
            Name = "Valid Name",
            SourceConnectorId = Guid.Empty,
            DestinationConnectorId = Guid.Empty,
            FieldMappings = JsonDocument.Parse("[]").RootElement
        };
        var result = _createPipelineValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.SourceConnectorId);
        result.ShouldHaveValidationErrorFor(x => x.DestinationConnectorId);
    }
}
