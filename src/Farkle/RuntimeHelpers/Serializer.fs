// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.RuntimeHelpers

open System
open System.AssemblyVersionInformation
open System.IO
open System.Runtime.Serialization.Formatters.Binary

/// A type signifying an error during the deserialization of an object.
type DeserializationError =
    /// An object is being deserialized that was serialized in a different version of Farkle.
    | IncorrectFarkleVersion of string
    /// An exception was encountered during deserialization.
    | DeserializationException of string
    override x.ToString() =
        match x with
        | IncorrectFarkleVersion x ->
            sprintf
                "Tried to deserialize an object coming from a different version of Farkle (%s; this is version %s). Create a template for the current version."
                x
                AssemblyVersion
        | DeserializationException x -> sprintf "An exception was encountered during deserialization: %s" x

/// This module implements a simple API to serialize and deserialize objects, powered by the .NET's binary serializer.
/// The objects are serialized to a Base64 string.
/// This string is not version-tolerant by design (i.e. you can deserialize only strings that were created in the same version of Farkle).
/// It was mainly made to embed `RuntimeGrammars` inside template source files.
/// It is __not__ recommended for use in user code.
module Serialization =

    type internal Container<'a> = {
            FarkleVersion: string
            ThePayload: 'a
        }

    /// Serializes an object to a Base64-encoded string.
    /// This string should be viewed as a black box.
    let serialize x =
        let cont = {
            FarkleVersion = AssemblyVersion
            ThePayload = x
        }
        let f = BinaryFormatter()
        let memStream = new MemoryStream()
        f.Serialize(memStream, cont)
        memStream.Close()
        memStream.ToArray() |> Convert.ToBase64String

    /// Deserializes a string back into an object.
    let deserialize<'a> x =
        try
            let memStream = new MemoryStream(Convert.FromBase64String x)
            let f = BinaryFormatter()
            let cont = f.Deserialize memStream :?> Container<'a>
            if cont.FarkleVersion = AssemblyVersion then
                Ok cont.ThePayload
            else
                cont.FarkleVersion |> IncorrectFarkleVersion |> Error
        with
        | e -> e.Message |> DeserializationException |> Error
