# Quick Start in C#: Creating a calculator

When Farkle's version 5 was released, one of its biggest features was first-class support for C#. Because C# is quite a different language from F#, a new API was created to make using Farkle with C# as smooth as possible. This tutorial will show you how to make a calculator, just like the [F# tutorial][quickstart-fsharp] (which you should probably read first), and indicate what is different during the process.

## Prerequisites

You can see at [the F# tutorial][quickstart-fsharp] how to install GOLD Parser, and how to compile our calculator's grammar. Having done that, save the EGT file next to our project files.

Now, it's time to install the necessary packages. Open [your favorite package manager][paket] (or another one), and install the packages `Farkle`, and `Farkle.Tools.MSBuild`. The first is the core Farkle library, and the second gives us some additional design-time support that we will need later. After that, open your project file and add these little lines:

``` xml
<ItemGroup>
  <Farkle Include="SimpleMaths.egt" />
</ItemGroup>
```

As you might remember from previously, thanks to this line, MSBuild, our tireless build system will generate a source file for us, describing the grammar we just made. Let's take a look at it:

``` csharp
// This file was created by Farkle.Tools version 5.0.0 at 2019-06-06.
// It should NOT be committed to source control.

namespace SimpleMaths.Definitions {
    /// <summary> A terminal of the SimpleMaths language.</summary>
    /// <seealso cref="Definitions.Production"/>
    public enum Terminal : uint {
        /// <summary><c>&#39;-&#39;</c></summary>
        Minus = 3,
        /// <summary><c>&#39;(&#39;</c></summary>
        LParen = 4,
        /// <summary><c>&#39;)&#39;</c></summary>
        RParen = 5,
        /// <summary><c>&#39;*&#39;</c></summary>
        Times = 6,
        /// <summary><c>&#39;/&#39;</c></summary>
        Div = 7,
        /// <summary><c>&#39;+&#39;</c></summary>
        Plus = 8,
        /// <summary><c>Number</c></summary>
        Number = 9
    }

    /// <summary> A production of the SimpleMaths language.</summary>
    /// <seealso cref="Terminal"/>
    public enum Production : uint {
        /// <summary><c>&lt;Expression&gt; ::= &lt;Add Exp&gt;</c></summary>
        Expression = 0,
        /// <summary><c>&lt;Add Exp&gt; ::= &lt;Add Exp&gt; &#39;+&#39; &lt;Mult Exp&gt;</c></summary>
        AddExpPlus = 1,
        /// <summary><c>&lt;Add Exp&gt; ::= &lt;Add Exp&gt; &#39;-&#39; &lt;Mult Exp&gt;</c></summary>
        AddExpMinus = 2,
        /// <summary><c>&lt;Add Exp&gt; ::= &lt;Mult Exp&gt;</c></summary>
        AddExp = 3,
        /// <summary><c>&lt;Mult Exp&gt; ::= &lt;Mult Exp&gt; &#39;*&#39; &lt;Negate Exp&gt;</c></summary>
        MultExpTimes = 4,
        /// <summary><c>&lt;Mult Exp&gt; ::= &lt;Mult Exp&gt; &#39;/&#39; &lt;Negate Exp&gt;</c></summary>
        MultExpDiv = 5,
        /// <summary><c>&lt;Mult Exp&gt; ::= &lt;Negate Exp&gt;</c></summary>
        MultExp = 6,
        /// <summary><c>&lt;Negate Exp&gt; ::= &#39;-&#39; &lt;Value&gt;</c></summary>
        NegateExpMinus = 7,
        /// <summary><c>&lt;Negate Exp&gt; ::= &lt;Value&gt;</c></summary>
        NegateExp = 8,
        /// <summary><c>&lt;Value&gt; ::= Number</c></summary>
        ValueNumber = 9,
        /// <summary><c>&lt;Value&gt; ::= &#39;(&#39; &lt;Expression&gt; &#39;)&#39;</c></summary>
        ValueLParenRParen = 10
    }

    public static class Grammar {
        #region Grammar as Base64
        public static readonly string AsBase64 = @"Too big to fit here :-(";
        #endregion
    }
}
```

Sorry for the unreadable comments, but they are actually surprisingly pretty in an IDE. As the comments of the file show, you shouldn't commit it so source control, because it gets generated when it's needed. What is more, we don't have to add this new source file to the project, because the compiler will automatically add it.

## Making a post-processor

It's now time to make a post-processor. To freshen up your memory, in F#, post-processors made of a list of transformers, which convert the terminals of our grammar into arbitrary objects, and fusers, which combine the terminals and nonterminals of a grammar into objects once again.

In C# however, there are no lists, but functions: a _transforming function_, and a _fuser-getting function_.

### The transforming function

Let's take a look at the first thing: Create a new source file, and write this:

``` csharp
using System;
using Farkle;
using Farkle.CSharp;
using Farkle.PostProcessor;
using SimpleMaths.Definitions;

namespace SimpleMaths
{
  public static class Language
  {
    // This function converts terminals to anything you want.
    // If you do not care about a terminal (like single characters),
    // you can let the default case return null.
    private static object Transform(uint terminal, Position position, ReadOnlySpan<char> data)
    {
      if (terminal == (uint) Terminal.Number)
        return int.Parse(data);
      return null;
    }
  }
}
```

As you see, our lovely function accepts the index of our terminal, its position, and its content, and returns an object. Here, we convert the terminal's content to an integer, only if the terminal is of type `Number`. Otherwise, we don't care and return `null`. Hooray!

You might be asking, why aren't we using transformers like our our F#-programming fellows do? The answer is surprisingly simple. You see, if we used transformers here, our code would be cluttered by type casts from the unsigned integer terminal index, to the enumerated terminal type. Moreover, F# requires us to explicitly cast our integers and other types to objects. Long story short, this style feels more idiomatic to C#.

Also, note that [`ReadOnlySpan` is a special type][ref-structs]. You cannot return it as it is. If you want to return the data _as a string_ however, you can simply call `data.ToString()`.

### The fuser-getting function

Now it's time for the fuser-getting function. In a nutshell, it accepts the index of a production of our grammar, and returns a fuser. But let's see how it actually looks like. Add this function below our previous one.

``` csharp
// The fusers merge the parts of a production into one object of your desire.
// This function maps each production to a fuser.
// Do not delete anything here, or the post-processor will fail.
private static Fuser GetFuser(uint prod)
{
  switch ((Production) prod)
  {
    case Production.Expression:
      return Fuser.First;
    case Production.AddExpPlus:
      return Fuser.Create<int, int, int>(0, 2, (x1, x2) => x1 + x2);
    case Production.AddExpMinus:
      return Fuser.Create<int, int, int>(0, 2, (x1, x2) => x1 - x2);
    case Production.AddExp:
      return Fuser.First;
    case Production.MultExpTimes:
      return Fuser.Create<int, int, int>(0, 2, (x1, x2) => x1 * x2);
    case Production.MultExpDiv:
      return Fuser.Create<int, int, int>(0, 2, (x1, x2) => x1 / x2);
    case Production.MultExp:
      return Fuser.First;
    case Production.NegateExpMinus:
      return Fuser.Create<int, int>(1, x => -x);
    case Production.NegateExp:
      return Fuser.First;
    case Production.ValueNumber:
      return Fuser.First;
    case Production.ValueLParenRParen:
      return Fuser.Create<int, int>(1, x => x);
    default: return null;
  }
}
```

A C# fuser contains just a delegate that converts an array of objects, into a single object. In other words, what fusers in Farkle usually do. This syntax avoids the noisy type casts that would be required were be using a fusing function instead.

We see two types of fusers. `Fuser.First` is a fuser that always takes the first element of a production as-is.

`Fuser.create` needs a little more attention. In our example, `Fuser.Create<int, int, int>(0, 2, (x1, x2) => x1 + x2)` is just a fuser that takes the integers at the zeroth and second position of our production - because as we all know, arrays start at zero - and just adds them. You can see all the other ways to create a fuser [in this page][fuser-doc].

And keep in mind, that fusers are mandatory. If this function returns null, it will result in an error.

### Assembling the post-processor

With both our functions ready, it's time to make our post-processor. Add this surprisingly simple line after our two functions:

``` csharp
private static readonly PostProcessor<int> _postProcessor = PostProcessor.Create<int>(Transform, GetFusers);
```

### Making the runtime Farkle

After the post-processor, we have to create a runtime Farkle. This marvelous object allows us to parse and post-process our text inputs into whatever we want. This object is made of a grammar, and a post-processor. We just made our beloved post-processor, and the grammar was given to us in Base64, by our brave build system. Add this line after the previous one:

``` csharp
public static readonly RuntimeFarkle<int> Runtime = RuntimeFarkle<int>.CreateFromBase64String(Grammar.AsBase64, _postProcessor);
```

We can now calculate simple mathematical expressions incredibly easily. Let's take a look at an example:

``` csharp
// The type is FSharpResult<int, FarkleError>.
// Not very convenient to write.
var result = SimpleMaths.Language.Runtime.Parse("2*2*181");

if (result.IsOk)
  Console.WriteLine(result.ResultValue);
else
  Console.WriteLine(result.ErrorValue);
```

There are many other functions available to use a runtime Farkle. You can explore them by looking at the functions your IDE will recommend to you.

## Bonus: Using Farkle.Tools

If you thought you were writing lots of repetitive code, there is a solution for you. Since version 5.0, there is an incredibly helpful command-line tool that helps you generate a skeleton source file for your post-processor. To use it, first install the tool with this simple command:

`dotnet tool install -g Farkle.Tools`

And order it to make a source file for you like this:

`farkle new -t postprocessor -lang C# SimpleMaths.egt`

You will immediately see a file called `SimpleMaths.cs` that contains most of the code we have already written. Complete it for yourself by fixing all the compile errors, and you are ready to go. Hooray!

---

So, I hope you enjoyed this little tutorial. If you did, don't forget to give Farkle a try, and maybe you feel especially eager to parse some strings with C# today, and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!

[quickstart-fsharp]: quickstart.html
[paket]: https://fsprojects.github.io/Paket/
[ref-structs]: https://blogs.msdn.microsoft.com/mazhou/2018/03/02/c-7-series-part-9-ref-structs/
[fuser-doc]: reference/farkle-csharp-fuser.html
