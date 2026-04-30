using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._EE.Supermatter.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterAtmosComponent : Component
{
    /// <summary>
    /// Used for logging if the supermatter has been powered
    /// </summary>
    [DataField]
    public bool HasBeenPowered;

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
    /// Affects the amount of oxygen and plasma that is released during supermatter reactions, as well as the heat generated
    /// </summary>
    [DataField]
    public float HeatModifier;
}
