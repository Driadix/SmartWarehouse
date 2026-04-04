# Каталог событий v0
### Канонические платформенные события текущего базового состава

**Статус:** Живой документ  
**Последнее обновление:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, Execution-Semantics-v0.md, Station-Site-Integration-v0.md, ADR-004, docs/api/southbound/asyncapi-v0.yaml

---

## 1. Назначение

Документ фиксирует минимальный нормативный каталог канонических событий платформы, достаточный для:

- согласованной реализации `WES`, `WCS`, `Digital Twin` и проекций операций;
- построения `Northbound API` как проекции поверх уже зафиксированных фактов;
- исключения ситуации, когда одна и та же бизнес-ситуация публикуется разными событиями в разных частях системы.

Документ описывает **канонические платформенные события**, а не сырую телеметрию и не wire-сообщения конкретного адаптера.

---

## 2. Границы каталога

Каталог включает:

- события жизненного цикла `Job`;
- события жизненного цикла `ExecutionTask`;
- события передачи и физического удержания груза;
- нормализованные операционные события `WCS`;
- канонические факты, приходящие от `Station/Site Integration Adapter`.

Каталог не включает:

- сырую телеметрию `Telemetry Firehose`;
- вендорные wire-сообщения;
- команды southbound-контракта;
- `Heartbeat` и `StateSnapshot` как транспортные сообщения нижнего контура;
- debug- и trace-события реализации.

---

## 3. Общие правила

### 3.1. Поля общего конверта платформенного события

Каждое каноническое платформенное событие должно содержать как минимум:

```text
eventId
eventName
eventVersion
occurredAt
correlationId
causationId?
visibility
payload
```

Дополнительные идентификаторы вроде `jobId`, `executionTaskId`, `payloadId`, `deviceId`, `stationId` включаются в `payload` в зависимости от типа события.

### 3.2. Visibility

- `internal` — событие используется только внутри платформы.
- `operations` — событие допустимо для операторских проекций, журналов и `Digital Twin`.
- `northbound` — событие может публиковаться внешним системам через `Northbound API`.

Если событие имеет `visibility = northbound`, это не означает автоматическую публикацию всего payload наружу; внешняя проекция может быть уже и стабильнее внутренней.

### 3.3. Порядок и идемпотентность

- причинная упорядоченность требуется в пределах агрегата-владельца;
- потребители обязаны дедуплицировать события по `eventId`;
- повторная публикация того же факта не должна менять итоговую проекцию;
- там, где это применимо, используется дополнительный семантический ключ идемпотентности, описанный в карточке события.

### 3.4. Владелец и producer

- `owner` — логический владелец факта и его бизнес-семантики;
- `producer` — компонент, который физически публикует событие на шину.

Во многих случаях `owner` и `producer` совпадают, но для нормализованных сигналов от адаптеров это не обязательно.

---

## 4. Сводная таблица

| Event | Visibility | Owner | Producer | Ключ порядка |
|---|---|---|---|---|
| `JobAccepted` | `operations`, `northbound` | `WES` | `WES` | `jobId` |
| `JobStateChanged` | `operations`, `northbound` | `WES` | `WES` | `jobId` |
| `ExecutionTaskStateChanged` | `internal`, `operations` | `WCS` | `WCS` | `executionTaskId` |
| `PayloadCustodyChanged` | `internal`, `operations` | `WCS` | `WCS` | `payloadId` |
| `TransferCommitted` | `internal`, `operations` | `WCS` | `WCS` | `correlationId` |
| `TransferAborted` | `internal`, `operations` | `WCS` | `WCS` | `correlationId` |
| `NodeReached` | `internal` | `WCS` | `WCS` | `deviceId` |
| `TransferReady` | `internal` | `WCS` | `WCS` | `correlationId` |
| `DeviceSessionLost` | `internal`, `operations` | `WCS` | `WCS` | `deviceId` |
| `CapabilityChanged` | `internal`, `operations` | `WCS` | `WCS` | `deviceId` |
| `FaultRaised` | `internal`, `operations` | `WCS` | `WCS` | `sourceId` |
| `FaultCleared` | `internal`, `operations` | `WCS` | `WCS` | `sourceId` |
| `VerticalCarrierPositionChanged` | `internal`, `operations` | `WCS` | `WCS` | `deviceId` |
| `ShuttleMovementModeChanged` | `internal`, `operations` | `WCS` | `WCS` | `deviceId` |
| `StationReadinessChanged` | `internal`, `operations` | `WCS` | `Station/Site Integration Adapter` | `stationId` |
| `StationTransferFactReported` | `internal`, `operations` | `WCS` | `Station/Site Integration Adapter` | `stationId + correlationId` |

---

## 5. Карточки событий

### 5.1. `JobAccepted`

- `visibility`: `operations`, `northbound`
- `owner`: `WES`
- `producer`: `WES`
- `consumers`: `Digital Twin`, операторские проекции, northbound-доставка
- `trigger`: `WES` принял входящий запрос и создал `Job`
- `postcondition`: `Job` существует и доступен для дальнейшего отслеживания
- `correlation`: `correlationId = jobId`
- `idempotency`: повторная публикация с тем же `jobId` не должна создавать второй `Job`
- `payload`:

```text
{
  jobId
  clientOrderId
  jobType
  sourceEndpoint
  targetEndpoint
  state
  priority
}
```

### 5.2. `JobStateChanged`

- `visibility`: `operations`, `northbound`
- `owner`: `WES`
- `producer`: `WES`
- `consumers`: `Digital Twin`, northbound-доставка, операторские проекции
- `trigger`: подтверждённый переход состояния `Job`
- `postcondition`: состояние `Job` обновлено в модели `WES`
- `correlation`: `correlationId = jobId`
- `idempotency`: дубликат одного и того же перехода не меняет проекцию
- `payload`:

```text
{
  jobId
  previousState
  newState
  reasonCode?
  activeExecutionTaskId?
}
```

### 5.3. `ExecutionTaskStateChanged`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: `Digital Twin`, операторские проекции, локальные проекции состояния
- `trigger`: подтверждённый переход состояния `ExecutionTask`
- `postcondition`: текущее состояние шага синхронизировано в операционной модели
- `correlation`: `correlationId = executionTask.correlationId`
- `idempotency`: дубликат одного и того же перехода по `executionTaskId + newState` не меняет проекцию
- `payload`:

```text
{
  executionTaskId
  jobId
  taskType
  assigneeId
  previousState
  newState
  sourceNode?
  targetNode?
  transferMode?
}
```

### 5.4. `PayloadCustodyChanged`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: `Digital Twin`, аудит исполнения, проекция состояния груза
- `trigger`: подтверждённый факт передачи или загрузки/выгрузки
- `postcondition`: у `Payload` обновлён текущий физический держатель
- `correlation`: `correlationId = executionTask.correlationId`
- `idempotency`: повторная публикация того же перехода `from -> to` не должна дублировать смену holder
- `payload`:

```text
{
  payloadId
  previousHolderType
  previousHolderId
  newHolderType
  newHolderId
}
```

### 5.5. `TransferCommitted`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: `Digital Twin`, аудит, проекция операций передачи
- `trigger`: конечный автомат передачи достиг подтверждённого commit
- `postcondition`: операция передачи завершена успешно
- `correlation`: `correlationId = executionTask.correlationId`
- `idempotency`: один `correlationId` должен иметь не более одного итогового commit
- `payload`:

```text
{
  executionTaskId
  transferMode
  transferPointId?
  participants[]
}
```

### 5.6. `TransferAborted`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: `Digital Twin`, аудит, операторская консоль
- `trigger`: конечный автомат передачи достиг подтверждённого abort
- `postcondition`: операция передачи зафиксирована как незавершённая и безопасно остановленная
- `correlation`: `correlationId = executionTask.correlationId`
- `idempotency`: один `correlationId` должен иметь не более одного итогового abort
- `payload`:

```text
{
  executionTaskId
  transferMode
  transferPointId?
  reasonCode
}
```

### 5.7. `NodeReached`

- `visibility`: `internal`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: конечный автомат материализации, логика восстановления, `Digital Twin`
- `trigger`: подтверждённый факт достижения логического узла устройством
- `postcondition`: `currentNode` устройства обновлён
- `correlation`: `correlationId = active execution correlation`
- `idempotency`: дубликат того же достижения узла в рамках того же активного шага не меняет итоговую проекцию
- `payload`:

```text
{
  deviceId
  family
  nodeId
}
```

### 5.8. `TransferReady`

- `visibility`: `internal`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: transfer FSM, recovery logic
- `trigger`: участник операции передачи подтвердил локальную готовность
- `postcondition`: соответствующая runtime-phase может перейти к следующей проверке
- `correlation`: `correlationId = executionTask.correlationId`
- `idempotency`: повторная готовность по тому же `transferPointId + role + correlationId` не меняет FSM
- `payload`:

```text
{
  deviceId
  transferPointId
  role
}
```

### 5.9. `DeviceSessionLost`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: логика восстановления, операторская консоль, `Digital Twin`
- `trigger`: истёк lease или зафиксирована потеря управляемого сеанса
- `postcondition`: ресурс выведен из нормального контура исполнения, связанный шаг приостановлен
- `correlation`: `correlationId = active execution correlation`, если шаг активен
- `idempotency`: повтор по тому же `deviceId + sessionId` не меняет состояние после первого suspend
- `payload`:

```text
{
  deviceId
  family
  lostSessionId
  activeExecutionTaskId?
}
```

### 5.10. `CapabilityChanged`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: планировочные проекции, операторская консоль, `Digital Twin`
- `trigger`: изменилась активная применимость возможностей ресурса
- `postcondition`: `activeCapabilities` обновлены в проекции устройства
- `correlation`: `correlationId = deviceId`
- `idempotency`: для `deviceId` действует правило "последнее состояние побеждает"
- `payload`:

```text
{
  deviceId
  previousActiveCapabilities[]
  activeCapabilities[]
}
```

### 5.11. `FaultRaised`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: логика карантина, операторская консоль, `Digital Twin`
- `trigger`: подтверждён новый активный отказ
- `postcondition`: отказ зафиксирован в операционной модели
- `correlation`: `correlationId = sourceId`
- `idempotency`: повтор по `sourceId + faultCode + ACTIVE` не должен дублировать fault
- `payload`:

```text
{
  sourceType
  sourceId
  faultCode
  severity
}
```

### 5.12. `FaultCleared`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: логика карантина, операторская консоль, `Digital Twin`
- `trigger`: ранее активный отказ подтверждённо снят
- `postcondition`: fault переходит в cleared-state
- `correlation`: `correlationId = sourceId`
- `idempotency`: повтор по `sourceId + faultCode + CLEARED` не меняет модель
- `payload`:

```text
{
  sourceType
  sourceId
  faultCode
}
```

### 5.13. `VerticalCarrierPositionChanged`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: `Digital Twin`, transfer FSM, recovery logic
- `trigger`: лифт подтверждённо достиг нового `CarrierNode`
- `postcondition`: положение `VerticalCarrier` и зависимых shuttle-passenger проекций обновлено
- `correlation`: `correlationId = active execution correlation`
- `idempotency`: повтор того же `carrierNode` подряд не меняет проекцию
- `payload`:

```text
{
  deviceId
  previousCarrierNode?
  currentCarrierNode
}
```

### 5.14. `ShuttleMovementModeChanged`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `WCS`
- `consumers`: `Digital Twin`, логика восстановления, операторская консоль
- `trigger`: шаттл перешёл между `AUTONOMOUS` и `CARRIER_PASSENGER`
- `postcondition`: обновлены `movementMode` и связанный `carrierId`
- `correlation`: `correlationId = executionTask.correlationId`
- `idempotency`: дубликат того же перехода `previousMode -> newMode` не меняет модель
- `payload`:

```text
{
  deviceId
  previousMode
  newMode
  carrierId?
}
```

### 5.15. `StationReadinessChanged`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `Station/Site Integration Adapter`
- `consumers`: логика передачи `WCS`, операторская консоль, `Digital Twin`
- `trigger`: адаптер получил новый подтверждённый факт о готовности пассивной станции
- `postcondition`: `StationBoundary.readiness` обновлена
- `correlation`: `correlationId = stationId`
- `idempotency`: для `stationId` действует правило "последнее состояние побеждает"
- `payload`:

```text
{
  stationId
  readiness
  reason?
}
```

### 5.16. `StationTransferFactReported`

- `visibility`: `internal`, `operations`
- `owner`: `WCS`
- `producer`: `Station/Site Integration Adapter`
- `consumers`: логика передачи `WCS`, аудит, операторская консоль
- `trigger`: адаптер получил подтверждённый факт передачи на границе пассивной станции
- `postcondition`: `WCS` может публиковать `PayloadCustodyChanged` и завершать `StationTransfer`
- `correlation`: `correlationId = executionTask.correlationId`, если он известен; иначе связывание выполняется по текущему контексту станции
- `idempotency`: повтор по `stationId + direction + correlationId + factType` не меняет модель
- `payload`:

```text
{
  stationId
  direction
  factType
  payloadId?
  correlationId?
}
```

---

## 6. Что не публикуется как northbound v0

Следующие события остаются внутренними и не должны напрямую становиться внешним API:

- `NodeReached`
- `TransferReady`
- `CapabilityChanged`
- `FaultRaised`
- `FaultCleared`
- `VerticalCarrierPositionChanged`
- `ShuttleMovementModeChanged`
- `StationReadinessChanged`
- `StationTransferFactReported`

Их можно использовать для операторских проекций, но не как стабильный бизнес-контракт интеграции с WMS.

---

## 7. Что сознательно отложено

- машиночитаемая спецификация платформенных событий верхнего уровня;
- отдельные northbound payload-варианты поверх внутренних событий;
- event versioning policy beyond `v0`;
- расширенный каталог причин `reasonCode`;
- события административного и операторского управления вне базового контура исполнения.
