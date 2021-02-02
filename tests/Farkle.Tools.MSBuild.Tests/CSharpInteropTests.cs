// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using System;
using System.Diagnostics.CodeAnalysis;
using Farkle.IO;

namespace Farkle.Tools.MSBuild.Tests
{
    [ExcludeFromCodeCoverage(Justification = "Methods of this class just need to be compiled without errors.")]
    public static class CSharpInteropTests
    {
        /// <summary>See https://github.com/dotnet/fsharp/issues/9997.</summary>
        public static void TestCharStreamPosition(CharStream cs)
        {
            ITransformerContext ctx = cs;
            ref readonly var csPos = ref cs.CurrentPosition;
            ref readonly var ctxPos = ref ctx.StartPosition;
            Console.WriteLine(ctxPos.Index == csPos.Index);
        }
    }
}
