using Content.Server.Botany.Components;

class DebugTest
{
    public static void TestComponentInit()
    {
        // Создаем компонент напрямую (как в коде)
        var directComponent = new BasicGrowthComponent();
        Console.WriteLine($"Direct creation: Water={directComponent.WaterConsumption}, Nutrient={directComponent.NutrientConsumption}");
        
        // Клонируем компонент
        var clonedComponent = directComponent.DupeComponent();
        Console.WriteLine($"After DupeComponent: Water={clonedComponent.WaterConsumption}, Nutrient={clonedComponent.NutrientConsumption}");
        
        // Проверяем что в компоненте задается нулевыми значениями
        var zeroComponent = new BasicGrowthComponent() { WaterConsumption = 0, NutrientConsumption = 0 };
        Console.WriteLine($"Zero values: Water={zeroComponent.WaterConsumption}, Nutrient={zeroComponent.NutrientConsumption}");
        
        var clonedZero = zeroComponent.DupeComponent();
        Console.WriteLine($"Cloned zero: Water={clonedZero.WaterConsumption}, Nutrient={clonedZero.NutrientConsumption}");
    }
}