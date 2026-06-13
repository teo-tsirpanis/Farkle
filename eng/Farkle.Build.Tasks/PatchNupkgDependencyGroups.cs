// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Farkle.Build.Tasks;

/// <summary>
/// Patches the given nupkg file to import its first dependency
/// group on any framework. Used on Farkle.Tools.MSBuild.
/// </summary>
public sealed class PatchNupkgDependencyGroups : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] InputPackages { get; set; } = [];

    private bool PatchNuspec(string nuspecPath)
    {
        Log.LogMessage($"Patching nuspec file {nuspecPath}...");
        using var nuspecStream = File.Open(nuspecPath, FileMode.Open, FileAccess.ReadWrite);
        var nuspecXml = XDocument.Load(nuspecStream);
        var ns = nuspecXml.Root?.GetDefaultNamespace() ?? XNamespace.None; // The nuspec file has many possible namespaces.
        var dependencyGroups = nuspecXml.Root
            ?.Element(ns + "metadata")
            ?.Element(ns + "dependencies")
            ?.Elements(ns + "group");
        if (dependencyGroups is null)
        {
            Log.LogMessage($"No dependency groups found.");
            return false;
        }
        bool sawFirst = false;
        foreach (var group in dependencyGroups)
        {
            if (!sawFirst)
            {
                sawFirst = true;
                if (group.Attribute("targetFramework") is { } attr)
                {
                    attr.Remove();
                }
                continue;
            }
            if (group.Descendants().Any())
            {
                Log.LogError("Only the first dependency group may have dependencies. When multi-targeting, export your dependencies only to the first target framework.");
            }
            group.Remove();
        }
        if (!sawFirst)
        {
            return false; // No changes were made.
        }
        nuspecStream.SetLength(0);
        nuspecXml.Save(nuspecStream);
        Log.LogMessage("Patched nuspec file {0}", nuspecPath);
        return true;
    }

    private void PatchNupkg(string packagePath, string nuspecPath)
    {
        using var nuspecStream = File.OpenRead(nuspecPath);
        using var nupkg = ZipFile.Open(packagePath, ZipArchiveMode.Update);
        var nuspecEntry = nupkg.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry is null)
        {
            Log.LogError($"Could not find nuspec file in package {packagePath}");
            return;
        }
        nuspecEntry.Delete();
        nupkg.CreateEntryFromFile(nuspecPath, nuspecEntry.FullName);
    }

    public override bool Execute()
    {
        var nuspec = InputPackages.SingleOrDefault(p => p.ItemSpec.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspec is null)
        {
            Log.LogError("Could not find nuspec file in input.");
            return true;
        }
        if (!PatchNuspec(nuspec.ItemSpec))
        {
            return true;
        }
        var nupkg = InputPackages.SingleOrDefault(p => p.ItemSpec.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
        if (nupkg is null)
        {
            Log.LogError($"Could not find nupkg file in input.");
            return false;
        }
        PatchNupkg(nupkg.ItemSpec, nuspec.ItemSpec);
        return !Log.HasLoggedErrors;
    }
}
