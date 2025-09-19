using System;
using Content.Shared.Medical.Disease;
using Content.Shared.Jittering;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomJitter : SymptomBehavior
{
    /// <summary>
    /// Duration in seconds for the jitter effect.
    /// </summary>
    [DataField]
    public float JitterSeconds { get; private set; } = 2.0f;

    /// <summary>
    /// Amplitude of jitter movement.
    /// </summary>
    [DataField]
    public float JitterAmplitude { get; private set; } = 6.0f;

    /// <summary>
    /// Frequency of jitter movement.
    /// </summary>
    [DataField]
    public float JitterFrequency { get; private set; } = 3.0f;
}

public sealed partial class SymptomJitter
{
    [Dependency] private readonly SharedJitteringSystem _jitterSystem = default!;

    /// <summary>
    /// Applies jitter to the carrier for a brief period.
    /// </summary>
    public override void OnSymptom(EntityUid uid, DiseasePrototype disease)
    {
        var dur = TimeSpan.FromSeconds(JitterSeconds);
        _jitterSystem.DoJitter(uid, dur, false, JitterAmplitude, JitterFrequency);
    }
}
