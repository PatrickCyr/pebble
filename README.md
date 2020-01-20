# Pebble

Pebble is a strongly and statically typed scripting language, written in and embeddable in C#. 

The intent with Pebble was to create a scripting language that was easy to use by people who aren't necessarily full-time programmers. For example, game designers. The conventional wisdom is that dynamically typed languages are easier for beginners because they don't require them to write out the types. I vigorously disagree. It may be faster to write code in a dynamically typed language, but I don't it's faster to write *correct* code. You can't avoid the problems that come with type limits, conversions, etc by ignoring them. Plus, the whole point of type systems is to help you find errors in your code. 

Pebble was created for use in Unity games, though there's nothing in it that specifically ties it to Unity.

## Installation

### To build the CLI (Command Line Interface, the standalone Pebble interpreter):
Windows: There is a Visual Studio 2017 project in the vs folder.
OS X: Run buildosx.sh. Requires Mono.

### To embed in a program:
Include all of the .cs files in the /src and /src/lib directories. CLI.cs isn't necessary but without the PEBBLECLI preprocessor symbol defined it will do nothing. CLI.cs in general provides a good example of how to use Pebble.

## Help

In lieu of standard documentation, Pebble instead has examples/tutorial.txt, which is Pebble script file which contains tons of examples of how (hopefully) every feature in the language works.

## License

Check out LICENSE.txt. It's an MIT License: free, pretty much do whatever you want with it.
