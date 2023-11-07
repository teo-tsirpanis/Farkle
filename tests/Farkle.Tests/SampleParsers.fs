// Copyright 2023 Theodore Tsirpanis.
// SPDX-License-Identifier: MIT

// Contains sample parsers used in tests.
// TODO-FARKLE7: Use built grammars when the builder is implemented in Farkle 7.
module Farkle.Tests.SampleParsers

open Farkle
open Farkle.Parser.Semantics
open System
open System.Collections.Generic
open System.Globalization
open System.Text.Json.Nodes

let json =
    let semanticProvider = {new ISemanticProvider<char, JsonNode> with
        member _.Transform(_, symbol, chars) =
            match symbol.Value with
            // Number
            | 8 -> JsonValue.Create(Decimal.Parse(chars,
                NumberStyles.AllowExponent ||| NumberStyles.Float, CultureInfo.InvariantCulture))
            // String
            // We just use the JSON parser to unescape the string.
            | 9 -> JsonNode.Parse(chars.ToString()).AsValue().GetValue<string>()
            // The other terminals are literals.
            | _ -> null
        member _.Fuse(_, production, members) =
            match production.Value with
            // <Array> ::= "[" <Array Optional> "]"
            | 0 -> members[1]
            // <Array Optional> ::= <Array Reversed>
            | 1 -> members[0]
            // <Array Optional> ::=
            | 2 -> JsonArray()
            // <Array Reversed> ::= <Value>
            | 3 -> JsonArray(members[0] :?> JsonNode)
            // <Array Reversed> ::= <Array Reversed> "," <Value>
            | 4 ->
                let array = members[0] :?> JsonArray
                array.Add(members[2])
                array
            // <Object> ::= "{" <Object Optional> "}"
            | 5 -> members[1]
            // <Object Element> ::= <Object Element> "," String ":" <Value>
            | 6 ->
                let obj = members[0] :?> JsonObject
                obj.Add(members[2] :?> string, members[4] :?> JsonNode)
                obj
            // <Object Element> ::= String ":" <Value>
            | 7 ->
                (members.[0] :?> string, members.[2] :?> JsonNode)
                |> KeyValuePair.Create
                |> Seq.singleton
                |> JsonObject
                :> obj
            // <Object Optional> ::= <Object Element>
            | 8 -> members[0]
            // <Object Optional> ::=
            | 9 -> JsonObject()
            // <Value> ::= false
            | 10 -> JsonValue.op_Implicit false
            // <Value> ::= true
            | 11 -> JsonValue.op_Implicit true
            // <Value> ::= <Array> | <Object> | Number
            | 12 | 13 | 14 -> members[0]
            // <Value> ::= null
            | 15 -> null
            // <Value> ::= String
            | 16 -> JsonValue.Create(members[0])
            | _ -> null
    }
    loadGrammar "JSON.grammar.dat"
    |> CharParser.create semanticProvider

let simpleMaths =
    let semanticProvider = {new ISemanticProvider<char, int> with
        member _.Transform(_, symbol, chars) =
            if symbol.Value = 6 then
                // Number
                Int32.Parse(chars)
            else
                // The other terminals are literals.
                null
        member _.Fuse(_, production, members) =
            match production.Value with
            // <Add Exp> ::= <Add Exp> "+" <Mult Exp>
            | 0 -> unbox members[0] + unbox members[2] |> box
            // <Add Exp> ::= <Add Exp> "-" <Mult Exp>
            | 1 -> unbox members[0] - unbox members[2] |> box
            // <Add Exp> ::= <Mult Exp>
            | 2 -> members[0]
            // <Expression> ::= <Add Exp>
            | 3 -> members[0]
            // <Mult Exp> ::= <Mult Exp> "*" <Negate Exp>
            | 4 -> unbox members[0] * unbox members[2] |> box
            // <Mult Exp> ::= <Mult Exp> "/" <Negate Exp>
            | 5 -> unbox members[0] / unbox members[2] |> box
            // <Mult Exp> ::= <Negate Exp>
            | 6 -> members[0]
            // <Negate Exp> ::= "-" <Value>
            | 7 -> -(unbox members[1]) |> box
            // <Negate Exp> ::= <Value>
            | 8 -> members[0]
            // <Value> ::= Number
            | 9 -> members[0]
            // <Value> ::= "(" <Expression> ")"
            | 10 -> members[1]
            | _ -> null
    }
    loadGrammar "SimpleMaths.egt"
    |> CharParser.create semanticProvider
