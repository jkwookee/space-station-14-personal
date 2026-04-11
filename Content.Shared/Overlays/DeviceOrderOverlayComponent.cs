using Robust.Shared.GameStates;

namespace Content.Shared.Overlays;

/// <summary>
/// Makes the entity see device order of atmospheric devices.
/// When added to a clothing item it will also grant the wearer the same overlay.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeviceOrderOverlayComponent : Component;
