using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared.Botany;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.Tests.Server
{
    [TestFixture]
    public class BotanyComponentCopyTest : ContentUnitTest
    {
        private BotanySystem _botanySystem = default!;
        private PlantHolderSystem _plantHolderSystem = default!;
        private EntityUid _plantEntity;
        private EntityUid _userEntity;

        [SetUp]
        public void Setup()
        {
            _botanySystem = new BotanySystem();
            _plantHolderSystem = new PlantHolderSystem();
            
            // Register systems
            var entMan = IoCManager.Resolve<IEntityManager>();
            entMan.AddSystem(_botanySystem);
            entMan.AddSystem(_plantHolderSystem);
            
            // Create test entities
            _plantEntity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            _userEntity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        }

        [Test]
        public void TestComponentCopyOnHarvest()
        {
            var entMan = IoCManager.Resolve<IEntityManager>();
            
            // Add PlantHolderComponent to plant
            var plantHolder = entMan.AddComponent<PlantHolderComponent>(_plantEntity);
            
            // Create a test seed with components
            var seed = new SeedData
            {
                Name = "test-seed",
                GrowthComponents = new List<PlantGrowthComponent>
                {
                    new PlantTraitsComponent
                    {
                        Lifespan = 100,
                        Maturation = 10,
                        Production = 5,
                        Yield = 3,
                        Potency = 20
                    }
                }
            };
            
            plantHolder.Seed = seed;
            
            // Add components to plant entity
            entMan.AddComponent<PlantTraitsComponent>(_plantEntity);
            entMan.AddComponent<BasicGrowthComponent>(_plantEntity);
            
            // Test that components are copied during harvest
            var products = _botanySystem.GenerateProduct(seed, MapCoordinates.Nullspace, 1, _plantEntity);
            
            Assert.That(products, Is.Not.Empty);
            
            foreach (var product in products)
            {
                var produce = entMan.GetComponent<ProduceComponent>(product);
                Assert.That(produce.Seed, Is.Not.Null);
                Assert.That(produce.Seed.GrowthComponents, Is.Not.Empty);
                
                // Check that PlantTraitsComponent was copied
                var traitsComponent = produce.Seed.GrowthComponents.OfType<PlantTraitsComponent>().FirstOrDefault();
                Assert.That(traitsComponent, Is.Not.Null);
                Assert.That(traitsComponent.Lifespan, Is.EqualTo(100));
                Assert.That(traitsComponent.Maturation, Is.EqualTo(10));
                Assert.That(traitsComponent.Production, Is.EqualTo(5));
                Assert.That(traitsComponent.Yield, Is.EqualTo(3));
                Assert.That(traitsComponent.Potency, Is.EqualTo(20));
            }
        }
    }
}