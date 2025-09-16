using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Area emitter for disease infection (airborne residue/aerosol). Server-driven.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseCloudComponent : Component
{
    /// <summary>
    /// List of disease prototype IDs this cloud can transmit.
    /// </summary>
    [DataField]
    public List<string> Diseases = new();

    /// <summary>
    /// Effective infection radius in world units.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// How often the cloud attempts infections.
    /// </summary>
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(1.0);

    /// <summary>
    /// Lifetime before the cloud expires naturally.
    /// </summary>
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromSeconds(8.0);

    /// <summary>
    /// Next time the cloud will tick infection.
    /// </summary>
    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Time when this cloud should be considered expired.
    /// </summary>
    [DataField]
    public TimeSpan Expiry;
}
