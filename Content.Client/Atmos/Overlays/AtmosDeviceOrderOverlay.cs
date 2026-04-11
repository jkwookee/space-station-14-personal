using Content.Shared.Atmos.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client.Atmos.Overlays;

public sealed class AtmosDeviceOrderOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly Texture[] _textures;
    private const int Digits = 10;
    private const string DigitRSIPath = "/Textures/Effects/text.rsi";
    // private const string DigitColor = "#dceb0c";
    private const float DigitScale = 1.25f; // Scaling of digit sprite size
    private const int SpritePixelWidth = 4; // Pixel width of digit sprites
    private const int SpritePixelHeight = 6; // Pixel height of digit sprites
    private const int HeightUntilSprite = 0; // Pixel difference from the top left of the png to the actual sprite
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public AtmosDeviceOrderOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _textures = new Texture[Digits];

        var digit = _resourceCache.GetResource<RSIResource>(new ResPath(DigitRSIPath)).RSI;
        for (var i = 0; i < Digits; i++)
        {
            if (!digit.TryGetState(i.ToString(), out var state))
                throw new ArgumentOutOfRangeException($"Digit RSI doesn't have state \"{i}\"!");

            _textures[i] = state.Frame0;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var xformQuery = _entity.GetEntityQuery<TransformComponent>();
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(DigitScale, DigitScale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entity.AllEntityQueryEnumerator<AtmosDeviceComponent, SpriteComponent, TransformComponent>();
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

            var digits = deviceOrder.ToString();
            var length = digits.Length;

            // Conversion from pixels to meters
            var digitWidth = SpritePixelWidth * (1f / EyeManager.PixelsPerMeter);
            var totalWidth = length * digitWidth;
            var digitHeight = SpritePixelHeight * (1f / EyeManager.PixelsPerMeter);
            var digitHeightUntilSprite = HeightUntilSprite * (1f / EyeManager.PixelsPerMeter);

            var xOffset = comp.Location switch
            {
                OrderOverlayLocation.TopRight or OrderOverlayLocation.BottomRight => bounds.Width / 2f / DigitScale - totalWidth,
                _ => -bounds.Width / 2f / DigitScale,
            };

            var yOffset = comp.Location switch
            {
                OrderOverlayLocation.BottomLeft or OrderOverlayLocation.BottomRight => -bounds.Height / 2f / DigitScale - digitHeightUntilSprite,
                _ => bounds.Height / 2f / DigitScale - digitHeight - digitHeightUntilSprite,
            };

            var position = new Vector2(xOffset, yOffset);

            for (var i = 0; i < length; i++)
            {
                var pos = position + new Vector2(i * digitWidth, 0);
                var digit = _textures[digits[i] - '0']; // Subtracting by ASCII value for correct index

                handle.DrawTexture(digit, pos, Color.Orange);
                // handle.DrawTexture(digit, pos, Color.FromHex(DigitColor));
            }

            handle.SetTransform(Matrix3x2.Identity);
        }
    }
}
