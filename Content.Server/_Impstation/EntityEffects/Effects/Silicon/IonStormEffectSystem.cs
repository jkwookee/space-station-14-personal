using Content.Server.StationEvents.Events;
using Content.Shared.EntityEffects;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared._Impstation.EntityEffects.Effects.Silicon;
namespace Content.Server._Impstation.EntityEffects.Effects.Silicon;

public sealed class IonStormEffectSystem : EntityEffectSystem<IonStormTargetComponent, IonStorm>
{
    // by default, the AI is not going to be affected by this. during a delam,
    // we add the IonStormTargetComponent to the AI so it is targetted.
    protected override void Effect(Entity<IonStormTargetComponent> entity, ref EntityEffectEvent<IonStorm> args)
    {
        if (!TryComp<SiliconLawBoundComponent>(entity, out var laws))
            return;

        var ev = new IonStormEvent();
        RaiseLocalEvent(entity.Owner, ref ev);
    }
}
