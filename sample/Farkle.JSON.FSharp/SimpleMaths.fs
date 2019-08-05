// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SimpleMaths

open Farkle
open Farkle.PostProcessor
open SimpleMaths.Definitions

open Fuser

/// A very simple grammar for parsing mathematical expressions.
let int =
    // The transformers convert terminals to anything you want.
    // If you don't care about a terminal (like single characters),
    // you can remove it from below. It will be automatically ignored.
    // And symbols other than terminals are automatically ignored,
    // even if they are listed below.
    let transformers =
        [
            Transformer.int Terminal.Number
        ]
    // The fusers merge the parts of a production into one object of your desire.
    // Do not delete anything here, or the post-processor will fail.
    let fusers =
        [
            identity Production.Expression
            take2Of Production.AddExpPlus (0, 2) (+)
            take2Of Production.AddExpMinus (0, 2) (-)
            identity Production.AddExp
            take2Of Production.MultExpTimes (0, 2) (*)
            take2Of Production.MultExpDiv (0, 2) (/)
            identity Production.MultExp
            take1Of Production.NegateExpMinus 1 (~-)
            identity Production.NegateExp
            identity Production.ValueNumber
            take1Of Production.ValueLParenRParen 1 id
        ]
    RuntimeFarkle.ofBase64String (PostProcessor.ofSeq<int> transformers fusers) Grammar.asBase64

type MathExpression =
    | Number of int
    | Add of MathExpression * MathExpression
    | Subtract of MathExpression * MathExpression
    | Multiply of MathExpression * MathExpression
    | Divide of MathExpression * MathExpression
    | Negate of MathExpression

let mathExpression =
    let transformers = [Transformer.int Terminal.Number]
    let fusers =
        [
            identity Production.Expression
            take2Of Production.AddExpPlus (0, 2) (fun x1 x2 -> Add(x1, x2))
            take2Of Production.AddExpMinus (0, 2) (fun x1 x2 -> Subtract(x1, x2))
            identity Production.AddExp
            take2Of Production.MultExpTimes (0, 2) (fun x1 x2 -> Multiply(x1, x2))
            take2Of Production.MultExpDiv (0, 2) (fun x1 x2 -> Divide(x1, x2))
            identity Production.MultExp
            take1Of Production.NegateExpMinus 1 Negate
            identity Production.NegateExp
            take1Of Production.ValueNumber 0 Number
            take1Of Production.ValueLParenRParen 1 id
        ]
    RuntimeFarkle.changePostProcessor (PostProcessor.ofSeq<MathExpression> transformers fusers) int
    
let rec evalExpression =
    function
    | Number x -> x
    | Add(x1, x2) -> evalExpression x1 + evalExpression x2
    | Subtract(x1, x2) -> evalExpression x1 - evalExpression x2
    | Multiply(x1, x2) -> evalExpression x1 * evalExpression x2
    | Divide(x1, x2) -> evalExpression x1 / evalExpression x2
    | Negate x -> - evalExpression x

let rec renderExpression =
    function
    | Number x -> string x
    | Add(x1, x2) -> sprintf "(%s)+(%s)" (renderExpression x1) (renderExpression x2)
    | Subtract(x1, x2) -> sprintf "(%s)-(%s)" (renderExpression x1) (renderExpression x2)
    | Multiply(x1, x2) -> sprintf "(%s)*(%s)" (renderExpression x1) (renderExpression x2)
    | Divide(x1, x2) -> sprintf "(%s)/(%s)" (renderExpression x1) (renderExpression x2)
    | Negate x -> sprintf "-(%s)" (renderExpression x)
