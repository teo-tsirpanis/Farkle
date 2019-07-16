// This file was created by Farkle.Tools and is a skeleton
// to help you write a post-processor for JSON.
// You should complete it yourself, and keep it to source control.

module ``JSON``.Language

open Farkle
open Farkle.PostProcessor
open ``JSON``.Definitions

// The transformers convert terminals to anything you want.
// If you do not care about a terminal (like single characters),
// you can remove it from below. It will be automatically ignored.
let private transformers =
    [
        Transformer.create Terminal.Comma <| C (fun x -> x)
        Transformer.create Terminal.Colon <| C (fun x -> x)
        Transformer.create Terminal.LBracket <| C (fun x -> x)
        Transformer.create Terminal.RBracket <| C (fun x -> x)
        Transformer.create Terminal.LBrace <| C (fun x -> x)
        Transformer.create Terminal.RBrace <| C (fun x -> x)
        Transformer.create Terminal.False <| C (fun x -> x)
        Transformer.create Terminal.Null <| C (fun x -> x)
        Transformer.create Terminal.Number <| C (fun x -> x)
        Transformer.create Terminal.String <| C (fun x -> x)
        Transformer.create Terminal.True <| C (fun x -> x)
    ]

open Fuser

// The fusers merge the parts of a production into one object of your desire.
// Do not delete anything here, or the post-processor will fail.
let private fusers =
    [
        FUSER_FUNCTION_HERE Production.ValueString
        FUSER_FUNCTION_HERE Production.ValueNumber
        FUSER_FUNCTION_HERE Production.Value
        FUSER_FUNCTION_HERE Production.Value
        FUSER_FUNCTION_HERE Production.ValueTrue
        FUSER_FUNCTION_HERE Production.ValueFalse
        FUSER_FUNCTION_HERE Production.ValueNull
        FUSER_FUNCTION_HERE Production.ArrayLBracketRBracket
        FUSER_FUNCTION_HERE Production.ArrayElementComma
        FUSER_FUNCTION_HERE Production.ArrayElement
        FUSER_FUNCTION_HERE Production.ObjectLBraceRBrace
        FUSER_FUNCTION_HERE Production.ObjectElementStringColonComma
        FUSER_FUNCTION_HERE Production.ObjectElement
    ]

let private createRuntimeFarkle() =
    RuntimeFarkle.ofBase64String
        (PostProcessor.ofSeq<TODO> transformers fusers)
        Grammar.asBase64

let runtime = createRuntimeFarkle()
