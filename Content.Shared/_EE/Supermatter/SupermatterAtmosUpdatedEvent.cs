using Content.Shared.Atmos;

namespace Content.Shared._EE.Supermatter.Components;

[ByRefEvent]
public struct SupermatterAtmosUpdatedEvent
{
    public float Power;
    public float PowerRatio;
    public GasMixture GasComposition;
    public float GasHeatModifier;
    public float HeatModifier;
    public float DynamicHeatResistance;
    public float MoleHeatPenaltyThreshold;
    public float TransmissionBonus;
};
