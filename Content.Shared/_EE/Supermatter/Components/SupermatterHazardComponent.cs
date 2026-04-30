using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._EE.Supermatter.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterHazardComponent : Component
{
    /// <summary>
    /// The internal energy of the supermatter stored in hazard to allow independent attatchment
    /// </summary>
    [DataField]
    public float HazardPower;

    [DataField]
    public Color LightColorNormal = Color.FromHex("#ffe000");

    [DataField]
    public Color LightColorDelam = Color.FromHex("#ff5555");

    [DataField]
    public EntProtoId[] LightningPrototypes =
    {
        "SupermatterLightning",
        "SupermatterLightningCharged",
        "SupermatterLightningSupercharged"
    };

    [DataField]
    public EntProtoId AnomalyBluespaceSpawnPrototype = "AnomalyBluespace";

    [DataField]
    public EntProtoId AnomalyGravitySpawnPrototype = "AnomalyGravity";

    [DataField]
    public EntProtoId AnomalyPyroSpawnPrototype = "AnomalyPyroclastic";

    [DataField]
    public SoundSpecifier PullSound = new SoundPathSpecifier("/Audio/_EE/Supermatter/marauder.ogg");

    /// <summary>
    /// The chance for supermatter lightning to strike random coordinates instead of an entity
    /// </summary>
    [DataField]
    public float ZapHitCoordinatesChance = 0.75f;

    /// <summary>
    /// The lifetime of a supermatter-spawned anomaly.
    /// </summary>
    [DataField]
    public float AnomalyLifetime = 60f;

    /// <summary>
    /// The minimum distance from the supermatter that anomalies will spawn at
    /// </summary>
    [DataField]
    public float AnomalySpawnMinRange = 5f;

    /// <summary>
    /// The maximum distance from the supermatter that anomalies will spawn at
    /// </summary>
    [DataField]
    public float AnomalySpawnMaxRange = 10f;

    /// <summary>
    /// The chance for a bluespace anomaly to spawn when power or damage is high
    /// </summary>
    [DataField]
    public float AnomalyBluespaceChance = 150f;

    /// <summary>
    /// The chance for a gravity anomaly to spawn when power or damage is high, and the severe power penalty threshold is exceeded
    /// </summary>
    [DataField]
    public float AnomalyGravityChanceSevere = 150f;

    /// <summary>
    /// The chance for a gravity anomaly to spawn when power or damage is high
    /// </summary>
    [DataField]
    public float AnomalyGravityChance = 750f;

    /// <summary>
    /// The chance for a pyroclastic anomaly to spawn when power or damage is high, and the severe power penalty threshold is exceeded
    /// </summary>
    [DataField]
    public float AnomalyPyroChanceSevere = 375f;

    /// <summary>
    /// The chance for a pyroclastic anomaly to spawn when power or damage is high, and the power penalty threshold is exceeded
    /// </summary>
    [DataField]
    public float AnomalyPyroChance = 2500f;
}
