using Content.Server.AlertLevel;
using Content.Server.Announcements.Systems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Impstation.Shuttles.Components;
using Content.Shared._Impstation.Shuttles.Events;
using Content.Shared.Pinpointer;
using Content.Shared.Station;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Impstation.Shuttles.Systems
{
    public sealed class AssaultPodConsoleSystem : EntitySystem
    {
        [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
        [Dependency] private readonly AnnouncerSystem _announcer = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly GameTicker _gameTicker = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly ShuttleSystem _shuttle = default!;
        [Dependency] private readonly SharedStationSystem _station = default!;
        private static readonly ProtoId<AlertLevelPrototype> GammaAlert = "Gamma";
        private static readonly string CommandAnnouncementId = "commandReport";

        public override void Initialize()
        {
            base.Initialize();
            Subs.BuiEvents<AssaultPodConsoleComponent>(StationMapUiKey.Key, subs =>
            {
                subs.Event<ClickCoordMessage>(OnClickCoord);
            });
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<AssaultPodConsoleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (!comp.Activated)
                    continue;

                if (comp.LaunchTime == null || comp.LaunchTime > _timing.CurTime)
                    continue;

                var shuttleUid = Transform(uid).GridUid;

                if (!TryComp(shuttleUid, out ShuttleComponent? shuttleComp))
                    continue;

                var mapUid = _mapSystem.GetMap(comp.TravelCoordinates.MapId);
                var targetCoordinates = new EntityCoordinates(mapUid, comp.TravelCoordinates.Position);

                _shuttle.FTLToCoordinates(
                    shuttleUid.Value,
                    shuttleComp,
                    targetCoordinates,
                    Angle.Zero,
                    hyperspaceTime: comp.TravelTime,
                    travelSound: comp.TravelSound,
                    arrivalSound: comp.ArrivalSound,
                    destroyFloor: true);

                SendDepartureAnnouncement(comp);

                comp.LaunchTime = null; // have it only activate once
            }
        }

        private void OnClickCoord(Entity<AssaultPodConsoleComponent> ent, ref ClickCoordMessage args)
        {
            if (ent.Comp.Activated)
                return;

            ent.Comp.Activated = true;
            ent.Comp.LaunchTime = _timing.CurTime + ent.Comp.TimeTillLaunch;
            ent.Comp.TravelCoordinates = args.Coordinates;

            _chat.DispatchFilteredAnnouncement(
                Filter.BroadcastMap(Transform(ent).MapID),
                Loc.GetString(ent.Comp.BeginDepartureAnouncement),
                sender: Loc.GetString(ent.Comp.BeginDepartureAnouncementSender),
                announcementSound: ent.Comp.BeginDepartureAnnouncementSound,
                colorOverride: Color.DarkRed
            );
        }

        private void SendDepartureAnnouncement(AssaultPodConsoleComponent comp)
        {
            foreach (var rule in _gameTicker.GetActiveGameRules())
            {
                if (!TryComp<NukeopsRuleComponent>(rule, out var nukeopsRule) || nukeopsRule.TargetStation == null)
                    continue;

                _alertLevelSystem.SetLevel(nukeopsRule.TargetStation.Value, GammaAlert, true, true, true);

                var stationGrid = _station.GetLargestGrid((nukeopsRule.TargetStation.Value, null));
                if (stationGrid == null)
                    continue;

                _announcer.SendAnnouncement(
                    _announcer.GetAnnouncementId(CommandAnnouncementId),
                    Filter.BroadcastGrid(stationGrid.Value),
                    Loc.GetString(comp.DepartureStationAnouncement),
                    Loc.GetString(comp.DepartureStationAnouncementSender),
                    station: nukeopsRule.TargetStation.Value,
                    colorOverride: Color.Cyan
                );
            }
        }
    }
}
