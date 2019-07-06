// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Microsoft.Build.Framework
open Microsoft.Build.Utilities

/// An MSBuild task that generates a skeleton program from a Farkle grammar and a Scriban template.
type FarkleTask() =
    inherit Task()

    [<Required>]
    /// <summary>The path to the grammar file in question.</summary>
    /// <remarks>It is required.</remarks>
    member val GrammarPath = "" with get, set

    /// <summary>The programming language of the generated template.</summary>
    /// <remarks>This property is used to load a built-in template. If both it,
    /// and <see cref="CustomTemplate"/> are set, then the latter get used. But
    /// if none of the two is set, it is an error.</remarks>
    /// <seealso cref="CustomTemplate"/>
    member val Language = "" with get, set

    /// <summary>The path of the custom Scriban template to use.</summary>
    /// <remarks>This property takes precedence if both it and
    /// <see cref="Language"/> is used.</remarks>
    /// <seealso cref="Language"/>
    member val CustomTemplate = "" with get, set

    /// <summary>An optional custom name for the grammar.</summary>
    /// <remarks>It is supported by the built-in templates, where it changes the
    /// prefix of the namespace, which defaults to the "Name" property as set in
    /// GOLD Parser.</remarks>
    member val CustomName = "" with get, set

    /// <summary>The file path to write the output to.</summary>
    /// <remarks>If not specified, it defaults to the name of the grammar file,
    /// with the extension set by the template, which defaults to <c>.out</c>.
    member val OutputFile = "" with get, set

    [<Output>]
    /// <summary>The file path where the output was generated to.</summary>
    member val GeneratedTo = "" with get, set

    override this.Execute() =
        true
