# Domain Model v0
### Phase 1: Shuttle3D + HybridLift + Load / Unload Stations

**Статус:** Черновик v0  
**Последнее обновление:** 2026-04-03  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, ADR-001, ADR-002, ADR-003, ADR-004

---

## 1. Назначение

`DomainModel-v0` фиксирует минимальную предметную модель, достаточную для начала реализации Phase 1 без выбранного конечного железа.

Модель не пытается заранее описать все будущие семейства устройств. Она задаёт только те сущности и инварианты, без которых нельзя писать `WES`, `WCS`, симулятор и ACL.

---

## 2. Граница модели

Phase 1 покрывает:

- `Shuttle3D`
- `HybridLift` как реализацию `VerticalCarrier`
- `LoadStation`
- `UnloadStation`
- payload transfer от зоны загрузки до зоны выгрузки
- inter-level transfer через `HybridLift`

Phase 1 не покрывает подробно:

- `DirectStorageCrane`
- `payload-only transfer mode for VerticalCarrier`
- многоместные carrier
- WMS inventory semantics

---

## 3. Основные сущности

### 3.1. Job

`Job` — макро-намерение платформы.

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

- создаётся и владеется `WES`;
- может содержать несколько `ExecutionTask`;
- не передаётся между устройствами в ходе исполнения.

### 3.2. Payload

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

- для Phase 1 платформа отслеживает custody и location только в объёме, нужном для исполнения маршрута;
- `custodyHolder` в нормальном режиме единственный.

### 3.3. ExecutionTask

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

Поддерживаемые `taskType` в Phase 1:

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
- в каждый момент времени принадлежит одному assignee.

### 3.4. Device

Базовая сущность всех активных ресурсов.

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

### 3.5. Shuttle3D

```text
Shuttle3D extends Device {
  movementMode: AUTONOMOUS | CARRIER_PASSENGER
  dispatchStatus: AVAILABLE | OCCUPIED | SUSPENDED | MAINTENANCE
  carrierId?
  carriedPayloadId?
}
```

Смысл:

- основной мобильный ресурс Phase 1;
- в `AUTONOMOUS` выполняет движение по уровневому графу;
- в `CARRIER_PASSENGER` наследует положение от vertical carrier.

### 3.6. VerticalCarrier / HybridLift

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
- в Phase 1 он перевозит шаттл вместе с удерживаемым payload;
- не владеет `Job`.

### 3.7. Station

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

- station является доменной сущностью на границе платформы;
- handoff со station всегда проходит через явный `ExecutionTask` и подтверждённый transfer.

---

## 4. Топологические сущности

### 4.1. Node

```text
Node {
  nodeId
  nodeType
  level?
}
```

Поддерживаемые `nodeType` в Phase 1:

- `TravelNode`
- `SwitchNode`
- `TransferPoint`
- `CarrierNode`
- `StationNode`
- `ChargeNode`
- `ServiceNode`

### 4.2. Edge

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

### 4.3. Reservation

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
- `EXECUTION` используется для short-horizon reservations и sliding window.

---

## 5. Операционные сущности

### 5.1. DeviceSession

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

### 5.2. Fault

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

`Fault` может приводить к деградации capabilities, quarantine topology и приостановке `ExecutionTask`.

### 5.3. CapabilitySet

```text
CapabilitySet {
  staticCapabilities[]
  activeCapabilities[]
}
```

Статические и активные capabilities разделяются. Именно `activeCapabilities` используются при выборе ресурса и исполнении.

---

## 6. Базовые инварианты модели

1. `WES` — единственный владелец `Job`.
2. `WCS` — владелец фактического исполнения `ExecutionTask`.
3. `ACL` не создаёт `Job` и `ExecutionTask`.
4. `Payload` имеет одного текущего custody holder в нормальном режиме.
5. `Shuttle3D.movementMode = CARRIER_PASSENGER` => `carrierId != null`.
6. `Shuttle3D.dispatchStatus = AVAILABLE` => у шаттла нет активного `ExecutionTask`.
7. `HybridLift.slotCount = 1` в Phase 1.
8. `occupiedShuttleId != null` => второй шаттл не может войти в тот же `HybridLift`.
9. Любой handoff между разными семействами проходит через `TransferPoint` или `StationNode` и подтверждённый transfer FSM.
10. `SwitchNode` требует эксклюзивной execution-reservation.

---

## 7. Поддерживаемые сценарии Phase 1

### 7.1. Inbound flow

```text
LoadStation -> Shuttle3D -> HybridLift -> Shuttle3D -> UnloadStation
```

### 7.2. Charge / service relocation

```text
Shuttle3D -> HybridLift -> Shuttle3D -> ChargeNode / ServiceNode
```

### 7.3. Empty repositioning

```text
Shuttle3D -> branch graph -> waiting area / transfer point
```

---

## 8. Отложенные расширения

Следующие сущности и режимы будут добавляться отдельными решениями:

- `DirectStorageCrane`
- payload-only transfer mode for `VerticalCarrier`
- multi-slot vertical carrier
- station networks с собственной внутренней orchestration-логикой
- vendor-specific дополнительные состояния, не попадающие в каноническое ядро
