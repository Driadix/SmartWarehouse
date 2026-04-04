# Правила исполнения v0
### Текущий базовый состав: 3D-шаттл + гибридный лифт + пассивные станции и сервисные точки

**Статус:** Живой документ  
**Последнее обновление:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, DomainModel-v0.md, Topology-Configuration-Model-v0.md, Station-Site-Integration-v0.md, Capability-Catalog-Phase-1.md, ADR-004, ADR-005, ADR-006, ADR-007

---

## 1. Назначение

Документ фиксирует минимальные правила исполнения, достаточные для того, чтобы `WCS`, симулятор и `Digital Twin` одинаково трактовали:

- что означает плановый `ExecutionTask`;
- как материализуются шаги `Navigate`, `StationTransfer`, `CarrierTransfer`;
- что подтверждает завершение шага;
- какие исходы допустимы при повторном согласовании после потери сеанса.

Документ не заменяет `ADR-005` и `ADR-006`, а делает их исполнимыми для текущего базового состава.

---

## 2. Основные различия

### 2.1. `ExecutionTask` и `RuntimePhase`

- `ExecutionTask` — плановый макрошаг уровня `WES`.
- `RuntimePhase` — внутренняя фаза исполнения внутри `WCS`.
- `RuntimePhase` не является частью верхнего бизнес-контракта между `WES` и `WCS`.

### 2.2. Физический факт и изменение состояния платформы

- `NodeReached(nodeId)` подтверждает достижение логического узла устройством.
- `PayloadCustodyChanged` фиксирует изменение физического удержания груза.
- Завершение шага допускается только тогда, когда подтверждён физический факт, соответствующий постусловиям шага.

### 2.3. Пассивная станция и пассивная сервисная точка

- `LoadStation` и `UnloadStation` являются `StationBoundary`.
- `ChargeNode` и `ServiceNode` не являются `StationBoundary`.
- Пассивная станция не имеет собственного `DeviceSession`, команд и событий нижнего уровня.
- Готовность пассивной станции и подтверждённые факты передачи на её границе поступают в `WCS` через `Station/Site Integration Adapter`.
- Пассивная сервисная точка в текущем базовом составе также не имеет собственного сеанса управления.
- `NodeReached(StationNode)` подтверждает только позиционирование на границе станции.
- `NodeReached(ChargeNode)` и `NodeReached(ServiceNode)` подтверждают прибытие к сервисной точке.
- Для `ChargeNode` событие `NodeReached` также считается подтверждением начала шага зарядки, так как отдельная стыковка не требуется.

---

## 3. Общие правила исполнения

1. Только `WES` создаёт плановый `ExecutionTask`.
2. `WCS` владеет выбором и сменой `RuntimePhase`.
3. Шаг не считается завершённым по таймеру или по предположению.
4. Изменение физического удержания груза происходит только по подтверждённому факту.
5. Для пассивной станции `WCS` не ждёт station-side southbound-команд и использует `Station/Site Integration Adapter` как источник готовности и фактов передачи.
6. Для пассивной сервисной точки `WCS` не ждёт отдельного подтверждения от станции.
7. Если `StateSnapshot` не доказывает постусловия шага, шаг остаётся `Suspended` или продолжается после повторного согласования.

---

## 4. Семантика шагов текущего базового состава

### 4.1. `Navigate`

`Navigate` применяется для перемещения устройства по графу без операции передачи.

Типовые `RuntimePhase`:

```text
Accepted -> MotionAuthorized -> InMotion -> Arrived -> Completed | Suspended
```

Материализация:

- `WCS` выдаёт `GrantMotionWindow { nodePath[] }`;
- окно движения должно заканчиваться на ближайшей конфликтной точке, либо на `targetNode`, если конфликтной точки раньше нет;
- устройство подтверждает продвижение событиями `NodeReached`;
- `WCS` может отзывать окно через `RevokeMotionWindow`;
- завершение шага подтверждается `NodeReached(targetNode)`.

Специальные случаи текущего базового состава:

- `Navigate` к `ChargeNode` завершается по `NodeReached(targetNode)`;
- `Navigate` к `ServiceNode` завершается по `NodeReached(targetNode)`;
- дальнейшая зарядка или сервисное воздействие не создают отдельной station-side фазы, если точка остаётся пассивной.

### 4.2. `StationTransfer`

`StationTransfer` применяется только для пассивных `LoadStation` и `UnloadStation` текущего базового состава.

Типовые `RuntimePhase`:

```text
Accepted -> ReachingBoundary -> BoundaryPositionConfirmed -> CommitCustody -> Completed | Suspended
```

Материализация:

- `WCS` ведёт шаттл к `StationNode`, привязанному к `StationBoundary`;
- `NodeReached(attachedNode)` подтверждает прибытие шаттла на границу станции;
- после прибытия `WCS` выполняет проверку `StationBoundary.readiness`, полученной через `Station/Site Integration Adapter`;
- изменение физического удержания груза фиксируется только после подтверждённого факта передачи, полученного через `Station/Site Integration Adapter`;
- завершение шага возможно только после достижения постусловий по `PayloadCustodyChanged`.

Подварианты:

#### `LoadStation -> Shuttle3D`

Постусловия шага:

- шаттл подтверждённо находится на `StationNode`;
- физическое удержание груза перешло к `Shuttle3D`.

#### `Shuttle3D -> UnloadStation`

Постусловия шага:

- шаттл подтверждённо находится на `StationNode`;
- физическое удержание груза перешло к `UnloadStation`.

Строгое правило:

- `NodeReached(StationNode)` само по себе **не** завершает `StationTransfer`;
- без `PayloadCustodyChanged` загрузка и выгрузка считаются незавершёнными.

### 4.3. `CarrierTransfer`

`CarrierTransfer` описывает межуровневую передачу в режиме `SHUTTLE_RIDES_HYBRID_LIFT_WITH_PAYLOAD`.

Типовые `RuntimePhase`:

```text
Accepted
-> PrepareTransfer
-> BoardCarrier
-> MoveCarrier
-> ExitCarrier
-> CommitTransfer
-> Completed | Suspended | Aborted
```

Материализация:

1. шаттл достигает `TransferPoint` исходного уровня;
2. лифт достигает соответствующего `CarrierNode`;
3. `WCS` проверяет готовность участников и выдаёт команды передачи;
4. после подтверждённого въезда:
   - `shuttle.movementMode = CARRIER_PASSENGER`
   - `shuttle.carrierId = liftId`
   - `lift.occupiedShuttleId = shuttleId`
5. лифт выполняет `MoveCarrier(targetCarrierNode)`;
6. после подтверждённого выезда:
   - `shuttle.movementMode = AUTONOMOUS`
   - `shuttle.carrierId = null`
   - `shuttle.currentNode = targetTransferPoint`
   - `lift.occupiedShuttleId = null`

Постусловия шага:

- шаттл подтверждённо находится в `targetTransferPoint`;
- шаттл больше не является пассажиром лифта;
- лифт более не занят этим шаттлом;
- физическое удержание груза осталось у шаттла.

---

## 5. Минимальные правила повторного согласования

### 5.1. `Navigate`

| Подтверждённый снимок состояния | Допустимый исход |
|---|---|
| `currentNode == targetNode` | Безопасно завершить шаг |
| `currentNode` находится на разрешённом пути до `targetNode`, критичного отказа нет | Безопасно продолжить шаг |
| `currentNode` отсутствует на разрешённом пути или не может быть подтверждён | Эскалировать на повторное согласование / операторское решение |

### 5.2. `StationTransfer`

| Подтверждённый снимок состояния | Допустимый исход |
|---|---|
| шаттл на `StationNode`, `PayloadCustody` ещё у исходной стороны, transfer-fact от `Station/Site Integration Adapter` отсутствует | Безопасно продолжить шаг |
| шаттл на `StationNode`, `PayloadCustody` уже у принимающей стороны, transfer-fact от `Station/Site Integration Adapter` подтверждён | Безопасно завершить шаг |
| `PayloadCustody` изменилась, но шаттл не подтверждён на `StationNode` | Эскалировать как противоречивое состояние |
| adapter-fact противоречит `PayloadCustody` или текущему направлению шага | Эскалировать как противоречивое состояние |

### 5.3. `CarrierTransfer`

| Подтверждённый снимок состояния | Допустимый исход |
|---|---|
| шаттл `AUTONOMOUS`, `carrierId = null`, находится у исходного `TransferPoint`; лифт у исходного `CarrierNode` | Безопасно продолжить фазу посадки |
| шаттл `CARRIER_PASSENGER`, `carrierId = liftId`, лифт подтверждённо содержит этот шаттл | Безопасно продолжить межуровневую передачу |
| шаттл `AUTONOMOUS`, `carrierId = null`, находится у целевого `TransferPoint`, лифт больше не содержит этот шаттл | Безопасно завершить шаг |
| шаттл и лифт дают противоречивые поля `movementMode`, `carrierId`, `occupiedShuttleId` | Эскалировать на перепланирование или операторское вмешательство |

---

## 6. Явно отложенные детали

- детальные эвристики `GrantMotionWindow`;
- полный каталог кодов ошибок и причин `ExecutionRejected`;
- активные станции с собственным нижним сеансом управления;
- вендорские расширения `StateSnapshot`;
- правила исполнения для `DirectStorageCrane`, `Shuttle1D` и иных семейств вне текущего базового состава.
