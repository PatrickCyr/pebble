# Pebble

Pebble is a strongly and statically typed scripting language, written in and embeddable in C#. 

The intent with Pebble was to create a scripting language that was easy to use by people who aren't necessarily full-time programmers. For example, game designers. The conventional wisdom is that dynamically typed languages are easier for beginners because they don't require them to write out the types. I vigorously disagree. It may be faster to write code in a dynamically typed language, but I don't it's faster to write *correct* code. You can't avoid the problems that come with type limits, conversions, etc by ignoring them. Plus, the whole point of type systems is to help you find errors in your code. 

Pebble was created for use in Unity games, though there's nothing in it that specifically ties it to Unity.

## Installation

### To build the CLI (Command Line Interface, the standalone Pebble interpreter):
**Windows**: There is a Visual Studio 2017 project in the vs folder.
**OS X**: Install Mono, then run buildosx.sh.

### To embed in a program:
Include all of the .cs files in the /src and /src/lib directories. CLI.cs isn't necessary but without the PEBBLECLI preprocessor symbol defined it will do nothing. CLI.cs in general provides a good example of how to use Pebble, but in essence you just create an Engine, register optional libraries, and call RunScript.

```
Engine engine = new Engine();
List<ParseErrorInst> errors = new List<ParseErrorInst>();
engine.RunScript("Print(\"Hello, world!");", ref errors, false);
```

## Help

In lieu of standard documentation, Pebble instead has examples/tutorial.txt, which is Pebble script file which contains tons of examples of how (almost) every feature in the language works.

## License

Pebble is free software distributed under the terms of the MIT license reproduced below; it may be used for any purpose, including commercial purposes, at absolutely no cost without having to ask us. The only requirement is that if you do use Pebble, then you should give us credit by including the appropriate copyright notice somewhere in your product or its documentation. 

---

Copyright (c) 2015-2020 Patrick Cyr.

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
