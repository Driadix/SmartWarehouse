# Каталог возможностей Phase 1
### Базовый состав: `Shuttle3D`, `HybridLift`, пассивные станции

**Статус:** Живой документ  
**Последнее обновление:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, DomainModel-v0.md, ADR-004, Execution-Semantics-v0.md

---

## 1. Назначение

Документ фиксирует минимальный каталог канонических возможностей, достаточный для:

- выбора ресурса в `WES`;
- проверки исполнимости шага в `WCS`;
- нормализации `StateSnapshot` и `CapabilityChanged`;
- отделения аппаратных возможностей устройства от реально активированного поведения платформы.

---

## 2. Правило использования

- `staticCapabilities` описывают то, что ресурс поддерживает аппаратно или на уровне локального контроллера.
- `activeCapabilities` описывают то, что разрешено и доступно платформе сейчас.
- `WES` и `WCS` принимают решения только на основе `activeCapabilities`.
- Новый режим платформы не активируется только потому, что он присутствует в `staticCapabilities`.

---

## 3. Общее ядро возможностей

Следующие возможности обязательны для всех активных устройств текущего базового состава:

| CapabilityId | Смысл |
|---|---|
| `session.lease` | Устройство поддерживает управляемый сеанс с контролем срока действия. |
| `snapshot.state` | Устройство может вернуть `StateSnapshot` для восстановления. |
| `event.nodeReached` | Устройство подтверждает достижение логического узла. |
| `event.fault` | Устройство сообщает формализованные отказы и их снятие. |
| `execution.suspendResume` | Устройство поддерживает команды `SuspendExecution` и `ResumeExecution` либо эквивалентное поведение. |

---

## 4. Возможности `Shuttle3D`

| CapabilityId | Тип | Смысл |
|---|---|---|
| `motion.windowed` | static/active | Исполнение движения по короткому окну `GrantMotionWindow`. |
| `transfer.station.passive` | static/active | Работа с пассивной станцией через `StationTransfer`. |
| `transfer.lift.hybridPassenger` | static/active | Режим `SHUTTLE_RIDES_HYBRID_LIFT_WITH_PAYLOAD`. |
| `mode.carrierPassenger` | static/active | Переход в `CARRIER_PASSENGER` и наследование положения от лифта. |
| `action.stationLocal` | optional | Поддержка `ExecuteAction`, если действие на станции выполняется самим шаттлом. |

---

## 5. Возможности `HybridLift`

| CapabilityId | Тип | Смысл |
|---|---|---|
| `motion.vertical.singleSlot` | static/active | Вертикальное перемещение с одним местом для шаттла. |
| `transfer.lift.receiveShuttle` | static/active | Приём шаттла на `CarrierNode` / `TransferPoint`. |
| `transfer.lift.dispatchShuttle` | static/active | Выдача шаттла на целевом уровне. |
| `occupancy.singleShuttle` | static/active | Поддержка не более одного шаттла одновременно. |

---

## 6. Намеренно не включённые возможности

Следующие возможности могут существовать аппаратно, но не входят в активированный базовый состав:

| CapabilityId | Причина исключения |
|---|---|
| `transfer.lift.payloadOnly` | Не активирован режим передачи груза без въезда шаттла. |
| `motion.vertical.multiSlot` | Не поддерживаются многоместные перевозчики. |
| `station.activeSession` | Пассивные станции не имеют собственного `DeviceSession`. |

---

## 7. Минимальные требования к `StateSnapshot`

Если capability активна, снимок состояния должен позволять `WCS` проверить её применимость:

- для `motion.windowed` требуется `currentNode` и `executionState`;
- для `mode.carrierPassenger` требуются `movementMode` и `carrierId`;
- для `occupancy.singleShuttle` требуется `occupiedShuttleId`;
- для `snapshot.state` требуется полный снимок по `ADR-004`.
