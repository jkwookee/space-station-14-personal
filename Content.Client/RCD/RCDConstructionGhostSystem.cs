using Content.Client.Hands.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;

namespace Content.Client.RCD;

/// <summary>
/// System for handling structure ghost placement in places where RCD can create objects.
/// </summary>
public sealed class RCDConstructionGhostSystem : EntitySystem
{
    private const string PlacementMode = nameof(AlignRCDConstruction);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlacementManager _placementManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    private string _placementMode = typeof(AlignRCDConstruction).Name;
    private string _rpdPlacementMode = typeof(AlignRPDAtmosPipeLayers).Name;
    private Direction _placementDirection = default;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Get current placer data
        var placerEntity = _placementManager.CurrentPermission?.MobUid;
        var placerProto = _placementManager.CurrentPermission?.EntityType;
        var placerIsRCD = HasComp<RCDComponent>(placerEntity);

        // Exit if erasing or the current placer is not an RCD (build mode is active)
        if (_placementManager.Eraser || (placerEntity != null && !placerIsRCD))
            return;

        // Determine if player is carrying an RCD in their active hand
        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return;

        var heldEntity = _hands.GetActiveItem(player);

        // Don't open the placement overlay for client-side RCDs.
        // This may happen when predictively spawning one in your hands.
        if (heldEntity != null && IsClientSide(heldEntity.Value))
            return;

        if (!TryComp<RCDComponent>(heldEntity, out var rcd))
        {
            // If the player was holding an RCD, but is no longer, cancel placement
            if (placerIsRCD)
                _placementManager.Clear();

            return;
        }
        var prototype = _protoManager.Index(rcd.ProtoId);

        // Update the direction the RCD prototype based on the placer direction
        if (_placementDirection != _placementManager.Direction)
        {
            _placementDirection = _placementManager.Direction;
            RaiseNetworkEvent(new RCDConstructionGhostRotationEvent(GetNetEntity(heldEntity.Value), _placementDirection));
        }

        // If the placer has not changed build it.
        _rcdSystem.UpdateCachedPrototype(heldEntity.Value, rcd);
        var useProto = (_useMirrorPrototype && !string.IsNullOrEmpty(rcd.CachedPrototype.MirrorPrototype)) ? rcd.CachedPrototype.MirrorPrototype : rcd.CachedPrototype.Prototype;

        // Funky - Check if RPD and prototype supports layered placement
        if (rcd.IsRpd && useProto != null && _protoManager.TryIndex<RCDPrototype>(rcd.CachedPrototype.ID, out var rcdProto) && !rcdProto.NoLayers)
        {
            _placementManager.Clear();
            CreateLayeredPlacer(heldEntity.Value, rcd, useProto);
        }
        else if (heldEntity != placerEntity || useProto != placerProto)
        {
            _placementManager.Clear();
            CreatePlacer(heldEntity.Value, rcd, useProto);
        }


    }

    private void CreatePlacer(EntityUid uid, RCDComponent component, string? prototype)
    {
        // Create a new placer
        var newObjInfo = new PlacementInformation
        {
            MobUid = heldEntity.Value,
            PlacementOption = PlacementMode,
            EntityType = prototype.Prototype,
            Range = (int)Math.Ceiling(SharedInteractionSystem.InteractionRange),
            IsTile = (prototype.Mode == RcdMode.ConstructTile),
            UseEditorContext = false,
        };

        _placementManager.Clear();
        _placementManager.BeginPlacing(newObjInfo);
    }

    private void CreateLayeredPlacer(EntityUid uid, RCDComponent component, string? prototype)
    {
        // Create a layer placer
        var newObjInfo = new PlacementInformation
        {
            MobUid = uid,
            PlacementOption = _placementMode,
            EntityType = prototype,
            Range = (int) Math.Ceiling(SharedInteractionSystem.InteractionRange),
            IsTile = (component.CachedPrototype.Mode == RcdMode.ConstructTile),
            UseEditorContext = false,
        };

        _placementManager.BeginPlacing(newObjInfo);
    }

    private void CreateLayeredPlacer(EntityUid uid, RCDComponent component, string? prototype)
    {
        // Create a layer placer
        var newObjInfo = new PlacementInformation
        {
            MobUid = uid,
            PlacementOption = _rpdPlacementMode,
            EntityType = prototype,
            Range = (int) Math.Ceiling(SharedInteractionSystem.InteractionRange),
            IsTile = (component.CachedPrototype.Mode == RcdMode.ConstructTile),
            UseEditorContext = false,
        };

        _placementManager.BeginPlacing(newObjInfo);
    }
}
