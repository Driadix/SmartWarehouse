# SmartWarehouse

Монорепозиторий платформы WES/WCS для фазы 1.

## Что в репозитории сейчас

- `platform-core` на `C#/.NET 10`
- `simulation-host` на `Go`
- архитектурные документы, ADR и машиночитаемые контракты
- локальная инфраструктура для `PostgreSQL`, `NATS JetStream`, `OpenTelemetry Collector`, `Prometheus`, `Loki`, `Tempo`, `Grafana`

Кодовая база пока на раннем этапе: архитектура и контракты уже зафиксированы, реализация `platform-core` и симуляционного контура только разворачивается.

## Ключевые каталоги

- `docs/` — архитектурные документы, глоссарий, ADR, контракты
- `src/platform-core/` — основной серверный контур
- `src/simulation-host/` — симуляция и нижняя интеграция
- `tests/` — unit, integration, contract, e2e
- `topologies/phase1/` — фикстуры топологии
- `deploy/local/` — локальные конфигурации инфраструктуры
- `eng/` — bootstrap, scripts, проверка контрактов

## С чего начинать

- `docs/Glossary.md` — правила терминологии и оформления документации
- `docs/ArchitecturalVision.md` — архитектурные границы и инварианты
- `docs/Architecture-Baseline-Phase-1.md` — текущий базовый состав реализации
- `docs/ADR/` — принятые решения по архитектуре и стеку

## Локальный запуск

1. Скопировать `.env.example` в `.env`, если требуется переопределить порты или учётные данные для локального стенда.
2. Выполнить `powershell -ExecutionPolicy Bypass -File eng/scripts/bootstrap.ps1`
3. Выполнить `powershell -ExecutionPolicy Bypass -File eng/scripts/dev-up.ps1`
4. Для полного контейнерного стенда с приложениями выполнить `powershell -ExecutionPolicy Bypass -File eng/scripts/dev-up.ps1 -IncludeApps`
5. Для запуска из IDE или терминала использовать `powershell -ExecutionPolicy Bypass -File eng/scripts/dotnet.ps1 build SmartWarehouse.slnx`

Полезные команды:

- `powershell -ExecutionPolicy Bypass -File eng/scripts/dev-down.ps1`
- `powershell -ExecutionPolicy Bypass -File eng/scripts/dev-down.ps1 -RemoveVolumes`
- `powershell -ExecutionPolicy Bypass -File eng/scripts/validate-contracts.ps1`
- `powershell -ExecutionPolicy Bypass -File eng/scripts/dotnet.ps1 test SmartWarehouse.slnx`
- `powershell -ExecutionPolicy Bypass -File eng/scripts/test-e2e.ps1`
- `docker compose --env-file .env.example --profile infra --profile observability --profile apps config`

Профили Compose:

- `infra` — `PostgreSQL` и `NATS`
- `observability` — `OpenTelemetry Collector`, `Prometheus`, `Loki`, `Tempo`, `Grafana`
- `apps` — `platform-core`, `db-migrator`, `simulation-host`

Основные локальные адреса по умолчанию:

- `platform-core`: `http://localhost:8080`
- `simulation-host`: `http://localhost:8090`
- `Grafana`: `http://localhost:3300`
- `Prometheus`: `http://localhost:9091`

`Taskfile.yml` использует `pwsh`, поэтому для кроссплатформенного запуска требуется `PowerShell 7+`.
