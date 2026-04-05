# SmartWarehouse

Стартовый монорепозиторий для платформы WES/WCS фазы 1.

Текущее решение следует базовому составу из документации:

- `platform-core` на `C#/.NET 10`
- `simulation-host` на `Go`
- резерв под `edge-integration-host`
- резерв под `digital-twin-ui`

## Структура

- `src/platform-core` — ядро платформы, `Northbound API`, `WES`, `WCS`, проекции и инфраструктура
- `src/simulation-host` — симулятор нижнего контура
- `src/edge-integration-host` — будущий контур реальных `ACL` и site integration
- `src/digital-twin-ui` — будущий пользовательский интерфейс
- `tests` — unit, integration, contract и e2e-тесты
- `topologies/phase1` — версионируемые фикстуры конфигурации склада
- `eng` — bootstrap, локальная автоматизация и tooling для проверки контрактов
- `deploy/local` — локальные конфигурации инфраструктуры

## Быстрый старт

1. `powershell -ExecutionPolicy Bypass -File eng/scripts/bootstrap.ps1`
2. `docker compose --env-file .env.example up -d`
3. `.\.dotnet\dotnet.exe build SmartWarehouse.slnx`

Для `.NET 10` текущий CLI создаёт решение в формате `slnx`. Если потребуется совместимость со старым tooling, решение можно дополнительно сгенерировать в формате `.sln`.
