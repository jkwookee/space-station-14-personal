using System.Numerics;

namespace Content.Server._Impstation.Shuttles.Components
{

    /// <summary>
    /// Marker component for FasterThanLight to smimsh after an update has passed
    /// </summary>
    [RegisterComponent]
    public sealed partial class ToSmimshComponent : Component
    {
        [ViewVariables(VVAccess.ReadOnly)]
        public HashSet<EntityUid> FTLTravellingEntities;
    }
}
