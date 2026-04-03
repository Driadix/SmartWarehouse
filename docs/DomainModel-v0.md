# Доменная модель v0
### Текущий базовый состав: 3D-шаттл + гибридный лифт + станции загрузки и выгрузки

**Статус:** Черновик v0  
**Последнее обновление:** 2026-04-03  
**Связанные артефакты:** Glossary, ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, ADR-001, ADR-002, ADR-003, ADR-004

---

## 1. Назначение

`DomainModel-v0` фиксирует минимальную предметную модель, достаточную для начала реализации текущего базового состава системы без выбранного конечного железа.

Модель не пытается заранее описать все будущие семейства устройств. Она задаёт только те сущности и инварианты, без которых нельзя разрабатывать `WES`, `WCS`, имитационную модель и адаптерный слой изоляции.

---

## 2. Граница модели

Текущий базовый состав покрывает:

- `Shuttle3D`;
- `HybridLift` как реализацию `VerticalCarrier`;
- `LoadStation`;
- `UnloadStation`;
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

`ExecutionTask` — атомарный шаг исполнения.

```text
ExecutionTask {
  taskId
  jobId
  assigneeType
  assigneeId
  taskType
  sourceNode?
  targetNode?
  state
  correlationId
}
```

Поддерживаемые `taskType` в текущем базовом составе:

- `Navigate`
- `LoadFromStation`
- `UnloadToStation`
- `PrepareTransfer`
- `CommitTransfer`
- `BoardCarrier`
- `MoveCarrier`
- `ExitCarrier`

Свойства:

- создаётся `WES`;
- исполняется и материализуется `WCS`;
- в каждый момент времени назначается одному исполнителю.

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

### 3.7. `Station`

```text
Station {
  stationId
  stationType: LOAD | UNLOAD
  attachedNode
  readiness: READY | BLOCKED | OFFLINE | MAINTENANCE
  bufferCapacity
}
```

Смысл:

- станция является доменной сущностью на границе платформы;
- операция передачи со станцией всегда проходит через явный `ExecutionTask` и подтверждённую фиксацию передачи.

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

`DeviceSession` материализуется в `WCS` и не принадлежит `WES`.

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

---

## 6. Базовые инварианты модели

1. `WES` — единственный владелец `Job`.
2. `WCS` — владелец фактического исполнения `ExecutionTask`.
3. `ACL` не создаёт `Job` и `ExecutionTask`.
4. `Payload` имеет одного текущего держателя в нормальном режиме.
5. `Shuttle3D.movementMode = CARRIER_PASSENGER` => `carrierId != null`.
6. `Shuttle3D.dispatchStatus = AVAILABLE` => у шаттла нет активного `ExecutionTask`.
7. `HybridLift.slotCount = 1` в текущем базовом составе.
8. `occupiedShuttleId != null` => второй шаттл не может войти в тот же `HybridLift`.
9. Любая операция передачи между разными семействами проходит через `TransferPoint` или `StationNode` и подтверждённый конечный автомат передачи.
10. `SwitchNode` требует исключительного краткосрочного резервирования.

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
Shuttle3D -> branch graph -> waiting area / transfer point
```

---

## 8. Отложенные расширения

Следующие сущности и режимы будут добавляться отдельными решениями:

- `DirectStorageCrane`;
- режим передачи грузовой единицы без въезда шаттла для `VerticalCarrier`;
- многоместный вертикальный перевозчик;
- сети станций с собственной внутренней логикой оркестрации;
- дополнительные вендорские состояния, не входящие в каноническое ядро.
