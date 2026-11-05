# Web Forum API

ASP.NET Core API форума с PostgreSQL 16, Redis, JWT access-токенами и refresh-сессией в HttpOnly cookie.

## Требования

- .NET SDK 10
- Docker и Docker Compose

## Быстрый старт

1. Скопируйте `.env.example` в локальный `.env` и задайте безопасные значения секретов.
2. Запустите хранилища: `make postgres-up` и `make redis-up`.
3. Примените миграции: `make migrate`.
4. Запустите API из IDE или полностью через Compose: `make app-up`.

API в development-режиме публикует Swagger UI по `/swagger`. Проверка готовности доступна по `/health/ready`.

## Основные команды

- `make build` — сборка API.
- `make test` — запуск тестов.
- `make app-up` / `make app-down` — запуск и остановка API в Compose.
- `make ps` — статус контейнеров.
- `make postgres-cleanup` — удаление локальных данных PostgreSQL после явного подтверждения `YES`.

## Конфигурация

Все параметры окружения, включая PostgreSQL, Redis, JWT, cookie refresh-сессии, CORS, кеширование, подсчёт просмотров и rate limit, документированы в `.env.example`. Файл `.env` не хранится в репозитории.

## Тесты

Тестовый проект расположен в `tests/Forum.Tests`. Он запускается локальным .NET SDK командой `make test` или `dotnet test tests/Forum.Tests/Forum.Tests.csproj`.
