# ADR-002: Current baseline scope, ресурсные семейства и enabled transfer modes

**Статус:** Принято  
**Дата:** 2026-04-03  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, ADR-001, ADR-003, ADR-004, DomainModel-v0

---

## 1. Контекст

Команда должна начать разработку без реального набора конечных устройств, но с понятной целевой конфигурацией первой поставки. Основной риск — смешать текущий implementation baseline с архитектурными инвариантами платформы.

Для старта нужен baseline, который:

- покрывает первый поставляемый склад;
- проверяет ключевые механики платформы;
- не фиксирует лишние будущие семейства как обязательные для текущей реализации;
- не маскирует hardware capability устройства под автоматически включённый orchestration mode.

---

## 2. Решение

### 2.1. Current baseline resource families

В текущий baseline включены следующие семейства и concrete realizations:

- **`MobileTransportUnit` -> `Shuttle3D`**
- **`VerticalCarrier` -> `HybridLift`**
- **`StationBoundary` -> `LoadStation`, `UnloadStation`**

### 2.2. Resource families, не входящие в baseline

В текущий baseline не входят:

- `Shuttle1D`
- `DirectStorageCrane`
- multi-slot vertical carriers
- сложные station networks с собственной внутренней orchestration-логикой

### 2.3. Topology patterns baseline

Current baseline обязан поддерживать:

- уровневые графы с ветвлениями и поворотами;
- `SwitchNode` как узел, требующий эксклюзивного short reservation;
- `TransferPoint` рядом с `CarrierNode`;
- `StationNode` для зон загрузки и выгрузки.

Таким образом, baseline не ограничивается линейным рельсовым сценарием и сразу покрывает `Shuttle3D`.

### 2.4. Enabled transfer modes baseline

В baseline включены только следующие transfer modes:

- `LoadStation -> Shuttle3D`
- `Shuttle3D -> HybridLift -> Shuttle3D`
- `Shuttle3D -> UnloadStation`

Inter-level transfer mode фиксируется как:

`SHUTTLE_RIDES_HYBRID_LIFT_WITH_PAYLOAD`

### 2.5. Hardware capability vs enabled transfer mode

Для current baseline принимается правило:

- hardware capability устройства и enabled transfer mode платформы — не одно и то же;
- `HybridLift` может физически поддерживать payload-only transfer, но это не означает автоматической поддержки этого режима платформой;
- включение нового transfer mode требует отдельного baseline update и, при необходимости, нового ADR.

### 2.6. Transfer modes, не входящие в baseline

В baseline не включены:

- `payload-only transfer mode for VerticalCarrier`;
- multi-shuttle carrier transfer;
- carrier-to-station direct payload transfer без baseline mobile transport pattern.

### 2.7. Правило расширения архитектуры

Добавление нового семейства или нового transfer mode допустимо только при выполнении трёх условий:

1. сохраняется ownership model `WES / WCS / ACL`;
2. появляется семейственная execution semantics и, при необходимости, новый `Family Controller`;
3. baseline обновляется явно, а не скрыто через допущения в коде.

---

## 3. Последствия

### 3.1. Что становится возможным уже сейчас

Current baseline достаточен, чтобы начать разработку:

- topology compiler;
- route planning по branch-графу;
- `WCS Execution Core`;
- family controllers для `Shuttle3D` и `VerticalCarrier`;
- симулятор южного контура;
- `Digital Twin`;
- end-to-end сценарии от `LoadStation` до `UnloadStation`.

### 3.2. Что сознательно откладывается

Не требуется сейчас:

- проектировать storage-семантику `DirectStorageCrane`;
- поддерживать `payload-only transfer mode for VerticalCarrier`;
- вводить универсальные абстракции для всех возможных будущих семейств ресурсов.

### 3.3. Что должно остаться расширяемым

В то же время current baseline не должен зашивать следующие жёсткие инварианты:

- что любой vertical transport всегда есть lift;
- что payload всегда принадлежит mobile transport unit во всех будущих режимах;
- что любая station всегда пассивна;
- что вся платформа обязана физически быть набором отдельных микросервисов.

---

## 4. Рассмотренные альтернативы

### Альтернатива A: Ограничить baseline линейным shuttle-only сценарием

Отклонена. Это не покрывает реальную поставку и не проверяет самые рисковые механики inter-level orchestration.

### Альтернатива B: Сразу детально описать crane family

Отклонена. Сейчас это создаст объём абстракций, не дающих ценности для первого поставляемого склада.

### Альтернатива C: Вообще не фиксировать baseline, а только capabilities

Отклонена. Для старта реализации это слишком размыто и не даёт однозначных execution semantics.
