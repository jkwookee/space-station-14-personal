using Content.Client.Atmos.Overlays;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Robust.Client.Graphics;

namespace Content.Client.Overlays;

public sealed partial class AtmosDeviceOrderOverlaySystem : EquipmentHudSystem<DeviceOrderOverlayComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private AtmosDeviceOrderOverlay _atmosDeviceOrderOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _atmosDeviceOrderOverlay = new();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<DeviceOrderOverlayComponent> component)
    {
        base.UpdateInternal(component);

        _overlayMan.AddOverlay(_atmosDeviceOrderOverlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlayMan.RemoveOverlay(_atmosDeviceOrderOverlay);
    }
}
