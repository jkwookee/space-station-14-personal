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

public sealed partial class SupermatterSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly GravityWellSystem _gravityWell = default!;
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly ParacusiaSystem _paracusia = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly StrangeMoodsSystem _moods = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _link = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SupermatterComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityManager.EntityQueryEnumerator<SupermatterComponent>();
        while (query.MoveNext(out var uid, out var sm))
            AnnounceCoreDamage(uid, sm);
    }

    private void OnMapInit(EntityUid uid, SupermatterComponent sm, MapInitEvent args)
    {
        // Set the yell timer
        sm.YellTimer = TimeSpan.FromSeconds(_config.GetCVar(EECCVars.SupermatterYellTimer));

        // Set the sound
        _ambient.SetAmbience(uid, true);

        // Add air to the initialized SM in the map so it doesn't delam on its own
        var mix = _atmosphere.GetContainingMixture(uid, true, true);
        mix?.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard - mix.GetMoles(Gas.Oxygen));
        mix?.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard - mix.GetMoles(Gas.Nitrogen));

        // Send the inactive port for any linked devices
        if (HasComp<DeviceLinkSourceComponent>(uid))
            _link.InvokePort(uid, sm.PortInactive);
    }

    public void OnAtmosUpdate(EntityUid uid, SupermatterComponent sm, AtmosDeviceUpdateEvent args)
    {
        if (sm.Damage >= sm.DamageDelaminationPoint || sm.Delamming)
            HandleDelamination(uid, sm);

        HandleVision(uid, sm);
        HandleStatus(uid, sm);
        HandleSoundLoop(uid, sm);
        HandleAccent(uid, sm);
    }

    /// <summary>
    /// Checks whether a mob can see the supermatter, then applies hallucinations and psychologist coefficient
    /// </summary>
    private void HandleVision(EntityUid uid, SupermatterComponent sm)
    {
        var psyDiff = -0.007f;
        var lookup = _entityLookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 20f);

        foreach (var mob in lookup)
        {
            // Not in line of sight, or is dead
            if (!_examine.InRangeUnOccluded(uid, mob, sm.HallucinationRange) ||
                mob.Comp.CurrentState == MobState.Dead)
                continue;

            // Someone (generally a psychologist), when looking at the supermatter within hallucination range, makes it easier to manage.
            if (HasComp<SupermatterSootherComponent>(mob))
                psyDiff = 0.007f;

            if (HasComp<SupermatterHallucinationImmuneComponent>(mob) || // Immune to supermatter hallucinations
                HasComp<SiliconLawBoundComponent>(mob) ||                // Silicons don't get supermatter hallucinations
                HasComp<PermanentBlindnessComponent>(mob) ||             // Blind people don't get supermatter hallucinations
                HasComp<TemporaryBlindnessComponent>(mob))               // Neither do blinded people
                continue;

            // Everyone else gets hallucinations
            // These values match the paracusia disability, since we can't double up on paracusia
            // TODO: change this from paracusia to actual hallucinations whenever those are real
            var paracusiaSounds = new SoundCollectionSpecifier("Paracusia");
            var paracusiaMinTime = 0.1f;
            var paracusiaMaxTime = 300f;
            var paracusiaDistance = 7f;

            if (!EnsureComp<ParacusiaComponent>(mob, out var paracusia))
            {
                _popup.PopupEntity(Loc.GetString("supermatter-paracusia-player-message"), mob, mob, PopupType.LargeCaution);
                _audio.PlayEntity(sm.GainParacusiaSound, mob, mob);
                _audio.PlayEntity(sm.GiveParacusiaSound, mob, uid);
                _paracusia.SetSounds(mob, paracusiaSounds, paracusia);
                _paracusia.SetTime(mob, paracusiaMinTime, paracusiaMaxTime, paracusia);
                _paracusia.SetDistance(mob, paracusiaDistance, paracusia);
            }
        }

        sm.PsyCoefficient = Math.Clamp(sm.PsyCoefficient + psyDiff, 0f, 1f);

        // Adjust the opacity of the supermatter's psychologist overlay based on the coefficient
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, SupermatterVisuals.Psy, sm.PsyCoefficient, appearance);
    }

    /// <summary>
    /// Sets the supermatter's status and speech sound based on thresholds
    /// </summary>
    private void HandleStatus(EntityUid uid, SupermatterComponent sm)
    {
        var currentStatus = GetStatus(uid, sm);

        // Send port updates out for any linked devices
        if (sm.Status != currentStatus && HasComp<DeviceLinkSourceComponent>(uid))
        {
            var port = currentStatus switch
            {
                SupermatterStatusType.Normal => sm.PortNormal,
                SupermatterStatusType.Caution => sm.PortCaution,
                SupermatterStatusType.Warning => sm.PortWarning,
                SupermatterStatusType.Danger => sm.PortDanger,
                SupermatterStatusType.Emergency => sm.PortEmergency,
                SupermatterStatusType.Delaminating => sm.PortDelaminating,
                _ => sm.PortInactive
            };

            _link.InvokePort(uid, port);
        }

        sm.Status = currentStatus;

        if (!TryComp<SpeechComponent>(uid, out var speech))
            return;

        // Supermatter is healing, so don't play speech sounds
        if (sm.Damage < sm.DamageArchived && currentStatus != SupermatterStatusType.Delaminating)
        {
            sm.StatusCurrentSound = sm.StatusSilentSound;
            speech.SpeechSounds = sm.StatusSilentSound;
            return;
        }

        sm.StatusCurrentSound = currentStatus switch
        {
            SupermatterStatusType.Warning => sm.StatusWarningSound,
            SupermatterStatusType.Danger => sm.StatusDangerSound,
            SupermatterStatusType.Emergency => sm.StatusEmergencySound,
            SupermatterStatusType.Delaminating => sm.StatusDelamSound,
            _ => sm.StatusSilentSound
        };

        if (currentStatus == SupermatterStatusType.Warning)
            speech.AudioParams = AudioParams.Default.AddVolume(7.5f);
        else
            speech.AudioParams = AudioParams.Default.AddVolume(10f);

        speech.SpeechSounds = sm.StatusCurrentSound;
    }

    // This currently has some audio clipping issues: this is likely an issue with AmbientSoundComponent or the engine
    /// <summary>
    /// Swaps out ambience sounds when the SM is delamming or not.
    /// </summary>
    private void HandleSoundLoop(EntityUid uid, SupermatterComponent sm)
    {
        if (!TryComp<AmbientSoundComponent>(uid, out var ambient))
            return;

        var volume = (float)Math.Round(Math.Clamp(sm.Power / 50 - 5, -5, 5));

        _ambient.SetVolume(uid, volume);

        if (sm.Status >= SupermatterStatusType.Danger && sm.CurrentSoundLoop != sm.DelamLoopSound)
            sm.CurrentSoundLoop = sm.DelamLoopSound;

        else if (sm.Status < SupermatterStatusType.Danger && sm.CurrentSoundLoop != sm.CalmLoopSound)
            sm.CurrentSoundLoop = sm.CalmLoopSound;

        if (ambient.Sound != sm.CurrentSoundLoop)
            _ambient.SetSound(uid, sm.CurrentSoundLoop!, ambient);
    }

    /// <summary>
    /// Plays normal/delam sounds at a rate determined by power and damage
    /// </summary>
    private void HandleAccent(EntityUid uid, SupermatterComponent sm)
    {
        if (sm.AccentLastTime >= _timing.CurTime || !_random.Prob(0.05f))
            return;

        var aggression = Math.Min((sm.Damage / 800) * (sm.Power / 2500), 1) * 100;
        var nextSound = Math.Max(Math.Round((100 - aggression) * 5), sm.AccentMinCooldown);
        var sound = sm.CalmAccent;

        if (sm.AccentLastTime + TimeSpan.FromSeconds(nextSound) > _timing.CurTime)
            return;

        if (sm.Status >= SupermatterStatusType.Danger)
            sound = sm.DelamAccent;

        sm.AccentLastTime = _timing.CurTime;
        _audio.PlayPvs(sound, Transform(uid).Coordinates);
    }

    private SupermatterStatusType GetStatus(EntityUid uid, SupermatterComponent sm)
    {
        var mix = _atmosphere.GetContainingMixture(uid, true, true);

        if (mix is not { })
            return SupermatterStatusType.Error;

        if (sm.Delamming || sm.Damage >= sm.DamageDelaminationPoint)
            return SupermatterStatusType.Delaminating;

        if (sm.Damage >= sm.DamagePenaltyPoint)
            return SupermatterStatusType.Emergency;

        if (sm.Damage >= sm.DamageDelamAlertPoint)
            return SupermatterStatusType.Danger;

        if (sm.Damage >= sm.DamageWarningThreshold)
            return SupermatterStatusType.Warning;

        if (mix.Temperature > Atmospherics.T0C + _config.GetCVar(EECCVars.SupermatterHeatPenaltyThreshold) * 0.8)
            return SupermatterStatusType.Caution;

        if (sm.Power > 5)
            return SupermatterStatusType.Normal;

        return SupermatterStatusType.Inactive;
    }
}
