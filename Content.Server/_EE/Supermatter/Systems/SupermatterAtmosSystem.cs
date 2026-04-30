using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Chat.Managers;
using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Database;
using Robust.Shared.Configuration;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterAtmosSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterAtmosComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
    }

    public void OnAtmosUpdate(Entity<SupermatterAtmosComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;
        var mix = _atmosphere.GetContainingMixture(ent.Owner, true, true);

        if (mix is not { })
            return;

        // Variable mix was copied as to not interfere with other calculations when gasReleased is merged
        comp.GasMixture = mix.Clone();
        comp.GasStorage = mix.Remove(comp.GasEfficiency * mix.TotalMoles);

        // Let's get the proportions of the gases in the mix for scaling stuff later
        // They range between 0 and 1
        foreach (var gasId in Enum.GetValues<Gas>())
        {
            var proportion = comp.GasStorage.GetMoles(gasId) / comp.GasStorage.TotalMoles;
            comp.GasComposition.SetMoles(gasId, Math.Clamp(proportion, 0, 1));
        }

        // No less then zero, and no greater then one, we use this to do explosions and heat to power transfer.
        var powerRatio = Math.Clamp(SupermatterGasData.GetPowerMixRatios(comp.GasComposition), 0, 1);

        // Be mindful of the order in which these are called
        // Ammonia effect changes stored ammonia coun
        AmmoniaEffect(ent, powerRatio);
        CarbonDioxideEffect(ent);
        CalculatePowerGain(ent, powerRatio);

        // TODO: make all of these into one function that sends these values through a event probably and remove them being a component, let the things that need it have it as a component so they can independently work

        var transmissionBonus = SupermatterGasData.GetTransmitModifiers(comp.GasComposition);
        var h2OBonus = 1 - comp.GasComposition.GetMoles(Gas.WaterVapor) * 0.25f;
        transmissionBonus *= h2OBonus;

        // Affects plasma, o2 and heat output.
        var gasHeatModifier = SupermatterGasData.GetHeatPenalties(comp.GasComposition);
        comp.HeatModifier = Math.Max(gasHeatModifier, 0.5f);

        // Affects the damage heat does to the crystal
        var heatResistance = SupermatterGasData.GetHeatResistances(comp.GasComposition);
        var dynamicHeatResistance = Math.Max(heatResistance, 1);

        // More moles of gases are harder to heat than fewer, so let's scale heat damage around them
        var moleHeatPenaltyThreshold = (float)Math.Max(comp.GasStorage.TotalMoles / _config.GetCVar(EECCVars.SupermatterMoleHeatPenalty), 0.25);

        var psyCoefficient = 0f;
        if (TryComp<SupermatterComponent>(ent, out var sm))
            psyCoefficient = sm.PsyCoefficient;

        // Power * 0.55 * a value between 1 and 0.8
        // This has to be differentiated with respect to time, since its going to be interacting with systems
        // that also differentiate. Basically, if we don't multiply by 2 * frameTime, the supermatter will explode faster if your server's tickrate is higher.
        var energy = comp.Power * _config.GetCVar(EECCVars.SupermatterReactionPowerModifier) * (1f - psyCoefficient * 0.2f) * 2 * args.dt;
        ReleaseWaste(ent, mix, energy);

        // After this point power is lowered
        // This wraps around to the begining of the function
        var powerReduction = (float)Math.Pow(comp.Power / 500, 3);
        comp.PowerLoss = Math.Min(powerReduction * comp.PowerlossInhibitor, comp.Power * 0.83f * comp.PowerlossInhibitor);
        comp.Power = Math.Max(comp.Power - comp.PowerLoss, 0f);

        if (sm is { })
        {
            // Log the first powering of the supermatter
            if (comp.Power > 0 && !sm.HasBeenPowered)
                LogFirstPower((ent, sm), comp.GasMixture);

            sm.Power = comp.Power;
        }

        var ev = new SupermatterAtmosUpdatedEvent()
        {
            Power = comp.Power,
            PowerRatio = powerRatio,
            GasComposition = comp.GasComposition,
            GasHeatModifier = gasHeatModifier,
            HeatModifier = comp.HeatModifier,
            DynamicHeatResistance = dynamicHeatResistance,
            MoleHeatPenaltyThreshold = moleHeatPenaltyThreshold,
            TransmissionBonus = transmissionBonus
        };
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    private void ReleaseWaste(Entity<SupermatterAtmosComponent> ent, GasMixture mix, float energy)
    {
        var comp = ent.Comp;

        // Keep in mind we are only adding this temperature to (efficiency)% of the one tile the rock is on.
        // An increase of 4°C at 25% efficiency here results in an increase of 1°C / (#tilesincore) overall.
        // Power * 0.55 * 1.5~23 / 5
        var gasReleased = comp.GasStorage.Clone();

        gasReleased.Temperature += energy * comp.HeatModifier / _config.GetCVar(EECCVars.SupermatterThermalReleaseModifier);
        gasReleased.Temperature = Math.Max(0,
            Math.Min(gasReleased.Temperature, 2500f * comp.HeatModifier));

        // Release the waste
        gasReleased.AdjustMoles(
            Gas.Plasma,
            Math.Max(energy * comp.HeatModifier / _config.GetCVar(EECCVars.SupermatterPlasmaReleaseModifier), 0f));
        gasReleased.AdjustMoles(
            Gas.Oxygen,
            Math.Max((energy + gasReleased.Temperature * comp.HeatModifier - Atmospherics.T0C) / _config.GetCVar(EECCVars.SupermatterOxygenReleaseModifier), 0f));

        _atmosphere.Merge(mix, gasReleased);
    }

    private void CalculatePowerGain(Entity<SupermatterAtmosComponent> ent, float powerRatio)
    {
        var comp = ent.Comp;

        // Based on gas mix, makes the power more based on heat or less effected by heat
        var tempFactor = powerRatio > 0.8 ? 50f : 30f;

        if (comp.MatterPower != 0)
        {
            // We base our removed power off 1/10 the matter_power.
            var removedMatter = Math.Max(comp.MatterPower / _config.GetCVar(EECCVars.SupermatterMatterPowerConversion), 40);
            // Adds at least 40 power
            comp.Power = Math.Max(comp.Power + removedMatter, 0);
            // Removes at least 40 matter power
            comp.MatterPower = Math.Max(comp.MatterPower - removedMatter, 0);
        }

        // If there is more frezon and N2 than anything else, we receive no power increase from heat
        comp.Power = Math.Max(comp.GasStorage.Temperature * tempFactor / Atmospherics.T0C * powerRatio + comp.Power, 0);
    }

    private void LogFirstPower(Entity<SupermatterComponent> ent, GasMixture gas)
    {
        _adminLog.Add(LogType.Unknown, LogImpact.Extreme, $"{EntityManager.ToPrettyString(ent):ent} was powered for the first time by gas mixture at {Transform(ent).Coordinates:coordinates}");
        _chatManager.SendAdminAlert($"{EntityManager.ToPrettyString(ent):ent} was powered for the first time by gas mixture");
        ent.Comp.HasBeenPowered = true;
    }
}
