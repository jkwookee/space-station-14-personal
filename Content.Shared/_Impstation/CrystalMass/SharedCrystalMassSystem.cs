using System.Numerics;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared._Impstation.CrystalMass;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Spreader;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Impstation.CrystalMass;

public abstract class SharedCrystalMassSystem : EntitySystem
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

        SubscribeLocalEvent<CrystalMassComponent, ComponentStartup>(SetupCrystalMass);

        SubscribeLocalEvent<CrystalMassComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<CrystalMassComponent, StepTriggeredOnEvent>(OnStepTriggered);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveTileClearCrystalMassComponent, CrystalMassComponent>();
        while (query.MoveNext(out var uid, out _, out var crystal))
        {
            // Requires a delay so that entities can register being on the tile for ClearTile
            ClearTile((uid, crystal));

            // Delay adding pointlight for when multiple are on one tile deleting each other so that it isn't jarring
            if (crystal.IsLight)
            {
                EnsureComp<SharedPointLightComponent>(uid);
                _lights.SetRadius(uid, crystal.LightRadius);
                _lights.SetEnergy(uid, crystal.LightEnergy);
                _lights.SetColor(uid, crystal.LightColor);
            }

            RemCompDeferred<ActiveTileClearCrystalMassComponent>(uid);
        }
    }

    private void SetupCrystalMass(Entity<CrystalMassComponent> ent, ref ComponentStartup args)
    {
        if (!ent.Comp.StartupAppearance)
            return;

        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.SetData(ent, CrystalMassVisuals.Variant, _robustRandom.Next(1, ent.Comp.SpriteVariants + 1), appearance);
    }

    private void ClearTile(Entity<CrystalMassComponent> ent)
    {
        var xform = Transform(ent);
        var gridUid = xform.GridUid;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tilePos = _map.LocalToTile(gridUid.Value, grid, xform.Coordinates);

        foreach (var target in _lookup.GetLocalEntitiesIntersecting(gridUid.Value, tilePos, flags: LookupFlags.Uncontained))
        {
            if (target == ent.Owner)
                continue;

            if (HasComp<SupermatterImmuneComponent>(target)
                || HasComp<GodmodeComponent>(target)
                || HasComp<GhostComponent>(target))
                continue;

            // Prevent multiple crystal mass entities on one tile from queuing everyones downfall
            RemComp<CrystalMassComponent>(target);

            // Popup text for nearby players could be added eventually
            if (HasComp<MobStateComponent>(target)
                || HasComp<ItemComponent>(target))
                _audio.PlayPvs(ent.Comp.DustSound, Transform(target).Coordinates);

            EntityManager.QueueDeleteEntity(target);
        }
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
