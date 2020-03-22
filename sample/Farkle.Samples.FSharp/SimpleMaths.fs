// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// Having a midule named SimpleMaths effectively
// hides the SimpleMaths.Definitions namespace.
module SimpleMaths.SimpleMaths

open Farkle
open Farkle.Builder

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

    let expression, addExp, multExp, negateExp, value =
        nonterminal "Expression", nonterminal "Add Exp", nonterminal "Mult Exp", nonterminal "Negate Exp", nonterminal "Value"

    expression.SetProductions(!@ addExp => id)

    addExp.SetProductions(
        !@ addExp .>> "+" .>>. multExp => fAdd,
        !@ addExp .>> "-" .>>. multExp => fSub,
        !@ multExp => id
    )

    multExp.SetProductions(
        !@ multExp .>> "*" .>>. negateExp => fMul,
        !@ multExp .>> "/" .>>. negateExp => fDiv,
        !@ negateExp => id
    )

    negateExp.SetProductions(
        !& "-" .>>. value => fNeg,
        !@ value => id
    )

    value.SetProductions(
        !@ number => id,
        !& "(" .>>. expression .>>  ")" => id
    )
    expression
    |> DesigntimeFarkle.addLineComment "//"
    |> DesigntimeFarkle.addBlockComment "/*" "*/"

let int =
    makeDesigntime id (+) (-) (*) (/) (~-)
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
