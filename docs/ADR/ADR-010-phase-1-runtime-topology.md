# ADR-010: Физическая runtime-топология `Phase 1`

**Статус:** Proposed  
**Дата:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, Station-Site-Integration-v0.md, ADR-003, ADR-004, ADR-008, ADR-009

---

## 1. Контекст

Архитектура уже фиксирует логические контуры:

- `WES Orchestration Core`;
- `WCS Execution Core`;
- контроллеры семейств ресурсов;
- `ACL Adapters`;
- `Station/Site Integration Adapter`;
- `Digital Twin`;
- контур имитационного моделирования.

При этом физическая схема `Phase 1` до настоящего момента не была зафиксирована:

- что является deployable unit;
- нужен ли отдельный runtime для edge-интеграции;
- требуется ли брокер сообщений с первого дня;
- должна ли система сразу распадаться на микросервисы.

---

## 2. Решение

### 2.1. Для `Phase 1` принимается ограниченная гибридная topology

Физическая схема `Phase 1` строится как:

- один stateful `platform-core`;
- один edge runtime для southbound- и station/site-интеграции;
- отдельный runtime симуляции;
- отдельный frontend/runtime для `Digital Twin` UI;
- минимальный набор shared infrastructure.

### 2.2. Deployable units

#### `platform-core`

Один deployable unit, внутри которого совместно развёрнуты:

- `Northbound API`;
- `WES Orchestration Core`;
- `WCS Execution Core`;
- внутренний application command/event bus;
- projection builders;
- webhook dispatcher;
- observability exporters.

Это **не означает**, что `WES` и `WCS` теряют логическую границу. Они остаются разными модулями внутри одного runtime.

#### `edge-integration-host`

Отдельный deployable unit для:

- `ACL Adapters` реальных устройств;
- модулей интеграции `Station/Site`, если их сигналы приходят из внешнего edge- или site-specific контура.

Логические роли `ACL` и `Station/Site Integration Adapter` не смешиваются, даже если они развёрнуты в одном физическом runtime.

#### `simulation-host`

Отдельный deployable unit, реализующий тот же канонический southbound-контракт, что и реальные адаптеры.

Назначение:

- воспроизводимые сценарии исполнения;
- contract+simulation тесты;
- локальная разработка без реального оборудования.

#### `digital-twin-ui`

Отдельный frontend/runtime для:

- операторских экранов;
- визуализации `Digital Twin`;
- подписки на read-model / event-stream / push-updates.

#### Shared infrastructure

Для `Phase 1` рекомендуются:

- `PostgreSQL`;
- лёгкий broker/stream transport между `platform-core` и edge/simulation runtime;
- `OpenTelemetry Collector`.

### 2.3. Границы взаимодействия

Для `Phase 1` принимаются следующие правила:

1. Внутри `platform-core` взаимодействие `WES <-> WCS` происходит через in-process application contract.
2. `Northbound API` взаимодействует с внешними системами через `REST + static webhooks`.
3. `platform-core <-> edge-integration-host` взаимодействуют через transport binding прикладных и southbound-сообщений, а не через общую БД.
4. `platform-core <-> simulation-host` взаимодействуют по тому же каноническому southbound-контракту, что и с реальным оборудованием.
5. `digital-twin-ui` не получает write-доступ к operational state ядра.

### 2.4. Single-writer правило ядра

Для `Phase 1` принимается правило **single active writer per site**:

- один активный экземпляр `platform-core` владеет write-model конкретной площадки;
- горизонтальное масштабирование `platform-core` как нескольких активных writers не является целью `Phase 1`;
- read-only scale-out допускается на уровне UI и операторских read-model, если в этом появится практическая необходимость.

Это решение снижает сложность:

- резервирований;
- transfer FSM;
- reconciliation;
- причинной упорядоченности событий.

### 2.5. Рекомендуемый transport binding между runtime

Для межпроцессного обмена `Phase 1` рекомендуется один лёгкий broker/stream transport.

Требования к нему:

- pub/sub и point-to-point для прикладных команд и событий;
- request/reply или эквивалент для операций наподобие `RequestStateSnapshot`;
- поддержка durable consumers и повторной доставки;
- лёгкий локальный запуск в контейнерах.

Выбранный рекомендуемый вариант фиксируется отдельно в `ADR-011`.

### 2.6. Что сознательно не делается в `Phase 1`

Не принимаются как обязательные:

- отдельный микросервис на каждое устройство;
- отдельный микросервис на каждое семейство ресурса;
- прямой write-доступ edge runtime в БД `platform-core`;
- обязательный Kubernetes-only deployment model;
- многорегиональная или multi-site active-active координация.

---

## 3. Последствия

### 3.1. Что упрощается

- уменьшается количество распределённых границ в самом критичном месте платформы;
- `WES` и `WCS` можно развивать как изолированные модули без раннего service split;
- edge-интеграции и симуляция получают собственный жизненный цикл и могут эволюционировать отдельно;
- локальная разработка и acceptance через симуляцию остаются реалистичными.

### 3.2. Что становится обязательным

- у каждого deployable unit должна быть чёткая ownership boundary;
- межпроцессный transport не должен подменять собой бизнес-семантику канонических контрактов;
- `platform-core` должен оставаться single-writer для operational state сайта.

---

## 4. Рассмотренные альтернативы

### Альтернатива A: Один giant process без отдельного edge runtime и без брокера

Плюсы:

- минимальная инфраструктура;
- очень быстрый старт локальной разработки.

Минусы:

- смешивает device/session code с platform core;
- затрудняет изоляцию real equipment от бизнес-логики;
- плохо готовит систему к реальным интеграциям и simulator parity.

Не выбрана.

### Альтернатива B: Полный микросервисный split `WES`, `WCS`, `ACL`, projections, twin backend`

Плюсы:

- жёсткие физические границы;
- потенциально проще независимый lifecycle сервисов в далёкой перспективе.

Минусы:

- слишком рано увеличивает число отказов, брокерных и интеграционных сценариев;
- удорожает тестирование recovery и causality;
- не соответствует целям не-оверинжинирить `Phase 1`.

Не выбрана.

### Альтернатива C: Stateful `platform-core` + отдельный edge runtime + отдельный simulator/runtime UI

Плюсы:

- хорошо соответствует логическим границам и текущему размеру команды;
- концентрирует самый сложный stateful core в одном runtime;
- сохраняет реалистичную границу между платформой и оборудованием.

Минусы:

- остаётся одна межпроцессная интеграционная граница, которую нужно поддерживать;
- требует явного транспортного решения между ядром и edge runtime.

Выбрана как рекомендуемая для `Phase 1`.
