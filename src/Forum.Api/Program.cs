using Forum.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ForumDbContext>(options =>
    options.UseNpgsql(CreatePostgresConnectionString(builder.Configuration)));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/health/ready", () => Results.Ok(new { status = "Ready" })).AllowAnonymous();

app.Run();

static string CreatePostgresConnectionString(IConfiguration configuration)
{
    var host = configuration["POSTGRES_HOST"] ?? throw new InvalidOperationException("POSTGRES_HOST is required.");
    var port = configuration["POSTGRES_PORT"] ?? throw new InvalidOperationException("POSTGRES_PORT is required.");
    var username = configuration["POSTGRES_USER"] ?? throw new InvalidOperationException("POSTGRES_USER is required.");
    var password = configuration["POSTGRES_PASSWORD"] ?? throw new InvalidOperationException("POSTGRES_PASSWORD is required.");
    var database = configuration["POSTGRES_DB"] ?? throw new InvalidOperationException("POSTGRES_DB is required.");

    return new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = int.Parse(port),
        Username = username,
        Password = password,
        Database = database
    }.ConnectionString;
}

public partial class Program;
