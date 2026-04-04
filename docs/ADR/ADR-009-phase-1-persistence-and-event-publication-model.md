# ADR-009: Модель хранения данных и публикации событий для `Phase 1`

**Статус:** Proposed  
**Дата:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, DomainModel-v0.md, Event-Catalog-v0.md, Topology-Configuration-Model-v0.md, Northbound-API-v0.md, ADR-003, ADR-004, ADR-008

---

## 1. Контекст

Архитектурное видение уже требует:

- хранение состояния `WES`;
- хранение состояния `WCS`;
- канонические платформенные события;
- `Transactional Outbox` или эквивалентный механизм;
- проекции для `Northbound API`, `Digital Twin` и операторского чтения;
- восстановление после сбоя за счёт снимков состояния и воспроизведения надёжно сохранённых событий.

При этом до настоящего момента не была зафиксирована прикладная модель хранения:

- какие агрегаты имеют собственное хранилище;
- должна ли система сразу переходить на полный event sourcing;
- как разделить write-model, outbox, inbox, projections и audit journal;
- должны ли разные логические контуры иметь разные физические БД уже в `Phase 1`.

---

## 2. Решение

### 2.1. Для `Phase 1` принимается state-first persistence model

Основной моделью хранения для `Phase 1` является **state-first persistence**:

- агрегаты и operational state хранятся как текущее авторитетное состояние;
- канонические платформенные события сохраняются как append-only journal и публикуются через outbox;
- полный event sourcing не является базовым механизмом хранения `Phase 1`.

### 2.2. Физическая база данных

Для `platform-core` принимается один физический экземпляр `PostgreSQL` на площадку / окружение.

В пределах этой БД используются отдельные логические схемы:

```text
config
wes
wcs
integration
projection
audit
```

Такое разделение:

- сохраняет границы владения внутри одной БД;
- упрощает транзакции и outbox в модульном монолите;
- не требует раннего распределения по нескольким БД без доказанной необходимости.

### 2.3. Авторитетные данные по схемам

#### `config`

Схема предназначена для materialized runtime-копии активной конфигурации склада, загруженной из версионируемых YAML-файлов.

Типовые таблицы:

```text
config.topology_versions
config.topology_nodes
config.topology_edges
config.topology_stations
config.topology_service_points
config.device_bindings
config.endpoint_mappings
```

#### `wes`

`WES` остаётся единственным владельцем планового и бизнес-состояния.

Типовые таблицы:

```text
wes.jobs
wes.execution_task_plans
wes.job_route_segments
wes.resource_assignments
```

#### `wcs`

`WCS` остаётся единственным владельцем runtime-состояния исполнения.

Типовые таблицы:

```text
wcs.execution_task_runtime
wcs.reservations
wcs.device_sessions
wcs.device_shadows
wcs.faults
wcs.station_boundary_state
```

#### `integration`

Схема предназначена для надёжной публикации и приёма внешних и межпроцессных сообщений.

Типовые таблицы:

```text
integration.outbox_messages
integration.inbox_messages
integration.northbound_idempotency
integration.webhook_deliveries
```

#### `projection`

Схема содержит read-model проекции, не являющиеся authoritative write-model.

Типовые таблицы:

```text
projection.payload_transfer_jobs
projection.digital_twin_devices
projection.digital_twin_payloads
projection.digital_twin_stations
projection.digital_twin_reservations
```

#### `audit`

Схема предназначена для append-only журнала канонических платформенных событий.

Типовая таблица:

```text
audit.platform_event_journal
```

### 2.4. Правила владения и записи

В `Phase 1` фиксируются следующие правила:

1. `WES` не изменяет таблицы схемы `wcs`, кроме чтения через явно определённые проекции.
2. `WCS` не изменяет таблицы схемы `wes`, кроме чтения через явно определённые прикладные команды или проекции.
3. `ACL adapters`, `Station/Site Integration Adapter`, симулятор и UI **не имеют прямого write-доступа** к БД `platform-core`.
4. Любая межмодульная координация выполняется через прикладные команды, канонические события и проекции, а не через скрытую запись в чужие таблицы.

### 2.5. Модель outbox

Для `Phase 1` принимается **единая физическая таблица** `integration.outbox_messages` с явным указанием логического владельца сообщения.

Минимальные поля:

```text
outboxId
producer: WES | WCS
messageKind: PLATFORM_EVENT | INTERNAL_COMMAND | WEBHOOK
aggregateType
aggregateId
correlationId
causationId?
payload
publishedAt?
```

Решение в пользу единой физической таблицы принято потому, что:

- `Phase 1` использует один `platform-core`;
- не требуется координация между несколькими независимыми БД;
- логическая граница сохраняется за счёт поля `producer`, а не за счёт дублирования инфраструктуры.

### 2.6. Модель inbox

`integration.inbox_messages` обязательна для всех внешних асинхронных сообщений, которые приходят в `platform-core` из:

- `ACL adapters`;
- `Station/Site Integration Adapter`;
- симулятора;
- внешнего transport/broker слоя.

Минимальные поля:

```text
inboxId
source
messageId
correlationId
receivedAt
payloadHash
handledAt?
```

Назначение `inbox`:

- идемпотентная обработка внешних событий;
- защита от повторной доставки по модели at-least-once;
- воспроизводимая диагностика обработки.

### 2.7. Projections и audit journal

Для `Phase 1` фиксируются следующие правила:

- `Northbound API` читает только из `projection.payload_transfer_jobs` или эквивалентной read-model;
- `Digital Twin` читает только из проекций схемы `projection`;
- `audit.platform_event_journal` не заменяет собой outbox и не является источником межмодульной синхронизации;
- rebuild проекций допускается из `audit.platform_event_journal` и/или надёжно сохранённых платформенных событий.

### 2.8. Что сознательно не принимается в `Phase 1`

Не принимаются как обязательные:

- полный event sourcing как основная write-model;
- отдельная физическая БД для каждого логического контура;
- прямой доступ edge-адаптеров к operational schema ядра;
- обязательный `Redis` как центральный operational state store.

---

## 3. Последствия

### 3.1. Что упрощается

- надёжная публикация канонических событий становится совместимой с модульным монолитом;
- хранение runtime-состояния и планового состояния не смешивается;
- `Northbound API` и `Digital Twin` получают явные read-model;
- миграция к отдельным процессам остаётся возможной без немедленного разрыва схемы хранения.

### 3.2. Что становится обязательным

- схема БД должна отражать границы `wes / wcs / integration / projection / audit`;
- outbox и inbox должны тестироваться на идемпотентность и повторную доставку;
- event journal должен оставаться append-only.

---

## 4. Рассмотренные альтернативы

### Альтернатива A: Полный event sourcing с самого начала

Плюсы:

- единый источник правды в виде потока событий;
- удобно для replay и rebuild.

Минусы:

- резко усложняет первую реализацию;
- увеличивает стоимость миграций и отладки;
- не требуется для текущего baseline и целевого объёма `Phase 1`.

Не выбрана.

### Альтернатива B: Отдельная физическая БД на каждый логический контур уже в `Phase 1`

Плюсы:

- очень жёсткая изоляция ownership boundaries;
- ранняя подготовка к service split.

Минусы:

- усложняет транзакционную публикацию событий;
- увеличивает количество инфраструктуры и интеграционных отказов;
- даёт мало практической пользы при рекомендуемом модульном монолите `platform-core`.

Не выбрана.

### Альтернатива C: State-first модель с одной БД, разнесённой по схемам, плюс outbox/inbox/journal

Плюсы:

- хорошо соответствует `Phase 1`;
- достаточно проста для быстрой разработки;
- сохраняет логические границы и не мешает дальнейшему выделению процессов.

Минусы:

- требует дисциплины по доступу к схемам;
- не даёт “бесплатной” физической изоляции между модулями.

Выбрана как рекомендуемая для `Phase 1`.
