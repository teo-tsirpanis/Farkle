// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Constants shared on many places in Farkle's builder.
module internal Farkle.Builder.BuilderCommon

/// The set of characters Farkle considers to be whitespace by default.
/// It is made of exactly the horizontal tab, the line feed, the carriage return and the space.
let whitespaceCharacters = set ['\t'; '\n'; '\r'; ' ']

/// The set of characters Farkle considers to be whitespace by default,
/// but without those it considers to be newlines.
/// It is made of exactly the horizontal tab and the space.
let whitespaceCharactersNoNewLine = set ['\t'; ' ']

/// Throws the exception for when a custom implementation of
/// the designtime Farkle interface was detected, which is prohibited.
let throwCustomDesigntimeFarkle() = invalidOp "Using a custom implementation of the DesigntimeFarkle interface is not allowed."
