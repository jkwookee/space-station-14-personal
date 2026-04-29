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
        SubscribeLocalEvent<SupermatterComponent, AtmosDeviceUpdateEvent>(OnSupermatterUpdated);

        SubscribeLocalEvent<SupermatterComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<SupermatterComponent, GravPulseEvent>(OnGravPulse);
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

    public void OnSupermatterUpdated(EntityUid uid, SupermatterComponent sm, AtmosDeviceUpdateEvent args)
    {
        ProcessAtmos(uid, sm, args.dt);
        HandleDamage(uid, sm);

        if (sm.Damage >= sm.DamageDelaminationPoint || sm.Delamming)
            HandleDelamination(uid, sm);

        HandleLight(uid, sm);
        HandleVision(uid, sm);
        HandleStatus(uid, sm);
        HandleSoundLoop(uid, sm);
        HandleAccent(uid, sm);

        if (sm.Power > _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold) || sm.Damage > sm.DamagePenaltyPoint)
        {
            SupermatterZap(uid, sm);
            GenerateAnomalies(uid, sm);
        }
    }

    private void OnGravPulse(Entity<SupermatterComponent> ent, ref GravPulseEvent args)
    {
        if (!TryComp<GravityWellComponent>(ent, out var gravityWell))
            return;

        var nextPulse = 0.5f * _random.NextFloat(1f, 30f);
        _gravityWell.SetPulsePeriod(ent, TimeSpan.FromSeconds(nextPulse), gravityWell);

        var audioParams = AudioParams.Default.WithMaxDistance(gravityWell.MaxRange);
        _audio.PlayPvs(ent.Comp.PullSound, ent, audioParams);
    }

    private void OnExamine(EntityUid uid, SupermatterComponent sm, ref ExaminedEvent args)
    {
        // For ghosts: alive players can use the console
        if (HasComp<GhostComponent>(args.Examiner) && args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("supermatter-examine-integrity", ("integrity", GetIntegrity(sm).ToString("0.00"))));
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
