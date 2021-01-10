/*
The core built-in functions. Unlike other libraries, these functions exist in the global namespace.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class CoreLib {

		public static string ValueToString(ExecContext context, object val, bool quoteStrings) {
			string result = "";
			if (null == val)
				result += "null";
			else if (val is bool)
				result += (bool)val ? "true" : "false";
			else if (val is string)
				result += quoteStrings ? "\"" + (string)val + "\"" : (string)val;
			else if (val is double)
				result += val.ToString();
			else if (val is ClassValue) {
				if (null != context)
					result += ((ClassValue)val).ToString(context); // this searches the class for a ToString member
				else
					result += ((ClassValue)val).debugName;
			} else
				result += val.ToString();
			return result;
		}

		public static string StandardPrintFunction(ExecContext context, List<object> args) {
			string result = "";
			foreach (object val in args) {
				result += CoreLib.ValueToString(context, val, false);
			}
			return result;
		}

		public static string ValueToScript(ExecContext context, object value, string prefix = "", bool topLevel = true) {
			string result = "";
			string postfix = topLevel ? ";" : "";
			if (value is double) {
				return Convert.ToString((double)value) + postfix;
			} else if (value is bool) {
				return ((bool)value ? "true" : "false") + postfix;
			} else if (value is string) {
				return "\"" + (string)value + "\"" + postfix;
			} else if (value is ClassValue_Enum) { // is enum
				return ((ClassValue_Enum)value).ToString() + postfix;
			} else if (value is ClassValue) {
				ClassValue table = value as ClassValue;
				result = "new " + table.classDef.typeDef.ToString() + " {\n";

				// ThisToScript must not be static. It's going to be printing info about a class instance, right?
				MemberRef toStrMem = table.classDef.GetMemberRef(null, "ThisToScript", ClassDef.SEARCH.NORMAL);
				Variable funcVar = null;
				if (!toStrMem.isInvalid)
					funcVar = table.Get(toStrMem) as Variable;
				if (null != funcVar) {
					FunctionValue funcVal = funcVar.value as FunctionValue;
					result += funcVal.Evaluate(context, new List<object> { prefix }, table) as string;
				} else {
					foreach (Variable kvp in table.fieldVars) {
						if (!(kvp.type is TypeDef_Function) && !kvp.type.IsConst()) {
							result += prefix + "\t" + kvp.name + " = " + ValueToScript(context, kvp.value, prefix + "\t", false);
							if (!result.EndsWith(";"))
								result += ";";
							result += "\n";
						}
						
					}
				}
				result += prefix + "}" + postfix;
			} else if (null == value) {
				result = "null" + postfix;
			}
			return result;
		}

		public static PebbleEnum scriptErrorEnum;
		public static ClassValue_Enum scriptErrorEnum_noErrorValue;

		public static ClassDef resultClassDef = null;

		private static TypeDef_Class scriptResultBoolTypeDef;
		private static ClassDef scriptResultBoolClassDef;

		public static void Register(Engine engine) {

			//*****************************
			// Create ScriptError enum.

			scriptErrorEnum = new PebbleEnum(engine.defaultContext, "ScriptError", IntrinsicTypeDefs.CONST_STRING);

			// Add a value for "no error" since enums can't be null.
			scriptErrorEnum.AddValue_Literal(engine.defaultContext, "NoError", "NoError");

			// Add both Parse and Runtime errors to the list.
			foreach (string name in Enum.GetNames(typeof(ParseErrorType)))
				scriptErrorEnum.AddValue_Literal(engine.defaultContext, name, name);
			foreach (string name in Enum.GetNames(typeof(RuntimeErrorType)))
				scriptErrorEnum.AddValue_Literal(engine.defaultContext, name, name);

			// Finalize.
			scriptErrorEnum.EvaluateValues(engine.defaultContext);

			// Save the value for NoError for convenience.
			scriptErrorEnum_noErrorValue = scriptErrorEnum.GetValue("NoError");

			//*******************************
			//@ class Result<T> 
			//   This was added just in case users might have a need for a templated class that encapsulates a value and a status code.
			{
				TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("Result", new ArgList { IntrinsicTypeDefs.TEMPLATE_0 }, false);
				ClassDef classDef = engine.defaultContext.CreateClass("Result", ourType, null, new List<string> { "T" });
				classDef.Initialize();

				//@ T value;
				//   The resultant value IF there was no error.
				classDef.AddMember("value", IntrinsicTypeDefs.TEMPLATE_0);
				//@ num status;
				//   A numeric status code. By convention, 0 means no error and anything else means error.
				classDef.AddMemberLiteral("status", IntrinsicTypeDefs.NUMBER, 0.0);
				//@ string message;
				//   A place to store error messages if desired.
				classDef.AddMember("message", IntrinsicTypeDefs.STRING);

				//@ bool IsSuccess()
				//   Returns true iff status == 0.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						ClassValue scope = thisScope as ClassValue;
						return (double)scope.GetByName("status").value == 0.0;
					};

					FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { }, eval, false, ourType);
					classDef.AddMemberLiteral("IsSuccess", newValue.valType, newValue);
				}

				//@ string ToString()
				//   Returns a string representation of the Result.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						ClassValue scope = thisScope as ClassValue;
						double status = (double)scope.GetByName("status").value;

						string result = scope.classDef.typeDef.ToString() + "[";
						if (0.0 == status) {
							result += CoreLib.ValueToString(context, scope.GetByName("value").value, true);
						} else {
							result += status + ": \"" + (string)scope.GetByName("message").value + "\"";
						}

						return result + "]";
					};

					FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false, ourType);
					classDef.AddMemberLiteral("ThisToString", newValue.valType, newValue);
				}

				classDef.FinalizeClass(engine.defaultContext);
				resultClassDef = classDef;
			}

			//*******************************
			//@ class ScriptResult<T>
			//   For returning the result of something that can error, like an Exec call.
			{
				TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("ScriptResult", new ArgList { IntrinsicTypeDefs.TEMPLATE_0 }, false);
				ClassDef classDef = engine.defaultContext.CreateClass("ScriptResult", ourType, null, new List<string> { "T" });
				classDef.Initialize();

				//@ T value;
				//   The return value if there was no error.
				classDef.AddMember("value", IntrinsicTypeDefs.TEMPLATE_0);
				//@ ScriptError error;
				//   ScriptError.NoError if no error.
				classDef.AddMemberLiteral("error", CoreLib.scriptErrorEnum._classDef.typeDef, CoreLib.scriptErrorEnum_noErrorValue);
				//@ string message;
				//   Optional error message.
				classDef.AddMember("message", IntrinsicTypeDefs.STRING);

				//@ bool IsSuccess()
				//   Returns true iff error == ScriptError.NoError.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						ClassValue scope = thisScope as ClassValue;
						return scope.GetByName("error").value == CoreLib.scriptErrorEnum_noErrorValue;
					};

					FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { }, eval, false, ourType);
					classDef.AddMemberLiteral("IsSuccess", newValue.valType, newValue);
				}

				//@ string ToString()
				//   Returns a string representation of the ScriptError.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						ClassValue scope = thisScope as ClassValue;
						var error = (ClassValue_Enum)scope.GetByName("error").value;

						string result = scope.classDef.typeDef.ToString() + "[";
						if (CoreLib.scriptErrorEnum_noErrorValue == error) {
							result += CoreLib.ValueToString(context, scope.GetByName("value").value, true);
						} else {
							result += error.GetName() + ": \"" + (string)scope.GetByName("message").value + "\"";
						}

						return result + "]";
					};

					FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false, ourType);
					classDef.AddMemberLiteral("ThisToString", newValue.valType, newValue);
				}

				classDef.FinalizeClass(engine.defaultContext);
				resultClassDef = classDef;
			}

			// This code makes sure that Result<bool> is a registered class and type.
			List<ITypeDef> genericTypes = new ArgList();
			genericTypes.Add(IntrinsicTypeDefs.BOOL);
			scriptResultBoolTypeDef = TypeFactory.GetTypeDef_Class("ScriptResult", genericTypes, false);
			scriptResultBoolClassDef = engine.defaultContext.RegisterIfUnregisteredTemplate(scriptResultBoolTypeDef);
			Pb.Assert(null != scriptResultBoolTypeDef && null != scriptResultBoolClassDef, "Error initializing ScriptResult<bool>.");


			////////////////////////////////////////////////////////////////////////////
			// Register non-optional libraries.

			//CoreResult.Register(engine);
			// List and Dictionary probably need to be first because other libraries sometimes use them.
			CoreList.Register(engine);
			CoreDictionary.Register(engine);
			MathLib.Register(engine);
			StringLib.Register(engine);
			StreamLib.Register(engine);

			//@ global const num FORMAX;
			//   The highest value a for iterator can be. Attempting to exceed it generates an error.
			engine.defaultContext.CreateGlobal("FORMAX", IntrinsicTypeDefs.CONST_NUMBER, Expr_For.MAX);

			////////////////////////////////////////////////////////////////////////////
			// Library functions

			//@ global ScriptResult<bool> Exec(string script)
			//   Executes the supplied script.
			//   Since this is not running "interactive" (or inline), the only way the script can
			//   have an external effect is if it affects global things (variables, class definitions).
			//   The returned ScriptResult's value is only true(success) or false (error).
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string script = (string)args[0];

					ClassValue scriptResultInst = scriptResultBoolClassDef.Allocate(context);
					Variable value = scriptResultInst.GetByName("value");
					Variable error = scriptResultInst.GetByName("error");
					Variable message = scriptResultInst.GetByName("message");

					ScriptResult result = context.engine.RunScript(script, false, null, true);
					if (null != result.parseErrors) {
						value.value = false;
						error.value = scriptErrorEnum.GetValue(result.parseErrors[0].type.ToString()); ;
						message.value = result.parseErrors[0].ToString();
					} else if (null != result.runtimeError) {
						value.value = false;
						error.value = result.value;
						message.value = result.runtimeError.ToString();
					} else {
						value.value = true;
						error.value = scriptErrorEnum_noErrorValue;
						message.value = "";
					}

					return scriptResultInst;
				};
				FunctionValue newValue = new FunctionValue_Host(scriptResultBoolTypeDef, new ArgList { IntrinsicTypeDefs.STRING }, eval, false);
				engine.AddBuiltInFunction(newValue, "Exec");
			}

			//@ global ScriptResult<bool> ExecInline(string)
			//   This executes the given script in the current scope. This is different from Exec, because Exec exists in its own scope.
			//   The returned ScriptResult's value is only true(success) or false (error).
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string script = (string)args[0];

					ClassValue scriptResultInst = scriptResultBoolClassDef.Allocate(context);
					Variable value = scriptResultInst.GetByName("value");
					Variable error = scriptResultInst.GetByName("error");
					Variable message = scriptResultInst.GetByName("message");

					ScriptResult result = context.engine.RunInteractiveScript(script, false);
					if (null != result.parseErrors) {
						value.value = false;
						error.value = scriptErrorEnum.GetValue(result.parseErrors[0].type.ToString()); ;
						message.value = result.parseErrors[0].ToString();
					} else if (null != result.runtimeError) {
						value.value = false;
						error.value = result.value;
						message.value = result.runtimeError.ToString();
					} else {
						value.value = true;
						error.value = scriptErrorEnum_noErrorValue;
						message.value = "";
					}
					return scriptResultInst;
				};
				FunctionValue newValue = new FunctionValue_Host(scriptResultBoolTypeDef, new ArgList { IntrinsicTypeDefs.STRING }, eval, false);
				engine.AddBuiltInFunction(newValue, "ExecInline");
			}

			//@ global string Print(...)
			//   Converts all arguments to strings, concatenates them, then outputs the result using the Engine' Log function.
			//   This function can be set to whatever the host program likes: see Engine.Log
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string result = StandardPrintFunction(context, args);
					context.engine.Log(result);
					return result;
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new List<ITypeDef> { IntrinsicTypeDefs.ANY }, eval, true);
				engine.AddBuiltInFunction(newValue, "Print");
			}

			//@ global string ToScript(any)
			//   Returns a script which, when run, returns a value equal to the value passed into ToScript.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object val0 = args[0];
					return ValueToScript(context, val0);
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.ANY }, eval, false);
				engine.AddBuiltInFunction(newValue, "ToScript");
			}


			/////////////////////////////////////////////////////////////////
			// Type Conversion

			//@ global bool ToBool(any)
			//   Attempts to convert input into a boolean value. 
			//   0 and null are false. != 0 and non-null references are true. Strings are handled by Convert.ToBoolean,
			//   which can throw an exception if it doesn't know how to convert the string.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object val = args[0];
					if (null == val)
						return false;
					else if (val is bool)
						return (bool)val;
					else if (val is double)
						return Convert.ToBoolean((double)val);
					else if (val is string) {
						try {
							return Convert.ToBoolean((string)val); // this loves to throw errors
						} catch (Exception e) {
							context.SetRuntimeError(RuntimeErrorType.ConversionInvalid, "ToBool - C# error: " + e.ToString());
							return null;
						}
					} else
						return true;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.ANY }, eval, false);
				engine.AddBuiltInFunction(newValue, "ToBool");
			}

			//@ global num ToNum(any)
			//   Attempts to convert input to a num.
			//   true -> 1, false -> 0, null -> 0, non-null object reference -> 1. Strings are handled by Convert.ToDouble, 
			//   which can throw an error if it doesn't know how to convert the string.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object val = args[0];
					if (null == val)
						return 0.0;
					else if (val is double)
						return (double)val;
					else if (val is bool)
						return (bool)val ? 1.0 : 0.0;
					else if (val is string)
						try {
							return Convert.ToDouble((string)val);	// this loves to throw errors
						} catch {
							context.SetRuntimeError(RuntimeErrorType.ConversionInvalid, "ToNum - Cannot convert string \"" + ((string) val) + "\" to number.");
							return null;
						}
					else
						return 1.0;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.ANY }, eval, false);
				engine.AddBuiltInFunction(newValue, "ToNum");
			}

			//@ global string ToString(...)
			//   Converts all arguments to strings, concatenates them, and returns the result.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					return StandardPrintFunction(context, args);
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.ANY }, eval, true);
				engine.AddBuiltInFunction(newValue, "ToString");
			}

			UnitTests.testFuncDelegates.Add("CoreLib", RunTests);
		}

		public static bool RunTests(Engine engine, bool verbose) {
			bool result = true;
			engine.Log("\n*** CoreLib: Running tests...");

			// * Exec - tested in unittests/coreLibTests.txt
			// * ExecInLine	- tested in unittests/coreLibTests.txt
			// * Print - How could we tell if the text was actually printed?
			// * ToScript
			result &= engine.RunTest("ToScript(3.14);", "3.14;", verbose);
			result &= engine.RunTest("ToScript(true);", "true;", verbose);
			result &= engine.RunTest("ToScript(\"hi\");", "\"hi\";", verbose);
			result &= engine.RunTest("{ num ser = 3.14; ToScript(ser); }", "3.14;", verbose);
			result &= engine.RunTest("{ bool ser = true; ToScript(ser); }", "true;", verbose);
			result &= engine.RunTest("{ string ser = \"hi\"; ToScript(ser); }", "\"hi\";", verbose);
			result &= engine.RunTest("ToScript(null);", "null;", verbose);
			result &= engine.RunTest("{ A ser; ToScript(ser); }", "null;", verbose);
			result &= engine.RunTest("ToScript(Math::Sqrt);", "", verbose);

			// -- static class members and functions are not serialized
			result &= engine.RunTest(@"{ 
				class SerTest { 
					num x;
					static num sx; 
					num F() { 1; } 
				}; 
				SerTest serTest = new; 
				String::Replace(String::Replace(ToScript(serTest), ""\t"", """"), ""\n"", """"); }", "new SerTest {x = 0;};", verbose);
			// * ToNum - Tested in UnitTests
			// * ToString - Tested in UnitTests

			engine.Log("*** CoreLib: Tests " + (result ? "succeeded" : "FAILED"));
			return result;
		}
	}
}