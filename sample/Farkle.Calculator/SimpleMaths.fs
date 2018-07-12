// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module ``SimpleMaths``

open Farkle
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
/// Identifier
| Identifier =  9
/// <Add Exp>
| AddExp     = 10
/// <Expression>
| Expression = 11
/// <Mult Exp>
| MultExp    = 12
/// <Negate Exp>
| NegateExp  = 13
/// <Program>
| Program    = 14
/// <Value>
| Value      = 15

type Production =
/// <Program> ::= <Expression>
| Program           =  0
/// <Expression> ::= <Add Exp>
| Expression        =  1
/// <Add Exp> ::= <Add Exp> '+' <Mult Exp>
| AddExpPlus        =  2
/// <Add Exp> ::= <Add Exp> '-' <Mult Exp>
| AddExpMinus       =  3
/// <Add Exp> ::= <Mult Exp>
| AddExp            =  4
/// <Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
| MultExpTimes      =  5
/// <Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
| MultExpDiv        =  6
/// <Mult Exp> ::= <Negate Exp>
| MultExp           =  7
/// <Negate Exp> ::= '-' <Value>
| NegateExpMinus    =  8
/// <Negate Exp> ::= <Value>
| NegateExp         =  9
/// <Value> ::= Identifier
| ValueIdentifier   = 10
/// <Value> ::= '(' <Expression> ')'
| ValueLParenRParen = 11

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
            Symbol.Identifier, Transformer.create Convert.ToInt32
        ]
    // The fusers merge the parts of a production into one object of your desire.
    // Do not delete anything here, or the post-processor will fail.
    let fusers =
        [
            Production.Program          , identity
            Production.Expression       , identity
            Production.AddExpPlus       , take2Of (0, 2) 3 (+)
            Production.AddExpMinus      , take2Of (0, 2) 3 (-)
            Production.AddExp           , identity
            Production.MultExpTimes     , take2Of (0, 2) 3 (*)
            Production.MultExpDiv       , take2Of (0, 2) 3 (/)
            Production.MultExp          , identity
            Production.NegateExpMinus   , take1Of 1 2 (~-)
            Production.NegateExp        , identity
            Production.ValueIdentifier  , identity
            Production.ValueLParenRParen, take1Of 1 3 id
        ]
    RuntimeFarkle<int>.CreateFromFile
        "SimpleMaths.egt"
        (function | Terminal (x, _) -> x |> int |> enum<Symbol> | _ -> enum -1)
        (Indexable.index >> int >> enum<Production>)
        (PostProcessor.ofSeq transformers fusers)
