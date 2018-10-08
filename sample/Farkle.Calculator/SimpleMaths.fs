// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module ``SimpleMaths``

open Farkle
open Farkle.PostProcessor
open System

//#region Grammar types

type Symbol =
/// (EOF)
| EOF        =  0u
/// (Error)
| Error      =  1u
/// Whitespace
| Whitespace =  2u
/// '-'
| Minus      =  3u
/// '('
| LParen     =  4u
/// ')'
| RParen     =  5u
/// '*'
| Times      =  6u
/// '/'
| Div        =  7u
/// '+'
| Plus       =  8u
/// Number
| Number     =  9u
/// <Add Exp>
| AddExp     = 10u
/// <Expression>
| Expression = 11u
/// <Mult Exp>
| MultExp    = 12u
/// <Negate Exp>
| NegateExp  = 13u
/// <Value>
| Value      = 14u

type Production =
/// <Expression> ::= <Add Exp>
| Expression        =  0u
/// <Add Exp> ::= <Add Exp> '+' <Mult Exp>
| AddExpPlus        =  1u
/// <Add Exp> ::= <Add Exp> '-' <Mult Exp>
| AddExpMinus       =  2u
/// <Add Exp> ::= <Mult Exp>
| AddExp            =  3u
/// <Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
| MultExpTimes      =  4u
/// <Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
| MultExpDiv        =  5u
/// <Mult Exp> ::= <Negate Exp>
| MultExp           =  6u
/// <Negate Exp> ::= '-' <Value>
| NegateExpMinus    =  7u
/// <Negate Exp> ::= <Value>
| NegateExp         =  8u
/// <Value> ::= Number
| ValueNumber       =  9u
/// <Value> ::= '(' <Expression> ')'
| ValueLParenRParen = 10u

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
            Transformer.create Symbol.Number Convert.ToInt32
        ]
    // The fusers merge the parts of a production into one object of your desire.
    // Do not delete anything here, or the post-processor will fail.
    let fusers =
        [
            identity Production.Expression
            take2Of Production.AddExpPlus (0, 2) 3 (+)
            take2Of Production.AddExpMinus (0, 2) 3 (-)
            identity Production.AddExp
            take2Of Production.MultExpTimes (0, 2) 3 (*)
            take2Of Production.MultExpDiv (0, 2) 3 (/)
            identity Production.MultExp
            take1Of Production.NegateExpMinus 1 2 (~-)
            identity Production.NegateExp
            identity Production.ValueNumber
            take1Of Production.ValueLParenRParen 1 3 id
        ]
    RuntimeFarkle.ofEGTFile
        (PostProcessor.ofSeq<int> transformers fusers)
        "SimpleMaths.egt"
