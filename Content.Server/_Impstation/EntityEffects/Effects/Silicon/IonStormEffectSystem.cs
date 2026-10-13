using Content.Server.Silicons.Laws;
using Content.Shared.EntityEffects;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared._Impstation.EntityEffects.Effects.Silicon; // IMP IonStorm is imp scoped
namespace Content.Server._DV.EntityEffects.Effects.Silicon;

public sealed class IonStormEffectSystem : EntityEffectSystem<IonStormTargetComponent, IonStorm>
{
    [Dependency] private readonly IonStormSystem _ionStorm = default!;

    // dosent target ai rn I think
    protected override void Effect(Entity<IonStormTargetComponent> entity, ref EntityEffectEvent<IonStorm> args)
    {
        if (!TryComp<SiliconLawBoundComponent>(entity, out var laws))
            return;

        return;
        //_ionStorm.IonStormTarget((entity.Owner, laws, entity.Comp));
        //_ionStorm.IonStormTarget(laws.Owner, )
    }
}
