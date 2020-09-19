/*
Pebble Command Line Interface (CLI)
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;
using System.IO;
using Pebble;

#if PEBBLECLI

namespace PebbleCLI {
	class PebbleCLI {
		public static void Main(string[] arg) {

			// *** Parse arguments.

			if (arg.Length == 0) {
				PrintUsage();
				return;
			}

			bool optRunTests = false;
			bool optInteractiveMode = false;
			bool optVerbose = false;
			bool optPause = false;
			bool optDump = false;

			List<string> optFiles = new List<string>();
			List<string> passThroughArgs = null;
			for (int iArg = 0; iArg < arg.Length; ++iArg) {
				string ar = arg[iArg];
				if (ar[0] == '-' || ar[0] == '/') {
					if (ar[1] == 't')
						optRunTests = true;
					else if (ar[1] == 'i')
						optInteractiveMode = true;
					else if (ar[1] == 'v') {
						Console.WriteLine("verbose enabled.");
						optVerbose = true;
					} else if (ar[1] == 'p')
						optPause = true;
					else if (ar[1] == 'd')
						optDump = true;
					else if (ar[1] == 'a') {
						if (iArg == arg.Length - 1) {
							Console.WriteLine("Argument expected after -a option.");
							PrintUsage();
							return;
						} else {
							passThroughArgs = passThroughArgs ?? new List<string>();
							passThroughArgs.Add(arg[++iArg]);
						}
					} else {
						Console.WriteLine("Unrecognized option '" + ar[1] + "'.");
						PrintUsage();
						return;
					}
				} else {
					optFiles.Add(ar);
				}
			}


			// *** Initialize engine.

			// Create engine.
			Engine engine = new Engine();
			engine.LogError = (msg) => {
				ConsoleColor sav = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(msg);
				Console.ForegroundColor = sav;
			};

			// Register optional libraries.
			DebugLib.Register(engine);
			FileLib.Register(engine);
			DateTimeLib.Register(engine);

			List<ParseErrorInst> errors = new List<ParseErrorInst>();

			// Create pass-through args.
			if (null != passThroughArgs) {
				string initScript = "global List<string> CLIargs = new { ";
				foreach (string pta in passThroughArgs) {
					initScript += "Add(\"" + pta + "\"); ";
				}
				initScript += "};";

				ScriptResult result = engine.RunScript(initScript, false);
				if (!result.success) { 
					Console.WriteLine("INTERNAL ERROR creating passthrough arguments array.");
					return;
				}
				errors.Clear();
			}

			// *** Do tasks.

			// Tests...
			if (optRunTests)
				UnitTests.RunTests(engine, optVerbose);

			// Files...
			foreach (string filename in optFiles) {
				string fileContents = File.ReadAllText(filename);
				errors.Clear();

				ScriptResult result = engine.RunScript(fileContents, optVerbose, filename);
				if (result.success)
					Console.WriteLine("Returned: " + CoreLib.ValueToString(engine.defaultContext, result.value, true));
				else { 
					Console.WriteLine(result);
				}
			}

			// Interactive mode...
			if (optInteractiveMode) {
				Console.WriteLine("Pebble Interpreter (C) 2019 Patrick Cyr");
				Console.WriteLine("Interactive mode. Enter 'exit' to exit, 'help' for help:");

				// Lines in interactive mode don't use their own scope. Instead, they 
				// use one shared scope that is created here.
				engine.defaultContext.stack.PushTerminalScope("<interactive mode>", null);

				while (true) {
					Console.Write("> ");
					string line = Console.ReadLine();

					line = line.Trim();
					if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
						break;
					if (line.Equals("help", StringComparison.OrdinalIgnoreCase)) {
						line = "Debug::DumpClass(\"Debug\");";
						Console.WriteLine(line);
					}

					ScriptResult result = engine.RunInteractiveScript(line, optVerbose);
					if (result.success) { 
						if (null == result.value) {
							Console.WriteLine("<null>");
						} else {
							Console.WriteLine(CoreLib.ValueToString(engine.defaultContext, result.value, true));
						}
					}
				}
			}

			// Dump memory...
			if (optDump) {
				Console.WriteLine();
				Console.WriteLine(engine.defaultContext);
			}

			// Pause before exiting...
			if (optPause) {
				Console.WriteLine("Press any key to exit.");
				Console.ReadKey();
			}
		}

		public static void PrintUsage() {
			Console.WriteLine("[options] [filename [filename ...]]");
			Console.WriteLine("If files and interactive mode are both specified, files will be executed before interactive mode.");
			Console.WriteLine("Options:");
			Console.WriteLine("-a [argument]: argument to pass to script via args global list. Use multiple -a's for multiple arguments.");
			Console.WriteLine("-d : dump memory state before ending");
			Console.WriteLine("-i : interactive mode");
			Console.WriteLine("-p : pause before ending");
			Console.WriteLine("-t : run unit tests");
			Console.WriteLine("-v : verbose mode");
		}
	}
}

#endif