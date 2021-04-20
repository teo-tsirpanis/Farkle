# Copyright (c) 2021 Theodore Tsirpanis
# 
# This software is released under the MIT License.
# https://opensource.org/licenses/MIT

$ErrorActionPreference = "Stop"
$nuspecXmlns = [System.Xml.Linq.XNamespace]::Get("http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd")

# Patches the given nupkg file to import its first dependency
# group on any framework. Used on Farkle.Tools.MSBuild.
function Update-Nupkg-Dependencies($nupkgPath, $nuspecFile) {
    if (-not (Test-Path -Path $nupkgPath)) {
        Write-Output "Error: $nupkgPath does not exist."
        exit 1
    }
    $nupkg = [System.IO.Compression.ZipFile]::Open($nupkgPath, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        $nuspecEntry = $nupkg.GetEntry($nuspecFile)
        if ($NULL -eq $nuspecEntry) {
            Write-Output "Error: $nuspecFile does not exist inside the package."
            exit 1
        }
        $nuspec = $nuspecEntry.Open()
        try {
            $nuspecXml = [System.Xml.Linq.XDocument]::Load($nuspec)
            $nuspecElement = $nuspecXml.Root
            $nuspecElement = $nuspecElement.Element($nuspecXmlns.GetName("metadata"))
            $nuspecElement = $nuspecElement.Element($nuspecXmlns.GetName("dependencies"))
            $nuspecElement = $nuspecElement.Element($nuspecXmlns.GetName("group"))
            $nuspecAttribute = $nuspecElement.Attribute([System.Xml.Linq.XName]::Get("targetFramework"))
            if ($NULL -eq $nuspecAttribute) {
                Write-Output "Target framework attribute does not exist; perhaps the nupkg is already patched."
                exit 0
            }
            $nuspecAttribute.Remove()
            $nuspec.SetLength(0)
            $nuspecXml.Save($nuspec)
        }
        finally {
            $nuspec.Dispose()
        }
    }
    finally {
        $nupkg.Dispose()
    }
    Write-Output "$nupkgPath was successfully patched."
}

Update-Nupkg-Dependencies $args[0] $args[1]
