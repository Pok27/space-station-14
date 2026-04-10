using Content.Shared.Camera;
using Content.Shared.E3D.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared.E3D.Systems;

public sealed class SharedFirstPersonPvsSystem : EntitySystem
{
    private const float FirstPersonPvsScale = 0.75f;

    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FirstPersonViewComponent, ComponentStartup>(OnFirstPersonStartup);
        SubscribeLocalEvent<FirstPersonViewComponent, ComponentShutdown>(OnFirstPersonShutdown);
        SubscribeLocalEvent<FirstPersonViewComponent, GetEyePvsScaleEvent>(OnGetEyePvsScale);
    }

    private void OnFirstPersonStartup(Entity<FirstPersonViewComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<ContentEyeComponent>(ent.Owner) && HasComp<EyeComponent>(ent.Owner))
            _contentEye.UpdatePvsScale(ent.Owner);
    }

    private void OnFirstPersonShutdown(Entity<FirstPersonViewComponent> ent, ref ComponentShutdown args)
    {
        if (HasComp<ContentEyeComponent>(ent.Owner) && HasComp<EyeComponent>(ent.Owner))
            _contentEye.UpdatePvsScale(ent.Owner);
    }

    private void OnGetEyePvsScale(Entity<FirstPersonViewComponent> ent, ref GetEyePvsScaleEvent args)
    {
        args.Scale += FirstPersonPvsScale;
    }
}
