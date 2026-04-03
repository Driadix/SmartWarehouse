# ADR-001: Hybrid lift transfer mode и shuttle-passenger state model для current baseline

**Статус:** Принято  
**Дата:** 2026-04-03  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, ADR-002, ADR-003, ADR-004, DomainModel-v0

---

## 1. Контекст

### 1.1. Phase 1 конфигурация

Phase 1 системы ориентирован на конфигурацию:

- `Shuttle3D` как основной исполнитель перемещения payload по ярусам;
- `HybridLift` как реализация семейства `VerticalCarrier`;
- `LoadStation` и `UnloadStation` как точки входа и выхода payload.

Первый поставляемый склад использует режим, в котором шаттл въезжает в `HybridLift` вместе с удерживаемым payload и покидает его на целевом уровне.

### 1.2. Проблемы, которые требуется решить

1. **Двойственная роль шаттла:** шаттл остаётся активным ресурсом исполнения, но на время нахождения внутри lift перестаёт быть автономным источником движения.
2. **Трассируемость:** Digital Twin и операционный мониторинг должны непрерывно знать местоположение шаттла во время vertical transfer.
3. **Разделение ownership:** `Job` не должен «переезжать» между устройствами во время inter-level transfer.
4. **Контроль scope:** решение для Phase 1 не должно быть ошибочно объявлено универсальной моделью для всех будущих vertical carrier или кранов-штабелёров.

---

## 2. Решение

### 2.1. Hybrid lift моделируется как Device семейства VerticalCarrier

`HybridLift` моделируется только как `Device`, а не как `Edge` и не как гибрид `Device + TransitEdge`.

Причины:

- lift имеет собственное состояние, liveness, capabilities и fault-модель;
- lift является участником handoff и исполняет `ExecutionTask`;
- lift должен быть видимым доменным ресурсом для `WCS`, `Digital Twin` и recovery-логики.

При этом в графе существуют `CarrierNode`, которые представляют точки положения vertical carrier по уровням. Эти рёбра доступны только через `VerticalCarrier`, но не превращают сам ресурс в ребро графа.

### 2.2. Phase 1 transfer mode фиксируется явно

Для Phase 1 принимается единственный inter-level transfer mode:

`SHUTTLE_RIDES_HYBRID_LIFT_WITH_PAYLOAD`

Смысл режима:

- шаттл удерживает payload;
- шаттл въезжает в `HybridLift` вместе с payload;
- во время вертикального перемещения lift является носителем шаттла;
- payload custody не передаётся lift.

Альтернативные режимы, где payload передаётся carrier без въезда шаттла, не входят в current baseline. Тот же `HybridLift` может физически поддерживать такой режим, но для платформы он будет считаться отдельным enabled transfer mode и потребует отдельного baseline update и, при необходимости, отдельного ADR.

### 2.3. Топологическая модель lift-shaft для Phase 1

Лифтовая шахта представляется набором `CarrierNode` — по одному на каждый уровень:

```text
CarrierShaft_A:
  Level_1_CarrierNode_A
  Level_2_CarrierNode_A
  Level_3_CarrierNode_A
  Level_4_CarrierNode_A
```

Рядом с каждым `CarrierNode` существует обязательный `TransferPoint`:

```text
[уровневый граф] -> [TransferPoint_Level_N_A] <-> [Level_N_CarrierNode_A]
```

`TransferPoint` является единственной доменной точкой:

- входа шаттла в lift;
- выхода шаттла из lift;
- проверки локальной готовности перед `BoardCarrier` / `ExitCarrier`.

### 2.4. Модель состояния Shuttle3D

Для Phase 1 принимается двухосевая модель состояния шаттла:

```text
Shuttle3D {
  movementMode:   AUTONOMOUS | CARRIER_PASSENGER
  dispatchStatus: AVAILABLE | OCCUPIED | SUSPENDED | MAINTENANCE
  carrierId?:     DeviceId
}
```

#### `movementMode`

- `AUTONOMOUS` — шаттл сам исполняет команды движения по ярусному графу.
- `CARRIER_PASSENGER` — шаттл находится внутри `VerticalCarrier`, не получает собственных команд движения и наследует своё положение от carrier.

#### `dispatchStatus`

- `AVAILABLE` — шаттл может принять новый `Job`.
- `OCCUPIED` — шаттл исполняет `ExecutionTask` в составе активного `Job`.
- `SUSPENDED` — исполнение приостановлено, ожидается ресурс, retry или операторское вмешательство.
- `MAINTENANCE` — шаттл выведен из рабочего пула.

#### Допустимые комбинации Phase 1

| movementMode | dispatchStatus | Сценарий |
|---|---|---|
| `AUTONOMOUS` | `AVAILABLE` | Свободный шаттл |
| `AUTONOMOUS` | `OCCUPIED` | Шаттл выполняет маршрут по ярусу |
| `AUTONOMOUS` | `SUSPENDED` | Шаттл ожидает lift / station / retry |
| `AUTONOMOUS` | `MAINTENANCE` | Шаттл направлен на сервис или зарядку |
| `CARRIER_PASSENGER` | `OCCUPIED` | Шаттл перевозится lift в составе активного Job |
| `CARRIER_PASSENGER` | `MAINTENANCE` | Шаттл перевозится carrier на сервисный уровень |

Недопустимая комбинация: `CARRIER_PASSENGER + AVAILABLE`.

### 2.5. Ownership и payload custody

Принимаются следующие правила:

- `WES` остаётся владельцем `Job` на всём протяжении inter-level transfer.
- `WCS` остаётся владельцем `ExecutionTask` и handoff state machine.
- шаттл не становится владельцем `Job`, но может оставаться текущим держателем payload custody.
- `HybridLift` не становится владельцем `Job` и не принимает payload custody в Phase 1 режиме.

Таким образом, vertical transfer не меняет доменный ownership, а только меняет carrier-context исполнения.

### 2.6. Трассируемость шаттла внутри lift

Когда `Shuttle3D.movementMode = CARRIER_PASSENGER`, используется правило:

```text
shuttle.currentNode := carrier.currentNode
```

Это означает:

- `Digital Twin` всегда знает актуальное положение шаттла;
- recovery после рестарта не требует «исчезновения» шаттла из модели;
- шаттл продолжает участвовать в контексте `Job`, но не получает автономных команд движения.

### 2.7. Handoff последовательность Phase 1

#### `BoardCarrier`

1. Шаттл прибывает в `TransferPoint` и подтверждает `ReadyToBoard`.
2. `HybridLift` прибывает в соответствующий `CarrierNode` и подтверждает `ReadyToReceive`.
3. `WCS` проверяет локи, состояние station/payload и готовность обоих участников.
4. `WCS` выдаёт commit на физический въезд.
5. После подтверждения физического въезда:
   - `shuttle.movementMode = CARRIER_PASSENGER`
   - `shuttle.carrierId = liftId`
6. Lift может начать вертикальное перемещение.

#### `ExitCarrier`

1. `HybridLift` достигает целевого `CarrierNode`.
2. `WCS` проверяет свободный `TransferPoint` целевого уровня.
3. `WCS` выдаёт commit на физический выезд.
4. После подтверждения выезда:
   - `shuttle.movementMode = AUTONOMOUS`
   - `shuttle.carrierId = null`
   - `shuttle.currentNode = targetTransferPoint`

### 2.8. Ограничение Phase 1 по вместимости lift

Для Phase 1 принимается ограничение:

- один `HybridLift` имеет **один shuttle-slot**;
- внутри lift одновременно поддерживается не более одного шаттла;
- шаттл может находиться внутри вместе со своим удерживаемым payload.

Многоместные carrier и multi-shuttle occupancy в Phase 1 не поддерживаются.

### 2.9. Граница применимости решения

Настоящее решение применимо только к Phase 1 mode `SHUTTLE_RIDES_HYBRID_LIFT_WITH_PAYLOAD`.

Оно **не утверждает**, что:

- любой future `VerticalCarrier` обязан работать по тем же правилам custody;
- любой кран-штабелёр может быть описан как `HybridLift` с иным названием;
- payload всегда должен оставаться на шаттле во всех будущих конфигурациях.

---

## 3. Последствия для смежных компонентов

### 3.1. Domain Model

- `Shuttle3D` получает `movementMode`, `dispatchStatus`, `carrierId`.
- `HybridLift` моделируется как `VerticalCarrier` с `slotCount = 1` и `occupiedShuttleId`.
- `TransferPoint` становится обязательной доменной сущностью на каждом уровне lift.

### 3.2. WES Routing

- `WES` строит сквозной payload-route через `TransferPoint` и `CarrierNode`.
- `WES` не передаёт ownership `Job` между устройствами.
- inter-level transfer остаётся частью единого `Job`.

### 3.3. WCS Execution

- `WCS` оркестрирует `BoardCarrier`, `MoveCarrier`, `ExitCarrier`.
- `WCS` несёт ответственность за перевод `movementMode` и за локальные проверки готовности.
- `WCS` не должен полагаться на wall-clock устройств как на условие handoff.

### 3.4. Digital Twin

- `Digital Twin` обязан материализовать положение lift и shuttle одновременно.
- событие вертикального перемещения обновляет currentNode carrier и всех shuttle с `carrierId = carrier.deviceId`.

### 3.5. Event Model

Phase 1 требует как минимум следующих operational events:

```text
ShuttleMovementModeChanged
VerticalCarrierPositionChanged
TransferCommitted
TransferAborted
PayloadCustodyChanged
```

---

## 4. Рассмотренные альтернативы

### Альтернатива A: Lift как Edge + Device

Отклонена. Двойная семантика усложняет ownership, recovery и handoff FSM без практической выгоды.

### Альтернатива B: Один атрибут `mode`

Отклонена. Одна ось не покрывает различие между управлением движением и доступностью ресурса для диспетчеризации.

### Альтернатива C: `Job` принадлежит шаттлу

Отклонена. Это плохо масштабируется на более сложные transfer patterns и делает `Job` слишком зависимым от конкретного семейства устройства.

### Альтернатива D: Сразу объявить Phase 1 модель универсальной для всех carrier и кранов

Отклонена. Это привело бы либо к ложной универсальности, либо к преждевременной перегрузке модели абстракциями.
