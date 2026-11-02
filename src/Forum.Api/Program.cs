using System.Security.Claims;
using System.Text;
using Forum.Api.Common.Authorization;
using Forum.Api.Features.Auth;
using Forum.Api.Features.Bookmarks;
using Forum.Api.Features.Users;
using Forum.Api.Features.Categories;
using Forum.Api.Features.Posts;
using Forum.Api.Features.Reactions;
using Forum.Api.Features.Topics;
using Forum.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen(options =>
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Forum.Api.xml")));

var jwtOptions = JwtOptionsFactory.Create(builder.Configuration);
var refreshTokenOptions = RefreshTokenOptionsFactory.Create(builder.Configuration);
var topicPaginationOptions = TopicPaginationOptionsFactory.Create(builder.Configuration);

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(refreshTokenOptions);
builder.Services.AddSingleton(topicPaginationOptions);

builder.Services.AddForumPasswordHashing(builder.Configuration);

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BookmarksService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<CategoriesService>();
builder.Services.AddScoped<PostsService>();
builder.Services.AddScoped<ReactionsService>();
builder.Services.AddScoped<TopicsService>();
builder.Services.AddScoped<ActiveUserBanService>();

builder.Services.AddScoped<IAuthorizationHandler, ActiveUserRequirementHandler>();

builder.Services.AddDbContext<ForumDbContext>(options =>
    options.UseNpgsql(PostgresConnectionStringFactory.Create(builder.Configuration)));

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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/health/ready", () => Results.Ok(new { status = "Ready" })).AllowAnonymous();

app.Run();

static AuthorizationPolicy CreateActiveUserPolicy() => new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .AddRequirements(new ActiveUserRequirement())
    .Build();
