// Copyright (c) 2022 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.CodeGen.DynamicCodeGenInterface

/// The default implementation of IDynamicCodeGenInterface.
let instance = {new IDynamicCodeGenInterface with
    member _.CreatePostProcessor(transformers, fusers, ppGenericParam) =
        DynamicPostProcessor.create transformers fusers ppGenericParam}
