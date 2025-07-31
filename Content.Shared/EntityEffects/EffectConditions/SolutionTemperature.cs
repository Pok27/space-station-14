using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.EffectConditions;

/// <summary>
///     Requires the solution to be above or below a certain temperature.
///     Used for things like explosives.
/// </summary>
public sealed partial class SolutionTemperature : EntityEffectCondition
{
    [DataField]
    public float Min = 0.0f;

    [DataField]
    public float Max = float.PositiveInfinity;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (args is EntityEffectReagentArgs reagentArgs)
        {
            return reagentArgs?.Source != null &&
                   reagentArgs.Source.Temperature >= Min &&
                   reagentArgs.Source.Temperature <= Max;
        }

        // For non-reagent effects, we need to find the solution differently
        // This could be through a component reference, entity lookup, or other means
        // For now, we'll implement a basic approach that looks for solution components
        if (args.EntityManager.TryGetComponent(args.TargetEntity, out SolutionContainerManagerComponent? container))
        {
            // Check the first available solution for temperature
            foreach (var (name, solution) in EnumerateSolutions((args.TargetEntity, container)))
            {
                if (solution.Solution.Temperature >= Min && solution.Solution.Temperature <= Max)
                    return true;
            }
        }

        // If no suitable solution is found, the condition fails
        return false;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-guidebook-solution-temperature",
            ("max", float.IsPositiveInfinity(Max) ? (float) int.MaxValue : Max),
            ("min", Min));
    }
}
