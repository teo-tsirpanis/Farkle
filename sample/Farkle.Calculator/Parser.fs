// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Calculator.Parser

open Farkle
open Farkle.Grammar
open Farkle.PostProcessor
open System

type Symbol =    
| SYMBOL_EOF        =  0 // (EOF)
| SYMBOL_ERROR      =  1 // (Error)
| SYMBOL_WHITESPACE =  2 // Whitespace
| SYMBOL_MINUS      =  3 // '-'
| SYMBOL_LPAREN     =  4 // '('
| SYMBOL_RPAREN     =  5 // ')'
| SYMBOL_TIMES      =  6 // '*'
| SYMBOL_DIV        =  7 // '/'
| SYMBOL_PLUS       =  8 // '+'
| SYMBOL_IDENTIFIER =  9 // Identifier
| SYMBOL_ADDEXP     = 10 // <Add Exp>
| SYMBOL_EXPRESSION = 11 // <Expression>
| SYMBOL_MULTEXP    = 12 // <Mult Exp>
| SYMBOL_NEGATEEXP  = 13 // <Negate Exp>
| SYMBOL_PROGRAM    = 14 // <Program>
| SYMBOL_VALUE      = 15 // <Value>

type Rule =
| RULE_PROGRAM             =  0 // <Program> ::= <Expression>
| RULE_EXPRESSION          =  1 // <Expression> ::= <Add Exp>
| RULE_ADDEXP_PLUS         =  2 // <Add Exp> ::= <Add Exp> '+' <Mult Exp>
| RULE_ADDEXP_MINUS        =  3 // <Add Exp> ::= <Add Exp> '-' <Mult Exp>
| RULE_ADDEXP              =  4 // <Add Exp> ::= <Mult Exp>
| RULE_MULTEXP_TIMES       =  5 // <Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
| RULE_MULTEXP_DIV         =  6 // <Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
| RULE_MULTEXP             =  7 // <Mult Exp> ::= <Negate Exp>
| RULE_NEGATEEXP_MINUS     =  8 // <Negate Exp> ::= '-' <Value>
| RULE_NEGATEEXP           =  9 // <Negate Exp> ::= <Value>
| RULE_VALUE_IDENTIFIER    = 10 // <Value> ::= Identifier
| RULE_VALUE_LPAREN_RPAREN = 11 // <Value> ::= '(' <Expression> ')'

let TheRuntimeFarkle =
    let transformers =
        [
            Symbol.SYMBOL_IDENTIFIER, Transformer.create Convert.ToInt32
        ]

    let fusers =
        [
            Rule.RULE_PROGRAM, Fuser.create1 id
            Rule.RULE_EXPRESSION, Fuser.create1 id
            Rule.RULE_ADDEXP_PLUS, Fuser.take2Of (0, 2) 3 (+)
            Rule.RULE_ADDEXP_MINUS, Fuser.take2Of (0, 2) 3 (-)
            Rule.RULE_ADDEXP, Fuser.create1 id
            Rule.RULE_MULTEXP_TIMES, Fuser.take2Of (0, 2) 3 (*)
            Rule.RULE_MULTEXP_DIV, Fuser.take2Of (0, 2) 3 (/)
            Rule.RULE_MULTEXP, Fuser.create1 id
            Rule.RULE_NEGATEEXP_MINUS, Fuser.take1Of 1 2 (~-)
            Rule.RULE_NEGATEEXP, Fuser.create1 id
            Rule.RULE_VALUE_IDENTIFIER, Fuser.create1 id
            Rule.RULE_VALUE_LPAREN_RPAREN, Fuser.take1Of 1 3 id
        ]
    RuntimeFarkle<int>.CreateFromFile
        "mygrammar.egt"
        (function | Terminal (x, _) -> x |> int |> enum<Symbol> | _ -> enum -1)
        (Indexable.index >> int32 >> enum<Rule>)
        (PostProcessor.ofSeq transformers fusers)