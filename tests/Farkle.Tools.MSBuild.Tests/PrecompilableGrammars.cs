// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using static Chiron;
using Farkle.Builder;
using Farkle.Samples.FSharp;
using System.Collections.Generic;
using PCDF = Farkle.Builder.PrecompilableDesigntimeFarkle<int>;

namespace Farkle.Tools.MSBuild.Tests
{
    public class PrecompilableGrammars
    {
        private static PCDF CreatePCDF(string name) => Terminals.Int32(name).MarkForPrecompile();

        // The following designtime Farkles must be discovered.
        public static readonly PrecompilableDesigntimeFarkle<Json> PublicJSON =
            JSON.designtime.Rename("JSON").MarkForPrecompile();
        internal static readonly PrecompilableDesigntimeFarkle<Regex> InternalRegex =
            RegexGrammar.Designtime.MarkForPrecompile();
        private static readonly PCDF Private = CreatePCDF(nameof(Private));

        static class NestedClass
        {
            public static readonly PCDF Nested = CreatePCDF(nameof(Nested));
        }

        public static readonly PCDF MarkedAgain =
            Private.InnerDesigntimeFarkle.Rename(nameof(MarkedAgain)).MarkForPrecompile();

        public static readonly PrecompilableDesigntimeFarkle Untyped =
            Terminal.Literal(nameof(Untyped)).MarkForPrecompile();

        // And the following must not.
        public static readonly PCDF SameReference = Private;
        public static PCDF Mutable = CreatePCDF(nameof(Mutable));
        public readonly PCDF InstanceField = CreatePCDF(nameof(InstanceField));
        public PCDF InstanceProperty => CreatePCDF(nameof(InstanceProperty));
        public static PCDF StaticProperty => CreatePCDF(nameof(StaticProperty));

        public static HashSet<PrecompilableDesigntimeFarkle> All => new ()
        {
            PublicJSON,
            InternalRegex,
            Private,
            NestedClass.Nested,
            MarkedAgain,
            Untyped
        };
    }
}
