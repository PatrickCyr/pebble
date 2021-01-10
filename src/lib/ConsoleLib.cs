/*
Console library (functions for using the console).
See Copyright Notice in LICENSE.TXT

This library is optional. Its Register function must be called if you want to use it.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class ConsoleLib {

		public static void Register(Engine engine) {

			PebbleEnum consoleColorEnum = new PebbleEnum(engine.defaultContext, "ConsoleColor", IntrinsicTypeDefs.CONST_NUMBER);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "Black", 0.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "DarkBlue", 1.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "DarkGreen", 2.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "DarkCyan", 3.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "DarkRed", 4.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "DarkMagenta", 5.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "DarkYellow", 6.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "LightGray", 7.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "DarkGray", 8.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "Blue", 9.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "Green", 10.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "Cyan", 11.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "Red", 12.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "Magenta", 13.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "Yellow", 14.0);
			consoleColorEnum.AddValue_Literal(engine.defaultContext, "White", 15.0);
			// Finalize.
			consoleColorEnum.EvaluateValues(engine.defaultContext);

			// **********************************

			Regex colorRegex = new Regex(@"(#\d+b?#)", RegexOptions.Compiled);

			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("Console", null, false);
			ClassDef classDef = engine.defaultContext.CreateClass("Console", ourType, null, null, true);
			classDef.Initialize();

			//@ global void Clear()
			//   Clears the screen.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					Console.Clear();
					return null;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.VOID, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("Clear", newValue.valType, newValue, true);
			}

			//@ global string GetCh()
			//   Waits for the user to press a key and returns it.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					ConsoleKeyInfo cki = Console.ReadKey(true);
					return cki.KeyChar.ToString();
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("GetCh", newValue.valType, newValue, true);
			}

			//@ global string Print(...)
			//   Alias of WriteLine.

			//@ global string ReadLine()
			//   Reads a line of input and returns it.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					return Console.ReadLine();
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("ReadLine", newValue.valType, newValue, true);
			}

			//@ global void ResetColor()
			//   Resets foreground and background colors to their defaults.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					Console.ResetColor();
					return null; 
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.VOID, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("ResetColor", newValue.valType, newValue, true);
			}

			//@ global num SetBackgroundColor(ConsoleColor color)
			//   Sets the background color to the given value. 
			//   Returns the previous color.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					ClassValue_Enum enumVal = args[0] as ClassValue_Enum;
					int iColor = Convert.ToInt32((double)enumVal.GetValue());

					int iPrevColor = Convert.ToInt32(Console.BackgroundColor);
					Console.BackgroundColor = (ConsoleColor)iColor;
					return consoleColorEnum._classDef.staticVars[iPrevColor].value;
				};
				FunctionValue newValue = new FunctionValue_Host(consoleColorEnum.enumType, new ArgList { consoleColorEnum.enumType }, eval, false);
				classDef.AddMemberLiteral("SetBackgroundColor", newValue.valType, newValue, true);
			}

			//@ global num SetForegroundColor(num color)
			//   Sets the foreground color to the given value. The valid values are 0-15.
			//   Returns the previous color.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					ClassValue_Enum enumVal = args[0] as ClassValue_Enum;
					int iColor = Convert.ToInt32((double)enumVal.GetValue());

					int iPrevColor = Convert.ToInt32(Console.ForegroundColor);
					Console.ForegroundColor = (ConsoleColor)iColor;
					return consoleColorEnum._classDef.staticVars[iPrevColor].value;
				};
				FunctionValue newValue = new FunctionValue_Host(consoleColorEnum.enumType, new ArgList { consoleColorEnum.enumType }, eval, false);
				classDef.AddMemberLiteral("SetForegroundColor", newValue.valType, newValue, true);
			}

			//@ global string Write(...)
			//   Works much like Print but doesn't automatically include a newline, so you can write partial lines.
			//   Also, if an argument is a ConsoleColor, rather than writing it the function temporarily sets the foreground color to the given color.
			//   Colors can be inserted into the string by using, for example, #1#, which will set the foreground color to 1, or #11b# which sets the background color to 11.
			//   This function restores the colors to what they were before the function was called.
			//   Returns the aggregated string, minus any provided colors.
			FunctionValue_Host.EvaluateDelegate evalWrite;
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					ConsoleColor startingForeground = Console.ForegroundColor;
					ConsoleColor startingBackground = Console.BackgroundColor;

					string result = "";
					foreach (object val in args) {
						if (val is ClassValue_Enum) {
							ClassValue_Enum cve = (ClassValue_Enum)val;
							if (cve.classDef == consoleColorEnum._classDef) {
								int color = Convert.ToInt32((double)cve.GetValue());
								Console.ForegroundColor = (ConsoleColor)color;
								continue;
							}
						}


						if (val is string) {
							string v = val as string;

							string[] splits = colorRegex.Split(v);
							foreach (string str in splits) {
								if (str.Length > 2 && '#' == str[0] && '#' == str[str.Length - 1]) {
									int iColor;
									bool background = false;
									if ('b' == str[str.Length - 2]) {
										iColor = Convert.ToInt32(str.Substring(1, str.Length - 3));
										background = true;
									} else
										iColor = Convert.ToInt32(str.Substring(1, str.Length - 2));

									if (iColor < 0 || iColor > 15) {
										context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Write: Color escapes must be between 0 and 15.");
										Console.ForegroundColor = startingForeground;
										Console.BackgroundColor = startingBackground;
										return null;
									}
									if (background)
										Console.BackgroundColor = (ConsoleColor)iColor;
									else
										Console.ForegroundColor = (ConsoleColor)iColor;
								} else {
									result += str;
									Console.Write(str);
								}
							}
						} else {
							string s = CoreLib.ValueToString(context, val, false);
							result += s;
							Console.Write(s);
						}
					}

					Console.ForegroundColor = startingForeground;
					Console.BackgroundColor = startingBackground;

					return result;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.ANY }, eval, true);
				classDef.AddMemberLiteral("Write", newValue.valType, newValue, true);

				evalWrite = eval;
			}

			//@ global string WriteLine(...)
			//   Works exactly like Write but just adds a newline at the end.
			//   Returns the aggregated string, minus any provided colors.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object result = evalWrite(context, args, thisScope);
					Console.Write("\n");
					return result;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.ANY }, eval, true);
				classDef.AddMemberLiteral("WriteLine", newValue.valType, newValue, true);
				classDef.AddMemberLiteral("Print", newValue.valType, newValue, true);
			}

			classDef.FinalizeClass(engine.defaultContext);
		}


	}
}