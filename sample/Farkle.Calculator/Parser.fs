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
            Transformer.create Symbol.SYMBOL_IDENTIFIER (Convert.ToInt32)
        ]

    let fusers =
        [
            Fuser.create1 Rule.RULE_PROGRAM id
            Fuser.create1 Rule.RULE_EXPRESSION id
            Fuser.take2Of Rule.RULE_ADDEXP_PLUS (0, 2) 3 (+)
            Fuser.take2Of Rule.RULE_ADDEXP_MINUS (0, 2) 3 (-)
            Fuser.create1 Rule.RULE_ADDEXP id
            Fuser.take2Of Rule.RULE_MULTEXP_TIMES (0, 2) 3 (*)
            Fuser.take2Of Rule.RULE_MULTEXP_DIV (0, 2) 3 (/)
            Fuser.create1 Rule.RULE_MULTEXP id
            Fuser.take1Of Rule.RULE_NEGATEEXP_MINUS 1 2 (~-)
            Fuser.create1 Rule.RULE_NEGATEEXP id
            Fuser.create1 Rule.RULE_VALUE_IDENTIFIER id
            Fuser.take1Of Rule.RULE_VALUE_LPAREN_RPAREN 1 3 id
        ]
    RuntimeFarkle<int>.CreateFromFile
        "mygrammar.egt"
        (function | Terminal (x, _) -> x |> int |> enum<Symbol> | _ -> enum -1)
        (Indexable.index >> int32 >> enum<Rule>)
        (PostProcessor.create transformers fusers)