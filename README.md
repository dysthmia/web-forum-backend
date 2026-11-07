
![Логотип Fomo Forum](docs/fomo_forum_logo.png)

<h1 align="center">Fomo - Forum</h1>

<p align="center">
  <img src="https://img.shields.io/badge/C%23-171717?style=for-the-badge&logo=dotnet&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/ASP.NET_Core-171717?style=for-the-badge&logo=dotnet&logoColor=white" alt="ASP.NET Core" />
  <img src="https://img.shields.io/badge/PostgreSQL-171717?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL" />
  <img src="https://img.shields.io/badge/Redis-171717?style=for-the-badge&logo=redis&logoColor=white" alt="Redis" />
  <img src="https://img.shields.io/badge/Docker-171717?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
</p>

> REST API для тематических обсуждений на ASP.NET Core, PostgreSQL и Redis.

Web Forum предоставляет пользователей, категории, темы, сообщения, реакции, избранное и инструменты модерации. Backend работает вместе с [frontend-репозиторием](https://github.com/dysthmia/web-forum-frontend).

## Возможности

- **Пользователи и авторизация** — JWT access-токены, refresh-сессии, профили и роли.
- **Категории и темы** — создание, ответы, редактирование и soft delete.
- **Реакции и избранное** — лайки, дизлайки и сохранённые темы.
- **Полнотекстовый поиск** — русские и английские тексты через PostgreSQL.
- **Модерация** — жалобы, управление темами, временные и бессрочные блокировки.
- **Redis** — кэширование, подсчёт уникальных просмотров и rate limiting.

## Документация

| Раздел | Описание |
| --- | --- |
| [База данных](docs/database/README.md) | PostgreSQL, Redis и диаграммы keyspace/ER. |
| [Быстрый старт](docs/quick-start/README.md) | Локальный запуск, Compose, команды, конфигурация и тесты. |
| [Frontend](https://github.com/dysthmia/web-forum-frontend) | Клиентское приложение форума. |
