# SmartWarehouse

## С чего начинать

- Для любой работы с документацией сначала читать `docs/Glossary.md`.
- При изменении документации всегда следовать правилам языка и терминологии из `docs/Glossary.md`.
- Для документации не использовать кальку, держать естественный русский синтаксис и консистентные термины.
- В аудите и рефакторинге документации игнорировать `docs/Standards/`.

## Где искать

- `eng/knowledge-map/` — лёгкий индекс репозитория, exact-first поиск и связи между артефактами.
- `docs/ArchitecturalVision.md` — архитектурные границы и инварианты.
- `docs/Architecture-Baseline-Phase-1.md` — текущий базовый состав реализации.
- `docs/ADR/` — принятые архитектурные решения.
- `docs/DomainModel-v0.md`, `docs/Execution-Semantics-v0.md`, `docs/Topology-Configuration-Model-v0.md` — доменная модель, исполнение и топология.
- `docs/Station-Site-Integration-v0.md`, `docs/Event-Catalog-v0.md`, `docs/Northbound-API-v0.md`, `docs/Contract-Acceptance-Matrix-v0.md` — интеграция и контракты.
- `docs/api/` — машиночитаемые контракты и JSON Schema.

## Структура проекта

- `src/platform-core` — основной `C#/.NET 10` контур `platform-core`.
- `src/simulation-host` — `Go`-контур симуляции и нижней интеграции.
- `src/edge-integration-host` — будущий контур интеграции оборудования.
- `src/digital-twin-ui` — будущий UI.
- `tests/` — unit, integration, contract, e2e.
- `topologies/phase1/` — фикстуры топологии.
- `deploy/local/` и `docker-compose.yml` — локальная инфраструктура.

## MCP

- `microsoftLearn` — официальный Microsoft Learn MCP для `.NET`, `ASP.NET Core`, `EF Core` и смежной документации.
- `smartwarehousePostgresRo` — read-only доступ к локальному `PostgreSQL` проекта.
