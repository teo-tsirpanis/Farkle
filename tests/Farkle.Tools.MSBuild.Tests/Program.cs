using System;
using System.Collections.Generic;
using Farkle.Builder;
using PCDF = Farkle.Builder.PrecompilableDesigntimeFarkle<int>;

namespace Farkle.PrecompilerTests
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

        static int Main()
        {
            var gotError = false;

            Console.WriteLine("Checking the precompiled grammars...");

            var allPrecompiledGrammars =
                PrecompiledGrammar.GetAllFromAssembly(typeof(Program).Assembly);
            var expected = new HashSet<string>()
            {
                nameof(Public),
                nameof(Internal),
                nameof(Private),
                nameof(NestedClass.Nested),
                nameof(MarkedAgain),
                nameof(Untyped)
            };
            var actual = new HashSet<string>(allPrecompiledGrammars.Keys);

            if (!expected.SetEquals(actual))
            {
                Log("The discoverer found the wrong designtime Farkles.", ConsoleColor.Red);
                Log($"Expected: {string.Join(',', expected)}", ConsoleColor.Red);
                Log($"Actual:   {string.Join(',', actual)}", ConsoleColor.Red);
                gotError = true;
            }

            Console.WriteLine("Checking for grammar read errors...");

            foreach (var x in allPrecompiledGrammars)
            {
                try
                {
                    _ = x.Value.GetGrammar();
                }
                catch (Exception e)
                {
                    Log($"Error while reading {x.Key}", ConsoleColor.Red);
                    Log(e.ToString(), ConsoleColor.Red);
                    gotError = true;
                }
            }

            return gotError ? 1 : 0;
        }
    }
}
