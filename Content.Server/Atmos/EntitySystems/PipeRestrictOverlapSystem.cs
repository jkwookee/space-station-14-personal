using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Construction.Components;
using Content.Shared.NodeContainer;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// This handles restricting pipe-based entities from overlapping outlets/inlets with other entities.
/// </summary>
public sealed class PipeRestrictOverlapSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;

    private readonly List<EntityUid> _anchoredEntities = new();
    private EntityQuery<NodeContainerComponent> _nodeContainerQuery;
    // Goobstation - Allow device-on-pipe stacking
    private EntityQuery<PipeRestrictOverlapComponent> _restrictOverlapQuery;
    private EntityQuery<DeviceRestrictOverlapComponent> _deviceRestrictOverlapQuery; // Funky

    public bool StrictPipeStacking = false;
    public bool StrictDeviceStacking = true; // Funky

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PipeRestrictOverlapComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<PipeRestrictOverlapComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        // Funky - Added to handle device overlap
        SubscribeLocalEvent<DeviceRestrictOverlapComponent, AnchorStateChangedEvent>(OnDeviceAnchorStateChanged);
        SubscribeLocalEvent<DeviceRestrictOverlapComponent, AnchorAttemptEvent>(OnDeviceAnchorAttempt);
        Subs.CVar(_cfg, CCVars.StrictPipeStacking, (bool val) => {StrictPipeStacking = val;}, false);

        _nodeContainerQuery = GetEntityQuery<NodeContainerComponent>();
        // Goobstation - Allow device-on-pipe stacking
        _restrictOverlapQuery = GetEntityQuery<PipeRestrictOverlapComponent>();
        _deviceRestrictOverlapQuery = GetEntityQuery<DeviceRestrictOverlapComponent>();
    }

    private void OnAnchorStateChanged(Entity<PipeRestrictOverlapComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        if (HasComp<AnchorableComponent>(ent) && CheckOverlap(ent))
        {
            _popup.PopupEntity(Loc.GetString("pipe-restrict-overlap-popup-blocked", ("pipe", ent.Owner)), ent);
            _xform.Unanchor(ent, Transform(ent));
        }
    }

    private void OnAnchorAttempt(Entity<PipeRestrictOverlapComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_nodeContainerQuery.TryComp(ent, out var node))
            return;

        var xform = Transform(ent);
        if (CheckOverlap((ent, node, xform)))
        {
            _popup.PopupEntity(Loc.GetString("pipe-restrict-overlap-popup-blocked", ("pipe", ent.Owner)), ent, args.User);
            args.Cancel();
        }
    }

    /// <summary>
    /// Funky
    /// Checks if the device overlaps with any other devices.
    /// </summary>
    private void OnDeviceAnchorStateChanged(Entity<DeviceRestrictOverlapComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        if (HasComp<AnchorableComponent>(ent) && CheckDeviceOverlap(ent))
        {
            _popup.PopupEntity(Loc.GetString("device-restrict-overlap-popup-blocked", ("device", ent.Owner)), ent);
            _xform.Unanchor(ent, Transform(ent));
        }
    }

    private void OnDeviceAnchorAttempt(Entity<DeviceRestrictOverlapComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (CheckDeviceOverlap(ent))
        {
            _popup.PopupEntity(Loc.GetString("device-restrict-overlap-popup-blocked", ("device", ent.Owner)), ent, args.User);
            args.Cancel();
        }
    }

    [PublicAPI]
    public bool CheckOverlap(EntityUid uid)
    {
        if (!_nodeContainerQuery.TryComp(uid, out var node))
            return false;

        return CheckOverlap((uid, node, Transform(uid)));
    }

    public bool CheckOverlap(Entity<NodeContainerComponent, TransformComponent> ent)
    {
        if (ent.Comp2.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var indices = _map.TileIndicesFor(grid, gridComp, ent.Comp2.Coordinates);
        _anchoredEntities.Clear();
        _map.GetAnchoredEntities((grid, gridComp), indices, _anchoredEntities);

        foreach (var otherEnt in _anchoredEntities)
        {
            // this should never actually happen but just for safety
            if (otherEnt == ent.Owner)
                continue;

            if (!_nodeContainerQuery.TryComp(otherEnt, out var otherComp))
                continue;

            // Goobstation - Allow device-on-pipe stacking
            if (!_restrictOverlapQuery.HasComp(otherEnt))
                continue;

            var (overlapping, which) = PipeNodesOverlap(ent, (otherEnt, otherComp, Transform(otherEnt)), takenDirs);
            takenDirs |= which;

            if (overlapping)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Funky
    /// Checks if the device overlaps with any other devices.
    /// Returns true if it overlaps with another device.
    /// </summary>
    [PublicAPI]
    public bool CheckDeviceOverlap(EntityUid uid)
    {
        if (!_deviceRestrictOverlapQuery.HasComp(uid))
            return false;

        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var indices = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        _anchoredEntities.Clear();
        _map.GetAnchoredEntities((grid, gridComp), indices, _anchoredEntities);

        foreach (var otherEnt in _anchoredEntities)
        {
            if (otherEnt == uid)
                continue;

            if (!_deviceRestrictOverlapQuery.HasComp(otherEnt))
                continue;

            if (StrictDeviceStacking)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Funky
    /// Checks if the device overlaps with any other devices.
    /// Returns true if it overlaps with another device.
    /// </summary>
    [PublicAPI]
    public bool CheckDeviceOverlap(EntityUid uid)
    {
        if (!_deviceRestrictOverlapQuery.HasComp(uid))
            return false;

        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var indices = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        _anchoredEntities.Clear();
        _map.GetAnchoredEntities((grid, gridComp), indices, _anchoredEntities);

        foreach (var otherEnt in _anchoredEntities)
        {
            if (otherEnt == uid)
                continue;

            if (!_deviceRestrictOverlapQuery.HasComp(otherEnt))
                continue;

            if (StrictDeviceStacking)
                return true;
        }

        return false;
    }

    public bool PipeNodesOverlap(Entity<NodeContainerComponent, TransformComponent> ent, Entity<NodeContainerComponent, TransformComponent> other)
    {
        var entDirsAndLayers = GetAllDirectionsAndLayers(ent).ToList();
        var otherDirsAndLayers = GetAllDirectionsAndLayers(other).ToList();

        foreach (var (dir, layer) in entDirsAndLayers)
        {
            foreach (var (otherDir, otherLayer) in otherDirsAndLayers)
            {
                if ((dir & otherDir) != 0 && layer == otherLayer)
                    return true;
            }
        }

        return false;

        IEnumerable<(PipeDirection, AtmosPipeLayer)> GetAllDirectionsAndLayers(Entity<NodeContainerComponent, TransformComponent> pipe)
        {
            foreach (var node in pipe.Comp1.Nodes.Values)
            {
                // we need to rotate the pipe manually like this because the rotation doesn't update for pipes that are unanchored.
                if (node is PipeNode pipeNode)
                    yield return (pipeNode.OriginalPipeDirection.RotatePipeDirection(pipe.Comp2.LocalRotation), pipeNode.CurrentPipeLayer);
            }
        }
    }
}
