using System;
using System.Collections.Generic;
using Farkle.Builder;
using PCDF = Farkle.Builder.PrecompilableDesigntimeFarkle<int>;

namespace Farkle.Tools.MSBuild.Tests
{
    class Program
    {
        static PCDF CreatePCDF(string name) => Terminals.Int32(name).MarkForPrecompile();

        // The following designtime Farkles must be discovered.
        public static readonly PCDF Public = CreatePCDF(nameof(Public));
        internal static readonly PCDF Internal = CreatePCDF(nameof(Internal));
        private static readonly PCDF Private = CreatePCDF(nameof(Private));

        static class NestedClass
        {
            public static readonly PCDF Nested = CreatePCDF(nameof(Nested));
        }

        public static readonly PCDF MarkedAgain =
            Public.Rename(nameof(MarkedAgain)).MarkForPrecompile();

        public static readonly PrecompilableDesigntimeFarkle Untyped =
            Terminal.Literal(nameof(Untyped)).MarkForPrecompile();

        // And the following must not.
        public static readonly PCDF SameReference = Public;
        public static PCDF Mutable = CreatePCDF(nameof(Mutable));
        public readonly PCDF InstanceField = CreatePCDF(nameof(InstanceField));
        public PCDF InstanceProperty => CreatePCDF(nameof(InstanceProperty));
        public static PCDF StaticProperty => CreatePCDF(nameof(StaticProperty));
        public static readonly DesigntimeFarkle<int> Unmarked = CreatePCDF(nameof(Unmarked));

        public static readonly DesigntimeFarkle UnmarkedUntyped =
            Terminal.Literal(nameof(UnmarkedUntyped)).MarkForPrecompile();

        private static bool _gotError;

        static void Log(string text, ConsoleColor color)
        {
            var existingColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
            }
            finally
            {
                Console.ForegroundColor = existingColor;
            }
        }

        private static void Fail(string message)
        {
            Log(message, ConsoleColor.Red);
            _gotError = true;
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) Fail(message);
        }

        static void AssertThrows<TException>(Action f, string message) where TException : Exception
        {
            var hasThrown = false;
            try
            {
                f();
            }
            catch (TException)
            {
                hasThrown = true;
            }

            Assert(hasThrown, message);
        }

        static void TestDiscoverer()
        {
            var expected = new HashSet<PrecompilableDesigntimeFarkle>()
            {
                Public,
                Internal,
                Private,
                NestedClass.Nested,
                MarkedAgain,
                Untyped
            };
            var precompiledGrammarCount = PrecompiledGrammar.GetAllFromAssembly(typeof(Program).Assembly).Count;

            // The one we subtracted is the FaultyPrecompiled grammar which we embedded as a resource ourselves.
            Assert(expected.Count == precompiledGrammarCount,
                $"The precompiled grammars of this assembly are {precompiledGrammarCount} instead of {expected.Count}");
            foreach (var x in expected)
            {
                var grammar = x.TryGetPrecompiledGrammar();
                if (grammar != null)
                {
                    var grammarName = grammar.Value.GrammarName;
                    if (!grammarName.Equals(x.Name, StringComparison.Ordinal))
                    {
                        _gotError = true;
                        Fail("Name mismatch between precompiled grammar and designtime Farkle names.");
                        Fail($"The grammar is named {grammarName}.");
                        Fail($"The designtime Farkle is named {x.Name}");
                    }
                }
                else
                    Fail($"{x.Name} was not discovered.");
            }
        }

        static int Main()
        {
            Console.WriteLine("Testing the precompiled grammar discoverer...");
            TestDiscoverer();

            return _gotError ? 1 : 0;
        }
    }
}
