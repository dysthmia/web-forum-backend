# Визуализация структуры базы данных и связей между таблицами

> Скопируйте код написанный ниже и вставьте на сайт: https://dbdiagram.io/home


## Код для генерации диаграммы связей базы данных
``` SQL
Table users {
  id bigint [pk, increment]
  username varchar(32) [not null]
  email varchar(254) [not null]
  password_hash text [not null]
  role varchar(16) [not null, default: 'user', check: `role IN ('user', 'moderator', 'admin')`]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (`lower(username)`) [unique, name: 'ux_users_username_ci']
    (`lower(email)`) [unique, name: 'ux_users_email_ci']
  }
}

Table refresh_tokens {
  id bigint [pk, increment]
  user_id bigint [not null]
  token_hash text [not null, unique]
  created_at timestamptz [not null, default: `now()`]
  expires_at timestamptz [not null]
  revoked_at timestamptz

  checks {
    `expires_at > created_at` [name: 'chk_refresh_tokens_expiration']
    `revoked_at IS NULL OR revoked_at >= created_at` [name: 'chk_refresh_tokens_revocation']
  }

  indexes {
    (user_id, expires_at) [name: 'ix_refresh_tokens_user_expiration']
  }
}

Table user_bans {
  id bigint [pk, increment]
  user_id bigint [not null]
  moderator_id bigint [not null]
  reason text [not null, check: `btrim(reason) <> ''`]
  created_at timestamptz [not null, default: `now()`]
  expires_at timestamptz
  revoked_at timestamptz

  checks {
    `expires_at IS NULL OR expires_at > created_at` [name: 'chk_user_bans_expiration']
    `revoked_at IS NULL OR revoked_at >= created_at` [name: 'chk_user_bans_revocation']
  }

  indexes {
    (user_id, created_at [desc]) [name: 'ix_user_bans_history']
  }
}

Table categories {
  id bigint [pk, increment]
  name varchar(100) [not null, check: `btrim(name) <> ''`]
  slug varchar(100) [not null, unique]
  description text
}

Table topics {
  id bigint [pk, increment]
  category_id bigint [not null]
  author_id bigint [not null]
  title varchar(200) [not null, check: `btrim(title) <> ''`]
  view_count bigint [not null, default: 0, check: `view_count >= 0`]
  is_closed boolean [not null, default: false]
  is_pinned boolean [not null, default: false]
  created_at timestamptz [not null, default: `now()`]
  updated_at timestamptz [not null, default: `now()`]
  deleted_at timestamptz
  deleted_by bigint

  checks {
    `updated_at >= created_at` [name: 'chk_topics_updated_at']
    `((deleted_at IS NULL AND deleted_by IS NULL) OR (deleted_at IS NOT NULL AND deleted_by IS NOT NULL))` [name: 'chk_topics_delete_pair']
  }

  indexes {
    (category_id, created_at [desc], id [desc]) [name: 'ix_topics_category_feed']
    (author_id, created_at [desc]) [name: 'ix_topics_author_feed']
  }
}

Table posts {
  id bigint [pk, increment]
  topic_id bigint [not null]
  author_id bigint [not null]
  position integer [not null, check: `position >= 1`]
  content text [not null, check: `btrim(content) <> ''`]
  created_at timestamptz [not null, default: `now()`]
  updated_at timestamptz [not null, default: `now()`]
  deleted_at timestamptz
  deleted_by bigint

  checks {
    `updated_at >= created_at` [name: 'chk_posts_updated_at']
    `((deleted_at IS NULL AND deleted_by IS NULL) OR (deleted_at IS NOT NULL AND deleted_by IS NOT NULL))` [name: 'chk_posts_delete_pair']
  }

  indexes {
    (topic_id, position) [unique, name: 'ux_posts_topic_position']
    (author_id, created_at [desc]) [name: 'ix_posts_author_feed']
  }
}

Table reactions {
  user_id bigint [not null]
  post_id bigint [not null]
  type smallint [not null, check: `type IN (-1, 1)`]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (user_id, post_id) [pk]
    (post_id, type) [name: 'ix_reactions_post_type']
  }
}

Table bookmarks {
  user_id bigint [not null]
  topic_id bigint [not null]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (user_id, topic_id) [pk]
    (user_id, created_at [desc]) [name: 'ix_bookmarks_user_recent']
  }
}

Table reports {
  id bigint [pk, increment]
  reporter_id bigint [not null]
  post_id bigint [not null]
  reason text [not null, check: `btrim(reason) <> ''`]
  status varchar(16) [not null, default: 'pending', check: `status IN ('pending', 'accepted', 'rejected')`]
  resolved_by bigint
  created_at timestamptz [not null, default: `now()`]
  resolved_at timestamptz

  checks {
    `((status = 'pending' AND resolved_by IS NULL AND resolved_at IS NULL) OR (status IN ('accepted', 'rejected') AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL))` [name: 'chk_reports_resolution']
    `resolved_at IS NULL OR resolved_at >= created_at` [name: 'chk_reports_resolution_time']
  }

  indexes {
    (reporter_id, post_id) [unique, name: 'ux_reports_reporter_post']
    (status, created_at) [name: 'ix_reports_status_created']
  }
}

// внешние ключи
Ref: categories.id <? topics.category_id [delete: restrict]
Ref: users.id <? topics.author_id [delete: restrict]
Ref: topics.id <? posts.topic_id [delete: restrict]
Ref: users.id <? posts.author_id [delete: restrict]
Ref: users.id <? refresh_tokens.user_id [delete: restrict]
Ref: users.id <? user_bans.user_id [delete: restrict]
Ref: users.id <? user_bans.moderator_id [delete: restrict]
Ref: users.id <? reactions.user_id [delete: restrict]
Ref: posts.id <? reactions.post_id [delete: restrict]
Ref: users.id <? bookmarks.user_id [delete: restrict]
Ref: topics.id <? bookmarks.topic_id [delete: restrict]
Ref: users.id <? reports.reporter_id [delete: restrict]
Ref: posts.id <? reports.post_id [delete: restrict]
Ref: users.id ?<? topics.deleted_by [delete: restrict]
Ref: users.id ?<? posts.deleted_by [delete: restrict]
Ref: users.id ?<? reports.resolved_by [delete: restrict]
```