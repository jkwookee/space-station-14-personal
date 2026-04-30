using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterCollisionSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterCollisionComponent, StartCollideEvent>(OnCollideEvent);
        SubscribeLocalEvent<SupermatterCollisionComponent, EmbeddedEvent>(OnEmbedded);
        SubscribeLocalEvent<SupermatterCollisionComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<SupermatterCollisionComponent, InteractUsingEvent>(OnItemInteract);
    }
    private void OnCollideEvent(Entity<SupermatterCollisionComponent> ent, ref StartCollideEvent args)
    {
        TryCollision(ent, args.OtherEntity, args.OtherBody);
    }

    private void OnEmbedded(Entity<SupermatterCollisionComponent> ent, ref EmbeddedEvent args)
    {
        TryCollision(ent, args.Embedded, checkStatic: false);
    }

    private void OnHandInteract(Entity<SupermatterCollisionComponent> ent, ref InteractHandEvent args)
    {
        var comp = ent.Comp;
        var target = args.User;

        if (HasComp<SupermatterImmuneComponent>(target) || HasComp<GodmodeComponent>(target))
            return;

        ApplySupermatterPower(ent, target, 200f);

        _popup.PopupEntity(Loc.GetString("supermatter-collide-mob", ("sm", ent), ("target", target)), ent, PopupType.LargeCaution);
        _audio.PlayPvs(comp.DustSound, ent);

        // Prevent spam or excess power production
        AddComp<SupermatterImmuneComponent>(target);

        _chatManager.SendAdminAlert($"{EntityManager.ToPrettyString(ent):ent} has consumed {EntityManager.ToPrettyString(target):target}");
        _adminLog.Add(LogType.EntityDelete, LogImpact.High, $"{EntityManager.ToPrettyString(target):target} touched {EntityManager.ToPrettyString(ent):ent} and was destroyed at {Transform(ent).Coordinates:coordinates}");
        EntityManager.SpawnEntity(comp.CollisionResultPrototype, Transform(target).Coordinates);
        EntityManager.QueueDeleteEntity(target);

        args.Handled = true;
    }

    private void OnItemInteract(Entity<SupermatterCollisionComponent> ent, ref InteractUsingEvent args)
    {
        var comp = ent.Comp;
        var target = args.User;
        var item = args.Used;
        var othersFilter = Filter.Pvs(ent).RemovePlayerByAttachedEntity(target);
        var power = 0f;

        if (args.Handled ||
            HasComp<GhostComponent>(target) ||
            HasComp<SupermatterImmuneComponent>(item) ||
            HasComp<GodmodeComponent>(item))
            return;

        if (HasComp<UnremoveableComponent>(item))
        {
            power += 200f;

            if (TryComp<PhysicsComponent>(target, out var targetPhysics))
                power += targetPhysics.Mass;

            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert-unremoveable", ("target", target), ("sm", ent), ("item", item)), ent, othersFilter, true, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert-unremoveable-user", ("sm", ent), ("item", item)), ent, target, PopupType.LargeCaution);

            // Prevent spam or excess power production
            AddComp<SupermatterImmuneComponent>(target);

            _adminLog.Add(LogType.EntityDelete, LogImpact.High, $"{EntityManager.ToPrettyString(target):target} touched {EntityManager.ToPrettyString(ent):ent} with {EntityManager.ToPrettyString(item):item} and was destroyed at {Transform(ent).Coordinates:coordinates}");
            EntityManager.SpawnEntity(comp.CollisionResultPrototype, Transform(target).Coordinates);
            EntityManager.QueueDeleteEntity(target);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert", ("target", target), ("sm", ent), ("item", item)), ent, othersFilter, true, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert-user", ("sm", ent), ("item", item)), ent, target, PopupType.LargeCaution);

            _adminLog.Add(LogType.EntityDelete, LogImpact.High, $"{EntityManager.ToPrettyString(target):target} touched {EntityManager.ToPrettyString(ent):ent} with {EntityManager.ToPrettyString(item):item} and destroyed it at {Transform(ent).Coordinates:coordinates}");
        }

        _audio.PlayPvs(comp.DustSound, ent);

        // Prevent spam or excess power production
        AddComp<SupermatterImmuneComponent>(item);

        ApplySupermatterPower(ent, item, power);

        EntityManager.QueueDeleteEntity(item);

        args.Handled = true;
    }

    private void TryCollision(Entity<SupermatterCollisionComponent> ent, EntityUid target, PhysicsComponent? targetPhysics = null, bool checkStatic = true)
    {
        var comp = ent.Comp;

        if (!Resolve(target, ref targetPhysics))
            return;

        if (targetPhysics.BodyType == BodyType.Static && checkStatic ||
            HasComp<SupermatterImmuneComponent>(target) ||
            HasComp<GodmodeComponent>(target) ||
            _container.IsEntityInContainer(ent))
            return;

        if (HasComp<ProjectileComponent>(target))
        {
            var popup = "supermatter-collide";

            if (HasComp<MobStateComponent>(target))
            {
                popup = "supermatter-collide-mob";
                EntityManager.SpawnEntity(comp.CollisionResultPrototype, Transform(target).Coordinates);
                _chatManager.SendAdminAlert($"{EntityManager.ToPrettyString(ent):ent} has consumed {EntityManager.ToPrettyString(target):target}");
            }

            var targetProto = MetaData(target).EntityPrototype;
            if (targetProto != null && targetProto.ID != comp.CollisionResultPrototype)
            {
                _popup.PopupEntity(Loc.GetString(popup, ("sm", ent), ("target", target)), ent, PopupType.LargeCaution);
                _audio.PlayPvs(comp.DustSound, ent);
            }

            _adminLog.Add(LogType.EntityDelete, LogImpact.High, $"{EntityManager.ToPrettyString(target):target} collided with {EntityManager.ToPrettyString(ent):ent} at {Transform(ent).Coordinates:coordinates}");
        }

        // Prevent spam or excess power production
        AddComp<SupermatterImmuneComponent>(target);

        ApplySupermatterPower(ent, target);

        EntityManager.QueueDeleteEntity(target);
    }

    private void ApplySupermatterPower(Entity<SupermatterCollisionComponent> ent, EntityUid matter, float power = 0f)
    {
        if (!TryComp<SupermatterAtmosComponent>(ent, out var atmos))
            return;

        if (!atmos.HasBeenPowered)
            LogFirstPower((ent, atmos), matter);

        if (TryComp<SupermatterFoodComponent>(matter, out var food))
            atmos.Power += food.Energy;
        else if (TryComp<ProjectileComponent>(matter, out var projectile))
            atmos.Power += (float)projectile.Damage.GetTotal();
        else
            atmos.Power++;

        if (TryComp<PhysicsComponent>(matter, out var physics))
            power += physics.Mass;

        power += HasComp<MobStateComponent>(matter) ? 200 : 0;
        atmos.MatterPower += power;
    }

    private void LogFirstPower(Entity<SupermatterAtmosComponent> ent, EntityUid matter)
    {
        _adminLog.Add(LogType.Unknown, LogImpact.Extreme, $"{EntityManager.ToPrettyString(ent):ent} was powered for the first time by {EntityManager.ToPrettyString(matter):matter} at {Transform(ent).Coordinates:coordinates}");
        _chatManager.SendAdminAlert($"{EntityManager.ToPrettyString(ent):ent} was powered for the first time by {EntityManager.ToPrettyString(matter):matter}");
        ent.Comp.HasBeenPowered = true;
    }
}
