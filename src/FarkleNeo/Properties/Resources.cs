// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Resources;

namespace Farkle;

internal static class Resources
{
    private static readonly bool s_usingResourceKeys = AppContext.TryGetSwitch("System.Resources.UseSystemResourceKeys", out bool usingResourceKeys) && usingResourceKeys;

    private static ResourceManager? s_resourceManager;

    public static ResourceManager ResourceManager => s_resourceManager ??= new ResourceManager("Farkle.Resources", typeof(Resources).Assembly);

    // This method is used to decide if we need to append the exception message parameters to the message when calling SR.Format.
    // by default it returns the value of System.Resources.UseSystemResourceKeys AppContext switch or false if not specified.
    // Native code generators can replace the value this returns based on user input at the time of native code generation.
    // The trimming tools are also capable of replacing the value of this method when the application is being trimmed.
    internal static bool UsingResourceKeys() => s_usingResourceKeys;

    public static string GetResourceString(string resourceKey, CultureInfo? cultureInfo = null)
    {
        if (UsingResourceKeys())
        {
            return resourceKey;
        }

        return ResourceManager.GetString(resourceKey, cultureInfo)!;
    }

    public static string Grammar_TooNewFormat => GetResourceString(nameof(Grammar_TooNewFormat));

    public static string Grammar_TooOldFormat => GetResourceString(nameof(Grammar_TooOldFormat));

    public static string Grammar_EgtNeoNotSupported=> GetResourceString(nameof(Grammar_EgtNeoNotSupported));

    public static string Grammar_GoldParserMustConvert=> GetResourceString(nameof(Grammar_GoldParserMustConvert));

    public static string Grammar_UnrecognizedFormat => GetResourceString(nameof(Grammar_UnrecognizedFormat));

    public static string Grammar_Farkle7MustOpen => GetResourceString(nameof(Grammar_Farkle7MustOpen));

    public static string Grammar_FailedToConvert => GetResourceString(nameof(Grammar_FailedToConvert));

    public static string Parser_ResultAlreadySet => GetResourceString(nameof(Parser_ResultAlreadySet));

    public static string Parser_ResultNotSet => GetResourceString(nameof(Parser_ResultNotSet));
}
