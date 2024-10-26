using Content.Shared.Charges.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Charges.Components;

/// <summary>
/// Specifies the attached action has discrete charges, separate to a cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedChargesSystem))]
public sealed partial class LimitedChargesComponent : Component
{
    [DataField, AutoNetworkedField]
    public int LastCharges;

    /// <summary>
    ///     The max charges this action has.
    /// </summary>
    [DataField("maxCharges"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public FixedPoint2 MaxCharges = 3;

    /// <summary>
    /// Last time charges was changed. Used to derive current charges.
    /// </summary>
    [DataField("charges"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public FixedPoint2 Charges = 3;
}
