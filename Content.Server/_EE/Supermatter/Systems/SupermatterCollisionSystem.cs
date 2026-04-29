using Content.Server._Impstation.StrangeMoods;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Examine;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Lightning;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Silicons.Laws;
using Content.Server.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Server.Traits.Assorted;
using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterCollisionSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly GravityWellSystem _gravityWell = default!;
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly ParacusiaSystem _paracusia = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly StrangeMoodsSystem _moods = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _link = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

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

        if (!sm.HasBeenPowered)
            LogFirstPower((ent, sm), target);

        var power = 200f;
        if (TryComp<PhysicsComponent>(target, out var physics))
            power += physics.Mass;

        sm.MatterPower += power;

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

        if (args.Handled ||
            HasComp<GhostComponent>(target) ||
            HasComp<SupermatterImmuneComponent>(item) ||
            HasComp<GodmodeComponent>(item))
            return;

        if (HasComp<UnremoveableComponent>(item))
        {
            if (!sm.HasBeenPowered)
                LogFirstPower(ent, sm, target);

            var power = 200f;

            if (TryComp<PhysicsComponent>(target, out var targetPhysics))
                power += targetPhysics.Mass;

            if (TryComp<PhysicsComponent>(item, out var itemPhysics))
                power += itemPhysics.Mass;

            sm.MatterPower += power;

            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert-unremoveable", ("target", target), ("sm", ent), ("item", item)), ent, othersFilter, true, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert-unremoveable-user", ("sm", ent), ("item", item)), ent, target, PopupType.LargeCaution);
            _audio.PlayPvs(comp.DustSound, ent);

            // Prevent spam or excess power production
            AddComp<SupermatterImmuneComponent>(target);
            AddComp<SupermatterImmuneComponent>(item);

            _adminLog.Add(LogType.EntityDelete, LogImpact.High, $"{EntityManager.ToPrettyString(target):target} touched {EntityManager.ToPrettyString(ent):ent} with {EntityManager.ToPrettyString(item):item} and was destroyed at {Transform(ent).Coordinates:coordinates}");
            EntityManager.SpawnEntity(comp.CollisionResultPrototype, Transform(target).Coordinates);
            EntityManager.QueueDeleteEntity(target);
            EntityManager.QueueDeleteEntity(item);
        }
        else
        {
            if (!sm.HasBeenPowered)
                LogFirstPower(ent, sm, item);

            if (TryComp<PhysicsComponent>(item, out var physics))
                sm.MatterPower += physics.Mass;

            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert", ("target", target), ("sm", ent), ("item", item)), ent, othersFilter, true, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("supermatter-collide-insert-user", ("sm", ent), ("item", item)), ent, target, PopupType.LargeCaution);
            _audio.PlayPvs(sm.DustSound, ent);

            // Prevent spam or excess power production
            AddComp<SupermatterImmuneComponent>(item);

            _adminLog.Add(LogType.EntityDelete, LogImpact.High, $"{EntityManager.ToPrettyString(target):target} touched {EntityManager.ToPrettyString(ent):ent} with {EntityManager.ToPrettyString(item):item} and destroyed it at {Transform(ent).Coordinates:coordinates}");
            EntityManager.QueueDeleteEntity(item);
        }

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

        if (!sm.HasBeenPowered)
            LogFirstPower(ent, sm, target);

        if (!TryComp<ProjectileComponent>(target, out var projectile))
        {
            var popup = "supermatter-collide";

            if (HasComp<MobStateComponent>(target))
            {
                popup = "supermatter-collide-mob";
                EntityManager.SpawnEntity(sm.CollisionResultPrototype, Transform(target).Coordinates);
                _chatManager.SendAdminAlert($"{EntityManager.ToPrettyString(ent):ent} has consumed {EntityManager.ToPrettyString(target):target}");
            }

            var targetProto = MetaData(target).EntityPrototype;
            if (targetProto != null && targetProto.ID != sm.CollisionResultPrototype)
            {
                _popup.PopupEntity(Loc.GetString(popup, ("sm", ent), ("target", target)), ent, PopupType.LargeCaution);
                _audio.PlayPvs(sm.DustSound, ent);
            }

            sm.MatterPower += targetPhysics.Mass;
            _adminLog.Add(LogType.EntityDelete, LogImpact.High, $"{EntityManager.ToPrettyString(target):target} collided with {EntityManager.ToPrettyString(ent):ent} at {Transform(ent).Coordinates:coordinates}");
        }

        // Prevent spam or excess power production
        AddComp<SupermatterImmuneComponent>(target);

        EntityManager.QueueDeleteEntity(target);

        if (TryComp<SupermatterFoodComponent>(target, out var food))
            sm.Power += food.Energy;
        else if (projectile != null)
            sm.Power += (float)projectile.Damage.GetTotal();
        else
            sm.Power++;

        sm.MatterPower += HasComp<MobStateComponent>(target) ? 200 : 0;
    }

    private void LogFirstPower(Entity<SupermatterComponent> ent, EntityUid target)
    {
        _adminLog.Add(LogType.Unknown, LogImpact.Extreme, $"{EntityManager.ToPrettyString(ent):ent} was powered for the first time by {EntityManager.ToPrettyString(target):target} at {Transform(ent).Coordinates:coordinates}");
        _chatManager.SendAdminAlert($"{EntityManager.ToPrettyString(ent):ent} was powered for the first time by {EntityManager.ToPrettyString(target):target}");
        ent.Comp.HasBeenPowered = true;
    }
}