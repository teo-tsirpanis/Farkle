// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle;

internal static class Obsoletions
{
    public const string SharedUrlFormat = "https://github.com/teo-tsirpanis/Farkle/blob/mainstream/docs/diagnostics/{0}.md";

    public const string AsIsApiCode = "FARKLE1001";
    public const string AsIsApiMessage = "Use AsProduction() instead.";

    public const string BuildUntypedCode = "FARKLE1002";
    public const string BuildUntypedMessage = "Use BuildSyntaxCheck() instead.";

    public const string RegexAndOrCode = "FARKLE1003";

    public const string CompatibilityTypesCode = "FARKLE1004";
}
