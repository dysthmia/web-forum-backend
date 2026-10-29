using System.Security.Claims;
using System.Text;
using Forum.Api.Common.Authorization;
using Forum.Api.Features.Auth;
using Forum.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Forum.Api.xml")));
var jwtOptions = JwtOptionsFactory.Create(builder.Configuration);
var refreshTokenOptions = RefreshTokenOptionsFactory.Create(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(refreshTokenOptions);
builder.Services.AddForumPasswordHashing(builder.Configuration);
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ActiveUserBanService>();
builder.Services.AddScoped<IAuthorizationHandler, ActiveUserRequirementHandler>();
builder.Services.AddDbContext<ForumDbContext>(options =>
    options.UseNpgsql(CreatePostgresConnectionString(builder.Configuration)));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = CreateActiveUserPolicy();
    options.AddPolicy("Moderator", policy => policy.RequireRole(ForumRoles.Moderator, ForumRoles.Admin)
        .AddRequirements(new ActiveUserRequirement()));
    options.AddPolicy("Admin", policy => policy.RequireRole(ForumRoles.Admin)
        .AddRequirements(new ActiveUserRequirement()));
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
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

static AuthorizationPolicy CreateActiveUserPolicy() => new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .AddRequirements(new ActiveUserRequirement())
    .Build();

public partial class Program;
