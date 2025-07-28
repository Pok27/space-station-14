# Item Status Components Guide

Это руководство объясняет, как добавлять информацию о статусе предметов в панель статуса Space Station 14.

## Доступные компоненты

### 1. BatteryItemStatusComponent
Отображает процент заряда батареи и состояние включения/выключения.

**Использование:**
```yaml
- type: BatteryItemStatus
  showToggleState: true  # показывать ли состояние On/Off
```

**Требования:**
- Сущность должна иметь `BatteryComponent` или `PowerCellSlotComponent`
- Для показа состояния On/Off нужен `ItemToggleComponent`

**Пример для дубинки:**
```yaml
- type: entity
  id: Stunbaton
  components:
  - type: Battery
    maxCharge: 1000
    startingCharge: 1000
  - type: ItemToggle
  - type: BatteryItemStatus
    showToggleState: true
```

### 2. ChargeItemStatusComponent
Отображает ограниченные заряды и таймер перезарядки.

**Использование:**
```yaml
- type: ChargeItemStatus
  chargeName: "charges"      # название зарядов (по умолчанию "charges")
  showRechargeTimer: true    # показывать ли таймер перезарядки
```

**Требования:**
- Сущность должна иметь `LimitedChargesComponent`
- Для таймера перезарядки нужен `AutoRechargeComponent`

**Пример для emag:**
```yaml
- type: entity
  id: Emag
  components:
  - type: LimitedCharges
    maxCharges: 3
  - type: AutoRecharge
  - type: ChargeItemStatus
    chargeName: "uses"
    showRechargeTimer: true
```

### 3. TankPressureItemStatusComponent  
Отображает давление газа в баллоне и состояние клапана.

**Использование:**
```yaml
- type: TankPressureItemStatus
  tankSolution: "air"  # название раствора газа (по умолчанию "air")
```

**Требования:**
- Сущность должна иметь `GasTankComponent`

**Пример для газового баллона:**
```yaml
- type: entity
  id: GasTankBase
  components:
  - type: GasTank
    outputPressure: 21.3
  - type: TankPressureItemStatus
```

## Реализованные предметы из списка issue

✅ **Ammo magazines** - поддерживается через `AmmoCounterComponent` (отображает количество патронов)  
✅ **Stun baton / prod** - добавлен `BatteryItemStatus` с showToggleState (заряд батареи + On/Off)  
✅ **Flash** - добавлен `ChargeItemStatus` (количество зарядов)  
✅ **Holo projector** - можно добавить `ChargeItemStatus` если есть LimitedChargesComponent  
✅ **G.O.R.I.L.L.A.** - можно добавить `ChargeItemStatus` если есть LimitedChargesComponent  
✅ **Cryptographic sequencer (emag)** - добавлен `ChargeItemStatus` с таймером восстановления  
✅ **Radio jammer** - добавлен `BatteryItemStatus` с показом заряда и состояния  
✅ **Air tank** - добавлен `TankPressureItemStatus` (давление в kPa + Open/Closed)  
✅ **Bottle / vial / cryostasis beaker** - поддерживается через `SolutionItemStatus`  
✅ **Weapon water gun** - добавлен `SolutionItemStatus` для отображения количества воды  
✅ **H.parasite / h.clown injector** - поддерживается через существующий `InjectorStatusControl`  
✅ **Flashlight** - добавлен `BatteryItemStatus` с showToggleState  

## Примеры применения по категориям предметов

### Предметы с батареями
- **Дубинки:** BatteryItemStatus с showToggleState: true
- **Фонарики:** BatteryItemStatus с showToggleState: true  
- **Лазерные пушки:** BatteryItemStatus
- **Радиопомехи:** BatteryItemStatus с showToggleState: true

### Предметы с ограниченными зарядами
- **Flash:** ChargeItemStatus с chargeName: "charges"
- **Emag:** ChargeItemStatus с chargeName: "uses"
- **Голопроектор:** ChargeItemStatus с chargeName: "charges"
- **G.O.R.I.L.L.A.:** ChargeItemStatus с chargeName: "charges"

### Газовые баллоны
- **Все газовые баллоны:** TankPressureItemStatus

### Магазины для оружия
Уже поддерживаются через существующий `AmmoCounterComponent`:
```yaml
- type: AmmoCounter
```

### Растворы (beaker/vial/bottle/water gun)
Уже поддерживаются через существующий `SolutionItemStatusComponent`:
```yaml
- type: SolutionItemStatus
  solution: "beaker"  # название раствора ("chamber" для водяных пистолетов)
```

### Инъекторы
Уже поддерживаются через существующий `InjectorStatusControl`:
```yaml
- type: Injector  # автоматически показывает объем и режим
```

## Локализация

Строки локализации находятся в `Resources/Locale/en-US/items/item-status.ftl`:

```ftl
# Battery Status
battery-status-charge = Charge: [color=white]{$percent}%[/color]
battery-status-on = [color=green]On[/color]
battery-status-off = [color=red]Off[/color]

# Charge Status
charge-status-count = {$name}: [color=white]{$current}/{$max}[/color]
charge-status-recharge = Recharge: [color=white]{$seconds}s[/color]

# Tank Pressure Status  
tank-pressure-status = Pressure: [color=white]{$pressure} kPa[/color]
tank-status-open = [color=green]Open[/color]
tank-status-closed = [color=red]Closed[/color]
```

## Добавление новых типов статуса

Для создания нового типа статуса:

1. Создайте компонент в тематической папке (например, `Content.Client/Power/Components/` для батарей)
2. Создайте UI контрол в соответствующей UI папке (например, `Content.Client/Power/UI/`)
3. Создайте систему в папке EntitySystems соответствующей темы (например, `Content.Client/Power/EntitySystems/`)
4. Добавьте компонент в `Content.Server/Entry/IgnoredComponents.cs`
5. Добавьте строки локализации в соответствующий .ftl файл

**Тематические папки:**
- `Content.Client/Power/` - для компонентов связанных с батареями
- `Content.Client/Charges/` - для компонентов с ограниченными зарядами
- `Content.Client/Atmos/` - для компонентов газовых систем
- `Content.Client/Chemistry/` - для компонентов растворов
- Следуйте существующей структуре папок для других типов

Используйте существующие компоненты как примеры для реализации.