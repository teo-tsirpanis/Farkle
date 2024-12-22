// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Irony.Parsing;

namespace Farkle.Benchmarks;

[Language("JSON", "1.0", "JSON data format")]
public class IronyJsonGrammar : Grammar
{
    private static readonly LanguageData _languageData = new(new IronyJsonGrammar());

    public IronyJsonGrammar() {
        //Terminals
        var jstring = new StringLiteral("string", "\"");
        var jnumber = new NumberLiteral("number");
        var comma = ToTerm(",");

        //Nonterminals
        var jobject = new NonTerminal("Object");
        var jobjectBr = new NonTerminal("ObjectBr");
        var jarray = new NonTerminal("Array");
        var jarrayBr = new NonTerminal("ArrayBr");
        var jvalue = new NonTerminal("Value");
        var jprop = new NonTerminal("Property");

        //Rules
        jvalue.Rule = jstring | jnumber | jobjectBr | jarrayBr | "true" | "false" | "null";
        jobjectBr.Rule = "{" + jobject + "}";
        jobject.Rule = MakeStarRule(jobject, comma, jprop);
        jprop.Rule = jstring + ":" + jvalue;
        jarrayBr.Rule = "[" + jarray + "]";
        jarray.Rule = MakeStarRule(jarray, comma, jvalue);

        //Set grammar root
        Root = jvalue;
        MarkPunctuation("{", "}", "[", "]", ":", ",");
        MarkTransient(jvalue, jarrayBr, jobjectBr);
    }

    public static ParseTree Parse(string input) {
        var parser = new Irony.Parsing.Parser(_languageData);
        return parser.Parse(input);
    }
}
