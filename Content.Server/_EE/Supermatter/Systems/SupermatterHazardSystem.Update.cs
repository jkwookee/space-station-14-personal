using System.Linq;
using System.Numerics;
using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterHazardSystem
{
    /// <summary>
    /// Shoot lightning bolts depending on accumulated power.
    /// </summary>
    private void SupermatterZap(Entity<SupermatterHazardComponent> ent)
    {
        var comp = ent.Comp;

        var zapPower = 0;
        var zapCount = 0;
        var zapRange = Math.Clamp(comp.HazardPower / 1000, 2, 7);

        if (_random.Prob(0.05f))
            zapCount += 1;

        if (comp.HazardPower >= _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold))
            zapCount += 2;

        if (comp.HazardPower >= _config.GetCVar(EECCVars.SupermatterSeverePowerPenaltyThreshold))
        {
            zapPower += 1;
            zapCount += 1;
        }

        if (comp.HazardPower >= _config.GetCVar(EECCVars.SupermatterCriticalPowerPenaltyThreshold))
        {
            zapPower += 1;
            zapCount += 1;
        }

        if (zapCount >= 1)
            _lightning.ShootRandomLightnings(ent, zapRange, zapCount, comp.LightningPrototypes[zapPower], hitCoordsChance: comp.ZapHitCoordinatesChance, canExplode: false);
    }

    /// <summary>
    /// Generate temporary anomalies depending on accumulated power.
    /// </summary>
    private void GenerateAnomalies(Entity<SupermatterHazardComponent> ent)
    {
        var comp = ent.Comp;
        var xform = Transform(ent);
        var anomalies = new List<string>();

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        // Bluespace anomaly: ~1/150 chance
        if (_random.Prob(1 / comp.AnomalyBluespaceChance))
            anomalies.Add(comp.AnomalyBluespaceSpawnPrototype);

        // Gravity anomaly: ~1/150 chance above SeverePowerPenaltyThreshold, or ~1/750 chance otherwise
        if (comp.HazardPower > _config.GetCVar(EECCVars.SupermatterSeverePowerPenaltyThreshold) && _random.Prob(1 / comp.AnomalyGravityChanceSevere) ||
            _random.Prob(1 / comp.AnomalyGravityChance))
            anomalies.Add(comp.AnomalyGravitySpawnPrototype);

        // Pyroclastic anomaly: ~1/375 chance above SeverePowerPenaltyThreshold, or ~1/2500 chance above PowerPenaltyThreshold
        if (comp.HazardPower > _config.GetCVar(EECCVars.SupermatterSeverePowerPenaltyThreshold) && _random.Prob(1 / comp.AnomalyPyroChanceSevere) ||
            comp.HazardPower > _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold) && _random.Prob(1 / comp.AnomalyPyroChance))
            anomalies.Add(comp.AnomalyPyroSpawnPrototype);

        var count = anomalies.Count;
        if (count == 0)
            return;

        var tiles = GetSpawningPoints((ent, comp), count);
        if (tiles == null)
            return;

        foreach (var tileref in tiles)
        {
            var anomaly = Spawn(_random.Pick(anomalies), _map.ToCenterCoordinates(tileref, grid));
            EnsureComp<TimedDespawnComponent>(anomaly).Lifetime = sm.AnomalyLifetime;
        }
    }

    /// <summary>
    /// Gets random points around the supermatter.
    /// Most of this is from GetSpawningPoints() in SharedAnomalySystem
    /// </summary>
    private List<TileRef>? GetSpawningPoints(Entity<SupermatterHazardComponent> ent, int amount)
    {
        var comp = ent.Comp;
        var xform = Transform(ent);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var localpos = xform.Coordinates.Position;
        var tilerefs = _map.GetLocalTilesIntersecting(
            xform.GridUid.Value,
            grid,
            new Box2(localpos + new Vector2(-comp.AnomalySpawnMaxRange, -comp.AnomalySpawnMaxRange), localpos + new Vector2(comp.AnomalySpawnMaxRange, comp.AnomalySpawnMaxRange)))
            .ToList();

        if (tilerefs.Count == 0)
            return null;

        var physQuery = GetEntityQuery<PhysicsComponent>();
        var resultList = new List<TileRef>();
        while (resultList.Count < amount)
        {
            if (tilerefs.Count == 0)
                break;

            var tileref = _random.Pick(tilerefs);
            var distance = MathF.Sqrt(MathF.Pow(tileref.X - xform.LocalPosition.X, 2) + MathF.Pow(tileref.Y - xform.LocalPosition.Y, 2));

            // Cut outer & inner circle
            if (distance > comp.AnomalySpawnMaxRange || distance < comp.AnomalySpawnMinRange)
            {
                tilerefs.Remove(tileref);
                continue;
            }

            var valid = true;

            foreach (var entity in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tileref.GridIndices))
            {
                if (!physQuery.TryGetComponent(entity, out var body))
                    continue;

                if (body.BodyType != BodyType.Static ||
                    !body.Hard ||
                    (body.CollisionLayer & (int)CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }

            if (!valid)
            {
                tilerefs.Remove(tileref);
                continue;
            }

            resultList.Add(tileref);
        }

        return resultList;
    }
}
