// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace ComSharp;

[Flags]
public enum ComSharpInterfaceOptionsSpec
{
    ComSharpWrapper = 1,
    DotNetWrapper = 2,
    Both = ComSharpWrapper | DotNetWrapper,
}
