using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Chat.Systems;
using Content.Server.Singularity.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared._Impstation.StrangeMoods;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Chat;
using Content.Shared.DeviceLinking;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Speech;
using Content.Shared.Storage.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterHazardSystem
{
    /// <summary>
    /// Shoot lightning bolts depending on accumulated power.
    /// </summary>
    private void SupermatterZap(EntityUid uid, SupermatterComponent sm)
    {
        var zapPower = 0;
        var zapCount = 0;
        var zapRange = Math.Clamp(sm.Power / 1000, 2, 7);

        if (_random.Prob(0.05f))
            zapCount += 1;

        if (sm.Power >= _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold))
            zapCount += 2;

        if (sm.Power >= _config.GetCVar(EECCVars.SupermatterSeverePowerPenaltyThreshold))
        {
            zapPower += 1;
            zapCount += 1;
        }

        if (sm.Power >= _config.GetCVar(EECCVars.SupermatterCriticalPowerPenaltyThreshold))
        {
            zapPower += 1;
            zapCount += 1;
        }

        if (zapCount >= 1)
            _lightning.ShootRandomLightnings(uid, zapRange, zapCount, sm.LightningPrototypes[zapPower], hitCoordsChance: sm.ZapHitCoordinatesChance, canExplode: false);
    }

    /// <summary>
    /// Generate temporary anomalies depending on accumulated power.
    /// </summary>
    private void GenerateAnomalies(EntityUid uid, SupermatterComponent sm)
    {
        var xform = Transform(uid);
        var anomalies = new List<string>();

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        // Bluespace anomaly: ~1/150 chance
        if (_random.Prob(1 / sm.AnomalyBluespaceChance))
            anomalies.Add(sm.AnomalyBluespaceSpawnPrototype);

        // Gravity anomaly: ~1/150 chance above SeverePowerPenaltyThreshold, or ~1/750 chance otherwise
        if (sm.Power > _config.GetCVar(EECCVars.SupermatterSeverePowerPenaltyThreshold) && _random.Prob(1 / sm.AnomalyGravityChanceSevere) ||
            _random.Prob(1 / sm.AnomalyGravityChance))
            anomalies.Add(sm.AnomalyGravitySpawnPrototype);

        // Pyroclastic anomaly: ~1/375 chance above SeverePowerPenaltyThreshold, or ~1/2500 chance above PowerPenaltyThreshold
        if (sm.Power > _config.GetCVar(EECCVars.SupermatterSeverePowerPenaltyThreshold) && _random.Prob(1 / sm.AnomalyPyroChanceSevere) ||
            sm.Power > _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold) && _random.Prob(1 / sm.AnomalyPyroChance))
            anomalies.Add(sm.AnomalyPyroSpawnPrototype);

        var count = anomalies.Count;
        if (count == 0)
            return;

        var tiles = GetSpawningPoints(uid, sm, count);
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
    private List<TileRef>? GetSpawningPoints(EntityUid uid, SupermatterComponent sm, int amount)
    {
        var xform = Transform(uid);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var localpos = xform.Coordinates.Position;
        var tilerefs = _map.GetLocalTilesIntersecting(
            xform.GridUid.Value,
            grid,
            new Box2(localpos + new Vector2(-sm.AnomalySpawnMaxRange, -sm.AnomalySpawnMaxRange), localpos + new Vector2(sm.AnomalySpawnMaxRange, sm.AnomalySpawnMaxRange)))
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
            if (distance > sm.AnomalySpawnMaxRange || distance < sm.AnomalySpawnMinRange)
            {
                tilerefs.Remove(tileref);
                continue;
            }

            var valid = true;

            foreach (var ent in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tileref.GridIndices))
            {
                if (!physQuery.TryGetComponent(ent, out var body))
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
