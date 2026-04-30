using Content.Server._Impstation.StrangeMoods;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
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
using Content.Shared.Audio;
using Content.Shared.DeviceLinking;
using Content.Shared.Radiation.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterHazardSystem : EntitySystem
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

        // May need to change to a non ref to pass info
        SubscribeLocalEvent<SupermatterHazardComponent, SupermatterAtmosUpdatedEvent>(OnSupermatterAtmosUpdate);
        SubscribeLocalEvent<SupermatterHazardComponent, GravPulseEvent>(OnGravPulse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);


    }

    private void OnSupermatterAtmosUpdate(Entity<SupermatterHazardComponent> ent, ref SupermatterAtmosUpdatedEvent args)
    {
        var comp = ent.Comp;
        comp.HazardPower = args.Power;


        if (!TryComp<SupermatterComponent>(ent, out var sm))
            return;

        // TODO: move over to timespan and update
        if (comp.HazardPower > _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold) || sm.Damage > sm.DamagePenaltyPoint)
        {
            SupermatterZap(ent);
            GenerateAnomalies(ent);
        }

        // Irradiate stuff
        if (TryComp<RadiationSourceComponent>(ent, out var rad))
        {
            rad.Intensity =
                _config.GetCVar(EECCVars.SupermatterRadsBase) +
                comp.HazardPower
                * Math.Max(0, 1f + args.TransmissionBonus / 10f)
                * 0.003f
                * _config.GetCVar(EECCVars.SupermatterRadsModifier);

            rad.Slope = Math.Clamp(rad.Intensity / 15, 0.2f, 1f);
        }

        // Adjust the gravity pull range
        if (TryComp<GravityWellComponent>(ent, out var gravityWell))
            gravityWell.MaxRange = Math.Clamp(comp.HazardPower / 850f, 0.5f, 3f);
    }

    private void OnGravPulse(Entity<SupermatterHazardComponent> ent, ref GravPulseEvent args)
    {
        if (!TryComp<GravityWellComponent>(ent, out var gravityWell))
            return;

        var nextPulse = 0.5f * _random.NextFloat(1f, 30f);
        _gravityWell.SetPulsePeriod(ent, TimeSpan.FromSeconds(nextPulse), gravityWell);

        var audioParams = AudioParams.Default.WithMaxDistance(gravityWell.MaxRange);
        _audio.PlayPvs(ent.Comp.PullSound, ent, audioParams);
    }
}
