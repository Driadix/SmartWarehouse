# SmartWarehouse

Монорепозиторий платформы WES/WCS для фазы 1.

## Что в репозитории сейчас

- `platform-core` на `C#/.NET 10`
- `simulation-host` на `Go`
- архитектурные документы, ADR и машиночитаемые контракты
- локальная инфраструктура для `PostgreSQL`, `NATS JetStream`, `OpenTelemetry Collector`

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

1. `powershell -ExecutionPolicy Bypass -File eng/scripts/bootstrap.ps1`
2. `powershell -ExecutionPolicy Bypass -File eng/scripts/dev-up.ps1`
3. `.\.dotnet\dotnet.exe build SmartWarehouse.slnx`

Полезные команды:

- `powershell -ExecutionPolicy Bypass -File eng/scripts/dev-down.ps1`
- `powershell -ExecutionPolicy Bypass -File eng/scripts/validate-contracts.ps1`
- `.\.dotnet\dotnet.exe test SmartWarehouse.slnx`
- `powershell -ExecutionPolicy Bypass -File eng/scripts/test-e2e.ps1`

Альтернатива — использовать задачи из `Taskfile.yml`.
