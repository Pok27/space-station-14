using Robust.Shared.GameStates;

namespace Content.Shared.Mech.Components;

/// <summary>
/// Component for managing mech lock system (DNA and Card locks)
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechLockComponent : Component
{
    #region DNA Lock
    /// <summary>
    /// Whether DNA lock is registered
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DnaLockRegistered = false;

    /// <summary>
    /// Whether DNA lock is active (prevents access)
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DnaLockActive = false;

    /// <summary>
    /// DNA of the lock owner
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? OwnerDna;
    #endregion

    #region Card Lock
    /// <summary>
    /// Whether ID card lock is registered
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CardLockRegistered = false;

    /// <summary>
    /// Whether ID card lock is active (prevents access)
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CardLockActive = false;

    /// <summary>
    /// ID card name of the lock owner
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? OwnerCardName;
    #endregion

    #region Lock State
    /// <summary>
    /// Whether the mech is locked (prevents unauthorized access)
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsLocked = false;
    #endregion
}