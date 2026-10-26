using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Api.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ForumDbContext))]
[Migration("20261026000001_AddFullTextSearchIndexes")]
public partial class AddFullTextSearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE INDEX ix_topics_title_search_ru_en
            ON topics USING GIN (
                (to_tsvector('russian', coalesce(title, '')) || to_tsvector('english', coalesce(title, '')))
            )
            WHERE deleted_at IS NULL;

            CREATE INDEX ix_posts_content_search_ru_en
            ON posts USING GIN (
                (to_tsvector('russian', coalesce(content, '')) || to_tsvector('english', coalesce(content, '')))
            )
            WHERE deleted_at IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS ix_posts_content_search_ru_en;
            DROP INDEX IF EXISTS ix_topics_title_search_ru_en;
            """);
    }
}
