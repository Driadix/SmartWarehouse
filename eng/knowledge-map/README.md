# SmartWarehouse Knowledge Map V0

Лёгкий индекс репозитория для exact-first поиска и будущего project MCP.

## Состав

- `artifacts.yaml` — curated артефакты и discovery-правила для повторяющихся файлов.
- `entities.yaml` — доменные, архитектурные и технологические сущности.
- `links.yaml` — связи между артефактами и сущностями.

## Поля

- `artifacts.yaml > artifacts[]`: `id`, `kind`, `path`, `authority`, `aliases`, `tags`, `sourceOfTruthFor`.
- `artifacts.yaml > discovery[]`: `kind`, `root`, `pattern`, `authority`, `idPrefix`, `tags`.
- `entities.yaml > entities[]`: `id`, `kind`, `canonical`, `aliases`, `tags`, `authoritativeArtifacts`, `relatedArtifacts`.
- `links.yaml > links[]`: `from`, `to`, `relation`.

## Принципы

- Точный поиск первичен: `id`, canonical name, alias, basename.
- Семантический слой в V0 отсутствует.
- `docs/Standards/` сознательно не индексируется.
- Главный нормативный источник для терминологии и языка: `docs/Glossary.md`.

## Команды

- `task build-knowledge-map`
- Сборка и проверка: `powershell -ExecutionPolicy Bypass -File eng/scripts/build-knowledge-map.ps1`
- Поиск: `powershell -ExecutionPolicy Bypass -File eng/scripts/search-knowledge-map.ps1 -Query "ExecutionTask"`

## Выход

Сборка создаёт `eng/knowledge-map/build/knowledge-map.json`.
