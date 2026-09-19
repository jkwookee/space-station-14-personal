using Content.Shared._Impstation.CrystalMass;
using Robust.Client.GameObjects;

namespace Content.Server._Impstation.CrystalMass;

public sealed class CrystalMassSystem : SharedCrystalMassSystem
{
    protected override void OnAppearanceChange(EntityUid uid, CrystalMassComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        if (_appearance.TryGetData<int>(uid, CrystalMassVisuals.Variant, out var var, args.Component))
        {
            var index = SpriteSystem.LayerMapReserve((uid, args.Sprite), $"{component.Layer}");
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), index, $"crystal_cascade_{var}");
            args.Sprite.LayerSetShader(index, "unshaded");
        }
    }
}
