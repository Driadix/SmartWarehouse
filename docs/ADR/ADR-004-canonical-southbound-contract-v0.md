# ADR-004: Canonical southbound contract v0

**Статус:** Принято  
**Дата:** 2026-04-03  
**Связанные артефакты:** ArchitecturalVision.md, ADR-001, ADR-002, ADR-003, DomainModel-v0

---

## 1. Контекст

Команда начинает разработку без зафиксированных конечных вендоров, без окончательного выбора wire-протокола и без финального решения по локализации. При этом `WCS`, симулятор и будущие ACL уже сейчас нуждаются в едином каноническом контракте.

Без такого контракта невозможно:

- независимо разрабатывать `WCS Execution Core` и ACL;
- писать симулятор, совместимый с будущим железом;
- тестировать handoff, sliding window и recovery до появления устройств;
- избежать вендорной протечки протокола в доменную модель.

---

## 2. Решение

### 2.1. Контракт является каноническим и transport-agnostic

Southbound-контракт платформы описывает **семантику**, а не конкретный транспорт.

Следствия:

- одна реализация ACL может использовать MQTT;
- другая — serial bridge, TCP, gRPC или иной транспорт;
- симулятор обязан говорить на том же каноническом уровне, что и ACL;
- `WCS` не должен зависеть от особенностей конкретного wire-протокола.

### 2.2. Контракт node-based

Управляющая логика платформы работает только с логическими `NodeId`.

- raw coordinates, QR labels, RFID markers и lidar-localization остаются на стороне устройства/адаптера;
- ACL может выполнять mapping physical position -> `NodeId`;
- `WCS` и `WES` не принимают решений на основе сырых координат.

### 2.3. Базовый envelope сообщения

Каждое сообщение southbound-контракта должно содержать как минимум:

```text
messageId
schemaVersion
messageType
correlationId
causationId?
deviceId
family
sessionId
deviceTime?
platformTime
payload
```

Требования:

- `messageId` уникален в пределах системы;
- `correlationId` связывает команду, подтверждение и последующие события;
- `sessionId` отделяет текущее подключение устройства от предыдущих;
- `deviceTime` опционален и не является обязательным источником истины для управляющей логики.

### 2.4. Семантика доставки и порядка

Для v0 фиксируются следующие правила:

- порядок критичных сообщений гарантируется **в пределах device session**, а не глобально по всей платформе;
- доставка допускается `at-least-once`;
- команды должны быть идемпотентными по `messageId` / `correlationId`;
- повторное подтверждение физически уже выполненного действия должно быть безопасно обработано;
- разрыв session обнуляет ожидание продолжения старого канала и требует re-sync состояния.

### 2.5. Общие команды канонического контракта

Минимальный набор общих команд v0:

```text
RequestStateSnapshot
SuspendExecution
ResumeExecution
ClearFault
AcknowledgeAlarm
```

Эти команды применимы ко всем активным устройствам, если семейство поддерживает соответствующую capability.

### 2.6. Phase 1 family-specific команды

#### Для Shuttle3D

```text
GrantMotionWindow { nodePath[] }
RevokeMotionWindow
PrepareTransfer { transferPointId, role }
CommitTransfer   { transferPointId }
AbortTransfer    { transferPointId, reason }
ExecuteAction    { actionType }
```

Где:

- `GrantMotionWindow` выдаёт разрешённый ближайший путь для локального исполнения;
- `PrepareTransfer` подготавливает шаттл к station/lift handoff;
- `ExecuteAction` используется для station-действий, если они выполняются шаттлом.

#### Для VerticalCarrier / HybridLift

```text
MoveCarrier      { targetCarrierNode }
PrepareTransfer  { transferPointId, role }
CommitTransfer   { transferPointId }
AbortTransfer    { transferPointId, reason }
ExecuteAction    { actionType }
```

Типовые `actionType` для Phase 1: `OpenInterface`, `CloseInterface`, `PrepareReceive`.

#### Для активной Station

Если станция имеет собственное активное оборудование, она может реализовывать подмножество:

```text
PrepareTransfer
CommitTransfer
AbortTransfer
ExecuteAction
```

Пассивная station допускает отсутствие собственного southbound session; тогда её readiness моделируется через `WCS` и интеграционный слой станции.

### 2.7. Канонические события устройств

Минимальный набор southbound events v0:

```text
Heartbeat
StateSnapshot
CapabilityReported
CapabilityChanged
NodeReached
MotionWindowProgress
TransferReady
TransferCommitted
TransferAborted
ActionCompleted
FaultRaised
FaultCleared
ExecutionRejected
```

Смысл событий:

- `NodeReached` — устройство подтвердило достижение логического узла;
- `TransferReady` — локально достигнута готовность к handoff;
- `ExecutionRejected` — устройство не приняло команду и вернуло machine-readable причину;
- `StateSnapshot` — полное каноническое состояние устройства после reconnect/recovery.

### 2.8. Liveness и session semantics

Для всех активных устройств вводится device session c lease semantics:

- устройство открывает или восстанавливает `sessionId`;
- `Heartbeat` продлевает lease;
- истечение lease трактуется `WCS` как потеря управляемости ресурса;
- `MQTT LWT` может использоваться как частная оптимизация ACL, но не заменяет общую session semantics контракта.

### 2.9. Recovery semantics

После reconnect устройство должно уметь отдать `StateSnapshot`, содержащий как минимум:

```text
currentNode
executionState
activeCommand?
activeCapabilities
faultState
familySpecificState
```

`WCS` использует этот snapshot для reconciliation, а не пытается восстанавливать устройство по предположениям.

### 2.10. Требование к симулятору

Программный симулятор Phase 1 обязан реализовывать тот же канонический contract v0, что и реальные ACL-адаптеры. Симулятор не имеет права использовать «внутренние shortcut APIs», которых не будет у железа.

---

## 3. Последствия

### 3.1. Что можно делать сразу

После фиксации контракта можно независимо разрабатывать:

- `WCS Execution Core`;
- симулятор устройств;
- ACL для первого оборудования;
- end-to-end тесты маршрутизации и handoff.

### 3.2. Что остаётся открытым, но не блокирует Phase 1

Открытыми остаются:

- конкретный wire transport;
- формат сериализации payload;
- конкретная схема security/authentication канала;
- конкретная технология локализации.

Эти решения не должны менять канонический набор смыслов southbound-контракта.

---

## 4. Рассмотренные альтернативы

### Альтернатива A: Подождать с контрактом до выбора железа

Отклонена. Это блокирует симулятор, параллельную разработку и первые тесты платформы.

### Альтернатива B: Использовать wire-протокол первого вендора как внутренний стандарт

Отклонена. Это переносит вендорные ограничения внутрь доменной архитектуры.

### Альтернатива C: Ограничиться только «командами движения», а handoff и recovery определить позже

Отклонена. Для Phase 1 handoff и recovery не являются опциональными деталями — это центральная часть архитектурного риска.
