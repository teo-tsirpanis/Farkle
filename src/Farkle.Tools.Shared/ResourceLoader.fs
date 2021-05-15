// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.ResourceLoader

open System
open System.Collections.Concurrent
open System.IO
open System.Reflection

let private resourceCache = ConcurrentDictionary<_,string>(StringComparer.Ordinal)

let private doLoadResource = Func<_,_>(fun name ->
    let assembly = Assembly.GetExecutingAssembly()
    match assembly.GetManifestResourceStream(name) with
    | null -> raise (FileNotFoundException("Cannot find embedded resource.", name))
    | stream ->
        use sr = new StreamReader(stream)
        sr.ReadToEnd()
    )

/// Loads an embedded resource of the executing assembly with the given name.
let load name = resourceCache.GetOrAdd(name, doLoadResource)
