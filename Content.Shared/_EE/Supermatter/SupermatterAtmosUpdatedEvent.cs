using Content.Shared.Atmos;

namespace Content.Shared._EE.Supermatter.Components;

[ByRefEvent]
public struct SupermatterAtmosUpdatedEvent
{
    public GasMixture GasMixture;
    public GasMixture GasStorage;
    public GasMixture GasComposition;
    public float Power;
    public float PowerRatio;
    public float GasHeatModifier;
    public float HeatModifier;
    public float DynamicHeatResistance;
    public float MoleHeatPenaltyThreshold;
    public float TransmissionBonus;
};
