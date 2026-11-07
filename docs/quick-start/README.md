# Быстрый старт

## Требования

- .NET SDK 10
- Docker и Docker Compose

## Локальный запуск

1. Скопируйте `.env.example` в локальный `.env` и укажите безопасные значения секретов.
2. Запустите PostgreSQL и Redis:

   ```bash
   make postgres-up
   make redis-up
   ```

3. Примените миграции:

   ```bash
   make migrate
   ```

4. Запустите API из IDE или в Docker Compose:

   ```bash
   make app-up
   ```

В development-режиме Swagger UI доступен по `/swagger`, а endpoint готовности — по `/health/ready`.

## Полезные команды

| Команда | Назначение |
| --- | --- |
| `make build` | Собрать API. |
| `make test` | Запустить тесты. |
| `make app-up` / `make app-down` | Запустить или остановить API в Compose. |
| `make ps` | Показать состояние контейнеров. |
| `make postgres-cleanup` | Удалить локальные данные PostgreSQL после подтверждения `YES`. |

## Конфигурация

Все переменные окружения — PostgreSQL, Redis, JWT, refresh-cookie, CORS, кеширование, просмотры и rate limit — описаны в [`.env.example`](../../.env.example). Локальный `.env` не хранится в репозитории.
