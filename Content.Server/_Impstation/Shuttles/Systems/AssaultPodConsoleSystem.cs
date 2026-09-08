using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Impstation.Shuttles.Components;
using Content.Shared._Impstation.Shuttles.Events;
using Content.Shared.Pinpointer;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;

namespace Content.Server._Impstation.Shuttles.Systems
{
    public sealed class AssaultPodConsoleSystem : EntitySystem
    {
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly ShuttleSystem _shuttle = default!;

        public override void Initialize()
        {
            base.Initialize();
            Subs.BuiEvents<AssaultPodConsoleComponent>(StationMapUiKey.Key, subs =>
            {
                subs.Event<ClickCoordMessage>(OnClickCoord);
            });
        }

        private void OnClickCoord(Entity<AssaultPodConsoleComponent> ent, ref ClickCoordMessage args)
        {
            var shuttleUid = Transform(ent).GridUid;

            if (HasComp<FTLComponent>(shuttleUid))
                return;

            if (!TryComp(shuttleUid, out ShuttleComponent? shuttleComp))
                return;

            var mapUid = _mapSystem.GetMap(args.Coordinates.MapId);
            var targetCoordinates = new EntityCoordinates(mapUid, args.Coordinates.Position);

            _shuttle.FTLToCoordinates(shuttleUid.Value, shuttleComp, targetCoordinates, Angle.Zero, 2, 3, destroyFloor: true);// temp time of 2 instead of 30 because testing
        }
    }
}
