using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._EE.Supermatter.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterAtmosComponent : Component
{
    /// <summary>
    /// The supermatter's external gas mixture on the tile
    /// </summary>
    [DataField]
    public GasMixture GasMixture;

    /// <summary>
    /// The supermatter's internal gas storage
    /// </summary>
    [DataField]
    public GasMixture GasStorage;

    /// <summary>
    /// The supermatter's internal gas composition
    /// </summary>
    [DataField]
    public GasMixture GasComposition;

    [DataField]
    public EntProtoId[] LightningPrototypes =
    {
        "SupermatterLightning",
        "SupermatterLightningCharged",
        "SupermatterLightningSupercharged"
    };

    [DataField]
    public SoundSpecifier PullSound = new SoundPathSpecifier("/Audio/_EE/Supermatter/marauder.ogg");

    /// <summary>
    /// The internal energy of the supermatter
    /// </summary>
    [DataField]
    public float Power;

    /// <summary>
    /// Takes the energy that supermatter collision generates and slowly turns it into actual power
    /// </summary>
    [DataField]
    public float MatterPower;

    /// <summary>
    /// Affects the amount of oxygen and plasma that is released during supermatter reactions, as well as the heat generated
    /// </summary>
    [DataField]
    public float HeatModifier;

    /// <summary>
    /// The true value of <see cref="HeatModifier"/> without a lower bound, to be displayed on the monitoring console
    /// </summary>
    [DataField]
    public float GasHeatModifier;

    /// <summary>
    /// The percentage of the gas on the supermatter's tile that is absorbed each atmos tick.
    /// </summary>
    [DataField]
    public float GasEfficiency = 0.15f;

    /// <summary>
    /// Uses <see cref="PowerlossDynamicScaling"/> and <see cref="GasStorage"/> to lessen the effects of our powerloss functions
    /// </summary>
    [DataField]
    public float PowerlossInhibitor = 1;

    /// <summary>
    /// Based on CO2 percentage, this slowly moves between 0 and 1.
    /// We use it to calculate <see cref="PowerlossInhibitor"/>.
    /// </summary>
    [DataField]
    public float PowerlossDynamicScaling;

    /// <summary>
    /// The power decay of the supermatter, to be displayed on the supermatter console
    /// </summary>
    [DataField]
    public float PowerLoss;

    /// <summary>
    /// Affects the amount of damage and minimum point at which the SM takes heat damage
    /// </summary>
    [DataField]
    public float DynamicHeatResistance = 1;

    /// <summary>
    /// More moles of gases are harder to heat than fewer, so let's scale heat damage around them
    /// </summary>
    [DataField]
    public float MoleHeatPenaltyThreshold;

    /// <summary>
    /// Modifies rad output of the supermatter, increasing it the higher it is
    /// </summary>
    [DataField]
    public float TransmissionBonus;

}
