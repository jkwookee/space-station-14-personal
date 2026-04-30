using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterAtmosSystem
{
    private void AmmoniaEffect(Entity<SupermatterAtmosComponent> ent, float powerRatio)
    {
        var comp = ent.Comp;

        // Miasma is really just microscopic particulate. It gets consumed like anything else that touches the crystal.
        var ammoniaProportion = comp.GasComposition.GetMoles(Gas.Ammonia);

        if (ammoniaProportion > 0)
        {
            var ammoniaPartialPressure = comp.GasMixture.Pressure * ammoniaProportion;
            var consumedMiasma = Math.Clamp((ammoniaPartialPressure - _config.GetCVar(EECCVars.SupermatterAmmoniaConsumptionPressure)) /
                (ammoniaPartialPressure + _config.GetCVar(EECCVars.SupermatterAmmoniaPressureScaling)) *
                (1 + powerRatio * _config.GetCVar(EECCVars.SupermatterAmmoniaGasMixScaling)),
                0f, 1f);

            consumedMiasma *= ammoniaProportion * comp.GasStorage.TotalMoles;

            if (consumedMiasma > 0)
            {
                comp.GasStorage.AdjustMoles(Gas.Ammonia, -consumedMiasma);
                comp.MatterPower += consumedMiasma * _config.GetCVar(EECCVars.SupermatterAmmoniaPowerGain);
            }
        }
    }

    private void CarbonDioxideEffect(Entity<SupermatterAtmosComponent> ent)
    {
        var comp = ent.Comp;

        // Ramps up or down in increments of 0.02 up to the proportion of CO2
        // Given infinite time, powerloss_dynamic_scaling = co2comp
        // Some value from 0-1
        if (comp.GasStorage.TotalMoles > _config.GetCVar(EECCVars.SupermatterPowerlossInhibitionMoleThreshold) && // if there are more than 20 mols,
            comp.GasComposition.GetMoles(Gas.CarbonDioxide) > _config.GetCVar(EECCVars.SupermatterPowerlossInhibitionGasThreshold)) // and more than 20% co2
        {
            var co2powerloss = Math.Clamp(comp.GasComposition.GetMoles(Gas.CarbonDioxide) - comp.PowerlossDynamicScaling, -0.02f, 0.02f);
            comp.PowerlossDynamicScaling = Math.Clamp(comp.PowerlossDynamicScaling + co2powerloss, 0f, 1f);
        }
        else
            comp.PowerlossDynamicScaling = Math.Clamp(comp.PowerlossDynamicScaling - 0.05f, 0f, 1f);

        // Ranges from 0~1(1 - (0~1 * 1~(1.5 * (mol / 500))))
        // We take the mol count, and scale it to be our inhibitor
        comp.PowerlossInhibitor = Math.Clamp(
            1 - comp.PowerlossDynamicScaling * Math.Clamp(comp.GasStorage.TotalMoles / _config.GetCVar(EECCVars.SupermatterPowerlossInhibitionMoleBoostThreshold), 1f, 1.5f),
            0f, 1f);
    }
}
