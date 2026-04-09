using Content.Shared.Atmos.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Client.Atmos.Overlays;

public sealed class AtmosDeviceOrderOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;

    private readonly SharedTransformSystem _xformSys;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public AtmosDeviceOrderOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xformSys = _entManager.System<SharedTransformSystem>();
        _font = new VectorFont(_resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space != OverlaySpace.ScreenSpace)
            return;

        if (args.MapId == MapId.Nullspace)
            return;

        var eye = _eyeManager.CurrentEye;

        var query = _entManager.EntityQueryEnumerator<AtmosDeviceComponent, TransformComponent>();
        while (query.MoveNext(out _, out var atmos, out var xform))
        {
            if (atmos.DeviceOrder == -1)
                continue;

            if (xform.MapID != args.MapId)
                continue;

            var worldPos = _xformSys.GetWorldPosition(xform);
            if (!args.WorldBounds.Contains(worldPos))
                continue;

            // Check if within the eye's draw FOV
            if (!eye.DrawFov)
                continue;

            var anchorOffset = atmos.Location switch
            {
                OrderOverlayLocation.TopRight => new Vector2(0.4f, 0.4f),
                OrderOverlayLocation.TopLeft => new Vector2(-0.4f, 0.4f),
                OrderOverlayLocation.BottomRight => new Vector2(0.4f, -0.4f),
                OrderOverlayLocation.BottomLeft => new Vector2(-0.4f, -0.4f),
                _ => new Vector2(0.4f, 0.4f),
            };

            var screenPos = args.Viewport.WorldToLocal(worldPos + anchorOffset);
            args.ScreenHandle.DrawString(_font, screenPos, atmos.DeviceOrder.ToString(), Color.Cyan);
        }
    }
}
