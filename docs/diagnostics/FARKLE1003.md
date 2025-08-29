# FARKLE1003: The `And` and `Or` methods of `Regex` are obsolete

The `And` and `Or` methods of `Regex` are used to build a regex that matches the concatenation or alternation of this regex with another regex. Because the name `And` is a misnomer (should have been called `Then`), and in order to increase readability and intuitiveness, Farkle 7 introduced the `+` and `|` operators for the same purpose. You can resolve the obsoletion warning by simply replacing calls to `And` with `+` and calls to `Or` with `|`.
