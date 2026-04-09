using Content.Shared.Atmos.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client.Atmos.Overlays;

public sealed class AtmosDeviceOrderOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly SpriteSystem _sprite;

    private readonly TransformSystem _transform;
    private readonly Texture[] _digitTextures = new Texture[10];

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public AtmosDeviceOrderOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entityManager.System<SpriteSystem>();
        _transform = _entityManager.System<TransformSystem>();
        var rsi = _resourceCache.GetResource<RSIResource>(
        new ResPath("Textures/Interface/Alerts/generic_counter.rsi")).RSI;
        for (var i = 0; i < 10; i++)
            _digitTextures[i] = rsi[$"{i}"].Frame0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entityManager.AllEntityQueryEnumerator<AtmosDeviceComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId || !sprite.Visible)
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));

            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var deviceOrder = comp.DeviceOrder;
            if (deviceOrder == -1)
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var curTime = _timing.RealTime;
            // var texture = _sprite.GetFrame(proto.Icon, curTime);

            float yOffset;
            float xOffset;

            // if (texture.Height > _sprite.GetLocalBounds((uid, sprite)).Height * EyeManager.PixelsPerMeter)
            //     break;

            var offset = comp.Location switch
            {
                OrderOverlayLocation.TopRight => new Vector2(4, 4),
                OrderOverlayLocation.TopLeft => new Vector2(-4, 4),
                OrderOverlayLocation.BottomRight => new Vector2(4, -4),
                OrderOverlayLocation.BottomLeft => new Vector2(-4, -4),
                _ => new Vector2(4f, 4f),
            };

            yOffset = (bounds.Height + sprite.Offset.Y) / 2f + offset.Y / EyeManager.PixelsPerMeter;
            xOffset = -(bounds.Width + sprite.Offset.X) / 2f + offset.X / EyeManager.PixelsPerMeter;

            var position = new Vector2(xOffset, yOffset);

            var digits = deviceOrder.ToString();
            var texWidth = _digitTextures[0].Width / (float)EyeManager.PixelsPerMeter;
            for (var d = 0; d < digits.Length; d++)
            {
                var digit = digits[d] - '0';
                handle.DrawTexture(_digitTextures[digit], position + new Vector2(d * texWidth, 0));
            }

            handle.SetTransform(Matrix3x2.Identity);
        }
    }
}
