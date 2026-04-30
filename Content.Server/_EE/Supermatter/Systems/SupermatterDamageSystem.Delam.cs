using Content.Server._Impstation.StrangeMoods;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Examine;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Lightning;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Silicons.Laws;
using Content.Server.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Server.Traits.Assorted;
using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.DeviceLinking;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
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

public sealed partial class SupermatterDelaminationSystem
{
    /// <summary>
    /// Handle the end of the station.
    /// </summary>
    private void HandleDelamination(EntityUid uid, SupermatterComponent sm)
    {
        var xform = Transform(uid);

        sm.PreferredDelamType = ChooseDelamType(uid, sm);

        if (!sm.Delamming)
        {
            sm.Delamming = true;
            sm.DelamEndTime = _timing.CurTime + TimeSpan.FromSeconds(sm.DelamTimer);
            AnnounceCoreDamage(uid, sm);
        }

        if (sm.Damage < sm.DamageDelaminationPoint && sm.Delamming)
        {
            sm.Delamming = false;
            AnnounceCoreDamage(uid, sm);
        }

        if (_timing.CurTime < sm.DelamEndTime)
            return;

        var mapId = Transform(uid).MapID;
        var mapFilter = Filter.BroadcastMap(mapId);
        var message = Loc.GetString("supermatter-delam-player");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));

        // Send the reality distortion message to every player on the map
        _chatManager.ChatMessageToManyFiltered(mapFilter,
            ChatChannel.Server,
            message,
            wrappedMessage,
            uid,
            false,
            true,
            Color.Red);

        // Play the reality distortion sound for every player on the map
        _audio.PlayGlobal(sm.DistortSound, mapFilter, true);

        // Give effects to every mob on the map, except those in EntityStorage (lockers, etc)
        var mobLookup = new HashSet<Entity<MobStateComponent>>();
        _entityLookup.GetEntitiesOnMap<MobStateComponent>(mapId, mobLookup);
        mobLookup.RemoveWhere(x => HasComp<InsideEntityStorageComponent>(x));

        // Scramble the given shared moods
        foreach (var mood in sm.SharedMoodScrambleTargets)
        {
            _moods.NewSharedMoods(mood);
        }

        // Flickers all powered lights on the map
        var lightLookup = new HashSet<Entity<PoweredLightComponent>>();
        _entityLookup.GetEntitiesOnMap<PoweredLightComponent>(mapId, lightLookup);
        foreach (var light in lightLookup)
        {
            if (!_random.Prob(sm.LightFlickerChance))
                continue;
            _ghost.DoGhostBooEvent(light);
        }

        // Add post-delamination event scheduler
        var gamerule = _gameTicker.AddGameRule(sm.DelamGamerulePrototype);
        _gameTicker.StartGameRule(gamerule);

        var effects = _proto.Index(sm.DelamEffectsPrototype).Components;

        foreach (var mob in mobLookup)
        {
            // Scramble moods that follow the given shared moods
            if (TryComp<StrangeMoodsComponent>(mob, out var moods) &&
                moods.SharedMood is { UniqueId: not null } sharedMood &&
                sm.SharedMoodScrambleTargets.Contains(sharedMood.UniqueId))
            {
                _moods.RefreshMoods((mob, moods));
            }

            // Scramble laws for silicons, then ignore other effects
            if (TryComp<SiliconLawBoundComponent>(mob, out var law))
            {
                var target = EnsureComp<IonStormTargetComponent>(mob); // they hit the fucking ai
                var oldChance = target.Chance;
                target.Chance = 1f;
                var ev = new IonStormEvent();
                RaiseLocalEvent(mob, ref ev);
                target.Chance = oldChance; // hacky fucking code. whatever. don't look at me

                continue;
            }

            // Add effects to all mobs
            // TODO: change paracusia to actual hallucinations whenever those are real
            EntityManager.AddComponents(mob, effects, false);
        }

        switch (sm.PreferredDelamType)
        {
            case DelamType.Cascade:
                // one day...
                // Spawn(sm.KudzuSpawnPrototype, xform.Coordinates);
                break;

            case DelamType.Singulo:
                Spawn(sm.SingularitySpawnPrototype, xform.Coordinates);
                break;

            case DelamType.Tesla:
                Spawn(sm.TeslaSpawnPrototype, xform.Coordinates);
                break;

            default:
                _explosion.TriggerExplosive(uid);
                break;
        }
    }

    /// <summary>
    /// Decide on how to delaminate.
    /// </summary>
    public DelamType ChooseDelamType(EntityUid uid, SupermatterComponent sm)
    {
        if (_config.GetCVar(EECCVars.SupermatterDoForceDelam))
            return _config.GetCVar(EECCVars.SupermatterForcedDelamType);

        if (sm.GasStorage is { })
        {
            if (_config.GetCVar(EECCVars.SupermatterDoSingulooseDelam)
                && sm.GasStorage.TotalMoles >= _config.GetCVar(EECCVars.SupermatterMolePenaltyThreshold) * _config.GetCVar(EECCVars.SupermatterSingulooseMolesModifier))
                return DelamType.Singulo;
        }

        if (_config.GetCVar(EECCVars.SupermatterDoTeslooseDelam)
            && sm.Power >= _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold) * _config.GetCVar(EECCVars.SupermatterTesloosePowerModifier))
            return DelamType.Tesla;

        //TODO: Add resonance cascade when there's crazy conditions or a destabilizing crystal

        return DelamType.Explosion;
    }

    /// <summary>
    /// Scales the energy and radius of the supermatter's light based on its power,
    /// and gradients the color based on its integrity
    /// </summary>
    private void HandleLight(EntityUid uid, SupermatterComponent sm)
    {
        if (!TryComp<PointLightComponent>(uid, out var light))
            return;

        // Max light scaling reached at 2500 power
        var scalar = Math.Clamp(sm.Power / 2500f + 1f, 1f, 2f);

        // Blend colors between hsvNormal at 100% integrity, and hsvDelam at 0% integrity
        var integrity = GetIntegrity(sm);
        var hsvNormal = Color.ToHsv(sm.LightColorNormal);
        var hsvDelam = Color.ToHsv(sm.LightColorDelam);
        var hsvFinal = Vector4.Lerp(hsvDelam, hsvNormal, integrity / 100f);

        _light.SetEnergy(uid, 2f * scalar, light);
        _light.SetRadius(uid, 10f * scalar, light);
        _light.SetColor(uid, Color.FromHsv(hsvFinal), light);
    }
}
