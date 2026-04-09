using Content.Shared.Atmos.Components;
using Content.Shared.Examine;

namespace Content.Shared.Atmos.Piping.EntitySystems;

public abstract class SharedAtmosDeviceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AtmosDeviceComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<AtmosDeviceComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.DeviceOrder == -1)
            return;

        using (args.PushGroup(nameof(AtmosDeviceComponent)))
            args.PushMarkup(Loc.GetString("atmos-device-examine-order", ("deviceOrder", ent.Comp.DeviceOrder)));
    }
}
