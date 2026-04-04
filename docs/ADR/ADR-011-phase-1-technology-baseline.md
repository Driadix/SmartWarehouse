# ADR-011: Технологический baseline `Phase 1`

**Статус:** Proposed  
**Дата:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, ADR-004, ADR-008, ADR-009, ADR-010

---

## 1. Контекст

До настоящего момента в архитектуре были зафиксированы логические границы и контракты, но не был зафиксирован технологический baseline:

- какой именно backend stack считается базовым для `platform-core`;
- где проходит граница между `C#/.NET`, `Go` и `TypeScript`;
- нужны ли тяжёлые service-bus framework и broker-first архитектура с первого дня;
- какие early choices считаются запрещёнными, даже если технически возможны.

Для `Phase 1` это важно, потому что:

- команда уже неоднородна по стекам;
- экосистема `.NET` допускает слишком много разных направлений;
- ранняя неопределённость быстро превращается в несовместимые прототипы.

---

## 2. Решение

### 2.1. Язык и runtime для `platform-core`

Для `platform-core` принимается:

- `C#`
- `.NET 10 LTS`

Внутри `platform-core` базовыми runtime-компонентами считаются:

- `ASP.NET Core` для `Northbound API`;
- `Microsoft.Extensions.Hosting` / `Generic Host` для background workers, projections и delivery jobs;
- `Microsoft.Extensions.DependencyInjection` как базовый DI-контейнер `Phase 1`;
- `OpenTelemetry .NET` для трассировок, метрик и лог-экспорта.

### 2.2. Доступ к данным в `platform-core`

Для работы с `PostgreSQL` принимаются:

- `Npgsql` как базовый драйвер;
- `EF Core` как базовый механизм миграций и основная ORM для write-model и стандартных read-model;
- прямой SQL через `Npgsql` допускается для доказанных hot path `WCS`, если это подтверждено профилированием и не ломает ownership boundaries.

Для `Phase 1` **не** принимаются как baseline:

- тяжёлые actor/framework-платформы (`Orleans`, `Akka.NET` и т.п.);
- service-bus framework, скрывающий бизнес-семантику внутренних контрактов за транспортной абстракцией;
- обязательная зависимость от `Redis` как центрального operational store.

### 2.3. Edge и southbound runtime

Для `ACL adapters`, edge-интеграции и симулятора принимается:

- `Go`

Назначение этого runtime:

- работа с device sessions;
- southbound transport termination;
- wire-protocol translation;
- simulator/device emulation;
- edge/site-specific интеграции, если они логически лежат на границе оборудования.

### 2.4. UI baseline

Для `Digital Twin` и операторского frontend baseline `Phase 1`:

- `TypeScript`
- `React`

UI не является владельцем orchestration/execution логики и не должен становиться вторым backend-ядром платформы.

### 2.5. База данных

Для `Phase 1` принимается:

- `PostgreSQL`

База данных является обязательным infrastructural компонентом уже в первой версии. `SQLite` и in-memory persistence не считаются production baseline даже для первой поставки.

### 2.6. Межпроцессный transport

Для межпроцессного обмена между `platform-core` и edge/simulation runtime рекомендуемый baseline:

- `NATS JetStream`

Причины выбора:

- достаточно лёгкий для `Phase 1`;
- поддерживает pub/sub, durable consumers и request/reply;
- хорошо подходит для command/event обмена без перехода к тяжёлой Kafka-first архитектуре;
- удобен для локальной разработки в контейнерах.

При этом:

- внутри `platform-core` `NATS` не заменяет внутренний прикладной contract/bus;
- `NATS` является transport binding, а не источником бизнес-семантики.

### 2.7. Контейнеризация и локальная разработка

Для `Phase 1` обязательны:

- `OCI/Docker`-совместимые контейнерные образы;
- локальный запуск полного окружения через compose-like сценарий;
- единый способ поднятия `platform-core`, `PostgreSQL`, broker, simulator и observability stack для разработчиков.

### 2.8. Тестовый baseline

Для `platform-core` базовый стек тестирования:

- `xUnit`;
- `Testcontainers` для интеграционных тестов с реальным `PostgreSQL` и broker;
- contract tests поверх `OpenAPI`, `AsyncAPI` и JSON Schema.

Для `Go`-runtime:

- стандартный `go test`;
- интеграционные тесты с тем же broker и теми же каноническими сообщениями.

### 2.9. Явно запрещённые early choices

Для `Phase 1` не рекомендуются и не входят в baseline:

- `Kafka` как обязательный broker с первого дня;
- полный event sourcing как основная write-model;
- обязательный Kubernetes-only deployment;
- отдельный микросервис на каждое устройство;
- backend на `Node.js/TypeScript` как владелец жизненного цикла `Job` или `ExecutionTask`;
- прямой write-доступ адаптеров оборудования в БД ядра.

---

## 3. Последствия

### 3.1. Что упрощается

- основной stateful core сосредоточен в одном backend stack;
- `Go` используется там, где он действительно даёт практическую пользу на edge-границе;
- команда получает достаточно конкретный baseline по `.NET`, не уходя в ненужный framework sprawl;
- transport и persistence становятся частью baseline, а не случайным выбором первой реализации.

### 3.2. Что становится обязательным

- новые backend-модули должны либо укладываться в baseline, либо оформляться отдельным ADR;
- библиотечные исключения внутри `.NET` и `Go` должны быть явно мотивированы;
- межпроцессная интеграция должна оставаться contract-first.

---

## 4. Рассмотренные альтернативы

### Альтернатива A: Весь backend только на `C#/.NET`

Плюсы:

- минимальное число backend stack;
- проще обмен людьми внутри backend-команды.

Минусы:

- edge/runtime интеграции с wire-протоколами теряют естественную технологическую границу;
- слабее используется уже имеющаяся экспертиза команды в `Go`.

Не выбрана как базовая.

### Альтернатива B: `Go` для `WCS` и edge, `C#` только для `WES`

Плюсы:

- естественная технологическая граница на southbound стороне;
- можно использовать сильные стороны `Go` на transport edge.

Минусы:

- усложняет самый критичный stateful boundary внутри платформы;
- требует более тяжёлого межъязыкового договора между `WES` и `WCS`;
- хуже использует текущую численную ёмкость команды `C#`.

Не выбрана как baseline `Phase 1`.

### Альтернатива C: `C#/.NET` для `platform-core`, `Go` для edge/simulator, `TypeScript/React` для UI

Плюсы:

- хорошо соответствует логическим границам системы;
- уменьшает число языков в самом критичном stateful ядре;
- использует `Go` там, где он приносит максимальную пользу;
- хорошо совпадает с текущим составом команды и её масштабированием.

Минусы:

- остаются два backend stack вместо одного;
- нужны общие правила observability, contracts и release process между `C#` и `Go`.

Выбрана как рекомендуемая для `Phase 1`.
