using System.Numerics;
using Content.Server.Spreader;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared._Impstation.CrystalMass;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Spreader;
using Content.Shared.StepTrigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Impstation.CrystalMass;

public sealed class CrystalMassSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TileSystem _tile = default!;

    private static readonly ProtoId<EdgeSpreaderPrototype> CrystalMassGroup = "CrystalMass";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrystalMassComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<CrystalMassComponent, StepTriggeredOnEvent>(OnStepTriggered);
    }

    private void OnStepTriggered(Entity<CrystalMassComponent> ent, ref StepTriggeredOnEvent args)
    {
        if (HasComp<MobStateComponent>(args.Tripper)
            || HasComp<ItemComponent>(args.Tripper))
            _audio.PlayPvs(ent.Comp.DustSound, Transform(args.Tripper).Coordinates);

        EntityManager.QueueDeleteEntity(args.Tripper);
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
