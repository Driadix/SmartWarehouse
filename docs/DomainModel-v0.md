# Доменная модель v0
### Текущий базовый состав: 3D-шаттл + гибридный лифт + станции загрузки и выгрузки

**Статус:** Черновик v0<br>
**Последнее обновление:** 2026-04-05<br>
**Связанные артефакты:** Glossary, ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, Topology-Configuration-Model-v0.md, Execution-Semantics-v0.md, Station-Site-Integration-v0.md, Capability-Catalog-Phase-1.md, ADR-001, ADR-002, ADR-003, ADR-004, ADR-005, ADR-006, ADR-007

---

## 1. Назначение

`DomainModel-v0` фиксирует минимальную предметную модель, достаточную, чтобы начать реализацию текущего базового состава системы до выбора конкретного оборудования.

Модель не пытается заранее описать все будущие семейства устройств. Она задаёт только те сущности и инварианты, без которых невозможно разрабатывать `WES`, `WCS`, имитационную модель и адаптерный слой изоляции.

---

## 2. Граница модели

Текущий базовый состав охватывает:

- `Shuttle3D`;
- `HybridLift` как реализацию `VerticalCarrier`;
- пассивную `LoadStation`;
- пассивную `UnloadStation`;
- передачу грузовой единицы от зоны загрузки до зоны выгрузки;
- межуровневую передачу через `HybridLift`.

Текущий базовый состав не описывает подробно:

- `DirectStorageCrane`;
- режим передачи грузовой единицы без въезда шаттла для `VerticalCarrier`;
- многоместные вертикальные перевозчики;
- семантику складского учёта уровня WMS.

---

## 3. Основные сущности

### 3.1. `Job`

`Job` — задание верхнего уровня, выражающее макронамерение платформы.

```text
Job {
  jobId
  jobType: PAYLOAD_TRANSFER | CHARGE | RELOCATION
  payloadId?
  sourceEndpoint
  targetEndpoint
  state
  priority
  plannedRoute
}
```

Свойства:

- создаётся и поддерживается `WES`;
- может содержать несколько `ExecutionTask`;
- не передаётся между устройствами в ходе исполнения.

### 3.2. `Payload`

`Payload` — физическая единица груза.

```text
Payload {
  payloadId
  payloadKind
  dimensions
  weight
  custodyHolderType
  custodyHolderId
}
```

Свойства:

- платформа отслеживает местонахождение и физическое удержание груза в объёме, необходимом для исполнения маршрута;
- в нормальном режиме у грузовой единицы один текущий держатель.

### 3.3. `ExecutionTask`

`ExecutionTask` — плановый макрошаг исполнения.

```text
ExecutionTask {
  taskId
  jobId
  assigneeType
  assigneeId
  participantRefs[]
  taskType
  sourceNode?
  targetNode?
  transferMode?
  state
  correlationId
}
```

Поддерживаемые `taskType` в текущем базовом составе:

- `Navigate`
- `StationTransfer`
- `CarrierTransfer`

Свойства:

- создаётся `WES` как плановый макрошаг;
- `assigneeId` указывает на основной ресурс исполнения, а `participantRefs` фиксирует обязательных участников операции;
- исполняется контуром `WCS`, который материализует его во внутренние фазы исполнения и канонические команды нижнего уровня;
- для `CarrierTransfer` внутренние фазы исполнения могут включать `PrepareTransfer`, `BoardCarrier`, `MoveCarrier`, `ExitCarrier`, `CommitTransfer`, `AbortTransfer`;
- внутренние фазы исполнения `WCS` не являются самостоятельными плановыми `taskType` уровня `WES`.

### 3.4. `Device`

`Device` — базовая сущность всех активных ресурсов.

```text
Device {
  deviceId
  family
  currentNode
  healthState
  activeCapabilities
  executionState
}
```

Смысл:

- `Device` описывает активный управляемый ресурс нижнего контура;
- именно для `Device` применяются команды канонического нижнего контракта, `DeviceSession` и события уровня устройства;
- `Device` не заменяет ни `StationBoundary`, ни топологический `Node`.

### 3.5. `Shuttle3D`

```text
Shuttle3D extends Device {
  movementMode: AUTONOMOUS | CARRIER_PASSENGER
  dispatchStatus: AVAILABLE | OCCUPIED | SUSPENDED | MAINTENANCE
  carrierId?
  carriedPayloadId?
}
```

Смысл:

- основной мобильный ресурс текущего базового состава;
- в режиме `AUTONOMOUS` выполняет движение по уровневому графу;
- в режиме `CARRIER_PASSENGER` наследует своё положение от вертикального перевозчика.

### 3.6. `VerticalCarrier` / `HybridLift`

```text
VerticalCarrier extends Device {
  carrierKind
  slotCount
  occupiedShuttleId?
}

HybridLift extends VerticalCarrier {
  carrierKind = HYBRID_LIFT
  slotCount = 1
}
```

Смысл:

- вертикальный перевозчик является активным ресурсом;
- в текущем базовом составе он перевозит шаттл вместе с удерживаемой им грузовой единицей;
- не владеет `Job`.

### 3.7. `StationBoundary` / `LoadStation` / `UnloadStation`

```text
StationBoundary {
  stationId
  stationType: LOAD | UNLOAD
  attachedNode
  controlMode: PASSIVE | ACTIVE
  readiness: READY | BLOCKED | OFFLINE | MAINTENANCE
  bufferCapacity
}
```

Смысл:

- `StationBoundary` является доменной сущностью на границе платформы и описывает семантику передачи, а не инвентарный состав оборудования;
- `StationBoundary` не совпадает ни с `Device`, ни с `StationNode`;
- в текущем базовом составе поддерживается только `controlMode = PASSIVE`;
- `controlMode` описывает способ взаимодействия платформы с границей станции, а не классифицирует физическое оборудование площадки;
- пассивная граница станции не имеет собственного `DeviceSession` и не требует отдельного контроллера семейства ресурсов;
- готовность пассивной станции и факты передачи на её границе поступают через `Station/Site Integration Adapter`;
- `NodeReached(attachedNode)` подтверждает позиционирование ресурса на границе станции;
- операция передачи со станцией всегда проходит через явный `ExecutionTask` и подтверждённую фиксацию передачи;
- для загрузки и выгрузки одного `NodeReached` недостаточно: завершение требует подтверждённой смены `Payload.custodyHolder`.

Активная граница станции допускается как отложенное расширение: в этом случае сама граница станции остаётся отдельной доменной сущностью, а обслуживающее её активное оборудование моделируется отдельно как `Device`.

---

## 4. Топологические сущности

### 4.1. `Node`

```text
Node {
  nodeId
  nodeType
  level?
}
```

Поддерживаемые `nodeType` в текущем базовом составе:

- `TravelNode`
- `SwitchNode`
- `TransferPoint`
- `CarrierNode`
- `StationNode`
- `ChargeNode`
- `ServiceNode`

Смысл специальных узлов текущего базового состава:

- `StationNode` — узел примыкания `StationBoundary`;
- `ChargeNode` — пассивная сервисная точка зарядки, не являющаяся `StationBoundary`;
- `ServiceNode` — пассивная сервисная точка обслуживания, не являющаяся `StationBoundary`.

Топологические узлы описывают места позиционирования и передачи, но не заменяют активные устройства и не описывают физический состав оборудования площадки.

### 4.2. `Edge`

```text
Edge {
  edgeId
  fromNode
  toNode
  traversalMode
  weight
}
```

Поддерживаемые `traversalMode`:

- `OPEN`
- `CARRIER_ONLY`
- `RESTRICTED`

### 4.3. `Reservation`

```text
Reservation {
  reservationId
  ownerType
  ownerId
  nodes[]
  horizon: PLAN | EXECUTION
  state
}
```

Смысл:

- `PLAN` используется как плановый контекст маршрута;
- `EXECUTION` используется для краткосрочных резервирований и скользящего окна резервирования.

Структура конфигурации топологии и связей между `Node`, `Edge`, станциями, шахтами и сервисными точками задаётся в [docs/Topology-Configuration-Model-v0.md](/c:/Projects/SmartWarehouse/docs/Topology-Configuration-Model-v0.md).

---

## 5. Операционные сущности

### 5.1. `DeviceSession`

```text
DeviceSession {
  sessionId
  deviceId
  state
  leaseUntil
  lastHeartbeatAt
}
```

`DeviceSession` ведётся в `WCS` и не принадлежит `WES`.

Потеря активного `DeviceSession` во время исполнения переводит связанный `ExecutionTask` в `Suspended` до повторного согласования по `StateSnapshot`.

### 5.2. `Fault`

```text
Fault {
  faultId
  sourceType
  sourceId
  faultCode
  severity
  state
}
```

`Fault` может приводить к деградации возможностей, карантинной изоляции участков топологии и приостановке `ExecutionTask`.

### 5.3. `CapabilitySet`

```text
CapabilitySet {
  staticCapabilities[]
  activeCapabilities[]
}
```

Статические и активные возможности разделяются. Именно `activeCapabilities` используются при выборе ресурса и исполнении.

Канонический состав возможностей текущего базового состава определяется в [docs/Capability-Catalog-Phase-1.md](/c:/Projects/SmartWarehouse/docs/Capability-Catalog-Phase-1.md).

---

## 6. Базовые инварианты модели

1. `WES` — единственный владелец `Job`.
2. `WCS` — владелец фактического исполнения `ExecutionTask`.
3. `ACL` не создаёт `Job` и `ExecutionTask`.
4. `Payload` имеет одного текущего держателя в нормальном режиме.
5. `Shuttle3D.movementMode = CARRIER_PASSENGER` => `carrierId != null`.
6. `Shuttle3D.movementMode = CARRIER_PASSENGER` => `Shuttle3D.currentNode` наследуется от текущего `VerticalCarrier.currentNode`.
7. `Shuttle3D.dispatchStatus = AVAILABLE` => у шаттла нет активного `ExecutionTask`.
8. `HybridLift.slotCount = 1` в текущем базовом составе.
9. `occupiedShuttleId != null` => второй шаттл не может войти в тот же `HybridLift`.
10. В текущем базовом составе используется только `StationBoundary.controlMode = PASSIVE`.
11. `StationBoundary` является смысловой границей передачи и не подменяет `Device` или `StationNode`.
12. `StationBoundary.controlMode` описывает способ взаимодействия платформы с границей станции, а не принадлежность физического оборудования к семейству ресурсов.
13. `CarrierTransfer` планируется в `WES` как макрошаг, а внутренние фазы исполнения `BoardCarrier` / `MoveCarrier` / `ExitCarrier` материализуются только внутри `WCS`.
14. Любая операция передачи между разными семействами проходит через `TransferPoint` или `StationNode` и подтверждённый конечный автомат передачи.
15. `SwitchNode` требует исключительного краткосрочного резервирования.

---

## 7. Поддерживаемые сценарии текущего базового состава

### 7.1. Входной поток

```text
LoadStation -> Shuttle3D -> HybridLift -> Shuttle3D -> UnloadStation
```

### 7.2. Перемещение на зарядку или сервис

```text
Shuttle3D -> HybridLift -> Shuttle3D -> ChargeNode / ServiceNode
```

### 7.3. Перераспределение пустого ресурса

```text
Shuttle3D -> граф с ветвлениями -> зона ожидания / TransferPoint
```

---

## 8. Отложенные расширения

Следующие сущности и режимы будут добавляться отдельными решениями:

- `DirectStorageCrane`;
- режим передачи грузовой единицы без въезда шаттла для `VerticalCarrier`;
- многоместный вертикальный перевозчик;
- активные границы станции, обслуживаемые активным оборудованием;
- сети станций с собственной внутренней логикой оркестрации;
- дополнительные состояния конкретного поставщика, не входящие в каноническое ядро.
