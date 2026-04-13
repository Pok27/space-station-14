using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controllers;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IClyde _clyde = default!;

    private FirstPersonViewControl? _control;
    private ICursor? _hiddenCursor;
    private bool? _mainViewportWasVisible;
    private bool? _mainViewportAlwaysRender;

    public bool Enabled => _control?.Visible == true;

    public void OnStateEntered(GameplayState state)
    {
        _control = new FirstPersonViewControl
        {
            Visible = false,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        UIManager.RootControl.AddChild(_control);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_control == null)
            return;

        UIManager.RootControl.RemoveChild(_control);
        _control.Dispose();
        _control = null;
        _clyde.SetCursor(null);
        _hiddenCursor?.Dispose();
        _hiddenCursor = null;
        RestoreMainViewportRender();
    }

    public void Toggle()
    {
        if (_control == null)
            return;

        _control.Visible = !_control.Visible;
        UpdateMainViewportRender();
        UpdateCursor();
    }

    public void SetEnabled(bool enabled)
    {
        if (_control == null)
            return;

        _control.Visible = enabled;
        UpdateMainViewportRender();
        UpdateCursor();
    }

    public bool TryGetControl([NotNullWhen(true)] out FirstPersonViewControl? control)
    {
        control = _control;
        return control != null;
    }

    private void UpdateCursor()
    {
        if (!Enabled)
        {
            _clyde.SetCursor(null);
            return;
        }

        _hiddenCursor ??= CreateHiddenCursor();
        _clyde.SetCursor(_hiddenCursor);
    }

    private void UpdateMainViewportRender()
    {
        var viewport = UIManager.ActiveScreen?.GetWidget<MainViewport>();
        if (viewport == null)
            return;

        if (Enabled)
        {
            _mainViewportWasVisible ??= viewport.Visible;
            _mainViewportAlwaysRender ??= viewport.Viewport.AlwaysRender;
            viewport.Viewport.AlwaysRender = false;
            viewport.Visible = false;
            return;
        }

        RestoreMainViewportRender();
    }

    private void RestoreMainViewportRender()
    {
        var viewport = UIManager.ActiveScreen?.GetWidget<MainViewport>();
        if (viewport == null)
            return;

        if (_mainViewportAlwaysRender.HasValue)
            viewport.Viewport.AlwaysRender = _mainViewportAlwaysRender.Value;

        if (_mainViewportWasVisible.HasValue)
            viewport.Visible = _mainViewportWasVisible.Value;

        _mainViewportAlwaysRender = null;
        _mainViewportWasVisible = null;
    }

    private ICursor CreateHiddenCursor()
    {
        using var image = new Image<Rgba32>(1, 1);
        image[0, 0] = new Rgba32(0, 0, 0, 0);
        return _clyde.CreateCursor(image.Clone(), Vector2i.Zero);
    }
}

