// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using System;
using Farkle.IO;

namespace Farkle.Samples.CSharp
{
    /// <summary>
    /// A dummy class that tests some edge-
    /// case F#-C# interoperability issues.
    /// It just needs to be compiled without errors.
    /// </summary>
    /// <remarks>See https://github.com/dotnet/fsharp/issues/9997.</remarks>
    public class CSharpInteropTests
    {
        public static void TestCharStreamPosition()
        {
            var cs = new CharStream("");
            ITransformerContext ctx = cs;
            ref readonly var csPos = ref cs.CurrentPosition;
            ref readonly var ctxPos = ref ctx.StartPosition;
            Console.WriteLine(ctxPos.Index == csPos.Index);
        }
    }
}
