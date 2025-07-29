# Автоматическое добавление BatteryItemStatusComponent

## Описание

Реализована система автоматического добавления `BatteryItemStatusComponent` к предметам с батареями. Теперь компонент добавляется автоматически и не требует ручного указания в YAML файлах.

## Что изменилось

### 1. Создана новая система: `BatteryItemStatusAutoSystem`

**Файл:** `Content.Server/Power/EntitySystems/BatteryItemStatusAutoSystem.cs`

Система автоматически добавляет `BatteryItemStatusComponent` к сущностям, которые:
- Имеют компонент `ItemComponent` (могут быть осмотрены)
- Имеют либо `BatteryComponent`, либо `PowerCellSlotComponent`

### 2. События, которые обрабатывает система:

- `MapInitEvent` для `BatteryComponent` - когда предмет с батареей инициализируется
- `MapInitEvent` для `PowerCellSlotComponent` - когда предмет со слотом для батареи инициализируется  
- `MapInitEvent` для `ItemComponent` - когда предмет инициализируется (проверяет наличие батарей)
- `EntInsertedIntoContainerMessage` для `PowerCellSlotComponent` - когда батарея вставляется в слот

### 3. Удалены ручные добавления компонента

Из следующих файлов удалены строки `- type: BatteryItemStatus`:
- `Resources/Prototypes/Entities/Objects/Weapons/security.yml`
- `Resources/Prototypes/Entities/Objects/Weapons/Melee/stunprod.yml`
- `Resources/Prototypes/Entities/Objects/Tools/jammer.yml`
- `Resources/Prototypes/Entities/Objects/Devices/holoprojectors.yml`

## Как это работает

1. Когда предмет с батареей загружается из прототипа, срабатывает событие `MapInitEvent`
2. Система проверяет, является ли предмет осматриваемым (`ItemComponent`)
3. Если да, проверяет наличие батареи (`BatteryComponent` или `PowerCellSlotComponent`)
4. Если батарея есть, автоматически добавляет `BatteryItemStatusComponent`
5. Компонент отображает статус батареи при осмотре предмета

## Преимущества

- **Автоматизация**: Больше не нужно вручную добавлять компонент к каждому предмету
- **Консистентность**: Все предметы с батареями автоматически получают отображение статуса
- **Меньше ошибок**: Невозможно забыть добавить компонент к новому предмету с батареей

## Примеры предметов, которые получают автоматический статус:

- Электрошокеры (stunbaton, stunprod)
- Фонарики (flashlights)
- Энергетическое оружие (laser guns)
- Радиоглушители (jammers)
- Голопроекторы (holoprojectors)
- И все остальные предметы с компонентами `Battery` или `PowerCellSlot` + `Item`

## Тестирование

Система протестирована на нескольких типах предметов:
- ✅ Предметы с `BatteryComponent`
- ✅ Предметы с `PowerCellSlotComponent`
- ✅ Удаление всех ручных добавлений компонента
- ✅ Корректное отображение статуса батареи при осмотре