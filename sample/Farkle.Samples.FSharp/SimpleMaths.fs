// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module SimpleMaths

open Farkle
open Farkle.Builder
open Farkle.Builder.OperatorPrecedence

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

let rec renderExpression x =
    match x.Expr with
    | Number x -> string x
    | Add(x1, x2) -> sprintf "(%s)+(%s)" (renderExpression x1) (renderExpression x2)
    | Subtract(x1, x2) -> sprintf "(%s)-(%s)" (renderExpression x1) (renderExpression x2)
    | Multiply(x1, x2) -> sprintf "(%s)*(%s)" (renderExpression x1) (renderExpression x2)
    | Divide(x1, x2) -> sprintf "(%s)/(%s)" (renderExpression x1) (renderExpression x2)
    | Negate x -> sprintf "-(%s)" (renderExpression x)

let makeDesigntime fNumber fAdd fSub fMul fDiv fNeg =
    let number =
        Regex.chars PredefinedSets.Number
        |> Regex.atLeast 1
        |> terminal "Number" (T(fun _ data -> System.Int32.Parse(data.ToString()) |> fNumber))

    let expression = nonterminal "Expression"

    let negatePrec = obj()

    expression.SetProductions(
        !@ expression .>> "+" .>>. expression => fAdd,
        !@ expression .>> "-" .>>. expression => fSub,
        !@ expression .>> "*" .>>. expression => fMul,
        !@ expression .>> "/" .>>. expression => fDiv,
        !& "-" .>>. expression |> prec negatePrec => fNeg,
        !& "(" .>>. expression .>> ")" |> asIs,
        !@ number |> asIs
    )

    let opScope =
        OperatorScope(
            LeftAssociative("+", "-"),
            LeftAssociative("*", "/"),
            PrecedenceOnly(negatePrec)
        )

    expression
    |> DesigntimeFarkle.addLineComment "//"
    |> DesigntimeFarkle.addBlockComment "/*" "*/"
    |> DesigntimeFarkle.withOperatorScope opScope

let int =
    makeDesigntime id (+) (-) ( * ) (/) (~-)
    |> RuntimeFarkle.build

let mathExpression =
    let inline mkExpr f x1 x2 = f(x1, x2) |> MathExpression.Create
    let pp =
        makeDesigntime
            (Number >> MathExpression.Create)
            (mkExpr Add)
            (mkExpr Subtract)
            (mkExpr Multiply)
            (mkExpr Divide)
            (Negate >> MathExpression.Create)
        |> DesigntimeFarkleBuild.buildPostProcessorOnly
    RuntimeFarkle.changePostProcessor pp int
