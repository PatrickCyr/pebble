/*
Engine is the thing which compiles and executes Pebble scripts.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Pebble {

	interface IPebbleLib {
		void Register(Engine engine);
		bool RunTests(Engine engine, bool verbose);
	};

	/*
	 * Instantiate an instance of this to run Pebble scripts.
	 */
	public class Engine {
		public delegate void LogFunction(string msg);

		public LogFunction Log = DefaultLogFunction;
		public LogFunction LogError = DefaultLogFunction;
		public bool logCompileErrors = true;
		public string currentFileOrScriptName;

		protected List<ParseErrorInst> _parseErrors = new List<ParseErrorInst>();

		public readonly ExecContext defaultContext;

		/*
		delegate bool LibraryTestFunc(Engine engine, bool verbose);

		public static void RegisterLibraryTests(LibraryTestFunc testFunc) {
			testFunc();
		}
		*/

		// **************************************************************************

		public Engine() {
			defaultContext = new ExecContext(this);
			CoreLib.Register(this);
		}

		public override string ToString() {
			return defaultContext.ToString();
		}

		protected static void DefaultLogFunction(string msg) {
			Console.WriteLine(msg);
		}

		public ParseErrorInst GetParseErrorAndClear() {
			ParseErrorInst pei = _parseErrors[0];
			_parseErrors.Clear();
			return pei;
		}

		internal void LogCompileError(ParseErrorType error, string msg) {
			ParseErrorInst inst = new ParseErrorInst(error, msg);
			_parseErrors.Add(inst);
			if (logCompileErrors)
				LogError(inst.ToString());
		}

		/*	Returns global variable with given name.
			BE VERY CAREFUL IF YOU DECIDE TO CHANGE THE VARIABLE
			Changing the type or the value such that the value is
			no longer of the Variable's type is surely disastrous.
		*/
		public Variable GetGlobalVariable(string variableName) {
			return defaultContext.stack.GetVariable(variableName, true);
		}

		/* Todo, maybe: this could be handy.
		public Variable GetClassStaticVariable(string className, string variableName) {
			ClassDef classDef = defaultContext.GetClass(className);
			MemberRef mref = classDef.GetMemberRef(variableName, ClassDef.SEARCH.STATIC);
			Variable var = classDef.GetVariable(mref);
			return var;
		}
		*/

		/* 
			Creates a new global variable with given name, type, and initial value. 
			BE VERY CAREFUL IF YOU DECIDE TO USE THIS. THIS FUNCTION DOES NO TYPE 
			CHECKING. 

			Returns false if variable already exists.
		*/
		public bool CreateGlobalVariable(string name, ITypeDef variableType, object value) {
			return defaultContext.CreateGlobal(name, variableType, value);
		}

		/*
		 * Registers a script function.
		 * If any names are already used, returns false.
		 */
		public bool AddBuiltInFunction(FunctionValue def, string name) {
			Pb.Assert(name != null);
			Pb.Assert(def.valType.isConst);
			return CreateGlobalVariable(name, def.valType, def);
		}

		// **************************************************************************

		/**
		 * Parses and evaluates Pebble script s.
		 * If there is a parse error, errors will be non-empty.
		 * If there is a runtime error, the return value will be of type RuntimeErrorInst
		 * On success, return value will be value returned from script.
		 *
		 * This function basically wraps Parse/EvaluateExpression. Unless you are doing
		 * something tricky it's probably simplest to use this function rather than those
		 * other two.
		 *
		 * createTempScope - When true, creates a scope for this script which is popped when 
		 * the function is done. This means that any local variables created during s will be
		 * gone. This should be the default behavior, with the notable exception of CLI 
		 * interactive mode.
		*/
		public object RunScript(string s, ref List<ParseErrorInst> errors, bool verbose = false, string filename = null) {
			string scriptName = filename != null ? filename : _GetScriptNameFromScript(s);
			return _RunScript(scriptName, s, ref errors, verbose, true);
		}

		// This version runs a script without creating a temp scope, so any local variables
		// made persist on the stack afterwards. Ideal for interactive mode.
		public object RunInteractiveScript(string s, ref List<ParseErrorInst> errors, bool verbose = false) {
			return _RunScript(null, s, ref errors, verbose, false);
		}

		private object _RunScript(string scriptName, string s, ref List<ParseErrorInst> errors, bool verbose = false, bool createTempScope = true) {
			IExpr expr = _Parse(scriptName, s, ref errors, verbose, createTempScope);
			if (null == expr)
				return null;

			return _EvaluateExpression(expr, createTempScope);
		}		

		/**
		 * Parses a script, and returns the resultant IExpr or null if there was an error.
		 * null can also be returned if the script evaluates to no expression (for example
		 * if it was an empty string), in which case errors will be empty.
		 */
		public IExpr Parse(string s, ref List<ParseErrorInst> errors, bool verbose = false, string scriptName = null) {
			if (null == s)
				return null;

			string sn = scriptName != null ? scriptName : _GetScriptNameFromScript(s);

			return _Parse(sn, s, ref errors, verbose, true);
		}

		private IExpr _Parse(string scriptName, string s, ref List<ParseErrorInst> errors, bool verbose = false, bool createTempScope = true) {
			errors.Clear();
			// Save this for LogCompileErrors.
			_parseErrors = errors;

			Buffer buffer = new SimpleBuffer(s);
			Scanner scanner = new Scanner(buffer);
			Parser parser = new Parser(scanner);
			parser.context = defaultContext;
			parser.scriptName = scriptName;
			parser.Parse();

			if (parser.errors.count > 0) {
				if (logCompileErrors) {
					for (int ii = 0; ii < parser.errors.errors.Count; ++ii) {
						LogError(parser.errors.errors[ii].ToString());
					}
				}

				errors.AddRange(parser.errors.errors);
				_parseErrors = null;
				return null;
			}

			IExpr expr = parser._headExpr;
			if (null == expr) {
				// This can happen if, for example, you try to parse ";".
				// Upon reflection I don't think this should be an error. Maybe someone is 
				// compiling a commented out file.
				//LogCompileError(ParseErrorType.NullExpression, "Script parses to no expression.");
				//_parseErrors = null;
				return null;
			}

			StackState state = defaultContext.stack.GetState();

			bool error = false;
			if (createTempScope) {
				if (!defaultContext.stack.PushTerminalScope("<Parse>", defaultContext)) {
					LogCompileError(ParseErrorType.StackOverflow, "_Parse: stack overflow");
					error = true;
				}
			}

			if (!error)
				expr.TypeCheck(defaultContext, ref error);

			if (verbose && !error)
				Log(expr.MyToString(""));

			// Restore state always. Parsing should never permanently affect the stack.
			defaultContext.stack.RestoreState(state);
			defaultContext.control.Clear();

			if (error) {
				_parseErrors = null;
				return null;
			}

			_parseErrors = null;
			return expr;
		}

		/* Evaluates an IExpr.

		The return value is either the successful return value (bool, etc), or a RuntimeErrorInst.
		(Unlike compile errors which can be multiple, evaluation stops as soon as it encounters
		a single error.)
		*/
		public object EvaluateExpression(IExpr expr) {
			return _EvaluateExpression(expr, true);
		}

		private object _EvaluateExpression(IExpr expr, bool createTempScope) {
			object res = null;

			if (null == expr) {
				LogError("Cannot evaluate null expression.");
				return null;
			}

			StackState stackState = defaultContext.stack.GetState();

			if (createTempScope) {
				if (!defaultContext.stack.PushTerminalScope("<Evaluate>", defaultContext)) {
					LogError("_EvaluateExpression: stack overflow");
					return null;
				}
			}

			res = expr.Evaluate(defaultContext);

			if (defaultContext.IsRuntimeErrorSet()) {
				defaultContext.stack.RestoreState(stackState);
				if (logCompileErrors)
					LogError(defaultContext.GetRuntimeErrorString());
				res = defaultContext.control.runtimeError;
				defaultContext.control.Clear();
			} else if (createTempScope) {
				defaultContext.stack.RestoreState(stackState);
				defaultContext.control.Clear();
			}

			return res;
		}

		// Creates a string "name" from the given script for use in error messages.
		private string _GetScriptNameFromScript(string s) {
			string scriptName;
			if (s.Length < 18)
				scriptName = "\"" + s + "\"";
			else
				scriptName = "\"" + s.Substring(0, 15) + "...\"";
			return scriptName;
		}

		// ****************************************************************
		// *** Test Functions
		// ****************************************************************

		// Returns true if the script executes successfully and returns expectedValue.
		// If expectedValue is null, then the return value is ignored and simply returns
		// true if there are no other errors.
		public bool RunTest(string script, object expectedValue, bool verbose = false) {
			if (verbose) Log("-> " + script);

			List<ParseErrorInst> errors = new List<ParseErrorInst>();
			IExpr expr = _Parse(_GetScriptNameFromScript(script), script, ref errors, verbose, false);
			if (errors.Count > 0) {
				if (!verbose) Log("-> " + script);
				if (verbose) 
					if (null == expr)
						LogError("Parse failed.");
					else
						LogError("Parse = " + expr.MyToString(""));
				return false;
			}

			object actualValue = null;
			try {
				if (null != expr)
					actualValue = _EvaluateExpression(expr, false);
				else
					actualValue = null;
			} catch (Exception e) {
				if (!verbose) Log("-> " + script);
				LogError(script + "\n Unhandled exception evaluating script: " + e.ToString());
				LogError("Parse = " + expr.MyToString(""));
				return false;
			}

			if (actualValue is RuntimeErrorInst) {
				LogError("Parse = " + expr.MyToString(""));
				return false;
			}

			if (expectedValue is int)
				expectedValue = Convert.ToDouble(expectedValue);

			if (actualValue == null) {
				if (expectedValue == null)
					return true;

				if (!verbose) Log("-> " + script);
				LogError("Parse = " + (null == expr ? "null" : expr.MyToString("")));
				LogError("Test result is null.");
				return false;
			} else if (actualValue is Exception) {
				if (!verbose) Log("-> " + script);
				LogError("Parse = " + expr.MyToString(""));
				LogError("Test return exception: "+actualValue.ToString());
				return false;
			}

			if (expectedValue == null)
				return true;

			if (expectedValue is double) {
				if (!(actualValue is double)) {
					if (!verbose) Log("-> " + script);
					LogError("Parse = " + expr.MyToString(""));
					LogError("Test result type (" + actualValue.GetType() + ") is not the expected double.");
					return false;
				} else if ((double)expectedValue != (double)actualValue) {
					if (!verbose) Log("-> " + script);
					LogError("Parse = " + expr.MyToString(""));
					LogError("Test result number (" + (double)actualValue + ") doesn't match expected result (" + (double)expectedValue + ").");
					return false;
				}
			} else if (expectedValue is string) {
				if ((string)expectedValue != (string)actualValue) {
					if (!verbose) Log("-> " + script);
					LogError("Parse = " + expr.MyToString(""));
					LogError("Test result string (" + (string)actualValue + ") doesn't match expected result (" + (string)expectedValue + ").");
					return false;
				}
			} else if (expectedValue is bool) {
				if ((bool)expectedValue != (bool)actualValue) {
					if (!verbose) Log("-> " + script);
					LogError("Parse = " + expr.MyToString(""));
					LogError("Test result bool (" + (bool)actualValue + ") doesn't match expected result (" + (bool)expectedValue + ").");
					return false;
				}
			} else {
				if (!verbose) Log("-> " + script);
				LogError("Parse = " + expr.MyToString(""));
				LogError("Test result returned unrecognized value type (" + expectedValue.GetType().ToString() + ")");
				return false;
			}

			return true;
		}

		// Returns true iff the script fails to compile and the first error is of the given type.
		public bool RunCompileFailTest(string script, ParseErrorType errorType, bool verbose = false) {
			if (verbose) Log("-> " + script);

			List<ParseErrorInst> errors = new List<ParseErrorInst>();
			object res = _Parse(_GetScriptNameFromScript(script), script, ref errors, false, false);
			if (null != res && errors.Count == 0) {
				if (!verbose) LogError("-> " + script);
				LogError("ERROR: Expected 'error' script to fail to compile.");
				return false;
			}
			if (errors.Count > 0 && ParseErrorType.Any != errorType && errors[0].type != errorType) {
				if (!verbose) LogError("-> " + script);
				LogError("ERROR: 'error' script returned error (" + errors[0].type + "), expected (" + errorType + ").");
				return false;
			}

			return true;
		}

		// Returns true iff the script compiles but generates the given runtime error.
		public bool RunRuntimeFailTest(string script, RuntimeErrorType errorType, bool verbose = false) {
			if (verbose) Log("-> " + script);

			List<ParseErrorInst> errors = new List<ParseErrorInst>();
			IExpr expr = _Parse(_GetScriptNameFromScript(script), script, ref errors, verbose, false);
			if (null == expr) {
				if (!verbose) LogError("-> " + script);
				LogError("Expected 'execution fail' script to compile: '" + script + "'");
				return false;
			}

			object result = _EvaluateExpression(expr, false);
			RuntimeErrorInst errResult = result as RuntimeErrorInst;
			if (null == errResult) {
				if (!verbose) LogError("-> " + script);
				LogError("Parse = " + expr);
				LogError("Expected 'execution fail' script to throw an error on execution, instead returned " + ((null != result) ? result.GetType().ToString() : "null"));
				if (result is Exception)
					LogError(((Exception)result).ToString());
				return false;
			} else if (errResult.type != errorType) {
				if (!verbose) LogError("-> " + script);
				LogError("Parse = " + expr);
				LogError("Expected 'execution fail' script to throw a (" + errorType + ") error on execution, instead returned " + errResult.type);
				return false;
			}

			return true;
		}	
	}

}
