using Forum.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Forum.Tests.Unit;

public class PersistenceMappingTests
{
    [Fact]
    public void IdentityPrimaryKeys_MapToLowercaseDatabaseColumnNames()
    {
        var options = new DbContextOptionsBuilder<ForumDbContext>()
            .UseNpgsql("Host=localhost;Database=forum;Username=forum_user;Password=test-password")
            .Options;
        using var dbContext = new ForumDbContext(options);

        var entityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty("Id") is not null);

        foreach (var entityType in entityTypes)
        {
            var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
            var idProperty = entityType.FindProperty("Id")!;

            Assert.Equal("id", idProperty.GetColumnName(table));
        }
    }
}
