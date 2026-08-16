using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Awake;

internal static class SceneDialoguePreview
{
    private static readonly List<GameEntity> Markers = new List<GameEntity>();
    private static Mission _mission;
    private static bool _markerSpawnFailed;
    private static bool _fallbackLogged;
    private static float _lastMarkerRange = -1f;

    internal static bool MarkerSpawnFailed => _markerSpawnFailed;

    internal static void Update(
        Mission mission,
        Agent mainAgent,
        IReadOnlyList<AwakeNpcTarget> candidates,
        int selectedAgentIndex,
        bool shoutMode,
        bool contourCapable,
        float rangeMeters)
    {
        if (mission == null || mainAgent == null)
        {
            Clear();
            return;
        }
        if (!ReferenceEquals(_mission, mission))
        {
            ClearMarkers();
            _mission = mission;
            _lastMarkerRange = -1f;
        }

        if (!contourCapable && !_fallbackLogged)
        {
            _fallbackLogged = true;
            AwakeLog.Write("scene_visual_contour_unavailable");
        }

        UpdateAgentHighlights(mission, candidates, selectedAgentIndex, shoutMode, contourCapable);
        if (contourCapable && rangeMeters > 0f)
        {
            UpdateMarkers(mission, mainAgent, rangeMeters);
        }
    }

    internal static void Clear()
    {
        ClearMarkers();
        if (_mission?.Agents != null)
        {
            foreach (Agent agent in _mission.Agents)
            {
                TrySetContour(agent, null);
            }
        }
        _mission = null;
        _lastMarkerRange = -1f;
    }

    private static void UpdateAgentHighlights(
        Mission mission,
        IReadOnlyList<AwakeNpcTarget> candidates,
        int selectedAgentIndex,
        bool shoutMode,
        bool contourCapable)
    {
        if (!contourCapable || mission?.Agents == null) return;
        foreach (Agent agent in mission.Agents)
        {
            if (agent == null || agent.AgentVisuals == null) continue;
            bool candidate = IsCandidate(candidates, agent.Index);
            if (!candidate)
            {
                TrySetContour(agent, null);
                continue;
            }
            if (shoutMode)
            {
                TrySetContour(agent, ColorFrom(1f, 0.95f, 0.6f));
                continue;
            }
            if (agent.Index == selectedAgentIndex)
            {
                TrySetContour(agent, ColorFrom(1f, 0.22f, 0.72f));
            }
            else
            {
                TrySetContour(agent, ColorFrom(1f, 0.84f, 0.2f));
            }
        }
    }

    private static bool IsCandidate(IReadOnlyList<AwakeNpcTarget> candidates, int agentIndex)
    {
        if (candidates == null) return false;
        foreach (AwakeNpcTarget target in candidates)
        {
            if (target != null && target.AgentIndex == agentIndex) return true;
        }
        return false;
    }

    private static void TrySetContour(Agent agent, uint? color)
    {
        try
        {
            agent?.AgentVisuals?.SetContourColor(color, true);
        }
        catch
        {
        }
    }

    private static uint ColorFrom(float r, float g, float b)
    {
        return new Color(r, g, b, 1f).ToUnsignedInteger();
    }

    private static void UpdateMarkers(Mission mission, Agent mainAgent, float rangeMeters)
    {
        if (_markerSpawnFailed)
        {
            return;
        }
        if (_lastMarkerRange >= 0f && Math.Abs(rangeMeters - _lastMarkerRange) < 2f)
        {
            return;
        }
        _lastMarkerRange = rangeMeters;
        float halfAngle = SceneDialoguePreviewMath.CurrentHalfAngle(4f);
        List<Vec3> points = SceneDialoguePreviewMath.BuildFanSegments(
            mainAgent.Position,
            mainAgent.LookDirection,
            rangeMeters,
            halfAngle,
            48);
        if (Markers.Count == points.Count)
        {
            for (int i = 0; i < Markers.Count; i++)
            {
                if (Markers[i] == null) continue;
                MatrixFrame frame = CreateMarkerFrame(points[i]);
                if (Markers[i].GlobalPosition.DistanceSquared(points[i]) > 0.25f)
                {
                    Markers[i].SetGlobalFrame(frame, false);
                }
            }
            return;
        }
        ClearMarkers();
        SpawnMarkers(mission, points);
    }

    private static void SpawnMarkers(Mission mission, List<Vec3> points)
    {
        ItemObject item = ResolveMarkerItem();
        if (item == null)
        {
            _markerSpawnFailed = true;
            AwakeLog.Write("scene_visual_marker_item_missing");
            return;
        }
        foreach (Vec3 point in points)
        {
            try
            {
                MissionWeapon weapon = new MissionWeapon(item, null, null);
                MatrixFrame frame = CreateMarkerFrame(point);
                GameEntity entity = mission.SpawnWeaponWithNewEntity(
                    ref weapon,
                    Mission.WeaponSpawnFlags.CannotBePickedUp,
                    frame);
                if (entity == null) continue;
                try
                {
                    entity.SetMobility(GameEntity.Mobility.Stationary);
                    entity.SetDoNotCheckVisibility(true);
                    entity.SetContourColor(ColorFrom(0.15f, 0.95f, 1f), true);
                }
                catch
                {
                }
                Markers.Add(entity);
            }
            catch (Exception ex)
            {
                _markerSpawnFailed = true;
                AwakeLog.Write("scene_visual_marker_spawn_error error=" + ex.Message);
                ClearMarkers();
                return;
            }
        }
    }

    private static MatrixFrame CreateMarkerFrame(Vec3 point)
    {
        return new MatrixFrame(
            3f, 0f, 0f, 0f,
            0f, 3f, 0f, 0f,
            0f, 0f, 3f, 0f,
            point.X, point.Y, point.Z, 1f);
    }

    private static ItemObject ResolveMarkerItem()
    {
        try
        {
            ItemObject item = Game.Current?.ObjectManager?.GetObject<ItemObject>("sling_leadammo");
            return item ?? Game.Current?.ObjectManager?.GetObject<ItemObject>("throwing_stone");
        }
        catch
        {
            return null;
        }
    }

    private static void ClearMarkers()
    {
        foreach (GameEntity entity in Markers)
        {
            try
            {
                entity?.Remove(0);
            }
            catch
            {
            }
        }
        Markers.Clear();
    }

    internal static void ResetForTesting()
    {
        Clear();
        _markerSpawnFailed = false;
        _fallbackLogged = false;
    }
}
