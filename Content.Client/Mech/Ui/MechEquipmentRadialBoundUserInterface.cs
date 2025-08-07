using Content.Client.UserInterface.Controls;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Localization;

namespace Content.Client.Mech.Ui;

[UsedImplicitly]
public sealed class MechEquipmentRadialBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private SimpleRadialMenu? _menu;

    public MechEquipmentRadialBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!_entManager.TryGetComponent<MechComponent>(Owner, out var mechComp))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);

        var options = new List<RadialMenuOption>();

        // Add "No Equipment" option
        options.Add(new RadialMenuActionOption<string>(data =>
        {
            SendMessage(new MechEquipmentSelectMessage(null));
            Close();
        }, "no_equipment")
        {
            ToolTip = Loc.GetString("mech-radial-no-equipment"),
            Sprite = null
        });

        // Add equipment options
        foreach (var equipment in mechComp.EquipmentContainer.ContainedEntities)
        {
            if (!_entManager.TryGetComponent<MetaDataComponent>(equipment, out var metaData))
                continue;

            var equipmentEntity = equipment;

            // Get tool prototype info if available
            string tooltip = metaData.EntityName;
            SpriteSpecifier? sprite = null;

            if (_entManager.TryGetComponent<ToolComponent>(equipment, out var toolComp))
            {
                if (_prototypeManager.TryIndex(toolComp.Qualities.FirstOrDefault(), out ToolQualityPrototype? qualityProto))
                {
                    tooltip = qualityProto.Name;
                    sprite = qualityProto.Icon;
                }
            }

            options.Add(new RadialMenuActionOption<string>(data =>
            {
                SendMessage(new MechEquipmentSelectMessage(_entManager.GetNetEntity(equipmentEntity)));
                Close();
            }, metaData.EntityName)
            {
                ToolTip = tooltip,
                Sprite = sprite
            });
        }

        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu?.Dispose();
    }
}
