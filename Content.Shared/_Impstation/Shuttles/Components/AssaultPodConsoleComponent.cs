using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Impstation.Shuttles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AssaultPodConsoleComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? LaunchTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public MapCoordinates TravelCoordinates;

    [DataField]
    public bool Activated;

    [DataField]
    public float TravelTime = 11f;

    [DataField]
    public TimeSpan TimeTillLaunch = TimeSpan.FromSeconds(30);

    [DataField]
    public LocId BeginDepartureAnouncement = "assault-pod-announcement-begin-departure";
    [DataField]
    public LocId BeginDepartureAnouncementSender = "assault-pod-announcement-sender-begin-departure";
    [DataField]
    public LocId DepartureStationAnouncement = "station-announcement-departure";
    [DataField]
    public LocId DepartureStationAnouncementSender = "station-announcement-sender-departure";
    [DataField]
    public SoundSpecifier BeginDepartureAnnouncementSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/ob_alert.ogg");
    [DataField]
    public SoundSpecifier TravelSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/gun_orbital_travel.ogg");
    [DataField]
    public SoundSpecifier ArrivalSound = new SoundCollectionSpecifier("RMCExplosionBig");
    [DataField]
    public SoundSpecifier testArrivalSound = new SoundPathSpecifier("/Audio/_RMC14/Explosion/bigboom1.ogg");
}
