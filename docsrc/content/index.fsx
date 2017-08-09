(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Farkle.Engine"

(**
What is Farkle?
======================

Farkle is a parser generator for F# and other .NET languages. It is made (or _will_ be made) of the following components:

* __Farkle.Engine__ (first version complete): An engine for the [GOLD Parsing system][gold].
* __TBA__ (under development, _only_ for F#): A code generator that creates type-safe Abstract Syntax Trees.
* __TBA__ (I'm not sure about it): A replacement of GOLD Parser Builder.
* __TBA__: MsBuild Integration.
* __TBA__: A Farkle type provider.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Farkle library can be <a href="https://nuget.org/packages/Farkle">installed from NuGet</a>:
      <pre>PM> Install-Package Farkle</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

This example demonstrates using a function defined in this sample library.

*)
#r "Farkle.Engine.dll"
#r "Chessie.dll"
open Farkle.Parser
open Chessie.ErrorHandling

let (result, log) =
  GOLDParser.Parse("sample.egt", "(111 * 555) + 617", false)
  |> GOLDParser.FormatErrors

printfn "##########LOG##########"
log |> Array.iter (printfn "%s")
match result with
| Choice1Of2 x ->
  printfn "##########SUCCESS##########"
  x |> Reduction.drawReductionTree |> printfn "%s"
| Choice2Of2 x ->
  printfn "##########ERROR##########"
  printfn "%s" x

(**
Samples & documentation
-----------------------

The library comes with comprehensible documentation. 
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content]. 
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library (to be written).

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under the MIT license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/teo-tsirpanis/Farkle/tree/master/docs/content
  [gh]: https://github.com/teo-tsirpanis/Farkle
  [issues]: https://github.com/teo-tsirpanis/Farkle/issues
  [readme]: https://github.com/teo-tsirpanis/Farkle/blob/master/README.md
  [license]: https://github.com/teo-tsirpanis/Farkle/blob/master/LICENSE.txt
  [gold]: http://www.goldparser.org/
*)
