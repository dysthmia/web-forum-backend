using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Api.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ForumDbContext))]
[Migration("20261026000002_AddForumSqlFeatures")]
public partial class AddForumSqlFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE VIEW category_statistics AS
            SELECT
                category.id AS category_id,
                category.name AS category_name,
                (
                    SELECT count(*)
                    FROM topics topic
                    WHERE topic.category_id = category.id AND topic.deleted_at IS NULL
                ) AS active_topic_count,
                (
                    SELECT count(*)
                    FROM posts post
                    INNER JOIN topics topic ON topic.id = post.topic_id
                    WHERE topic.category_id = category.id
                      AND topic.deleted_at IS NULL
                      AND post.deleted_at IS NULL
                ) AS active_post_count
            FROM categories category;

            CREATE FUNCTION user_forum_statistics(p_user_id bigint)
            RETURNS TABLE (active_topic_count bigint, active_post_count bigint, received_reaction_count bigint)
            LANGUAGE sql
            STABLE
            AS $$
                SELECT
                    (SELECT count(*) FROM topics WHERE author_id = p_user_id AND deleted_at IS NULL),
                    (SELECT count(*) FROM posts WHERE author_id = p_user_id AND deleted_at IS NULL),
                    (
                        SELECT count(*)
                        FROM reactions reaction
                        INNER JOIN posts post ON post.id = reaction.post_id
                        WHERE post.author_id = p_user_id AND post.deleted_at IS NULL
                    );
            $$;

            CREATE FUNCTION set_updated_at()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                NEW.updated_at = CURRENT_TIMESTAMP;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER trg_topics_set_updated_at
            BEFORE UPDATE ON topics
            FOR EACH ROW EXECUTE FUNCTION set_updated_at();

            CREATE TRIGGER trg_posts_set_updated_at
            BEFORE UPDATE ON posts
            FOR EACH ROW EXECUTE FUNCTION set_updated_at();

            CREATE PROCEDURE review_report(
                p_report_id bigint,
                p_resolved_by bigint,
                p_status varchar(16),
                p_hide_post boolean
            )
            LANGUAGE plpgsql
            AS $$
            DECLARE
                current_status varchar(16);
                reported_post_id bigint;
                reported_post_position integer;
            BEGIN
                IF p_status NOT IN ('accepted', 'rejected') THEN
                    RAISE EXCEPTION 'Report status must be accepted or rejected';
                END IF;

                SELECT status, post_id
                INTO current_status, reported_post_id
                FROM reports
                WHERE id = p_report_id
                FOR UPDATE;

                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Report % was not found', p_report_id;
                END IF;

                IF current_status <> 'pending' THEN
                    IF current_status = p_status THEN
                        RETURN;
                    END IF;

                    RAISE EXCEPTION 'Report % has already been resolved', p_report_id;
                END IF;

                IF p_hide_post AND p_status <> 'accepted' THEN
                    RAISE EXCEPTION 'Only an accepted report can hide a post';
                END IF;

                IF p_hide_post THEN
                    SELECT position INTO reported_post_position FROM posts WHERE id = reported_post_id FOR UPDATE;

                    IF reported_post_position = 1 THEN
                        RAISE EXCEPTION 'The first post of a topic cannot be deleted separately';
                    END IF;

                    UPDATE posts
                    SET deleted_at = COALESCE(deleted_at, CURRENT_TIMESTAMP),
                        deleted_by = COALESCE(deleted_by, p_resolved_by)
                    WHERE id = reported_post_id;
                END IF;

                UPDATE reports
                SET status = p_status,
                    resolved_by = p_resolved_by,
                    resolved_at = CURRENT_TIMESTAMP
                WHERE id = p_report_id;
            END;
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP PROCEDURE IF EXISTS review_report(bigint, bigint, varchar, boolean);
            DROP TRIGGER IF EXISTS trg_posts_set_updated_at ON posts;
            DROP TRIGGER IF EXISTS trg_topics_set_updated_at ON topics;
            DROP FUNCTION IF EXISTS set_updated_at();
            DROP FUNCTION IF EXISTS user_forum_statistics(bigint);
            DROP VIEW IF EXISTS category_statistics;
            """);
    }
}
