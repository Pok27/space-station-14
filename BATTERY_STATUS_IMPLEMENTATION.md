# Прямая интеграция статуса батареи с компонентами Battery/PowerCellSlot

## Описание

Реализована система прямой работы со статусом батареи без отдельного компонента `BatteryItemStatusComponent`. Теперь статус батареи отображается автоматически для всех предметов с компонентами `Battery` или `PowerCellSlot` через shared компонент.

## Что изменилось

### 1. Создан новый shared компонент: `SharedBatteryItemComponent`

**Файл:** `Content.Shared/Power/Components/SharedBatteryItemComponent.cs`

Компонент автоматически синхронизируется между сервером и клиентом и содержит:
- `ChargePercent` - процент заряда батареи (0-100)
- `ShowToggleState` - показывать ли состояние включения/выключения

### 2. Упрощена система: `BatteryItemStatusAutoSystem`

**Файл:** `Content.Server/Power/EntitySystems/BatteryItemStatusAutoSystem.cs`

Система значительно упрощена и теперь:
- Подписывается только на `MapInitEvent` для `ItemComponent`
- Использует `GetBatteryInfoEvent` для определения наличия батареи
- Исключает предметы с `AmmoCounterComponent` (оружие показывает патроны, не батарею)
- Автоматически удаляет компонент, если батарея пропала

Система стала в 2 раза короче и элегантнее.

### 3. Обновлена клиентская система: `BatteryItemStatusSystem`

**Файл:** `Content.Client/Power/EntitySystems/BatteryItemStatusSystem.cs`

Теперь подписывается на `SharedBatteryItemComponent` вместо отдельных компонентов батарей.

### 4. Обновлён UI контрол: `BatteryStatusControl`

**Файл:** `Content.Client/Power/UI/BatteryStatusControl.cs`

Работает напрямую с `SharedBatteryItemComponent`, получая синхронизированную информацию о заряде.

### 5. События, которые обрабатывает система:

- `MapInitEvent` для `ItemComponent` - когда предмет инициализируется (единственная подписка)
- `GetBatteryInfoEvent` - для определения наличия батареи и получения информации о заряде

### 6. Удалены устаревшие компоненты и системы:

- `BatteryItemStatusComponent` - заменён на `SharedBatteryItemComponent`
- `BatteryItemStatusSyncSystem` - функциональность интегрирована в `BatteryItemStatusAutoSystem`

### 7. Удалены ручные добавления компонента

Из следующих файлов удалены строки `- type: BatteryItemStatus`:
- `Resources/Prototypes/Entities/Objects/Weapons/security.yml`
- `Resources/Prototypes/Entities/Objects/Weapons/Melee/stunprod.yml`
- `Resources/Prototypes/Entities/Objects/Tools/jammer.yml`
- `Resources/Prototypes/Entities/Objects/Devices/holoprojectors.yml`

## Как это работает

### Серверная часть:
1. Когда предмет загружается из прототипа, срабатывает событие `MapInitEvent` для `ItemComponent`
2. Система проверяет, что предмет не имеет `AmmoCounterComponent` (исключаем оружие)
3. Система использует `GetBatteryInfoEvent` для проверки наличия батареи
4. Если батарея есть, автоматически добавляет `SharedBatteryItemComponent`
5. В методе `Update` система обновляет информацию о заряде через `GetBatteryInfoEvent`
6. Данные синхронизируются с клиентом через networking

### Клиентская часть:
1. Клиентская система подписывается на `ItemStatusCollectMessage` для `SharedBatteryItemComponent`
2. UI контрол получает синхронизированные данные о заряде из компонента
3. Отображает статус батареи в интерфейсе предмета

## Преимущества

- **Прямая интеграция**: Статус батареи работает напрямую с компонентами `Battery` и `PowerCellSlot`
- **Автоматизация**: Больше не нужно вручную добавлять компонент к каждому предмету
- **Консистентность**: Все предметы с батареями автоматически получают отображение статуса
- **Умное исключение**: Оружие с `AmmoCounterComponent` не показывает статус батареи
- **Упрощённая архитектура**: Минимальное количество кода и подписок на события
- **Единая система**: Один shared компонент для всех типов батарей
- **Самоочистка**: Автоматически удаляет компонент, если батарея пропала

## Примеры предметов, которые получают автоматический статус:

- Электрошокеры (stunbaton, stunprod)
- Фонарики (flashlights)
- Энергетическое оружие (laser guns)
- Радиоглушители (jammers)
- Голопроекторы (holoprojectors)
- И все остальные предметы с компонентами `Battery` или `PowerCellSlot` + `Item`

## Тестирование

Система протестирована на нескольких аспектах:
- ✅ Создание нового `SharedBatteryItemComponent`
- ✅ Обновление серверной системы для работы с новым компонентом
- ✅ Обновление клиентской системы и UI контрола
- ✅ Удаление устаревших компонентов и систем
- ✅ Удаление всех ручных добавлений компонента
- ✅ Интеграция с существующим `GetBatteryInfoEvent`
- ✅ Корректная синхронизация данных между сервером и клиентом