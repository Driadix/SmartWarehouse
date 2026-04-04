# Contract Acceptance Matrix v0
### `Northbound API v0` и минимальные сценарии приёмки через контракт и симуляцию

**Статус:** Живой документ  
**Последнее обновление:** 2026-04-04  
**Связанные артефакты:** ArchitecturalVision.md, Northbound-API-v0.md, Event-Catalog-v0.md, Topology-Configuration-Model-v0.md, Execution-Semantics-v0.md, docs/api/northbound/openapi-v0.yaml

---

## 1. Назначение

Документ фиксирует минимальную матрицу приёмки для `Northbound API v0`.

Цели документа:

- сделать внешний контракт исполнимым и проверяемым;
- связать синхронные ответы API, webhook-проекции и итоговые состояния `Job` с воспроизводимыми сценариями;
- не превращать приёмку в полный тест-план всей платформы.

Матрица является мостом между:

- спецификацией контракта;
- каталожными событиями платформы;
- сценариями симуляции и acceptance-тестами.

---

## 2. Область и границы

Матрица `v0` покрывает:

- только `Northbound API v0` для `PayloadTransferJob`;
- только текущий базовый состав реализации;
- только те сценарии, которые нужны для старта разработки, симуляции и первого контрактного тестирования.

Матрица `v0` не покрывает:

- нагрузочные и отказоустойчивые испытания на целевых объёмах;
- security-профили и transport-level аутентификацию;
- operator/admin API;
- подробный порядок всех внутренних событий `WCS`.

---

## 3. Общие правила приёмки

### 3.1. Граница между sync rejection и runtime execution

- ошибки формы запроса, идемпотентности, адресации и статической достижимости должны выявляться синхронно на входе `Northbound API`;
- runtime-проблемы после принятия задания не превращаются в синхронный отказ создания задания;
- если `endpointId` валиден, но допустимого маршрута в текущей `Topology Configuration` не существует, запрос должен отклоняться синхронно с `422`.

### 3.2. Внешняя наблюдаемость

- `job.accepted` может быть опубликован не более одного раза для одного `jobId`;
- `job.state_changed` не должен появляться раньше `job.accepted` для того же `jobId`;
- причинная последовательность webhook-событий фиксируется только в пределах одного `jobId`;
- повторный `POST` с тем же `clientOrderId` и тем же нормализованным телом не должен создавать новый `jobId` и не должен публиковать второй `job.accepted`.

### 3.3. Минимальные внутренние факты платформы

Матрица не фиксирует полный журнал внутренних событий, но для ключевых сценариев требует минимальные платформенные факты из `Event-Catalog-v0`:

- `JobAccepted`;
- `JobStateChanged`;
- при необходимости `PayloadCustodyChanged`;
- при необходимости `StationReadinessChanged`;
- при необходимости `ExecutionRejected`;
- при необходимости `DeviceSessionLost`.

Если сценарий проходит без этих фактов там, где они указаны как обязательные, сценарий считается неполным.

### 3.4. `Problem.code`

Для отрицательных синхронных ответов `Problem.code` является частью контракта `v0`.

Матрица фиксирует минимальный набор кодов:

- `IDEMPOTENCY_CONFLICT`
- `UNKNOWN_SOURCE_ENDPOINT`
- `UNKNOWN_TARGET_ENDPOINT`
- `IDENTICAL_ENDPOINTS`
- `NO_ADMISSIBLE_ROUTE`
- `JOB_NOT_FOUND`
- `CANCEL_NOT_ALLOWED`

### 3.5. Уровни автоматизации

- `contract-only` - достаточно HTTP-контракта без симуляции исполнения;
- `contract+projection` - требуется проверка HTTP-контракта, webhook-доставки и read-model проекции;
- `contract+simulation` - требуется симуляция исполнения `WCS` и нижнего контура.

---

## 4. Общие фикстуры

### `F-01 NominalRoute`

- в конфигурации существуют `endpointId = inbound.main` и `endpointId = outbound.main`;
- между ними есть допустимый маршрут;
- устройства доступны;
- станции на границе маршрута готовы к работе.

### `F-02 NoRouteBetweenValidEndpoints`

- `endpointId` валидны;
- допустимого маршрута между ними в текущей конфигурации нет.

### `F-03 RuntimeStationBlocked`

- задание может быть принято;
- во время исполнения станция или её граница переходят в `BLOCKED` или `OFFLINE`.

### `F-04 UnrecoverableExecutionError`

- задание принято;
- во время исполнения `WCS` получает нерешаемый для локального ретрая отказ или `ExecutionRejected`.

### `F-05 DeviceSessionLoss`

- задание принято и уже находится в активном исполнении;
- во время исполнения теряется `DeviceSession` активного устройства.

### `F-06 ExistingAcceptedJob`

- существует ранее созданный `PayloadTransferJob` в состоянии `ACCEPTED`.

### `F-07 ExistingInProgressJob`

- существует ранее созданный `PayloadTransferJob` в состоянии `IN_PROGRESS`.

### `F-08 ExistingCancelledJob`

- существует ранее созданный `PayloadTransferJob` в состоянии `CANCELLED`.

### `F-09 ExistingCompletedJob`

- существует ранее созданный `PayloadTransferJob` в состоянии `COMPLETED`.

---

## 5. Сводная матрица

| ScenarioId | Category | Stimulus | Sync expectation | Webhook expectation | Final state | Automation |
|---|---|---|---|---|---|---|
| `NB-REQ-001` | request-acceptance | валидный `POST` | `202 Accepted` | `job.accepted` | `ACCEPTED` | `contract+projection` |
| `NB-IDEMP-001` | idempotency | повтор того же `POST` | `200 OK`, тот же `jobId` | нет новых webhook | без изменений | `contract+projection` |
| `NB-IDEMP-002` | idempotency | тот же `clientOrderId`, другое тело | `409 Conflict` | нет webhook | без нового `Job` | `contract-only` |
| `NB-VAL-001` | validation | неизвестный `sourceEndpointId` | `422` | нет webhook | без нового `Job` | `contract-only` |
| `NB-VAL-002` | validation | неизвестный `targetEndpointId` | `422` | нет webhook | без нового `Job` | `contract-only` |
| `NB-VAL-003` | validation | одинаковые source/target | `422` | нет webhook | без нового `Job` | `contract-only` |
| `NB-VAL-004` | validation | валидные endpoint, но нет маршрута | `422` | нет webhook | без нового `Job` | `contract-only` |
| `NB-READ-001` | read-model | `GET` по существующему `jobId` | `200 OK` | нет webhook | без изменений | `contract+projection` |
| `NB-READ-002` | read-model | `GET` по существующему `clientOrderId` | `200 OK` | нет webhook | без изменений | `contract+projection` |
| `NB-READ-003` | read-model | `GET` по отсутствующему `jobId` | `404 Not Found` | нет webhook | нет ресурса | `contract-only` |
| `NB-LIFE-001` | lifecycle | исполнить принятое задание до конца | уже принято | `job.state_changed` до `COMPLETED` | `COMPLETED` | `contract+simulation` |
| `NB-LIFE-002` | lifecycle | блокировка станции во время исполнения | уже принято | `job.state_changed` до `SUSPENDED` | `SUSPENDED` | `contract+simulation` |
| `NB-LIFE-003` | lifecycle | нерешаемая ошибка исполнения | уже принято | `job.state_changed` до `FAILED` | `FAILED` | `contract+simulation` |
| `NB-CANCEL-001` | cancellation | `POST cancel` для активного задания | `202 Accepted` | `job.state_changed` до `CANCELLED` | `CANCELLED` | `contract+simulation` |
| `NB-CANCEL-002` | cancellation | `POST cancel` для уже отменённого задания | `200 OK` | нет новых webhook | `CANCELLED` | `contract+projection` |
| `NB-CANCEL-003` | cancellation | `POST cancel` для `COMPLETED` | `409 Conflict` | нет webhook | `COMPLETED` | `contract-only` |
| `NB-REC-001` | recovery | потеря `DeviceSession` во время исполнения | уже принято | `job.state_changed` до `SUSPENDED` | `SUSPENDED` | `contract+simulation` |

---

## 6. Карточки сценариев

### `NB-REQ-001` Create valid job

- `Category`: `request-acceptance`
- `Fixture`: `F-01 NominalRoute`
- `Preconditions`: задания с тем же `clientOrderId` не существует
- `Stimulus`: `POST /api/v0/payload-transfer-jobs` с валидным телом
- `Expected Sync Response`: `202 Accepted`, `Location` указывает на созданный ресурс, тело ответа содержит `jobId`, `clientOrderId`, `state = ACCEPTED`
- `Expected Webhook Sequence`: один `job.accepted`
- `Expected Final Job State`: `ACCEPTED`
- `Required Platform Facts`: `JobAccepted`
- `Negative Assertions`: нет `job.state_changed` раньше `job.accepted`, нет второго `job.accepted`
- `Automation Target`: `contract+projection`

### `NB-IDEMP-001` Replay same normalized request

- `Category`: `idempotency`
- `Fixture`: существующий `Job` из `NB-REQ-001`
- `Preconditions`: повторяется тот же `clientOrderId` и то же нормализованное тело
- `Stimulus`: повторный `POST /api/v0/payload-transfer-jobs`
- `Expected Sync Response`: `200 OK`, тот же `jobId`, та же внешняя проекция задания
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: без изменений
- `Required Platform Facts`: новый `JobAccepted` отсутствует
- `Negative Assertions`: не создаётся второй `Job`, не публикуется второй `job.accepted`
- `Automation Target`: `contract+projection`

### `NB-IDEMP-002` Replay with same `clientOrderId` and different body

- `Category`: `idempotency`
- `Fixture`: существующий `Job` из `NB-REQ-001`
- `Preconditions`: меняется хотя бы одно нормализуемое поле запроса
- `Stimulus`: повторный `POST /api/v0/payload-transfer-jobs` с тем же `clientOrderId` и другим телом
- `Expected Sync Response`: `409 Conflict`, `Problem.code = IDEMPOTENCY_CONFLICT`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: исходный `Job` не изменён, новый `Job` не создан
- `Required Platform Facts`: новые `JobAccepted` и `JobStateChanged` отсутствуют
- `Negative Assertions`: не возникает новый `jobId`
- `Automation Target`: `contract-only`

### `NB-VAL-001` Unknown source endpoint

- `Category`: `validation`
- `Fixture`: отсутствует `sourceEndpointId` в `Topology Configuration`
- `Preconditions`: `targetEndpointId` валиден
- `Stimulus`: `POST /api/v0/payload-transfer-jobs`
- `Expected Sync Response`: `422 Unprocessable Entity`, `Problem.code = UNKNOWN_SOURCE_ENDPOINT`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: новый `Job` не создаётся
- `Required Platform Facts`: отсутствуют
- `Negative Assertions`: нет `JobAccepted`
- `Automation Target`: `contract-only`

### `NB-VAL-002` Unknown target endpoint

- `Category`: `validation`
- `Fixture`: отсутствует `targetEndpointId` в `Topology Configuration`
- `Preconditions`: `sourceEndpointId` валиден
- `Stimulus`: `POST /api/v0/payload-transfer-jobs`
- `Expected Sync Response`: `422 Unprocessable Entity`, `Problem.code = UNKNOWN_TARGET_ENDPOINT`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: новый `Job` не создаётся
- `Required Platform Facts`: отсутствуют
- `Negative Assertions`: нет `JobAccepted`
- `Automation Target`: `contract-only`

### `NB-VAL-003` Identical source and target

- `Category`: `validation`
- `Fixture`: оба `endpointId` валидны
- `Preconditions`: `sourceEndpointId == targetEndpointId`
- `Stimulus`: `POST /api/v0/payload-transfer-jobs`
- `Expected Sync Response`: `422 Unprocessable Entity`, `Problem.code = IDENTICAL_ENDPOINTS`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: новый `Job` не создаётся
- `Required Platform Facts`: отсутствуют
- `Negative Assertions`: нет `JobAccepted`
- `Automation Target`: `contract-only`

### `NB-VAL-004` No admissible route between valid endpoints

- `Category`: `validation`
- `Fixture`: `F-02 NoRouteBetweenValidEndpoints`
- `Preconditions`: оба `endpointId` валидны, но маршрут отсутствует
- `Stimulus`: `POST /api/v0/payload-transfer-jobs`
- `Expected Sync Response`: `422 Unprocessable Entity`, `Problem.code = NO_ADMISSIBLE_ROUTE`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: новый `Job` не создаётся
- `Required Platform Facts`: отсутствуют
- `Negative Assertions`: нет `JobAccepted`
- `Automation Target`: `contract-only`

### `NB-READ-001` Get existing job by `jobId`

- `Category`: `read-model`
- `Fixture`: любой ранее созданный `Job`
- `Preconditions`: `jobId` существует
- `Stimulus`: `GET /api/v0/payload-transfer-jobs/{jobId}`
- `Expected Sync Response`: `200 OK`, тело соответствует текущей внешней проекции
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: без изменений
- `Required Platform Facts`: отсутствуют
- `Negative Assertions`: `GET` не изменяет состояние `Job`
- `Automation Target`: `contract+projection`

### `NB-READ-002` Get existing job by `clientOrderId`

- `Category`: `read-model`
- `Fixture`: любой ранее созданный `Job`
- `Preconditions`: `clientOrderId` существует
- `Stimulus`: `GET /api/v0/payload-transfer-jobs/by-client-order/{clientOrderId}`
- `Expected Sync Response`: `200 OK`, тело соответствует текущей внешней проекции
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: без изменений
- `Required Platform Facts`: отсутствуют
- `Negative Assertions`: `GET` не изменяет состояние `Job`
- `Automation Target`: `contract+projection`

### `NB-READ-003` Get missing job by `jobId`

- `Category`: `read-model`
- `Fixture`: отсутствует
- `Preconditions`: `jobId` не существует
- `Stimulus`: `GET /api/v0/payload-transfer-jobs/{jobId}`
- `Expected Sync Response`: `404 Not Found`, `Problem.code = JOB_NOT_FOUND`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: ресурс отсутствует
- `Required Platform Facts`: отсутствуют
- `Negative Assertions`: не возникает новый `Job`
- `Automation Target`: `contract-only`

### `NB-LIFE-001` Accepted job completes successfully

- `Category`: `lifecycle`
- `Fixture`: `F-01 NominalRoute` и `F-06 ExistingAcceptedJob`
- `Preconditions`: существует принятое задание в состоянии `ACCEPTED`
- `Stimulus`: выполнение задания в симуляторе до достижения целевой точки
- `Expected Sync Response`: не применяется, задание уже принято
- `Expected Webhook Sequence`: `job.state_changed(IN_PROGRESS)` -> `job.state_changed(COMPLETED)`
- `Expected Final Job State`: `COMPLETED`
- `Required Platform Facts`: `JobStateChanged(IN_PROGRESS)`, `JobStateChanged(COMPLETED)`, финальный `PayloadCustodyChanged`, согласованный с `targetEndpointId`
- `Negative Assertions`: нет `FAILED`, нет `CANCELLED`, нет второго `job.accepted`
- `Automation Target`: `contract+simulation`

### `NB-LIFE-002` Accepted job is suspended by runtime station blockage

- `Category`: `lifecycle`
- `Fixture`: `F-03 RuntimeStationBlocked` и `F-06 ExistingAcceptedJob`
- `Preconditions`: задание принято и может начать исполнение
- `Stimulus`: во время исполнения граница станции переходит в `BLOCKED` или `OFFLINE`
- `Expected Sync Response`: не применяется, задание уже принято
- `Expected Webhook Sequence`: `job.state_changed(IN_PROGRESS)` -> `job.state_changed(SUSPENDED)`
- `Expected Final Job State`: `SUSPENDED`
- `Required Platform Facts`: `JobStateChanged(IN_PROGRESS)`, `StationReadinessChanged`, `JobStateChanged(SUSPENDED)`
- `Negative Assertions`: нет `COMPLETED`, нет финального `PayloadCustodyChanged` к целевой конечной точке
- `Automation Target`: `contract+simulation`

### `NB-LIFE-003` Accepted job fails on unrecoverable execution error

- `Category`: `lifecycle`
- `Fixture`: `F-04 UnrecoverableExecutionError` и `F-06 ExistingAcceptedJob`
- `Preconditions`: задание принято и начало исполнение
- `Stimulus`: нижний контур возвращает нерешаемую ошибку исполнения
- `Expected Sync Response`: не применяется, задание уже принято
- `Expected Webhook Sequence`: `job.state_changed(IN_PROGRESS)` -> `job.state_changed(FAILED)`
- `Expected Final Job State`: `FAILED`
- `Required Platform Facts`: `JobStateChanged(IN_PROGRESS)`, `ExecutionRejected` или эквивалентный нерешаемый отказ, `JobStateChanged(FAILED)`
- `Negative Assertions`: нет `COMPLETED`, нет `CANCELLED`
- `Automation Target`: `contract+simulation`

### `NB-CANCEL-001` Cancel active job

- `Category`: `cancellation`
- `Fixture`: `F-07 ExistingInProgressJob`
- `Preconditions`: существует нетерминальное задание
- `Stimulus`: `POST /api/v0/payload-transfer-jobs/{jobId}/cancel`
- `Expected Sync Response`: `202 Accepted`
- `Expected Webhook Sequence`: один `job.state_changed(CANCELLED)`
- `Expected Final Job State`: `CANCELLED`
- `Required Platform Facts`: `JobStateChanged(CANCELLED)`
- `Negative Assertions`: после отмены не публикуются `COMPLETED` или `FAILED` для того же `jobId`
- `Automation Target`: `contract+simulation`

### `NB-CANCEL-002` Cancel already cancelled job

- `Category`: `cancellation`
- `Fixture`: `F-08 ExistingCancelledJob`
- `Preconditions`: задание уже находится в `CANCELLED`
- `Stimulus`: `POST /api/v0/payload-transfer-jobs/{jobId}/cancel`
- `Expected Sync Response`: `200 OK`, тело отражает состояние `CANCELLED`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: `CANCELLED`
- `Required Platform Facts`: новые `JobStateChanged` отсутствуют
- `Negative Assertions`: не публикуется второй terminal transition
- `Automation Target`: `contract+projection`

### `NB-CANCEL-003` Cancel completed job is rejected

- `Category`: `cancellation`
- `Fixture`: `F-09 ExistingCompletedJob`
- `Preconditions`: задание уже находится в `COMPLETED`
- `Stimulus`: `POST /api/v0/payload-transfer-jobs/{jobId}/cancel`
- `Expected Sync Response`: `409 Conflict`, `Problem.code = CANCEL_NOT_ALLOWED`
- `Expected Webhook Sequence`: отсутствует
- `Expected Final Job State`: `COMPLETED`
- `Required Platform Facts`: новые `JobStateChanged` отсутствуют
- `Negative Assertions`: состояние задания не меняется
- `Automation Target`: `contract-only`

### `NB-REC-001` Session loss during execution leads to suspension

- `Category`: `recovery`
- `Fixture`: `F-05 DeviceSessionLoss` и `F-07 ExistingInProgressJob`
- `Preconditions`: задание уже находится в `IN_PROGRESS`
- `Stimulus`: потеря `DeviceSession` активного устройства во время исполнения
- `Expected Sync Response`: не применяется, задание уже принято
- `Expected Webhook Sequence`: один `job.state_changed(SUSPENDED)`
- `Expected Final Job State`: `SUSPENDED`
- `Required Platform Facts`: `DeviceSessionLost`, `JobStateChanged(SUSPENDED)`
- `Negative Assertions`: нет `COMPLETED`, нет автоматического возврата в `IN_PROGRESS` без отдельного восстановления
- `Automation Target`: `contract+simulation`

---

## 7. Что сознательно отложено

- сценарии для batch-операций;
- сценарии security и transport-profile;
- сценарии operator/admin API;
- детальная матрица retry/compensation для каждого внутреннего `ExecutionTask`;
- точные SLA и тайминги webhook-доставки;
- сценарии multi-tenant и динамической регистрации webhook.