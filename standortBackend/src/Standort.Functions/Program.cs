using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Standort.Application.DTOs;
using Standort.Application.Interfaces;
using Standort.Application.Services;
using Standort.Application.Validators;
using Standort.Infrastructure.Configuration;
using Standort.Infrastructure.CosmosDb;
using Standort.Infrastructure.Security;
using Standort.Infrastructure.Time;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// JSON options for HTTP request/response (camelCase, case-insensitive deserialization).
builder.Services.Configure<JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// CosmosDb configuration.
builder.Services.Configure<CosmosDbOptions>(opts =>
{
    var config = builder.Configuration;
    opts.AccountEndpoint = config["COSMOSDB_ACCOUNT_ENDPOINT"] ?? string.Empty;
    opts.AccountKey = config["COSMOSDB_ACCOUNT_KEY"];
    opts.DatabaseName = config["COSMOSDB_DATABASE_NAME"] ?? string.Empty;
    opts.GroupsContainerName = config["COSMOSDB_GROUPS_CONTAINER"] ?? "groups";
    opts.InviteCodesContainerName = config["COSMOSDB_INVITECODES_CONTAINER"] ?? "inviteCodes";
});

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    return CosmosClientFactory.Create(opts);
});

builder.Services.AddSingleton<CosmosBootstrapper>();
builder.Services.AddSingleton<IGroupRepository, CosmosGroupRepository>();
builder.Services.AddSingleton<IInviteCodeRepository, CosmosInviteCodeRepository>();

builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
builder.Services.AddSingleton<ITokenGenerator, RandomTokenGenerator>();
builder.Services.AddSingleton<IInviteCodeGenerator, CrockfordInviteCodeGenerator>();

builder.Services.AddSingleton<GroupService>();
builder.Services.AddSingleton<LocationService>();

builder.Services.AddSingleton<IValidator<CreateGroupRequest>, CreateGroupRequestValidator>();
builder.Services.AddSingleton<IValidator<JoinGroupRequest>, JoinGroupRequestValidator>();
builder.Services.AddSingleton<IValidator<UpdateLocationRequest>, UpdateLocationRequestValidator>();

var host = builder.Build();

// Ensure Cosmos database + containers exist (local-emulator-friendly bootstrap).
using (var scope = host.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<CosmosBootstrapper>();
    await bootstrapper.EnsureCreatedAsync(CancellationToken.None);
}

await host.RunAsync();
