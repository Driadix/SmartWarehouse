# Architecture Baseline: Phase 1
### Shuttle3D + HybridLift + Load / Unload Stations

**Статус:** Базовый документ текущей реализации  
**Последнее обновление:** 2026-04-03  
**Связанные артефакты:** ArchitecturalVision.md, ADR-001, ADR-002, ADR-003, ADR-004, DomainModel-v0

---

## 1. Назначение

Настоящий документ фиксирует **текущий implementation baseline** платформы.

Он отвечает на вопросы:

- какие resource families реально поддерживаются в текущей реализации;
- какие transfer modes включены;
- какие ограничения по вместимости, topological patterns и execution semantics приняты сейчас;
- что может поддерживаться hardware capability устройства, но ещё не включено как enabled mode платформы.

Документ не заменяет `ArchitecturalVision.md`. При конфликте между baseline и архитектурным видением пересмотру подлежит либо baseline, либо отдельный ADR, но не скрытые допущения в коде.

---

## 2. Целевой склад текущей поставки

Текущий baseline ориентирован на склад следующего типа:

- `Shuttle3D` как основной mobile transport unit;
- `HybridLift` как реализация `VerticalCarrier`;
- `LoadStation` как точка входа payload в систему;
- `UnloadStation` как точка выхода payload из системы;
- `Digital Twin` и `Sandbox Simulation` как обязательные компоненты baseline.

Поставка не ориентирована на `Shuttle1D` и не требует детальной поддержки `DirectStorageCrane` на текущем этапе.

---

## 3. Enabled resource families

В baseline включены следующие семейства и concrete realizations:

- **`MobileTransportUnit` -> `Shuttle3D`**
- **`VerticalCarrier` -> `HybridLift`**
- **`StationBoundary` -> `LoadStation`, `UnloadStation`**

Поддерживаемые family controllers:

- `Shuttle3D Controller`
- `VerticalCarrier Controller`
- station-specific execution logic допускается как отдельный модуль только если station имеет активное оборудование и собственный southbound session.

---

## 4. Enabled transfer modes

### 4.1. Включённые режимы

В текущем baseline включены только следующие transfer modes:

- `LoadStation -> Shuttle3D`
- `Shuttle3D -> HybridLift -> Shuttle3D`
- `Shuttle3D -> UnloadStation`

Inter-level transfer mode фиксируется как:

`SHUTTLE_RIDES_HYBRID_LIFT_WITH_PAYLOAD`

Это означает:

- шаттл удерживает payload;
- шаттл въезжает в `HybridLift` вместе с payload;
- payload custody не передаётся lift;
- lift перевозят шаттл, а не заменяет его как holder payload в логике платформы.

### 4.2. Hardware capability vs enabled mode

`HybridLift` как тип устройства **может физически поддерживать** несколько режимов:

- перевозка пустого шаттла;
- перевозка шаттла с payload;
- перевозка payload без шаттла.

Однако в текущем baseline платформы **включён только** режим `SHUTTLE_RIDES_HYBRID_LIFT_WITH_PAYLOAD`.

Следовательно:

- physical capability устройства не равна автоматически enabled orchestration mode платформы;
- поддержка `payload-only transfer` потребует отдельного решения и обновления baseline, даже если конкретный `HybridLift` уже умеет это физически.

### 4.3. Отложенные transfer modes

В baseline не включены:

- `payload-only transfer mode for VerticalCarrier`;
- multi-shuttle carrier transfer;
- carrier-to-station direct payload transfer без участия baseline mobile transport pattern.

---

## 5. Топология baseline

Текущий baseline предполагает:

- branch/switch topology на уровне яруса;
- обязательные `SwitchNode` для развилок и поворотов;
- обязательные `TransferPoint` рядом с `CarrierNode` на каждом уровне;
- обязательные `StationNode` для `LoadStation` и `UnloadStation`;
- single-slot `HybridLift`.

Ограничения baseline:

- внутри одного `HybridLift` одновременно поддерживается не более одного шаттла;
- multi-slot vertical carrier не поддерживается;
- кран-штабелёр не участвует в текущей topology model.

---

## 6. Ownership и execution baseline

В baseline действуют общие архитектурные инварианты `ADR-003`:

- `WES` владеет `Job`;
- `WCS` владеет `ExecutionTask`;
- `ACL` ничего не оркестрирует;
- `Digital Twin` остаётся read-only.

Дополнительные baseline-specific правила:

- при inter-level transfer шаттл не становится владельцем `Job`;
- `HybridLift` не получает ownership `Job` и не получает payload custody;
- `Shuttle3D` в режиме `CARRIER_PASSENGER` не получает самостоятельных motion commands.

---

## 7. Baseline-specific state assumptions

### 7.1. Shuttle3D

```text
movementMode: AUTONOMOUS | CARRIER_PASSENGER
dispatchStatus: AVAILABLE | OCCUPIED | SUSPENDED | MAINTENANCE
```

### 7.2. HybridLift

```text
carrierKind = HYBRID_LIFT
slotCount   = 1
```

### 7.3. Недопустимые baseline combinations

- `CARRIER_PASSENGER + AVAILABLE`
- более одного шаттла в одном `HybridLift`
- payload-only transfer без отдельного transfer mode и отдельного baseline update

---

## 8. Реализация и развёртывание baseline

В текущем baseline допускается модульный монолит или ограниченное число процессов. Отдельный микросервис на каждое устройство или семейство устройства не является обязательным требованием.

Обязательны логические контуры:

- `WES Orchestration Core`
- `WCS Execution Core`
- `Family Controllers`
- `ACL Adapters`
- `Digital Twin`
- `Sandbox Simulation`

---

## 9. Что не входит в baseline

Не входит в текущий baseline:

- `DirectStorageCrane`
- `Shuttle1D`
- `payload-only transfer mode for VerticalCarrier`
- multi-slot vertical carriers
- station networks со сложной внутренней orchestration-логикой
- vendor-specific optimization paths, не выраженные через канонический southbound contract

---

## 10. Документы, определяющие baseline

Текущий baseline опирается на следующие документы:

- [ArchitecturalVision.md](/c:/Projects/SmartWarehouse/ArchitecturalVision.md)
- [ADR-001-lift-model-and-shuttle-modes.md](/c:/Projects/SmartWarehouse/docs/ADR/ADR-001-lift-model-and-shuttle-modes.md)
- [ADR-002-phase-1-scope-and-resource-families.md](/c:/Projects/SmartWarehouse/docs/ADR/ADR-002-phase-1-scope-and-resource-families.md)
- [ADR-003-wes-wcs-acl-authority-model.md](/c:/Projects/SmartWarehouse/docs/ADR/ADR-003-wes-wcs-acl-authority-model.md)
- [ADR-004-canonical-southbound-contract-v0.md](/c:/Projects/SmartWarehouse/docs/ADR/ADR-004-canonical-southbound-contract-v0.md)
- [DomainModel-v0.md](/c:/Projects/SmartWarehouse/docs/DomainModel-v0.md)
