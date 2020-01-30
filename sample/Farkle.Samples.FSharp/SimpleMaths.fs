// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// Having a midule named SimpleMaths effectively
// hides the SimpleMaths.Definitions namespace.
module SimpleMaths.SimpleMaths

open Farkle
open Farkle.Builder
open Farkle.PostProcessor
open SimpleMaths.Definitions

open Fuser

type MathExpression = {
    ValueTunk: Lazy<int>
    Expr: Expr
}
with
    static member Create expr = {Expr = expr; ValueTunk = lazy(MathExpression.Evaluate expr)}
    static member Evaluate =
        function
        | Number x -> x
        | Add (x1, x2) -> x1.Value + x2.Value
        | Subtract (x1, x2) -> x1.Value - x2.Value
        | Multiply (x1, x2) -> x1.Value * x2.Value
        | Divide (x1, x2) -> x1.Value / x2.Value
        | Negate x -> - x.Value
    member x.Value = x.ValueTunk.Value

and Expr =
    | Number of int
    | Add of MathExpression * MathExpression
    | Subtract of MathExpression * MathExpression
    | Multiply of MathExpression * MathExpression
    | Divide of MathExpression * MathExpression
    | Negate of MathExpression

let mathExpression =
    let transformers = [Transformer.createS Terminal.Number System.Int32.Parse]
    let fusers =
        [
            identity Production.Expression
            take2Of Production.AddExpPlus (0, 2) (fun x1 x2 -> MathExpression.Create <| Add(x1, x2))
            take2Of Production.AddExpMinus (0, 2) (fun x1 x2 -> MathExpression.Create <| Subtract(x1, x2))
            identity Production.AddExp
            take2Of Production.MultExpTimes (0, 2) (fun x1 x2 -> MathExpression.Create <| Multiply(x1, x2))
            take2Of Production.MultExpDiv (0, 2) (fun x1 x2 -> MathExpression.Create <| Divide(x1, x2))
            identity Production.MultExp
            take1Of Production.NegateExpMinus 1 (Negate >> MathExpression.Create)
            identity Production.NegateExp
            take1Of Production.ValueNumber 0 (Number >> MathExpression.Create)
            take1Of Production.ValueLParenRParen 1 id
        ]
    RuntimeFarkle.ofBase64String (PostProcessor.ofSeq<MathExpression> transformers fusers) Grammar.asBase64

let rec renderExpression x =
    match x.Expr with
    | Number x -> string x
    | Add(x1, x2) -> sprintf "(%s)+(%s)" (renderExpression x1) (renderExpression x2)
    | Subtract(x1, x2) -> sprintf "(%s)-(%s)" (renderExpression x1) (renderExpression x2)
    | Multiply(x1, x2) -> sprintf "(%s)*(%s)" (renderExpression x1) (renderExpression x2)
    | Divide(x1, x2) -> sprintf "(%s)/(%s)" (renderExpression x1) (renderExpression x2)
    | Negate x -> sprintf "-(%s)" (renderExpression x)

let buildInt() =
    let number =
        Regex.chars PredefinedSets.Number
        |> Regex.atLeast 1
        |> terminal "Number" (T(fun _ data -> System.Int32.Parse(data.ToString())))

    let expression, addExp, multExp, negateExp, value =
        nonterminal "Expression", nonterminal "Add Exp", nonterminal "Mult Exp", nonterminal "Negate Exp", nonterminal "Value"

    expression.SetProductions(!@ addExp => id)

    addExp.SetProductions(
        !@ addExp .>> "+" .>>. multExp => (+),
        !@ addExp .>> "-" .>>. multExp => (-),
        !@ multExp => id
    )

    multExp.SetProductions(
        !@ multExp .>> "*" .>>. negateExp => (*),
        !@ multExp .>> "/" .>>. negateExp => (/),
        !@ negateExp => id
    )

    negateExp.SetProductions(
        !& "-" .>>. value => (~-),
        !@ value => id
    )

    value.SetProductions(
        !@ number => id,
        !& "(" .>>. expression .>>  ")" => id
    )
    expression
    |> DesigntimeFarkle.addLineComment "//"
    |> DesigntimeFarkle.addBlockComment "/*" "*/"
    |> RuntimeFarkle.build

let int = buildInt()
