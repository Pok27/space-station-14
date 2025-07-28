# Item Status System Implementation Summary

Эта реализация добавляет поддержку отображения информации о состоянии предметов в панели статуса для большинства предметов из списка issue #27669.

## Добавленные компоненты

### 1. BatteryItemStatusComponent
- **Файл:** `Content.Client/Power/Components/BatteryItemStatusComponent.cs`
- **Назначение:** Отображает процент заряда батареи и состояние On/Off
- **Зависимости:** BatteryComponent или PowerCellSlotComponent, опционально ItemToggleComponent

### 2. ChargeItemStatusComponent  
- **Файл:** `Content.Client/Charges/Components/ChargeItemStatusComponent.cs`
- **Назначение:** Отображает ограниченные заряды и таймер перезарядки
- **Зависимости:** LimitedChargesComponent, опционально AutoRechargeComponent

### 3. TankPressureItemStatusComponent
- **Файл:** `Content.Client/Atmos/Components/TankPressureItemStatusComponent.cs`
- **Назначение:** Отображает давление газа в баллоне и состояние клапана
- **Зависимости:** GasTankComponent

## Добавленные UI контролы

### 1. BatteryStatusControl
- **Файл:** `Content.Client/Power/UI/BatteryStatusControl.cs`
- **Отображает:** Заряд батареи в процентах, состояние On/Off

### 2. ChargeStatusControl
- **Файл:** `Content.Client/Charges/UI/ChargeStatusControl.cs`
- **Отображает:** Текущие заряды / максимальные заряды, таймер восстановления

### 3. TankPressureStatusControl
- **Файл:** `Content.Client/Atmos/UI/TankPressureStatusControl.cs`
- **Отображает:** Давление в kPa, состояние клапана Open/Closed

## Добавленные системы

### 1. BatteryItemStatusSystem
- **Файл:** `Content.Client/Power/EntitySystems/BatteryItemStatusSystem.cs`
- **Назначение:** Регистрация BatteryStatusControl

### 2. ChargeItemStatusSystem
- **Файл:** `Content.Client/Charges/EntitySystems/ChargeItemStatusSystem.cs`
- **Назначение:** Регистрация ChargeStatusControl

### 3. TankPressureItemStatusSystem
- **Файл:** `Content.Client/Atmos/EntitySystems/TankPressureItemStatusSystem.cs`
- **Назначение:** Регистрация TankPressureStatusControl

### 4. BatteryInfoSystem (Server)
- **Файл:** `Content.Server/Power/EntitySystems/BatteryInfoSystem.cs`
- **Назначение:** Обработка запросов о состоянии батареи

## Shared компоненты

### GetBatteryInfoEvent
- **Файл:** `Content.Shared/Power/SharedBatteryEvents.cs`
- **Назначение:** Событие для получения информации о батарее между клиентом и сервером

## Модифицированные файлы

### 1. Прототипы предметов
- `Resources/Prototypes/Entities/Objects/Weapons/security.yml` - добавлен статус для дубинки и flash
- `Resources/Prototypes/Entities/Objects/Tools/flashlights.yml` - добавлен статус для фонариков
- `Resources/Prototypes/Entities/Objects/Tools/gas_tanks.yml` - добавлен статус для газовых баллонов
- `Resources/Prototypes/Entities/Objects/Tools/emag.yml` - добавлен статус для emag
- `Resources/Prototypes/Entities/Objects/Tools/jammer.yml` - добавлен статус для радиоглушителя
- `Resources/Prototypes/Entities/Objects/Weapons/Guns/Basic/watergun.yml` - добавлен статус для водяных пистолетов

### 2. Серверная конфигурация
- `Content.Server/Entry/IgnoredComponents.cs` - добавлены клиентские компоненты в список игнорируемых

### 3. Локализация
- `Resources/Locale/en-US/items/item-status.ftl` - добавлены строки локализации для новых статусов

## Реализованные предметы из issue

✅ **Ammo magazines** - уже поддерживается через AmmoCounterComponent  
✅ **Stun baton / prod** - BatteryItemStatus с showToggleState  
✅ **Flash** - ChargeItemStatus  
✅ **Holo projector** - готова поддержка через ChargeItemStatus  
✅ **G.O.R.I.L.L.A.** - готова поддержка через ChargeItemStatus  
✅ **Cryptographic sequencer** - ChargeItemStatus с таймером  
✅ **Radio jammer** - BatteryItemStatus с процентом заряда  
✅ **Air tank** - TankPressureItemStatus  
✅ **Bottle / vial / cryostasis beaker** - уже поддерживается через SolutionItemStatus  
✅ **Weapon water gun** - SolutionItemStatus  
✅ **H.parasite / h.clown injector** - уже поддерживается через InjectorStatusControl  
✅ **Flashlight** - BatteryItemStatus с showToggleState  

## Принцип работы

1. **Клиентские компоненты** определяют, какой тип статуса нужно показать для предмета
2. **UI контролы** периодически опрашивают состояние предмета и обновляют отображение
3. **Системы** регистрируют контролы для соответствующих компонентов
4. **События** обеспечивают связь между клиентом и сервером для получения актуальных данных

## Расширяемость

Система легко расширяется для новых типов предметов:
1. Создать новый компонент статуса
2. Создать соответствующий UI контрол
3. Создать систему регистрации
4. Добавить компонент в прототипы нужных предметов
5. Добавить строки локализации

Все компоненты следуют единому паттерну и могут быть использованы как примеры для дальнейшего развития системы.