// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.Build.Utilities;
using System.Resources;

namespace Farkle.Tools.Precompiler;

internal static class TaskLoggingHelperExtensions
{
    internal static readonly ResourceManager _resourceManager = new("Farkle.Tools.Precompiler.Resources", typeof(TaskLoggingHelperExtensions).Assembly);

    private static string GetResourceString(string resourceKey) => _resourceManager.GetString(resourceKey)!;

    extension(TaskLoggingHelper log)
    {
        private void LogErrorLocalized(string code, string resourceKey, params object[] args)
        {
            string helpLink = string.Format(Obsoletions.SharedUrlFormat, code);
            string message = GetResourceString(resourceKey);
            log.LogError(subcategory: null, code, null, helpLink, null, 0, 0, 0, 0, message, args);
        }

        private void LogWarningLocalized(string code, string resourceKey, params object[] args)
        {
            string helpLink = string.Format(Obsoletions.SharedUrlFormat, code);
            string message = GetResourceString(resourceKey);
            log.LogWarning(subcategory: null, code, null, helpLink, null, 0, 0, 0, 0, message, args);
        }

        public void IncompatiblePrecompilerInterface() => log.LogErrorLocalized("FARKLE0016", "Precompiler_IncompatiblePrecompilerInterface");

        public void FailedToUnloadAssembly() => log.LogWarningLocalized("FARKLE0017", "Precompiler_FailedToUnloadAssembly");

        public void LrConflicts(int conflictCount)
        {
            string resourceKey = conflictCount == 1 ? "Precompiler_LrConflicts_Singular" : "Precompiler_LrConflicts_Plural";
            // Use existing code for LR conflicts
            log.LogErrorLocalized("FARKLE0007", resourceKey, conflictCount);
        }
    }
}
