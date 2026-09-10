using System;
using System.Collections.Generic;
using System.Linq;

namespace Awake;

internal sealed class PersonaTagRegistry
{
    private readonly Dictionary<string, PersonaTagDefinition> _tags =
        new Dictionary<string, PersonaTagDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, PersonaBundleDefinition> _bundles =
        new Dictionary<string, PersonaBundleDefinition>(StringComparer.Ordinal);

    internal PersonaTagRegistry(PersonaTagRegistryDocument document)
    {
        if (document == null) return;
        foreach (PersonaTagDefinition tag in document.Tags ?? new List<PersonaTagDefinition>())
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.Id) || !PersonaTagCategories.IsKnown(tag.Category)) continue;
            if (!_tags.ContainsKey(tag.Id)) _tags[tag.Id] = tag;
        }
        foreach (PersonaBundleDefinition bundle in document.Bundles ?? new List<PersonaBundleDefinition>())
        {
            if (bundle == null || string.IsNullOrWhiteSpace(bundle.Id)) continue;
            if (!_bundles.ContainsKey(bundle.Id)) _bundles[bundle.Id] = bundle;
        }
    }

    internal int Count => _tags.Count;
    internal int BundleCount => _bundles.Count;

    internal bool Contains(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _tags.ContainsKey(id);
    }

    internal bool TryGet(string id, out PersonaTagDefinition tag)
    {
        return _tags.TryGetValue(id ?? string.Empty, out tag);
    }

    internal bool TryExpand(
        IEnumerable<string> directTags,
        IEnumerable<string> bundleIds,
        out List<PersonaTagDefinition> expanded,
        out List<string> warnings)
    {
        expanded = new List<PersonaTagDefinition>();
        warnings = new List<string>();
        HashSet<string> seenTags = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> visitingBundles = new HashSet<string>(StringComparer.Ordinal);
        bool valid = true;
        foreach (string tagId in directTags ?? Enumerable.Empty<string>())
        {
            if (!TryAddTag(tagId, expanded, seenTags, warnings)) valid = false;
        }
        foreach (string bundleId in bundleIds ?? Enumerable.Empty<string>())
        {
            if (!ExpandBundle(bundleId, expanded, seenTags, visitingBundles, warnings)) valid = false;
        }
        expanded.Sort((left, right) =>
        {
            int category = string.CompareOrdinal(left.Category, right.Category);
            return category != 0 ? category : string.CompareOrdinal(left.Id, right.Id);
        });
        return valid;
    }

    internal bool HasConflict(IReadOnlyList<PersonaTagDefinition> tags, out string conflict)
    {
        conflict = string.Empty;
        if (tags == null) return false;
        HashSet<string> ids = new HashSet<string>(tags.Select(tag => tag.Id), StringComparer.Ordinal);
        foreach (PersonaTagDefinition tag in tags)
        {
            foreach (string conflictId in tag.Conflicts ?? new List<string>())
            {
                if (!ids.Contains(conflictId)) continue;
                string left = string.CompareOrdinal(tag.Id, conflictId) <= 0 ? tag.Id : conflictId;
                string right = string.CompareOrdinal(tag.Id, conflictId) <= 0 ? conflictId : tag.Id;
                conflict = left + "<->" + right;
                return true;
            }
        }
        return false;
    }

    private bool ExpandBundle(
        string bundleId,
        List<PersonaTagDefinition> output,
        HashSet<string> seenTags,
        HashSet<string> visitingBundles,
        List<string> warnings)
    {
        PersonaBundleDefinition bundle;
        if (!_bundles.TryGetValue(bundleId ?? string.Empty, out bundle))
        {
            warnings.Add("persona.bundle_unregistered:" + (bundleId ?? string.Empty));
            return false;
        }
        if (!visitingBundles.Add(bundle.Id))
        {
            warnings.Add("persona.bundle_cycle:" + bundle.Id);
            return false;
        }
        bool valid = true;
        foreach (string member in bundle.Tags ?? new List<string>())
        {
            if (_bundles.ContainsKey(member))
            {
                if (!ExpandBundle(member, output, seenTags, visitingBundles, warnings)) valid = false;
            }
            else if (!TryAddTag(member, output, seenTags, warnings))
            {
                valid = false;
            }
        }
        visitingBundles.Remove(bundle.Id);
        return valid;
    }

    private bool TryAddTag(
        string tagId,
        List<PersonaTagDefinition> output,
        HashSet<string> seenTags,
        List<string> warnings)
    {
        PersonaTagDefinition tag;
        if (!_tags.TryGetValue(tagId ?? string.Empty, out tag))
        {
            warnings.Add("persona.tag_unregistered:" + (tagId ?? string.Empty));
            return false;
        }
        if (seenTags.Add(tag.Id)) output.Add(tag);
        return true;
    }
}