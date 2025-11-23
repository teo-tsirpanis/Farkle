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
    /// A list of types that have been observed to be updated by Hot Reload.
    /// </summary>
    /// <remarks>
    /// If this is <see langword="null"/>, all types are presumed to have been reloaded.
    /// </remarks>
    // Use volatile to synchronize between setting this to null and reading it.
    // The ConditionalWeakTable itself is thread-safe and uses locks for synchronization.
    private static volatile ConditionalWeakTable<Type, object?>? s_reloaded = [];

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

    /// <summary>
    /// Returns whether the given <see cref="Type"/> might have been updated by Hot Reload.
    /// </summary>
    public static bool IsMaybeReloaded(Type type) => s_reloaded?.TryGetValue(type, out _) ?? true;

    public static void ClearCache(Type[]? types)
    {
        if (types is null)
        {
            s_reloaded = null;
        }
        else
        {
            if (s_reloaded is { } reloaded)
            {
                foreach (var type in types)
                {
                    reloaded?.TryAdd(type, null);
                }
            }
        }

        foreach (IMetadataUpdatable item in types is null ? GetAllItems() : GetItems(types))
        {
            item.ClearCache();
        }
    }
}
#endif
