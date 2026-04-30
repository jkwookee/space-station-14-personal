using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._EE.Supermatter.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterDamageComponent : Component
{
    [DataField]
    public EntProtoId SingularitySpawnPrototype = "Singularity";

    [DataField]
    public EntProtoId TeslaSpawnPrototype = "TeslaEnergyBall";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId DelamEffectsPrototype = "SupermatterDelamEffects";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId DelamGamerulePrototype = "SupermatterDelamEventScheduler";

    [DataField]
    public HashSet<ProtoId<SharedMoodPrototype>> SharedMoodScrambleTargets = ["Thaven"];

    /// <summary>
    /// We yell if over 50 damage every YellTimer Seconds
    /// </summary>
    [DataField]
    public TimeSpan YellTimer;

    /// <summary>
    /// Last time the supermatter's damage was announced
    /// </summary>
    [DataField]
    public TimeSpan YellLast;

    /// <summary>
    /// Time when the delamination will occur
    /// </summary>
    [DataField]
    public TimeSpan DelamEndTime;

    /// <summary>
    /// How long it takes in seconds for the supermatter to delaminate after reaching zero integrity
    /// </summary>
    [DataField]
    public float DelamTimer = 30f;

    /// <summary>
    /// The chance for lights across the station to flicker on a delamination
    /// </summary>
    [DataField]
    public float LightFlickerChance = 0.33f;

    /// <summary>
    /// The amount of damage taken
    /// </summary>
    [DataField]
    public float Damage = 0f;

    /// <summary>
    /// The damage from before this cycle.
    /// Used to limit the damage we can take each cycle, and for safe alert.
    /// </summary>
    [DataField]
    public float DamageArchived = 0f;

    /// <summary>
    /// Is multiplied by ExplosionPoint to cap evironmental damage per cycle
    /// </summary>
    [DataField]
    public float DamageHardcap = 0.002f;

    /// <summary>
    /// Environmental damage is scaled by this
    /// </summary>
    [DataField]
    public float DamageIncreaseMultiplier = 0.25f;

    /// <summary>
    /// Max damage the SM will take per cycle
    /// </summary>
    [DataField]
    public float MaxDamage = 2;

    /// <summary>
    /// The point at which we should start sending radio messages about the damage.
    /// </summary>
    [DataField]
    public float DamageWarningThreshold = 50;

    /// <summary>
    /// The point at which we start sending station announcements about the damage.
    /// </summary>
    [DataField]
    public float DamageEmergencyThreshold = 500;

    /// <summary>
    /// The point at which the SM begins delaminating.
    /// </summary>
    [DataField]
    public int DamageDelaminationPoint = 900;

    /// <summary>
    /// The point at which the SM begins showing warning signs.
    /// </summary>
    [DataField]
    public int DamageDelamAlertPoint = 300;

    [DataField]
    public bool Delamming;

    [DataField]
    public DelamType PreferredDelamType = DelamType.Explosion;

    [DataField]
    public bool DelamAnnounced;

    /// <summary>
    /// The radio channel for supermatter alerts
    /// </summary>
    [DataField]
    public bool SuppressAnnouncements = false;

    /// <summary>
    /// The radio channel for supermatter alerts
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Engineering";

    /// <summary>
    /// The common radio channel for severe supermatter alerts
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> ChannelGlobal = "Common";
}

public enum DelamType : int
{
    Explosion = 0,
    Singulo = 1,
    Tesla = 2,
    Cascade = 3
}
