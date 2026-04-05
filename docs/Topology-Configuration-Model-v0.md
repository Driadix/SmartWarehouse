# Модель конфигурации топологии v0
### Текущий базовый состав: 3D-шаттл + гибридный лифт + станции загрузки и выгрузки

**Статус:** Живой документ  
**Последнее обновление:** 2026-04-05  
**Связанные артефакты:** ArchitecturalVision.md, Architecture-Baseline-Phase-1.md, DomainModel-v0.md, Execution-Semantics-v0.md, Station-Site-Integration-v0.md, ADR-002, ADR-004, ADR-005

---

## 1. Назначение

Документ фиксирует минимальную модель конфигурации топологии, достаточную для:

- разработки `WES`, `WCS`, симулятора и цифрового двойника (`Digital Twin`) на одном наборе исходных данных;
- описания уровней, узлов, рёбер, шахт, станций и сервисных точек без привязки к конкретному компилятору топологии (`Topology Compiler`);
- отделения декларативной конфигурации склада от состояния исполнения.

Документ сознательно **не** описывает внутреннюю реализацию `Topology Compiler`, формат хранения координат, протоколы обмена и алгоритмы планирования.

---

## 2. Принципы модели

- Конфигурация является **декларативной** и задаётся при развёртывании.
- Канонический источник конфигурации для фазы 1 хранится в версионируемых YAML-файлах в репозитории; JSON допускается как эквивалентное представление или производный артефакт.
- Управляющая логика работает только с логическими `NodeId`.
- Сырые координаты, QR, RFID, LiDAR и иные способы локализации остаются за пределами этой модели.
- Конфигурация описывает **что существует** в топологии, но не содержит состояния исполнения `Reservation`, `DeviceSession`, `Fault`.
- Станции в конфигурации описывают границы передачи и внешние бизнес-адреса, а не полный инвентарный состав оборудования площадки.
- `ChargeNode` и `ServiceNode` моделируются как пассивные сервисные точки, а не как `StationBoundary`.

---

## 3. Корневой агрегат конфигурации

```text
WarehouseTopologyConfig {
  topologyId
  version
  levels[]
  nodes[]
  edges[]
  shafts[]
  stations[]
  servicePoints[]
  deviceBindings[]
  endpointMappings[]
}
```

Смысл полей:

- `levels[]` — список уровней склада;
- `nodes[]` — топологические узлы;
- `edges[]` — направленные связи между узлами;
- `shafts[]` — описание вертикальных шахт и их точек передачи;
- `stations[]` — границы загрузки и выгрузки;
- `servicePoints[]` — зарядные и сервисные точки без отдельной станции;
- `deviceBindings[]` — статические привязки ресурсов к топологии;
- `endpointMappings[]` — сопоставление внешних бизнес-адресов со станциями и сервисными точками.

---

## 4. Конфигурационные сущности

### 4.1. Уровень

```text
LevelConfig {
  levelId
  ordinal
  name?
}
```

### 4.2. Узел

```text
TopologyNodeConfig {
  nodeId
  nodeType
  levelId?
  tags[]
  stationId?
  shaftId?
  servicePointId?
}
```

Поддерживаемые `nodeType` текущего базового состава:

- `TravelNode`
- `SwitchNode`
- `TransferPoint`
- `CarrierNode`
- `StationNode`
- `ChargeNode`
- `ServiceNode`

Правила ссылок:

- `StationNode` ссылается на `stationId`;
- `CarrierNode` и `TransferPoint` ссылаются на `shaftId`;
- `ChargeNode` и `ServiceNode` ссылаются на `servicePointId`.

### 4.3. Ребро

```text
TopologyEdgeConfig {
  edgeId
  fromNodeId
  toNodeId
  traversalMode
  weight
}
```

Поддерживаемые `traversalMode`:

- `OPEN`
- `CARRIER_ONLY`
- `RESTRICTED`

### 4.4. Вертикальная шахта

```text
CarrierShaftConfig {
  shaftId
  carrierDeviceId
  slotCount
  stops[] {
    levelId
    carrierNodeId
    transferPointId
  }
}
```

Для текущего базового состава:

- `slotCount = 1`;
- на каждом уровне, доступном лифту, должна существовать пара `CarrierNode + TransferPoint`.

### 4.5. Станция

```text
StationConfig {
  stationId
  stationType: LOAD | UNLOAD
  controlMode: PASSIVE | ACTIVE
  attachedNodeId
  bufferCapacity
}
```

Для текущего базового состава:

- поддерживается только `controlMode = PASSIVE`;
- `attachedNodeId` обязан указывать на `StationNode`;
- `StationConfig` описывает семантику границы передачи, а не устройство или локальный контроллер площадки;
- `controlMode` описывает способ взаимодействия платформы с границей станции: `PASSIVE` через сигналы площадки и подтверждённые факты передачи, `ACTIVE` через активное управляемое оборудование;
- возможная будущая привязка активной границы станции к конкретному устройству остаётся расширением вне `v0`.

### 4.6. Сервисная точка

```text
ServicePointConfig {
  servicePointId
  servicePointType: CHARGE | SERVICE
  nodeId
  passiveSemantics: ARRIVAL_CONFIRMS_ENGAGEMENT
}
```

Для текущего базового состава:

- `ChargeNode` и `ServiceNode` не образуют `StationBoundary`;
- `ARRIVAL_CONFIRMS_ENGAGEMENT` означает, что `NodeReached(nodeId)` достаточно для фиксации прибытия к точке;
- для `ChargeNode` это же правило используется как подтверждение начала шага зарядки, если не требуется отдельная стыковка.

### 4.7. Привязка устройства

```text
DeviceBindingConfig {
  deviceId
  family
  initialNodeId?
  homeNodeId?
  shaftId?
}
```

Правила:

- `Shuttle3D` не должен требовать `shaftId` как обязательный атрибут;
- `HybridLift` обязан быть связан с одной `CarrierShaftConfig`;
- `initialNodeId` и `homeNodeId` задают статическую конфигурацию, но не заменяют текущее состояние исполнения устройства.

### 4.8. Отображение внешних конечных точек

```text
EndpointMappingConfig {
  endpointId
  endpointKind: LOAD_STATION | UNLOAD_STATION | CHARGE_POINT | SERVICE_POINT
  stationId?
  servicePointId?
}
```

Смысл:

- верхний контур работает с бизнес-адресами;
- внутренний контур исполнения работает с `NodeId`, `stationId`, `servicePointId`;
- конкретная трансляция бизнес-адресов в `NodeId` реализуется вне этого документа;
- `endpointId` должен оставаться стабильным бизнес-адресом границы станции или сервисной точки независимо от физической реализации оборудования.

---

## 5. Инварианты конфигурации

1. Все идентификаторы конфигурационных сущностей уникальны в пределах `topologyId`.
2. Каждый `StationConfig.attachedNodeId` обязан указывать на существующий `StationNode`.
3. Каждый `ServicePointConfig.nodeId` обязан указывать на существующий `ChargeNode` или `ServiceNode`.
4. Каждая шахта должна иметь не более одного `CarrierNode` на уровень.
5. Для каждого `CarrierNode` текущего базового состава обязан существовать соответствующий `TransferPoint`.
6. Рёбра `CARRIER_ONLY` не должны использоваться для прямого движения шаттла вне `VerticalCarrier`.
7. Топология не должна допускать прямой переход из уровневого графа в `CarrierNode` в обход `TransferPoint`.
8. Пассивная станция не должна иметь собственного `deviceId`, `sessionId` или нижнего управляющего канала в конфигурации.
9. `endpointMappings[]` не должны указывать на произвольный `TravelNode`.
10. `EndpointId` не должен использоваться как псевдоним `deviceId`: внешняя адресация привязывается к границе станции или сервисной точке, а не к конкретному активному устройству.

---

## 6. Минимальный пример

```yaml
topologyId: WH-A
version: 0
levels:
  - levelId: L1
    ordinal: 1
  - levelId: L2
    ordinal: 2

nodes:
  - nodeId: L1_TRAVEL_001
    nodeType: TravelNode
    levelId: L1
    tags: []
  - nodeId: L1_SWITCH_A
    nodeType: SwitchNode
    levelId: L1
    tags: []
  - nodeId: L1_TP_LIFT_A
    nodeType: TransferPoint
    levelId: L1
    shaftId: LIFT_A
    tags: []
  - nodeId: L1_CARRIER_A
    nodeType: CarrierNode
    levelId: L1
    shaftId: LIFT_A
    tags: []
  - nodeId: L1_LOAD_01
    nodeType: StationNode
    levelId: L1
    stationId: LOAD_01
    tags: []
  - nodeId: L2_UNLOAD_01
    nodeType: StationNode
    levelId: L2
    stationId: UNLOAD_01
    tags: []
  - nodeId: L2_CHARGE_01
    nodeType: ChargeNode
    levelId: L2
    servicePointId: CHARGE_01
    tags: []

edges:
  - edgeId: E1
    fromNodeId: L1_TRAVEL_001
    toNodeId: L1_SWITCH_A
    traversalMode: OPEN
    weight: 1
  - edgeId: E2
    fromNodeId: L1_SWITCH_A
    toNodeId: L1_TP_LIFT_A
    traversalMode: OPEN
    weight: 1
  - edgeId: E3
    fromNodeId: L1_CARRIER_A
    toNodeId: L2_CARRIER_A
    traversalMode: CARRIER_ONLY
    weight: 1

shafts:
  - shaftId: LIFT_A
    carrierDeviceId: LIFT_A_DEVICE
    slotCount: 1
    stops:
      - levelId: L1
        carrierNodeId: L1_CARRIER_A
        transferPointId: L1_TP_LIFT_A
      - levelId: L2
        carrierNodeId: L2_CARRIER_A
        transferPointId: L2_TP_LIFT_A

stations:
  - stationId: LOAD_01
    stationType: LOAD
    controlMode: PASSIVE
    attachedNodeId: L1_LOAD_01
    bufferCapacity: 1
  - stationId: UNLOAD_01
    stationType: UNLOAD
    controlMode: PASSIVE
    attachedNodeId: L2_UNLOAD_01
    bufferCapacity: 1

servicePoints:
  - servicePointId: CHARGE_01
    servicePointType: CHARGE
    nodeId: L2_CHARGE_01
    passiveSemantics: ARRIVAL_CONFIRMS_ENGAGEMENT

deviceBindings:
  - deviceId: SHUTTLE_01
    family: Shuttle3D
    initialNodeId: L1_TRAVEL_001
    homeNodeId: L1_TRAVEL_001
  - deviceId: LIFT_A_DEVICE
    family: HybridLift
    shaftId: LIFT_A
    initialNodeId: L1_CARRIER_A

endpointMappings:
  - endpointId: inbound.main
    endpointKind: LOAD_STATION
    stationId: LOAD_01
  - endpointId: outbound.main
    endpointKind: UNLOAD_STATION
    stationId: UNLOAD_01
  - endpointId: charge.l2.a
    endpointKind: CHARGE_POINT
    servicePointId: CHARGE_01
```

---

## 7. Что сознательно не фиксируется в v0

- внутренняя реализация компилятора топологии;
- способ хранения сырых координат и калибровочных данных;
- полная схема адресации WMS;
- алгоритмы построения маршрута и эвристики резервирования;
- формат сериализации конфигурации при передаче по сети.
