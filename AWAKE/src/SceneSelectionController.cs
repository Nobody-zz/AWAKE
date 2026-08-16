using System;
using System.Collections.Generic;

namespace Awake;

internal sealed class SceneSelectionItem
{
    internal string Id { get; }
    internal string DisplayName { get; }
    internal float DistanceMeters { get; }

    internal SceneSelectionItem(string id, string displayName, float distanceMeters)
    {
        Id = id ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        DistanceMeters = distanceMeters;
    }
}

internal sealed class SceneSelectionController
{
    internal const int MaximumCandidates = 48;

    private readonly List<SceneSelectionItem> _items = new List<SceneSelectionItem>();
    private int _selectedIndex = -1;

    internal int Count => _items.Count;
    internal int SelectedIndex => _selectedIndex;
    internal bool HasSelection => _selectedIndex >= 0 && _selectedIndex < _items.Count;

    internal SceneSelectionItem Selected
    {
        get
        {
            if (!HasSelection) return null;
            return _items[_selectedIndex];
        }
    }

    internal void SetCandidates(IReadOnlyList<SceneSelectionItem> candidates, string preferredId = null)
    {
        _items.Clear();
        _selectedIndex = -1;
        if (candidates == null) return;

        string fallbackId = preferredId;
        int preferredIndex = -1;
        int count = 0;
        foreach (SceneSelectionItem item in candidates)
        {
            if (item == null || count >= MaximumCandidates) break;
            _items.Add(item);
            if (!string.IsNullOrEmpty(preferredId)
                && StringComparer.Ordinal.Equals(item.Id, preferredId))
            {
                preferredIndex = count;
            }
            count++;
        }

        if (preferredIndex >= 0)
        {
            _selectedIndex = preferredIndex;
            return;
        }
        if (!string.IsNullOrEmpty(fallbackId))
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (StringComparer.Ordinal.Equals(_items[i].Id, fallbackId))
                {
                    _selectedIndex = i;
                    return;
                }
            }
        }
        if (_items.Count > 0) _selectedIndex = 0;
    }

    internal void Cycle(int direction)
    {
        if (_items.Count == 0)
        {
            _selectedIndex = -1;
            return;
        }
        if (!HasSelection)
        {
            _selectedIndex = direction >= 0 ? 0 : _items.Count - 1;
            return;
        }
        int next = _selectedIndex + Math.Sign(direction);
        if (next < 0) next = _items.Count - 1;
        if (next >= _items.Count) next = 0;
        _selectedIndex = next;
    }

    internal void Clear()
    {
        _items.Clear();
        _selectedIndex = -1;
    }

    internal void ClearSelection()
    {
        _selectedIndex = -1;
    }
}
