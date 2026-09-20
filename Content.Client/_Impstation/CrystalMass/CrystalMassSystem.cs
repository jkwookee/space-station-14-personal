using Content.Shared._Impstation.CrystalMass;
using Robust.Client.GameObjects;

namespace Content.Client._Impstation.CrystalMass;

public sealed class CrystalMassSystem : SharedCrystalMassSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrystalMassComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<CrystalMassComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<int>(ent, CrystalMassVisuals.Variant, out var variant, args.Component))
            return;

        var index = _sprite.LayerMapReserve((ent.Owner, args.Sprite), $"{ent.Comp.Layer}");
        _sprite.LayerSetRsiState((ent.Owner, args.Sprite), index, $"crystal_cascade_{variant}");
        args.Sprite.LayerSetShader(index, "unshaded");
    }
}
