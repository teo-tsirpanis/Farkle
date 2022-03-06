// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace rec Farkle.Builder

open Farkle
open Farkle.Common
open Farkle.Builder.OperatorPrecedence
open System
open System.Collections.Immutable
#if NET
open System.Diagnostics.CodeAnalysis
#endif

/// A type of source code comment. As everybody might know,
/// comments are the text fragments that are ignored by the parser.
type internal Comment =
    /// A line comment. It starts when the given literal is encountered,
    /// and ends when the line ends.
    | LineComment of string
    /// A block comment. It starts when the first literal is encountered,
    /// and ends when when the second literal is encountered.
    | BlockComment of BlockStart: string * BlockEnd: string

/// <summary>Additional information about a grammar to be built.</summary>
/// <remarks>Each <see cref="DesigntimeFarkle"/> has one, but the metadata object that
/// will be taken into consideration when building will be the one belonging
/// to the topmost designtime Farkle, on which a build function was called.
/// An exception is made for the operator scopes, which are found in all
/// designtime Farkles of a grammar.</remarks>
type internal GrammarMetadata = {
    /// Whether the grammar is case sensitive.
    CaseSensitive: bool
    /// Whether to discard any whitespace characters encountered
    /// outside of any terminal. Farkle considers whitespace the
    /// characters: Space, Horizontal Tab, Carriage Return and Line feed.
    AutoWhitespace: bool
    /// The comments this grammar accepts.
    Comments: Comment ImmutableList
    /// Any other symbols definable by a regular expression that will
    /// be discarded if they appear anywhere outside of any terminal.
    NoiseSymbols: (string * Regex) ImmutableList
    /// <summary>An <see cref="OperatorScope"/> to assist
    /// the conflict resolution when building the grammar.</summary>
    /// <remarks>This property will be considered when set in
    /// any designtime Farkle of a grammar; not only the
    /// topmost one.</remarks>
    /// <seealso cref="OperatorScope.Empty"/>
    OperatorScope: OperatorScope
#if MODERN_FRAMEWORK
    /// An optional post-procecssor factory that will be used during building
    /// if specified. This design allows CodeGen to be trimmed away if unused.
    CodeGenInterface: CodeGen.IDynamicCodeGenInterface MaybeNull
#endif
}
with
    /// The default metadata of a grammar.
    /// According to them, the grammar is not case sensitive
    /// and white space is discarded.
    static member Default = GrammarMetadata._default
    /// A stricter set of metadata for a grammar.
    /// They specify a case sensitive grammar without any whitespace allowed.
    static member Strict = GrammarMetadata.strict

module private GrammarMetadata =

    let _default = {
        CaseSensitive = false
        AutoWhitespace = true
        NoiseSymbols = ImmutableList.Empty
        Comments = ImmutableList.Empty
        OperatorScope = OperatorScope.Empty
#if MODERN_FRAMEWORK
        CodeGenInterface = MaybeNull.nullValue
#endif
    }

    let strict = {
        _default with
            CaseSensitive = true
            AutoWhitespace = false
    }
