using Content.Shared.Actions.Events;
using Content.Shared.Charges.Components;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared.Charges.Systems;

public abstract class SharedChargesSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming _timing = default!;

    /*
     * Despite what a bunch of systems do you don't need to continuously tick linear number updates and can just derive it easily.
     */

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimitedChargesComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<LimitedChargesComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<LimitedChargesComponent, ActionAttemptEvent>(OnChargesAttempt);
        SubscribeLocalEvent<LimitedChargesComponent, MapInitEvent>(OnChargesMapInit);
        SubscribeLocalEvent<LimitedChargesComponent, ActionPerformedEvent>(OnChargesPerformed);
    }

    private void OnExamine(EntityUid uid, LimitedChargesComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var rechargeEnt = new Entity<LimitedChargesComponent?, AutoRechargeComponent?>(uid, comp, null);
        var charges = GetCurrentCharges(rechargeEnt);
        using var _ = args.PushGroup(nameof(LimitedChargesComponent));

        args.PushMarkup(Loc.GetString("limited-charges-charges-remaining", ("charges", charges)));
        if (charges == comp.MaxCharges)
        {
            args.PushMarkup(Loc.GetString("limited-charges-max-charges"));
        }

        // only show the recharging info if it's not full
        if (charges == comp.MaxCharges || !Resolve(uid, ref rechargeEnt.Comp2, false))
            return;

        var timeRemaining = GetNextRechargeTime(rechargeEnt);
        args.PushMarkup(Loc.GetString("limited-charges-recharging", ("seconds", timeRemaining.TotalSeconds.ToString("F1"))));
    }

    private void OnRejuvenate(Entity<LimitedChargesComponent> ent, ref RejuvenateEvent args)
    {
        ResetCharges(ent.AsNullable());
    }

    private void OnChargesAttempt(Entity<LimitedChargesComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var charges = GetCurrentCharges((ent.Owner, ent.Comp, null));

        if (charges <= 0)
        {
            args.Cancelled = true;
        }
    }

    private void OnChargesPerformed(Entity<LimitedChargesComponent> ent, ref ActionPerformedEvent args)
    {
        AddCharges((ent.Owner, ent.Comp), -1);
    }

    private void OnChargesMapInit(Entity<LimitedChargesComponent> ent, ref MapInitEvent args)
    {
        // If nothing specified use max.
        if (ent.Comp.LastCharges == 0)
        {
            ent.Comp.LastCharges = ent.Comp.MaxCharges;
        }
        // If -1 used then we don't want any.
        else if (ent.Comp.LastCharges < 0)
        {
            ent.Comp.LastCharges = 0;
        }

        ent.Comp.LastUpdate = _timing.CurTime;
        Dirty(ent);
    }

    [Pure]
    public bool HasCharges(Entity<LimitedChargesComponent?> action, int charges)
    {
        var current = GetCurrentCharges(action);

        return current >= charges;
    }

    /// <summary>
    /// Adds the specified charges. Does not reset the accumulator.
    /// </summary>
    public virtual void AddCharges(EntityUid uid, FixedPoint2 change, LimitedChargesComponent? comp = null)
    {
        if (addCharges == 0)
            return;

        action.Comp1 ??= EnsureComp<LimitedChargesComponent>(action.Owner);

        var lastCharges = GetCurrentCharges(action);
        var charges = lastCharges + addCharges;

        if (lastCharges == charges)
            return;

        var old = comp.Charges;
        comp.Charges = FixedPoint2.Clamp(comp.Charges + change, 0, comp.MaxCharges);
        if (comp.Charges != old)
            Dirty(uid, comp);
    }

    /// <summary>
    /// Resets action charges to MaxCharges.
    /// </summary>
    public void ResetCharges(Entity<LimitedChargesComponent?> action)
    {
        if (!Resolve(action.Owner, ref action.Comp, false))
            return;

        var charges = GetCurrentCharges((action.Owner, action.Comp, null));

        if (charges == action.Comp.MaxCharges)
            return;

        action.Comp.LastCharges = action.Comp.MaxCharges;
        action.Comp.LastUpdate = _timing.CurTime;
        Dirty(action);
    }

    /// <summary>
    /// Set the number of charges an action has.
    /// </summary>
    /// <param name="action">The action in question</param>
    /// <param name="value">
    /// The number of charges. Clamped to [0, MaxCharges].
    /// </param>
    /// <remarks>
    /// This method doesn't implicitly add <see cref="LimitedChargesComponent"/>
    /// unlike some other methods in this system.
    /// </remarks>
    public void SetCharges(Entity<LimitedChargesComponent?> action, int value)
    {
        if (!Resolve(action, ref action.Comp))
            return;

        var adjusted = Math.Clamp(value, 0, action.Comp.MaxCharges);

        if (action.Comp.LastCharges == adjusted)
        {
            return;
        }

        action.Comp.LastCharges = adjusted;
        action.Comp.LastUpdate = _timing.CurTime;
        Dirty(action);
    }

    /// <summary>
    /// Sets the maximum charges of a given action.
    /// </summary>
    /// <param name="action">The action being modified.</param>
    /// <param name="value">The new maximum charges of the action. Clamped to zero.</param>
    /// <remarks>
    /// Does not change the current charge count, or adjust the
    /// accumulator for auto-recharge. It also doesn't implicitly add
    /// <see cref="LimitedChargesComponent"/> unlike some other methods
    /// in this system.
    /// </remarks>
    public void SetMaxCharges(Entity<LimitedChargesComponent?> action, int value)
    {
        if (!Resolve(action, ref action.Comp))
            return;

        // You can't have negative max charges (even zero is a bit goofy but eh)
        var adjusted = Math.Max(0, value);
        if (action.Comp.MaxCharges == adjusted)
            return;

        action.Comp.MaxCharges = adjusted;
        Dirty(action);
    }

    /// <summary>
    /// The next time a charge will be considered to be filled.
    /// </summary>
    public bool HasInsufficientCharges(EntityUid uid, FixedPoint2 requiredCharges, LimitedChargesComponent? comp = null)
    {
        // can't be empty if there are no limited charges
        if (!Resolve(uid, ref comp, false))
            return false;

        return comp.Charges < requiredCharges;
    }

    /// <summary>
    /// Derives the current charges of an entity.
    /// </summary>
    public virtual void UseCharges(EntityUid uid, FixedPoint2 chargesUsed, LimitedChargesComponent? comp = null)
    {
        AddCharges(uid, -chargesUsed, comp);
    }
}
