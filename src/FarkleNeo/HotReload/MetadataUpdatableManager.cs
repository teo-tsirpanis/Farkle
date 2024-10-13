// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if NET6_0_OR_GREATER
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using Farkle.Collections;

[assembly: MetadataUpdateHandler(typeof(Farkle.HotReload.MetadataUpdatableManager))]

namespace Farkle.HotReload;

/// <summary>
/// Receives Hot Reload events and dispatches them to <see cref="IMetadataUpdatable"/> objects.
/// </summary>
internal static class MetadataUpdatableManager
{
    private static readonly ConditionalWeakTable<Type, ConditionalWeakList<IMetadataUpdatable>> s_items = [];

    /// <summary>
    /// Registers an <see cref="IMetadataUpdatable"/> object to
    /// receive Hot Reload events on the given <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// Neither <paramref name="type"/> nor <paramref name="item"/>
    /// are kept alive by this method.
    /// </remarks>
    public static void Register(Type type, IMetadataUpdatable item)
    {
        s_items.GetOrCreateValue(type).Add(item);
    }

    private static IEnumerable<IMetadataUpdatable> GetAllItems() =>
        s_items.SelectMany(x => x.Value);

    private static IEnumerable<IMetadataUpdatable> GetItems(Type[] types) =>
        types.SelectMany(x => s_items.TryGetValue(x, out var items) ? items.AsEnumerable() : []);

    public static void ClearCache(Type[]? types)
    {
        foreach (IMetadataUpdatable item in types is null ? GetAllItems() : GetItems(types))
        {
            item.ClearCache();
        }
    }
}
#endif
