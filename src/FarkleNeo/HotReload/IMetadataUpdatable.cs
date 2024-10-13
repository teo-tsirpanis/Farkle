// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.HotReload;

/// <summary>
/// Provides an interface to listen for Hot Reload events.
/// </summary>
internal interface IMetadataUpdatable
{
    /// <summary>
    /// Clears an object's internal caches after a metadata update.
    /// </summary>
    void ClearCache();

    // We don't need an UpdateApplication method at the moment.
}
