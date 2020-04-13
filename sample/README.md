This directory contains sample grammars created by Farkle or imported from `*.egt` files.

Currently we have:

* A grammar for JSON, written in both C# and F#. It outputs the documents in the same domain type with [Chiron](https://github.com/xyncro/chiron), to test for its correctness and performance. Keep in mind that this grammar is not for production use; there are much better (and faster) JSON libraries around.

* A gramamr for the GOLD Meta-Language, ported from the [official grammar's version 2.6.0](http://www.goldparser.org/grammars/index.htm), and written in F#. It was made for correctness and performance testing.

* A grammar for a simple mathematical expressions, written in the GOLD Parser and in F#. It was made to test Farkle's ability to parse GOLD Parser gramamrs, and for demonstration purposes.
