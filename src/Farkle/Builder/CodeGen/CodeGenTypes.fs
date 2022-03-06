// Copyright (c) 2022 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.CodeGen

open Farkle
open Farkle.Builder
open System
#if NET
open System.Diagnostics.CodeAnalysis
#endif

/// Abstracts Farkle's dynamic code generator,
/// in a way that allows it to be trimmed if not used.
type internal IDynamicCodeGenInterface =
    /// <summary>Creates a dynamically generated
    /// <see cref="T:Farkle.ITransformer`1"/> instance.</summary>
    /// <param name="transformerData">The post-processor's transformer data.</param>
    /// <param name="fuserData">The post-processor's fuser data.</param>
    /// <param name="ppGenericParam">The returned instance's result type.</param>
    abstract CreatePostProcessor: transformerData: TransformerData[] * fuserData: FuserData[] *
#if NET
        // We tell the trimmer not to remove the type and don't care about any of its members.
        [<DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.None)>]
#endif
        ppGenericParam: Type -> IPostProcessor
