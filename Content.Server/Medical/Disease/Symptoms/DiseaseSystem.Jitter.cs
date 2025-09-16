using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Applies screen jitter to the carrier for a brief period.
    /// </summary>
    private void DoJitter(Entity<DiseaseCarrierComponent> ent, SymptomJitter jitter)
    {
        var jitterSeconds = jitter.JitterSeconds;
        var jitterAmplitude = jitter.JitterAmplitude;
        var jitterFrequency = jitter.JitterFrequency;
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(jitterSeconds), false, jitterAmplitude, jitterFrequency);
    }
}
