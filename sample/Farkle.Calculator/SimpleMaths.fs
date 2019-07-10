// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module ``SimpleMaths``

open Farkle
open Farkle.PostProcessor

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

//#region Grammar as Base64
let private asBase64 = """
RwBPAEwARAAgAFAAYQByAHMAZQByACAAVABhAGIAbABlAHMALwB2ADUALgAwAAAATQQAYnBJAABT
TgBhAG0AZQAAAFNTAGkAbQBwAGwAZQBNAGEAdABoAHMAAABNBABicEkBAFNWAGUAcgBzAGkAbwBu
AAAAUzEALgAwAC4AMAAAAE0EAGJwSQIAU0EAdQB0AGgAbwByAAAAU1QAaABlAG8AZABvAHIAZQAg
AFQAcwBpAHIAcABhAG4AaQBzAAAATQQAYnBJAwBTQQBiAG8AdQB0AAAAU0EAIAB2AGUAcgB5ACAA
cwBpAG0AcABsAGUAIABnAHIAYQBtAG0AYQByACAAZgBvAHIAIABwAGEAcgBzAGkAbgBnACAAbQBh
AHQAaABlAG0AYQB0AGkAYwBhAGwAIABlAHgAcAByAGUAcwBzAGkAbwBuAHMALgAAAE0EAGJwSQQA
U0MAaABhAHIAYQBjAHQAZQByACAAUwBlAHQAAABTVQBuAGkAYwBvAGQAZQAAAE0EAGJwSQUAU0MA
aABhAHIAYQBjAHQAZQByACAATQBhAHAAcABpAG4AZwAAAFNXAGkAbgBkAG8AdwBzAC0AMQAyADUA
MgAAAE0EAGJwSQYAU0cAZQBuAGUAcgBhAHQAZQBkACAAQgB5AAAAU0cATwBMAEQAIABQAGEAcgBz
AGUAcgAgAEIAdQBpAGwAZABlAHIAIAA1AC4AMgAuADAALgAAAE0EAGJwSQcAU0cAZQBuAGUAcgBh
AHQAZQBkACAARABhAHQAZQAAAFMyADAAMQA4AC0AMAA4AC0AMgA1ACAAMQAzADoAMwA2AAAATQcA
YnRJDwBJCABJCwBJCQBJFABJAABNAwBiSUkAAEkAAE0dAGJjSQAASQAASQwARUkJAEkNAEkgAEkg
AEmFAEmFAEmgAEmgAEmAFkmAFkkOGEkOGEkAIEkKIEkmIEkmIEkoIEkpIEkvIEkvIElfIElfIEkA
MEkAME0HAGJjSQEASQAASQEARUktAEktAE0HAGJjSQIASQAASQEARUkoAEkoAE0HAGJjSQMASQAA
SQEARUkpAEkpAE0HAGJjSQQASQAASQEARUkqAEkqAE0HAGJjSQUASQAASQEARUkvAEkvAE0HAGJj
SQYASQAASQEARUkrAEkrAE0HAGJjSQcASQAASQEARUkwAEk5AE0EAGJTSQAAU0UATwBGAAAASQMA
TQQAYlNJAQBTRQByAHIAbwByAAAASQcATQQAYlNJAgBTVwBoAGkAdABlAHMAcABhAGMAZQAAAEkC
AE0EAGJTSQMAUy0AAABJAQBNBABiU0kEAFMoAAAASQEATQQAYlNJBQBTKQAAAEkBAE0EAGJTSQYA
UyoAAABJAQBNBABiU0kHAFMvAAAASQEATQQAYlNJCABTKwAAAEkBAE0EAGJTSQkAU04AdQBtAGIA
ZQByAAAASQEATQQAYlNJCgBTQQBkAGQAIABFAHgAcAAAAEkAAE0EAGJTSQsAU0UAeABwAHIAZQBz
AHMAaQBvAG4AAABJAABNBABiU0kMAFNNAHUAbAB0ACAARQB4AHAAAABJAABNBABiU0kNAFNOAGUA
ZwBhAHQAZQAgAEUAeABwAAAASQAATQQAYlNJDgBTVgBhAGwAdQBlAAAASQAATQUAYlJJAABJCwBF
SQoATQcAYlJJAQBJCgBFSQoASQgASQwATQcAYlJJAgBJCgBFSQoASQMASQwATQUAYlJJAwBJCgBF
SQwATQcAYlJJBABJDABFSQwASQYASQ0ATQcAYlJJBQBJDABFSQwASQcASQ0ATQUAYlJJBgBJDABF
SQ0ATQYAYlJJBwBJDQBFSQMASQ4ATQUAYlJJCABJDQBFSQ4ATQUAYlJJCQBJDgBFSQkATQcAYlJJ
CgBJDgBFSQQASQsASQUATR0AYkRJAABCAEkAAEVJAABJAQBFSQEASQIARUkCAEkDAEVJAwBJBABF
SQQASQUARUkFAEkGAEVJBgBJBwBFSQcASQgARU0IAGJESQEAQgFJAgBFSQAASQEARU0FAGJESQIA
QgFJAwBFTQUAYkRJAwBCAUkEAEVNBQBiREkEAEIBSQUARU0FAGJESQUAQgFJBgBFTQUAYkRJBgBC
AUkHAEVNBQBiREkHAEIBSQgARU0IAGJESQgAQgFJCQBFSQcASQgARU0jAGJMSQAARUkDAEkBAEkB
AEVJBABJAQBJAgBFSQkASQEASQMARUkKAEkDAEkEAEVJCwBJAwBJBQBFSQwASQMASQYARUkNAEkD
AEkHAEVJDgBJAwBJCABFTQ8AYkxJAQBFSQQASQEASQIARUkJAEkBAEkDAEVJDgBJAwBJCQBFTSMA
YkxJAgBFSQMASQEASQEARUkEAEkBAEkCAEVJCQBJAQBJAwBFSQoASQMASQQARUkLAEkDAEkKAEVJ
DABJAwBJBgBFSQ0ASQMASQcARUkOAEkDAEkIAEVNGwBiTEkDAEVJAABJAgBJCQBFSQMASQIASQkA
RUkFAEkCAEkJAEVJBgBJAgBJCQBFSQcASQIASQkARUkIAEkCAEkJAEVNEwBiTEkEAEVJAwBJAQBJ
CwBFSQgASQEASQwARUkAAEkCAEkAAEVJBQBJAgBJAABFTQcAYkxJBQBFSQAASQQASQAARU0bAGJM
SQYARUkGAEkBAEkNAEVJBwBJAQBJDgBFSQAASQIASQMARUkDAEkCAEkDAEVJBQBJAgBJAwBFSQgA
SQIASQMARU0bAGJMSQcARUkAAEkCAEkGAEVJAwBJAgBJBgBFSQUASQIASQYARUkGAEkCAEkGAEVJ
BwBJAgBJBgBFSQgASQIASQYARU0bAGJMSQgARUkAAEkCAEkIAEVJAwBJAgBJCABFSQUASQIASQgA
RUkGAEkCAEkIAEVJBwBJAgBJCABFSQgASQIASQgARU0bAGJMSQkARUkAAEkCAEkHAEVJAwBJAgBJ
BwBFSQUASQIASQcARUkGAEkCAEkHAEVJBwBJAgBJBwBFSQgASQIASQcARU0HAGJMSQoARUkFAEkB
AEkPAEVNGwBiTEkLAEVJAwBJAQBJAQBFSQQASQEASQIARUkJAEkBAEkDAEVJDABJAwBJEABFSQ0A
SQMASQcARUkOAEkDAEkIAEVNGwBiTEkMAEVJAwBJAQBJAQBFSQQASQEASQIARUkJAEkBAEkDAEVJ
DABJAwBJEQBFSQ0ASQMASQcARUkOAEkDAEkIAEVNFwBiTEkNAEVJAwBJAQBJAQBFSQQASQEASQIA
RUkJAEkBAEkDAEVJDQBJAwBJEgBFSQ4ASQMASQgARU0XAGJMSQ4ARUkDAEkBAEkBAEVJBABJAQBJ
AgBFSQkASQEASQMARUkNAEkDAEkTAEVJDgBJAwBJCABFTRsAYkxJDwBFSQAASQIASQoARUkDAEkC
AEkKAEVJBQBJAgBJCgBFSQYASQIASQoARUkHAEkCAEkKAEVJCABJAgBJCgBFTRsAYkxJEABFSQYA
SQEASQ0ARUkHAEkBAEkOAEVJAABJAgBJAgBFSQMASQIASQIARUkFAEkCAEkCAEVJCABJAgBJAgBF
TRsAYkxJEQBFSQYASQEASQ0ARUkHAEkBAEkOAEVJAABJAgBJAQBFSQMASQIASQEARUkFAEkCAEkB
AEVJCABJAgBJAQBFTRsAYkxJEgBFSQAASQIASQQARUkDAEkCAEkEAEVJBQBJAgBJBABFSQYASQIA
SQQARUkHAEkCAEkEAEVJCABJAgBJBABFTRsAYkxJEwBFSQAASQIASQUARUkDAEkCAEkFAEVJBQBJ
AgBJBQBFSQYASQIASQUARUkHAEkCAEkFAEVJCABJAgBJBQBF"""
//#endregion

type AST =
    | Number of int
    | Add of AST * AST
    | Subtract of AST * AST
    | Multiply of AST * AST
    | Divide of AST * AST
    | Negate of AST

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
            Transformer.int Symbol.Number
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
    RuntimeFarkle.ofBase64String (PostProcessor.ofSeq<int> transformers fusers) asBase64

let ast =
    let transformers = [Transformer.int Symbol.Number]
    let fusers =
        [
            identity Production.Expression
            take2Of Production.AddExpPlus (0, 2) (fun x1 x2 -> Add(x1, x2))
            take2Of Production.AddExpMinus (0, 2) (fun x1 x2 -> Add(x1, x2))
            identity Production.AddExp
            take2Of Production.MultExpTimes (0, 2) (fun x1 x2 -> Add(x1, x2))
            take2Of Production.MultExpDiv (0, 2) (fun x1 x2 -> Add(x1, x2))
            identity Production.MultExp
            take1Of Production.NegateExpMinus 1 Negate
            identity Production.NegateExp
            take1Of Production.ValueNumber 0 Number
            take1Of Production.ValueLParenRParen 1 Number
        ]
    RuntimeFarkle.changePostProcessor (PostProcessor.ofSeq<AST> transformers fusers) int
    
let rec evalAST =
    function
    | Number x -> x
    | Add(x1, x2) -> evalAST x1 + evalAST x2
    | Subtract(x1, x2) -> evalAST x1 - evalAST x2
    | Multiply(x1, x2) -> evalAST x1 * evalAST x2
    | Divide(x1, x2) -> evalAST x1 / evalAST x2
    | Negate x -> - evalAST x

let rec renderAST =
    function
    | Number x -> string x
    | Add(x1, x2) -> sprintf "(%s)+(%s)" (renderAST x1) (renderAST x2)
    | Subtract(x1, x2) -> sprintf "(%s)-(%s)" (renderAST x1) (renderAST x2)
    | Multiply(x1, x2) -> sprintf "(%s)*(%s)" (renderAST x1) (renderAST x2)
    | Divide(x1, x2) -> sprintf "(%s)/(%s)" (renderAST x1) (renderAST x2)
    | Negate x -> sprintf "-(%s)" (renderAST x)
