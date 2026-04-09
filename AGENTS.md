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

- `microsoftLearn` — официальный Microsoft Learn MCP для `.NET`, `ASP.NET Core`, `EF Core` и смежной документации. Используй как можно чаще при написании С# кода, планировании архитектуры, внесения изменений.
- `smartwarehousePostgresRo` — read-only доступ к локальному `PostgreSQL` проекта. Используй для проверки и отладки таблиц и данных.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.