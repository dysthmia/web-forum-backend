include .env
export

export PROJECT_ROOT := $(CURDIR)

.PHONY: help postgres-up postgres-down postgres-logs postgres-cleanup redis-up redis-down redis-logs migrate app-build app-up app-down app-logs build test ps

help:
	@echo 'make postgres-up       - start PostgreSQL'
	@echo 'make postgres-down     - stop PostgreSQL'
	@echo 'make postgres-logs     - PostgreSQL logs'
	@echo 'make postgres-cleanup  - delete local PostgreSQL data (confirmation required)'
	@echo 'make redis-up          - start Redis'
	@echo 'make redis-down        - stop Redis'
	@echo 'make redis-logs        - Redis logs'
	@echo 'make migrate           - apply EF Core migrations once'
	@echo 'make app-build         - build the API image'
	@echo 'make app-up            - start the API and its dependencies'
	@echo 'make app-down          - stop the API'
	@echo 'make app-logs          - API logs'
	@echo 'make build             - build the API'
	@echo 'make test              - run the test project'
	@echo 'make ps                - show containers'
	@echo 'make down              - stop containers'

postgres-up:
	@mkdir -p "$(PROJECT_ROOT)/out/pgdata"
	@docker compose up -d postgres

postgres-down:
	@docker compose stop postgres

postgres-logs:
	@docker compose logs -f postgres

postgres-cleanup:
	@read -r -p 'Очистить ВСЕ данные PostgreSQL? [YES/n]: ' ans; \
	if [ "$$ans" = "YES" ]; then \
		docker compose stop postgres && \
		docker compose rm -f postgres && \
		rm -rf -- "$(PROJECT_ROOT)/out/pgdata" && \
		echo 'Данные PostgreSQL очищены'; \
	else \
		echo 'Очистка отменена'; \
	fi

redis-up:
	@docker compose up -d redis

redis-down:
	@docker compose stop redis

redis-logs:
	@docker compose logs -f redis

migrate:
	@docker compose --profile tools build migrate
	@docker compose --profile tools run --rm migrate

app-build:
	@docker compose --profile runtime build api

app-up:
	@mkdir -p "$(PROJECT_ROOT)/out/pgdata"
	@docker compose --profile runtime up -d --build api

app-down:
	@docker compose --profile runtime stop api

app-logs:
	@docker compose --profile runtime logs -f api

build:
	@dotnet build src/Forum.Api/Forum.Api.csproj

test:
	@dotnet test tests/Forum.Tests/Forum.Tests.csproj

ps:
	@docker compose ps -a

down:
	@docker compose down
