// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Text;

namespace ComSharp;

public static class SymbolFormat
{
    /// <summary>
    /// Escapes a fully qualified type name into a format suitable for use as an identifier.
    /// </summary>
    public static string EscapeTypeName(string typeName)
    {
        if (typeName.StartsWith(Constants.GlobalPrefix, StringComparison.Ordinal))
        {
            typeName = typeName[Constants.GlobalPrefix.Length..];
        }
        // Convert dots to underscores.
        // Convert underscores to "_0".
        // Convert '0' to "00".
        if (typeName.AsSpan().IndexOfAny('_', '0') == -1)
        {
            // Fast path if there are no characters that would change the string's length.
            return typeName.Replace('.', '_');
        }
        var sb = new StringBuilder(typeName.Length);
        foreach (var c in typeName)
        {
            switch (c)
            {
                case '.':
                    sb.Append('_');
                    continue;
                case '_':
                    sb.Append("_0");
                    continue;
                case '0':
                    sb.Append("00");
                    continue;
                default:
                    sb.Append(c);
                    continue;
            }
        }
        return sb.ToString();
    }

    public static string GetComSharpWrapperName(string typeName)
    {
        return EscapeTypeName(typeName);
    }

    public static string GetDotNetWrapperName(string typeName)
    {
        return EscapeTypeName(typeName) + "_DotNetWrapper";
    }
}
