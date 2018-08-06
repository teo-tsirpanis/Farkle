// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module ``SimpleMaths``

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.PostProcessor
open System

//#region Grammar types

type Symbol =
/// (EOF)
| EOF        =  0
/// (Error)
| Error      =  1
/// Whitespace
| Whitespace =  2
/// '-'
| Minus      =  3
/// '('
| LParen     =  4
/// ')'
| RParen     =  5
/// '*'
| Times      =  6
/// '/'
| Div        =  7
/// '+'
| Plus       =  8
/// Number
| Number     =  9
/// <Add Exp>
| AddExp     = 10
/// <Expression>
| Expression = 11
/// <Mult Exp>
| MultExp    = 12
/// <Negate Exp>
| NegateExp  = 13
/// <Value>
| Value      = 14

type Production =
/// <Expression> ::= <Add Exp>
| Expression        =  0
/// <Add Exp> ::= <Add Exp> '+' <Mult Exp>
| AddExpPlus        =  1
/// <Add Exp> ::= <Add Exp> '-' <Mult Exp>
| AddExpMinus       =  2
/// <Add Exp> ::= <Mult Exp>
| AddExp            =  3
/// <Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
| MultExpTimes      =  4
/// <Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
| MultExpDiv        =  5
/// <Mult Exp> ::= <Negate Exp>
| MultExp           =  6
/// <Negate Exp> ::= '-' <Value>
| NegateExpMinus    =  7
/// <Negate Exp> ::= <Value>
| NegateExp         =  8
/// <Value> ::= Number
| ValueNumber       =  9
/// <Value> ::= '(' <Expression> ')'
| ValueLParenRParen = 10

//#endregion

open Fuser

/// A very simple grammar for parsing mathematical expressions.
let TheRuntimeFarkle =
    // The transformers convert terminals to anything you want.
    // If you don't care about a terminal (like single characters),
    // you can remove it from below. It will be automatically ignored.
    // And symbols other than terminals are automatically ignored,
    // even if they are listed below.
    let transformers =
        [
            Symbol.Number, Transformer.create Convert.ToInt32
        ]
    // The fusers merge the parts of a production into one object of your desire.
    // Do not delete anything here, or the post-processor will fail.
    let fusers =
        [
            Production.Expression       , identity
            Production.AddExpPlus       , take2Of (0, 2) 3 (+)
            Production.AddExpMinus      , take2Of (0, 2) 3 (-)
            Production.AddExp           , identity
            Production.MultExpTimes     , take2Of (0, 2) 3 (*)
            Production.MultExpDiv       , take2Of (0, 2) 3 (/)
            Production.MultExp          , identity
            Production.NegateExpMinus   , take1Of 1 2 (~-)
            Production.NegateExp        , identity
            Production.ValueNumber      , identity
            Production.ValueLParenRParen, take1Of 1 3 id
        ]
    RuntimeFarkle<int>.CreateFromFile
        "SimpleMaths.egt"
        (function | Terminal (x, _) -> x |> int |> enum<Symbol> | _ -> enum -1)
        (Indexable.index >> int >> enum<Production>)
        (PostProcessor.ofSeq transformers fusers)
