// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#pragma warning disable IDE1006 // Naming Styles

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Farkle
{
    /// <summary>
    /// Obsolete, use <see cref="CharParser{T}"/> instead.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use CharParser<T>.", error: true
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.CompatibilityTypesCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
    )]
    public sealed class RuntimeFarkle<T>;

    namespace Builder
    {
        /// <summary>
        /// Obsolete, use <see cref="IGrammarSymbol"/> for individual grammar symbols
        /// or <see cref="IGrammarBuilder"/> for whole grammars instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use IGrammarSymbol for individual grammar symbols or IGrammarBuilder for whole grammars instead.", error: true
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.CompatibilityTypesCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
    )]
        public interface DesigntimeFarkle { }

        /// <summary>
        /// Obsolete, use <see cref="IGrammarSymbol{T}"/> for individual grammar symbols
        /// or <see cref="IGrammarBuilder{T}"/> for whole grammars instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use IGrammarSymbol<T> for individual grammar symbols or IGrammarBuilder<T> for whole grammars instead.", error: true
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.CompatibilityTypesCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
        )]
        public interface DesigntimeFarkle<out T> : DesigntimeFarkle { }

        /// <summary>
        /// Obsolete, use <see cref="IGrammarBuilder"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use IGrammarBuilder instead.", error: true
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.CompatibilityTypesCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
        )]
        public class PrecompilableDesigntimeFarkle
        {
            [ExcludeFromCodeCoverage]
            private protected PrecompilableDesigntimeFarkle() { }
        }

        /// <summary>
        /// Obsolete, use <see cref="IGrammarBuilder{T}"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use IGrammarBuilder<T> instead.", error: true
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.CompatibilityTypesCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
        )]
        public sealed class PrecompilableDesigntimeFarkle<T> : PrecompilableDesigntimeFarkle
        {
            [ExcludeFromCodeCoverage]
            private PrecompilableDesigntimeFarkle() { }
        }
    }
}
