# Каталог событий v0
### Канонические платформенные события текущего базового состава

**Статус:** Живой документ  
**Последнее обновление:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, Execution-Semantics-v0.md, Station-Site-Integration-v0.md, Northbound-API-v0.md, ADR-004, ADR-008, docs/api/northbound/openapi-v0.yaml, docs/api/southbound/asyncapi-v0.yaml

---

## 1. Назначение

Документ фиксирует минимальный нормативный каталог канонических событий платформы, достаточный для:

- согласованной реализации `WES`, `WCS`, цифрового двойника (`Digital Twin`) и проекций операций;
- построения верхнего интеграционного интерфейса (`Northbound API`) как проекции поверх уже зафиксированных фактов;
- исключения ситуации, когда одна и та же бизнес-ситуация публикуется разными событиями в разных частях системы.

Документ описывает **канонические платформенные события**, а не сырую телеметрию и не низкоуровневые сообщения конкретного адаптера.

---

## 2. Границы каталога

Каталог включает:

- события жизненного цикла `Job`;
- события жизненного цикла `ExecutionTask`;
- события передачи и физического удержания груза;
- нормализованные операционные события `WCS`;
- канонические факты, приходящие от `Station/Site Integration Adapter`.

Каталог не включает:

- сырую телеметрию устройств;
- низкоуровневые сообщения конкретного вендора;
- команды нижнего интеграционного интерфейса;
- `Heartbeat` и `StateSnapshot` как транспортные сообщения нижнего контура;
- отладочные и трассировочные события реализации.

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
- `northbound` — событие может публиковаться внешним системам через верхний интеграционный интерфейс (`Northbound API`).

Если событие имеет `visibility = northbound`, это не означает автоматическую публикацию всей полезной нагрузки наружу; внешняя проекция может быть уже и стабильнее внутренней.

### 3.3. Порядок и идемпотентность

- причинная упорядоченность требуется в пределах агрегата-владельца;
- потребители обязаны дедуплицировать события по `eventId`;
- повторная публикация того же факта не должна менять итоговую проекцию;
- там, где это применимо, используется дополнительный семантический ключ идемпотентности, описанный в карточке события.

### 3.4. Владелец факта и публикатор

- `владелец факта` — логический владелец факта и его бизнес-семантики;
- `публикатор` — компонент, который физически публикует событие на шину.

Во многих случаях `владелец факта` и `публикатор` совпадают, но для нормализованных сигналов от адаптеров это не обязательно.

---

## 4. Сводная таблица

| Событие | Видимость | Владелец факта | Публикатор | Ключ порядка |
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
- `владелец факта`: `WES`
- `публикатор`: `WES`
- `потребители`: `Digital Twin`, операторские проекции, внешняя доставка через верхний интеграционный интерфейс
- `триггер`: `WES` принял входящий запрос и создал `Job`
- `постусловие`: `Job` существует и доступен для дальнейшего отслеживания
- `корреляция`: `correlationId = jobId`
- `идемпотентность`: повторная публикация с тем же `jobId` не должна создавать второй `Job`
- `состав полезной нагрузки`:

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
- `владелец факта`: `WES`
- `публикатор`: `WES`
- `потребители`: `Digital Twin`, внешняя доставка через верхний интеграционный интерфейс, операторские проекции
- `триггер`: подтверждённый переход состояния `Job`
- `постусловие`: состояние `Job` обновлено в модели `WES`
- `корреляция`: `correlationId = jobId`
- `идемпотентность`: дубликат одного и того же перехода не меняет проекцию
- `состав полезной нагрузки`:

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
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: `WES`, `Digital Twin`, операторские проекции, локальные проекции состояния
- `триггер`: подтверждённый переход состояния `ExecutionTask`
- `постусловие`: текущее состояние шага синхронизировано в операционной модели
- `корреляция`: `correlationId = executionTask.correlationId`
- `идемпотентность`: дубликат одного и того же перехода по `executionTaskId + newState` не меняет проекцию
- `состав полезной нагрузки`:

```text
{
  executionTaskId
  jobId
  taskType
  assigneeId
  previousState
  newState
  reasonCode?
  resolutionHint?: WAIT_AND_RETRY | REPLAN_REQUIRED | OPERATOR_ATTENTION
  replanRequired?
  sourceNode?
  targetNode?
  transferMode?
}
```

### 5.4. `PayloadCustodyChanged`

- `visibility`: `internal`, `operations`
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: `Digital Twin`, аудит исполнения, проекция состояния груза
- `триггер`: подтверждённый факт передачи или загрузки/выгрузки
- `постусловие`: у `Payload` обновлён текущий физический держатель
- `корреляция`: `correlationId = executionTask.correlationId`
- `идемпотентность`: повторная публикация того же перехода `from -> to` не должна дублировать смену holder
- `состав полезной нагрузки`:

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
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: `Digital Twin`, аудит, проекция операций передачи
- `триггер`: конечный автомат передачи перешёл в подтверждённое состояние успешного завершения
- `постусловие`: операция передачи завершена успешно
- `корреляция`: `correlationId = executionTask.correlationId`
- `идемпотентность`: для одного `correlationId` допускается не более одного итогового успешного завершения
- `состав полезной нагрузки`:

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
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: `Digital Twin`, аудит, операторская консоль
- `триггер`: конечный автомат передачи перешёл в подтверждённое состояние прерывания
- `постусловие`: операция передачи зафиксирована как незавершённая и безопасно остановленная
- `корреляция`: `correlationId = executionTask.correlationId`
- `идемпотентность`: для одного `correlationId` допускается не более одного итогового прерывания
- `состав полезной нагрузки`:

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
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: конечный автомат материализации, логика восстановления, `Digital Twin`
- `триггер`: подтверждённый факт достижения логического узла устройством
- `постусловие`: `currentNode` устройства обновлён
- `корреляция`: `correlationId = active execution correlation`
- `идемпотентность`: дубликат того же достижения узла в рамках того же активного шага не меняет итоговую проекцию
- `состав полезной нагрузки`:

```text
{
  deviceId
  family
  nodeId
}
```

### 5.8. `TransferReady`

- `visibility`: `internal`
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: конечный автомат передачи, логика восстановления
- `триггер`: участник операции передачи подтвердил локальную готовность
- `постусловие`: соответствующая внутренняя фаза исполнения может перейти к следующей проверке
- `корреляция`: `correlationId = executionTask.correlationId`
- `идемпотентность`: повторная готовность по тому же `transferPointId + role + correlationId` не меняет FSM
- `состав полезной нагрузки`:

```text
{
  deviceId
  transferPointId
  role
}
```

### 5.9. `DeviceSessionLost`

- `visibility`: `internal`, `operations`
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: логика восстановления, операторская консоль, `Digital Twin`
- `триггер`: истёк lease или зафиксирована потеря управляемого сеанса
- `постусловие`: ресурс выведен из нормального контура исполнения, связанный шаг приостановлен
- `корреляция`: `correlationId = active execution correlation`, если шаг активен
- `идемпотентность`: повтор по тому же `deviceId + sessionId` не меняет состояние после первого suspend
- `состав полезной нагрузки`:

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
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: планировочные проекции, операторская консоль, `Digital Twin`
- `триггер`: изменилась активная применимость возможностей ресурса
- `постусловие`: `activeCapabilities` обновлены в проекции устройства
- `корреляция`: `correlationId = deviceId`
- `идемпотентность`: для `deviceId` действует правило "последнее состояние побеждает"
- `состав полезной нагрузки`:

```text
{
  deviceId
  previousActiveCapabilities[]
  activeCapabilities[]
}
```

### 5.11. `FaultRaised`

- `visibility`: `internal`, `operations`
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: логика карантина, операторская консоль, `Digital Twin`
- `триггер`: подтверждён новый активный отказ
- `постусловие`: отказ зафиксирован в операционной модели
- `корреляция`: `correlationId = sourceId`
- `идемпотентность`: повтор по `sourceId + faultCode + ACTIVE` не должен дублировать fault
- `состав полезной нагрузки`:

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
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: логика карантина, операторская консоль, `Digital Twin`
- `триггер`: подтверждено, что ранее активный отказ снят
- `постусловие`: состояние отказа меняется на `CLEARED`
- `корреляция`: `correlationId = sourceId`
- `идемпотентность`: повтор по `sourceId + faultCode + CLEARED` не меняет модель
- `состав полезной нагрузки`:

```text
{
  sourceType
  sourceId
  faultCode
}
```

### 5.13. `VerticalCarrierPositionChanged`

- `visibility`: `internal`, `operations`
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: `Digital Twin`, конечный автомат передачи, логика восстановления
- `триггер`: подтверждено, что лифт достиг нового `CarrierNode`
- `постусловие`: положение `VerticalCarrier` и зависимых проекций шаттла-пассажира обновлено
- `корреляция`: `correlationId = active execution correlation`
- `идемпотентность`: повтор того же `carrierNode` подряд не меняет проекцию
- `состав полезной нагрузки`:

```text
{
  deviceId
  previousCarrierNode?
  currentCarrierNode
}
```

### 5.14. `ShuttleMovementModeChanged`

- `visibility`: `internal`, `operations`
- `владелец факта`: `WCS`
- `публикатор`: `WCS`
- `потребители`: `Digital Twin`, логика восстановления, операторская консоль
- `триггер`: шаттл перешёл между `AUTONOMOUS` и `CARRIER_PASSENGER`
- `постусловие`: обновлены `movementMode` и связанный `carrierId`
- `корреляция`: `correlationId = executionTask.correlationId`
- `идемпотентность`: дубликат того же перехода `previousMode -> newMode` не меняет модель
- `состав полезной нагрузки`:

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
- `владелец факта`: `WCS`
- `публикатор`: `Station/Site Integration Adapter`
- `потребители`: логика передачи `WCS`, операторская консоль, `Digital Twin`
- `триггер`: адаптер получил новый подтверждённый факт о готовности пассивной станции
- `постусловие`: `StationBoundary.readiness` обновлена
- `корреляция`: `correlationId = stationId`
- `идемпотентность`: для `stationId` действует правило "последнее состояние побеждает"
- `состав полезной нагрузки`:

```text
{
  stationId
  readiness
  reason?
}
```

### 5.16. `StationTransferFactReported`

- `visibility`: `internal`, `operations`
- `владелец факта`: `WCS`
- `публикатор`: `Station/Site Integration Adapter`
- `потребители`: логика передачи `WCS`, аудит, операторская консоль
- `триггер`: адаптер получил подтверждённый факт передачи на границе пассивной станции
- `постусловие`: `WCS` может публиковать `PayloadCustodyChanged` и завершать `StationTransfer`
- `корреляция`: `correlationId = executionTask.correlationId`, если он известен; иначе связывание выполняется по текущему контексту станции
- `идемпотентность`: повтор по `stationId + direction + correlationId + factType` не меняет модель
- `состав полезной нагрузки`:

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

## 6. Что не публикуется наружу в `Northbound API v0`

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

- машиночитаемая спецификация полного платформенного потока событий за пределами `Northbound API v0`;
- дополнительные варианты внешней полезной нагрузки за пределами `job.accepted` и `job.state_changed`;
- политика версионирования событий за пределами `v0`;
- расширенный каталог причин `reasonCode`;
- события административного и операторского управления вне базового контура исполнения.

