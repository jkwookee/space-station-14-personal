using Content.Server._Impstation.StationEvents.Events;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Map;

namespace Content.Server._Impstation.StationEvents.Components;

[RegisterComponent, Access(typeof(RadiationStormRule))]
public sealed partial class RadiationStormRuleComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public MinMax StormRadius = new(10, 25);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public LocId Announcement = "station-event-radiation-storm-announcement";

    [DataField]
    public MinMax TimeBetweenPulse = new(3, 5);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilNextPulse;

    [ViewVariables(VVAccess.ReadOnly)]
    public List<TileRef> AvailableTiles = new();
}
