using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.StepTrigger.Systems;

namespace Content.Shared._Impstation.CrystalMass;

public abstract class SharedCrystalMassSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrystalMassComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
    }

    private void OnStepTriggerAttempt(Entity<CrystalMassComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (HasComp<SupermatterImmuneComponent>(args.Tripper)
            || HasComp<GodmodeComponent>(args.Tripper)
            || HasComp<GhostComponent>(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        args.Continue = true;
    }
}
