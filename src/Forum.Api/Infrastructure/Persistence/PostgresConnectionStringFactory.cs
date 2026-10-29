using Npgsql;

namespace Forum.Api.Infrastructure.Persistence;

public static class PostgresConnectionStringFactory
{
    public static string Create(IConfiguration configuration) => Create(
        configuration["POSTGRES_HOST"],
        configuration["POSTGRES_PORT"],
        configuration["POSTGRES_USER"],
        configuration["POSTGRES_PASSWORD"],
        configuration["POSTGRES_DB"]);

    public static string CreateFromEnvironment() => Create(
        Environment.GetEnvironmentVariable("POSTGRES_HOST"),
        Environment.GetEnvironmentVariable("POSTGRES_PORT"),
        Environment.GetEnvironmentVariable("POSTGRES_USER"),
        Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"),
        Environment.GetEnvironmentVariable("POSTGRES_DB"));

    private static string Create(string? host, string? portValue, string? username, string? password, string? database)
    {
        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("POSTGRES_PORT must be an integer between 1 and 65535.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = Require(host, "POSTGRES_HOST"),
            Port = port,
            Username = Require(username, "POSTGRES_USER"),
            Password = Require(password, "POSTGRES_PASSWORD"),
            Database = Require(database, "POSTGRES_DB")
        }.ConnectionString;
    }

    private static string Require(string? value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{key} is required.") : value;
}
