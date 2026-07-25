// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.CodeDom.Compiler;

namespace Farkle.Analyzers;

internal static class IndentedTextWriterExtensions
{
    extension(IndentedTextWriter writer)
    {
        public BlockScope EnterBlock() => new(writer, "{", "}");

        public BlockScope EnterIndent() => new(writer, "", "");
    }

    internal readonly struct BlockScope : IDisposable
    {
        private readonly IndentedTextWriter _writer;
        private readonly string _indentEnd;

        public BlockScope(IndentedTextWriter writer, string indentStart, string indentEnd)
        {
            _writer = writer;
            _indentEnd = indentEnd;
            _writer.WriteLine(indentStart);
            _writer.Indent++;
        }

        public void Dispose()
        {
            _writer.Indent--;
            _writer.WriteLine(_indentEnd);
        }
    }
}
