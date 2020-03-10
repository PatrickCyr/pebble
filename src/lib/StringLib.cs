/*
The String library.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class StringLib {

		private readonly static string[] _defaultSeparators = new string[] { "\n" };

		public static void Register(Engine engine) {

			//@ class String

			// Because the type has the ! in it, users cannot attempt to use the type at all.
			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("String", null, false);
			ClassDef classDef = engine.defaultContext.CreateClass("String", ourType, null, null, true);
			classDef.Initialize();

			//@ static num CompareTo(string, string)
			//   Wraps C# CompareTo function, which essentially returns a number < 0 if a comes before b 
			//   alphabetically, > 0 if a comes after b, and 0 if they are identical.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string b = (string)args[1];
					return Convert.ToDouble(a.CompareTo(b));
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("CompareTo", newValue.valType, newValue, true);
			}

			//@ static string Concat(any[, ...])
			//   Converts all arguments to strings and concatenates them. Same as ToString.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					return CoreLib.StandardPrintFunction(context, args);
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.ANY }, eval, true);
				classDef.AddMemberLiteral("Concat", newValue.valType, newValue, true);
			}

			//@ static bool EndsWith(string s, string search)
			//   Wrapper for C# EndsWith. Returns true if s ends with search.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string b = (string)args[1];
					return a.EndsWith(b);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("EndsWith", newValue.valType, newValue, true);
			}

			//@ static bool Equals(string, string)
			//   Returns true iff the strings are exactly equal. The same thing as using the == operator.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string b = (string)args[1];
					return a.Equals(b);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("Equals", newValue.valType, newValue, true);
			}

			//@ static bool EqualsI(string, string)
			//   Returns true if the strings are equal, ignoring case. Equivalent to the ~= operator.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string b = (string)args[1];
					return a.ToLower().Equals(b.ToLower());
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("EqualsI", newValue.valType, newValue, true);
			}

			//@ static string Format(string[, any, ...])
			//   Generated formatted strings. Wrapper for C# String.Format(string, object[]). See documentation of that function for details.
			//   Putting weird things like Lists or functions into the args will produce undefined results.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string source = (string)args[0];
					Object[] a = new Object[args.Count - 1];
					args.CopyTo(1, a, 0, args.Count - 1);
					string result = "";
					try {
						result = String.Format(source, a);
					} catch (Exception e) {
						context.SetRuntimeError(RuntimeErrorType.NativeException, e.ToString());
						return null;
					}
					return result;
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.ANY }, eval, true);
				classDef.AddMemberLiteral("Format", newValue.valType, newValue, true);
			}

			//@ static num IndexOfChar(string toBeSearched, string searchChars)
			//   Returns the index of the first instance of any of the characters in search.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string b = (string)args[1];

					if (0 == a.Length || 0 == b.Length)
						return -1.0;

					int lowestIx = Int32.MaxValue;

					foreach (char c in b) {
						int ix = a.IndexOf(c);
						if (ix >= 0 && ix < lowestIx)
							lowestIx = ix;
					}

					if (lowestIx < Int32.MaxValue)
						return Convert.ToDouble(lowestIx);

					return -1.0;
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("IndexOfChar", newValue.valType, newValue, true);
			}

			//@ static num IndexOfString(string toBeSearched, string searchString)
			//   Returns the index of the first instance of the entire search string.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string b = (string)args[1];

					if (0 == a.Length || 0 == b.Length)
						return -1.0;

					int ix = a.IndexOf(b);
					return Convert.ToDouble(ix);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("IndexOfString", newValue.valType, newValue, true);
			}

			//@ static num Length(string)
			//   Returns the length of the string.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					return Convert.ToDouble(a.Length);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("Length", newValue.valType, newValue, true);
			}

			//@ static string PadLeft(string, num n, string pad)
			//   Returns s with n instances of string pad to the left.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];

					double nd = (double)args[1];
					int n = Convert.ToInt32(nd);

					string p = (string)args[2];
					if (0 == p.Length)
						p = " ";

					char c = p[0];
					return a.PadLeft(n, c);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("PadLeft", newValue.valType, newValue, true);
			}

			//@ static string PadRight(string s, num n, string pad)
			//   Returns s with n instances of string pad to the left.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];

					double nd = (double)args[1];
					int n = Convert.ToInt32(nd);

					string p = (string)args[2];
					if (0 == p.Length)
						p = " ";

					char c = p[0];
					return a.PadRight(n, c);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("PadRight", newValue.valType, newValue, true);
			}

			//@ static string Replace(string str, string find, string replace)
			//   Replaces all instances of the given string with the replacement string.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string find = (string)args[1];
					string replace = (string)args[2];

					if (0 == find.Length) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Find argument to Replace() cannot be the empty string.");
						return null;
					}

					return a.Replace(find, replace);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("Replace", newValue.valType, newValue, true);
			}

			//@ static List<string> Split(string str, List<string> separators = null)
			//   Splits input string into a list of strings given the provided separators.
			//   If no separators are provided, uses the newline character.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];

					string[] splitted;
					if (null == args[1]) {
						splitted = a.Split(_defaultSeparators, StringSplitOptions.None);
					} else {
						PebbleList delimsList = args[1] as PebbleList;
						if (0 == delimsList.list.Count) {
							context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "String::Split : Separators list cannot be empty.");
							return null;
						}

						List<string> dlist = delimsList.GetNativeList();

						// Usually I like to wrap native functions with a try-catch but I couldn't find a
						// way to make this throw an exception.
						splitted = a.Split(dlist.ToArray(), StringSplitOptions.None);
					}

					PebbleList list = PebbleList.AllocateListString(context, "String::Split result");
					foreach (string s in splitted)
						list.list.Add(new Variable(null, IntrinsicTypeDefs.STRING, s));
					return list;
				};

				List<Expr_Literal> defaults = new List<Expr_Literal>();
				defaults.Add(null);
				defaults.Add(new Expr_Literal(null, null, IntrinsicTypeDefs.NULL));
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.LIST_STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.LIST_STRING }, eval, false, null, true, defaults);
				classDef.AddMemberLiteral("Split", newValue.valType, newValue, true);
			}

			//@ static bool StartsWith(string s, string start)
			//   Returns true if s starts with start.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					string b = (string)args[1];
					return a.StartsWith(b);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("StartsWith", newValue.valType, newValue, true);
			}

			//@ static string Substring(string, startIx, length)
			//   Returns a substring of the input, starting at startIx, that is length characters long.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					double b = (double)args[1];
					double c = (double)args[2];
					int start = Convert.ToInt32(b);
					int len = Convert.ToInt32(c);
					if (start < 0 || len < 0) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Numeric arguments to Substring cannot be negative.");
						return null;
					}
					if (start + len > a.Length) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Substring attempting to read past end string.");
						return null;
					}

					return a.Substring(start, len);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Substring", newValue.valType, newValue, true);
			}

			//@ static string SubstringLeft(string str, num length)
			//   Returns the left 'length' characters of str.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					double b = (double)args[1];
					int len = Convert.ToInt32(b);
					if (len < 0) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Numeric arguments to Substring cannot be negative.");
						return null;
					}
					if (len > a.Length) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Substring attempting to read past end string.");
						return null;
					}

					return a.Substring(0, len);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("SubstringLeft", newValue.valType, newValue, true);
			}

			//@ static string SubstringRight(string, start)
			//   Returns the right part of the string starting at 'start'.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					double b = (double)args[1];
					int start = Convert.ToInt32(b);
					if (start < 0) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Numeric arguments to Substring cannot be negative.");
						return null;
					}
					if (start >= a.Length) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Substring attempting to read past end string.");
						return null;
					}

					return a.Substring(a.Length - start);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("SubstringRight", newValue.valType, newValue, true);
			}

			//@ static string ToLower(string)
			//   Converts the string to lowercase.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					return a.ToLower();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("ToLower", newValue.valType, newValue, true);
			}

			//@ static string ToUpper(string)
			//   Converts the string to uppercase.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					return a.ToUpper();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("ToUpper", newValue.valType, newValue, true);
			}

			//@ static string Trim(string)
			//   Removes leading and trailing whitespace characters.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string a = (string)args[0];
					return a.Trim();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING }, eval, false);
				classDef.AddMemberLiteral("Trim", newValue.valType, newValue, true);
			}

			classDef.FinalizeClass(engine.defaultContext);
		}

		public static bool RunTests(Engine engine, bool verbose) {

			bool result = true;

			engine.Log("\n*** StringLib: Running tests...");

			result &= engine.RunTest("String::Concat(\"A\", \"B\");", "AB", verbose);
			result &= engine.RunTest("String::CompareTo(\"B\", \"A\") > 0 && String::CompareTo(\"a\", \"b\") < 0;", true, verbose);
			result &= engine.RunTest("String::EndsWith(\"PoopyButt\", \"Butt\");", true, verbose);
			result &= engine.RunTest("String::EndsWith(\"PoopyButt\", \"BUTT\");", false, verbose);
			result &= engine.RunTest("String::Equals(\"hi\", \"hi\");", true, verbose);
			result &= engine.RunTest("String::Equals(\"HI\", \"hi\");", false, verbose);
			result &= engine.RunTest("String::Equals(\"HI\", \"HIT\");", false, verbose);
			result &= engine.RunTest("String::EqualsI(\"HI\", \"hi\");", true, verbose);
			result &= engine.RunTest("String::EqualsI(\"HI\", \"high\");", false, verbose);
			result &= engine.RunTest("String::Format(\"{0:#.###}\", 4/3);", "1.333", verbose);
			result &= engine.RunTest("String::IndexOfChar(\"Hello, world!\", \" !,\");", 5, verbose);
			result &= engine.RunTest("String::IndexOfChar(\"Hello, world!\", \"z\");", -1, verbose);
			result &= engine.RunTest("String::IndexOfChar(\"Hello, world!\", \"\");", -1, verbose);
			result &= engine.RunTest("String::IndexOfChar(\"\", \"a\");", -1, verbose);
			result &= engine.RunTest("String::IndexOfString(\"Hello, world!\", \"ld\");", 10, verbose);
			result &= engine.RunTest("String::IndexOfString(\"Hello, world!\", \"poo\");", -1, verbose);
			result &= engine.RunTest("String::IndexOfString(\"Hello, world!\", \"\");", -1, verbose);
			result &= engine.RunTest("String::IndexOfString(\"\", \"a\");", -1, verbose);
			result &= engine.RunTest("String::Length(\"\");", 0, verbose);
			result &= engine.RunTest("String::Length(\"pOo 42\");", 6, verbose);
			result &= engine.RunTest("String::PadLeft(\"AAA\", 5, \"B\");", "BBAAA", verbose);
			result &= engine.RunTest("String::PadLeft(\"AAA\", 6.3, \"\");", "   AAA", verbose);
			result &= engine.RunTest("String::PadLeft(\"\", 4, \"BC\");", "BBBB", verbose);
			result &= engine.RunTest("String::PadRight(\"AAA\", 5, \"B\");", "AAABB", verbose);
			result &= engine.RunTest("String::PadRight(\"AAA\", 6.3, \"\");", "AAA   ", verbose);
			result &= engine.RunTest("String::PadRight(\"\", 4, \"BC\");", "BBBB", verbose);
			result &= engine.RunTest("String::Replace(\"hello\", \"l\", \"\");", "heo", verbose);
			result &= engine.RunTest("String::Replace(\"hello\", \"z\", \"pop\");", "hello", verbose);
			result &= engine.RunTest("String::Replace(\"\", \"a\", \"hello\");", "", verbose);
			result &= engine.RunTest("{ List<string> split = String::Split(\"Hello,\\nworld!\"); Print(split); 2 == #split && \"Hello,\" == split[0] && \"world!\" == split[1]; }", true, verbose);
			result &= engine.RunTest("{ List<string> split = String::Split(\"Hello,\\nworld!\", new List<string> { Add(\"\\n\"); }); Print(split); 2 == #split && \"Hello,\" == split[0] && \"world!\" == split[1]; }", true, verbose);
			result &= engine.RunTest("String::StartsWith(\"PoopyButt\", \"Poo\");", true, verbose);
			result &= engine.RunTest("String::StartsWith(\"PoopyButt\", \"poo\");", false, verbose);
			result &= engine.RunTest("String::Substring(\"pOo 42\", 3, 3);", " 42", verbose);
			result &= engine.RunTest("String::Substring(\"pOo 42\", 3, 0);", "", verbose);
			result &= engine.RunTest("String::ToLower(\"pOo 42\");", "poo 42", verbose);
			result &= engine.RunTest("String::ToUpper(\"pOo 42\");", "POO 42", verbose);
			result &= engine.RunTest("String::Trim(\" poo \");", "poo", verbose);
			result &= engine.RunTest("String::Trim(\" p oo\");", "p oo", verbose);
			result &= engine.RunTest("String::Trim(\"po o \");", "po o", verbose);
			result &= engine.RunTest("String::Trim(\"\");", "", verbose);

			bool sav = engine.logCompileErrors;
			engine.logCompileErrors = false;

			result &= engine.RunCompileFailTest("String::Length(null);", ParseErrorType.TypeMismatch, verbose);
			result &= engine.RunCompileFailTest("String::ToLower(null);", ParseErrorType.TypeMismatch, verbose);
			result &= engine.RunCompileFailTest("String::ToUpper(null);", ParseErrorType.TypeMismatch, verbose);

			//seems to work but the breakpoint on exception is annoying 
			//result &= engine.RunRuntimeFailTest("String::Format(\"{1}\", 1);", RuntimeErrorType.NativeException, verbose);
			result &= engine.RunRuntimeFailTest("String::Replace(\"hello\", \"\", \"pop\");", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("String::Split(\"hello\", new List<string>);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("String::Substring(\"hello\", -1 , 0);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("String::Substring(\"hello\", 0 , -1);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("String::Substring(\"hello\", 10 , 1);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("String::Substring(\"hello\", 0 , 10);", RuntimeErrorType.ArgumentInvalid, verbose);

			engine.logCompileErrors = sav;
			engine.Log("*** StringLib: Tests " + (result ? "succeeded" : "FAILED"));

			return result;
		}
	}
}