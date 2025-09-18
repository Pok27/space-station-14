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

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Applies jitter to the carrier for a brief period.
    /// </summary>
    private void DoJitter(Entity<DiseaseCarrierComponent> ent, SymptomJitter jitter)
    {
        var jitterSeconds = jitter.JitterSeconds;
        var jitterAmplitude = jitter.JitterAmplitude;
        var jitterFrequency = jitter.JitterFrequency;
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(jitterSeconds), false, jitterAmplitude, jitterFrequency);
    }
}
