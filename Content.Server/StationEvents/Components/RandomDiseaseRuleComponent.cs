using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

/// <summary>
///     Configuration for the RandomDisease station event.
///     Provides a pool of disease prototype IDs to choose from and infection counts.
/// </summary>
[RegisterComponent, Access(typeof(RandomDiseaseRule))]
public sealed partial class RandomDiseaseRuleComponent : Component
{
    /// <summary>
    ///     Pool of diseases to choose from. One will be picked uniformly at random.
    /// </summary>
    [DataField(required: true)]
    public List<string> Disease = new();

    /// <summary>
    ///     Minimum number of players to infect.
    /// </summary>
    [DataField]
    public int MinInfections = 1;

    /// <summary>
    ///     Maximum number of players to infect.
    /// </summary>
    [DataField]
    public int MaxInfections = 3;

    /// <summary>
    ///     If true, immune players are skipped when infecting. If false, we still attempt Infect which will fail if immune.
    /// </summary>
    [DataField]
    public bool SkipImmune = true;
}


