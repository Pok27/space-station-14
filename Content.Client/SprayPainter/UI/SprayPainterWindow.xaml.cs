using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Decals;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.SprayPainter.UI;

/// <summary>
/// A window to select spray painter settings by object type, as well as pipe colours and decals.
/// </summary>
[GenerateTypedNameReferences]
public sealed partial class SprayPainterWindow : DefaultWindow
{
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    private readonly SpriteSystem _spriteSystem;

    // Events
    public event Action<string, string>? OnSpritePicked;
    public event Action<int, bool>? OnTabChanged;
    public event Action<ProtoId<DecalPrototype>>? OnDecalChanged;
    public event Action<ItemList.ItemListSelectedEventArgs>? OnSetPipeColor;
    public event Action<Color?>? OnDecalColorChanged;
    public event Action<int>? OnDecalAngleChanged;
    public event Action<bool>? OnDecalSnapChanged;

    // Pipe color data
    private ItemList _colorList = default!;
    public Dictionary<string, int> ItemColorIndex = new();

    private Dictionary<string, Color> _currentPalette = new();
    private const string ColorLocKeyPrefix = "pipe-painter-color-";

    // Paintable objects
    private Dictionary<string, Dictionary<string, EntProtoId>> _currentStylesByGroup = new();
    private Dictionary<string, List<string>> _currentGroupsByCategory = new();

    // Tab controls
    private Dictionary<string, SprayPainterGroup> _paintableControls = new();
    private BoxContainer? _pipeControl;

    // Decals
    private List<SprayPainterDecalEntry> _currentDecals = [];
    private SprayPainterDecals? _sprayPainterDecals;

    private readonly SpriteSpecifier _colorEntryIconTexture = new SpriteSpecifier.Rsi(
        new ResPath("Structures/Piping/Atmospherics/pipe.rsi"),
        "pipeStraight");

    public SprayPainterWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        _spriteSystem = _sysMan.GetEntitySystem<SpriteSystem>();
        Tabs.OnTabChanged += (index) => OnTabChanged?.Invoke(index, _sprayPainterDecals?.GetPositionInParent() == index);
    }

    private string GetColorLocString(string? colorKey)
    {
        if (string.IsNullOrEmpty(colorKey))
            return Loc.GetString("pipe-painter-no-color-selected");
        var locKey = ColorLocKeyPrefix + colorKey;

        if (!_loc.TryGetString(locKey, out var locString))
            locString = colorKey;

        return locString;
    }

    public string? IndexToColorKey(int index)
    {
        return _colorList[index].Text;
    }

    private void OnStyleSelected(ListData data)
    {
        if (data is SpriteListData listData)
            OnSpritePicked?.Invoke(listData.Group, listData.Style);
    }

    /// <summary>
    /// Wrapper to allow for selecting/deselecting the event to avoid loops
    /// </summary>
    private void OnColorPicked(ItemList.ItemListSelectedEventArgs args)
    {
        OnSetPipeColor?.Invoke(args);
    }

    /// <summary>
    /// Setup function for the window.
    /// </summary>
    /// <param name="stylesByGroup">Each group, mapped by name to the set of named styles by their associated entity prototype.</param>
    /// <param name="groupsByCategory">The set of categories and the groups associated with them.</param>
    /// <param name="decals">A list of each decal.</param>
    public void PopulateCategories(Dictionary<string, Dictionary<string, EntProtoId>> stylesByGroup, Dictionary<string, List<string>> groupsByCategory, List<SprayPainterDecalEntry> decals)
    {
        bool tabsCleared = false;
        var lastTab = Tabs.CurrentTab;

        if (!_currentGroupsByCategory.Equals(groupsByCategory))
        {
            // Destroy all existing tabs
            tabsCleared = true;
            _paintableControls.Clear();
            _pipeControl = null;
            _sprayPainterDecals = null;
            Tabs.RemoveAllChildren();
        }

        // Only clear if the entries change. Otherwise the list would "jump" after selecting an item
        if (tabsCleared || !_currentStylesByGroup.Equals(stylesByGroup))
        {
            _currentStylesByGroup = stylesByGroup;

            var tabIndex = 0;
            foreach (var (categoryName, categoryGroups) in groupsByCategory.OrderBy(c => c.Key))
            {
                if (categoryGroups.Count <= 0)
                    continue;

                // Repopulating controls:
                //      ensure that categories with multiple groups have separate subtabs
                //      but single-group categories do not.
                if (tabsCleared)
                {
                    TabContainer? subTabs = null;
                    if (categoryGroups.Count > 1)
                        subTabs = new();

                    foreach (var group in categoryGroups)
                    {
                        if (!stylesByGroup.TryGetValue(group, out var styles))
                            continue;

                        var groupControl = new SprayPainterGroup();
                        groupControl.OnButtonPressed += OnStyleSelected;
                        _paintableControls[group] = groupControl;
                        if (categoryGroups.Count > 1)
                        {
                            if (subTabs != null)
                            {
                                subTabs?.AddChild(groupControl);
                                var subTabLocalization = Loc.GetString("spray-painter-tab-group-" + group.ToLower());
                                TabContainer.SetTabTitle(groupControl, subTabLocalization);
                            }
                        }
                        else
                        {
                            Tabs.AddChild(groupControl);
                        }
                    }

                    if (subTabs != null)
                        Tabs.AddChild(subTabs);

                    var tabLocalization = Loc.GetString("spray-painter-tab-category-" + categoryName.ToLower());
                    Tabs.SetTabTitle(tabIndex, tabLocalization);
                    tabIndex++;
                }

                // Finally, populate all groups with new data.
                foreach (var group in categoryGroups)
                {
                    if (!stylesByGroup.TryGetValue(group, out var styles) ||
                        !_paintableControls.TryGetValue(group, out var control))
                        continue;

                    var dataList = styles
                        .Select(e => new SpriteListData(group, e.Key, e.Value, 0))
                        .OrderBy(d => Loc.GetString($"spray-painter-style-{group.ToLower()}-{d.Style.ToLower()}"))
                        .ToList();
                    control.PopulateList(dataList);
                }
            }
        }

        PopulateColors(_currentPalette);

        if (!_currentDecals.Equals(decals))
        {
            _currentDecals = decals;

            if (_sprayPainterDecals is null)
            {
                _sprayPainterDecals = new SprayPainterDecals();

                _sprayPainterDecals.OnDecalSelected += id => OnDecalChanged?.Invoke(id);
                _sprayPainterDecals.OnColorChanged += color => OnDecalColorChanged?.Invoke(color);
                _sprayPainterDecals.OnAngleChanged += angle => OnDecalAngleChanged?.Invoke(angle);
                _sprayPainterDecals.OnSnapChanged += snap => OnDecalSnapChanged?.Invoke(snap);

                Tabs.AddChild(_sprayPainterDecals);
                TabContainer.SetTabTitle(_sprayPainterDecals, Loc.GetString("spray-painter-tab-category-decals"));
            }

            _sprayPainterDecals.PopulateDecals(decals, _spriteSystem);
        }

        if (tabsCleared)
            SetSelectedTab(lastTab);
    }

    public void PopulateColors(Dictionary<string, Color> palette)
    {
        // Create pipe tab controls if they don't exist
        bool tabCreated = false;
        if (_pipeControl == null)
        {
            _pipeControl = new BoxContainer() { Orientation = BoxContainer.LayoutOrientation.Vertical };

            var label = new Label() { Text = Loc.GetString("spray-painter-selected-color") };

            _colorList = new ItemList() { VerticalExpand = true };
            _colorList.OnItemSelected += OnColorPicked;

            _pipeControl.AddChild(label);
            _pipeControl.AddChild(_colorList);

            Tabs.AddChild(_pipeControl);
            TabContainer.SetTabTitle(_pipeControl, Loc.GetString("spray-painter-tab-category-pipes"));
            tabCreated = true;
        }

        // Populate the tab if needed (new tab/new data)
        if (tabCreated || !_currentPalette.Equals(palette))
        {
            _currentPalette = palette;
            ItemColorIndex.Clear();
            _colorList.Clear();

            int index = 0;
            foreach (var color in palette)
            {
                var locString = GetColorLocString(color.Key);
                var item = _colorList.AddItem(locString, _spriteSystem.Frame0(_colorEntryIconTexture), metadata: color.Key);
                item.IconModulate = color.Value;

                ItemColorIndex.Add(color.Key, index);
                index++;
            }
        }
    }

    # region Setters
    public void SetSelectedStyles(Dictionary<string, string> selectedStyles)
    {
        foreach (var (group, style) in selectedStyles)
        {
            if (!_paintableControls.TryGetValue(group, out var control))
                continue;

            control.SelectItemByStyle(style);
        }
    }

    public void SelectColor(string color)
    {
        if (_colorList != null && ItemColorIndex.TryGetValue(color, out var colorIdx))
        {
            _colorList.OnItemSelected -= OnColorPicked;
            _colorList[colorIdx].Selected = true;
            _colorList.OnItemSelected += OnColorPicked;
        }
    }

    public void SetSelectedTab(int tab)
    {
        Tabs.CurrentTab = int.Min(tab, Tabs.ChildCount - 1);
    }

    public void SetSelectedDecal(string decal)
    {
        if (_sprayPainterDecals != null)
            _sprayPainterDecals.SetSelectedDecal(decal);
    }

    public void SetDecalAngle(int angle)
    {
        if (_sprayPainterDecals != null)
            _sprayPainterDecals.SetAngle(angle);
    }

    public void SetDecalColor(Color? color)
    {
        if (_sprayPainterDecals != null)
            _sprayPainterDecals.SetColor(color);
    }

    public void SetDecalSnap(bool snap)
    {
        if (_sprayPainterDecals != null)
            _sprayPainterDecals.SetSnap(snap);
    }
    # endregion
}

public record SpriteListData(string Group, string Style, EntProtoId Prototype, int SelectedIndex) : ListData;
