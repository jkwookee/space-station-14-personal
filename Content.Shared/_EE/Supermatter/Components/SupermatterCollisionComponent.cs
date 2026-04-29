using Content.Shared._Impstation.StrangeMoods;
using Content.Shared.Atmos;
using Content.Shared.DeviceLinking;
using Content.Shared.DoAfter;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._EE.Supermatter.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterCollisionComponent : Component
{
    [DataField]
    public EntProtoId CollisionResultPrototype = "Ash";

    [DataField]
    public SoundSpecifier DustSound = new SoundPathSpecifier("/Audio/_EE/Supermatter/supermatter.ogg");

}
