/*
See Copyright Notice in LICENSE.TXT

This library is automatically registered (see CoreLib.cs). Programs don't need to register it manually.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class RegexLib {

		public static ClassDef regexGroupClassDef = null;
		public static ClassDef listRegexGroupClassDef = null;
		public static ClassDef regexMatchClassDef = null;
		public static ClassDef listMatchClassDef = null;

		public static void Register(Engine engine) {

			//@ class RegexGroup
			//   Stores information about a Regex group match. Basically a wrapper for System.Text.RegularExpressions.Group.
			{
				TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("RegexGroup", null, false);
				ClassDef classDef = engine.defaultContext.CreateClass("RegexGroup", ourType, null, null);
				classDef.Initialize();

				//@ const num index;
				//   The index of the character at the start of this match.
				classDef.AddMember("index", IntrinsicTypeDefs.CONST_NUMBER);
				//@ const num length;
				//   The length of the substring of this match.
				classDef.AddMember("length", IntrinsicTypeDefs.CONST_NUMBER);
				//@ const string value;
				//   The substring of this match.
				classDef.AddMember("value", IntrinsicTypeDefs.CONST_STRING);

				classDef.FinalizeClass(engine.defaultContext);
				regexGroupClassDef = classDef;
			}

			// Make sure the List<RegexGroup> type is registered.
			listRegexGroupClassDef = engine.defaultContext.RegisterIfUnregisteredList(regexGroupClassDef.typeDef);

			//@ class RegexMatch
			//   Stores information about a single Regex substring match. Basically a wrapper for System.Text.RegularExpressions.Match.
			{
				TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("RegexMatch", null, false);
				ClassDef classDef = engine.defaultContext.CreateClass("RegexMatch", ourType, null, null);
				classDef.Initialize();

				//@ const num index;
				//   The index of the character at the start of this match.
				classDef.AddMember("index", IntrinsicTypeDefs.CONST_NUMBER);
				//@ const num length;
				//   The length of the substring of this match.
				classDef.AddMember("length", IntrinsicTypeDefs.CONST_NUMBER);
				//@ const string value;
				//   The substring of this match.
				classDef.AddMember("value", IntrinsicTypeDefs.CONST_STRING);
				//@ List<RegexGroup> groups;
				//   The regex groups of this match. If there are no groups this will be null.
				classDef.AddMember("groups", listRegexGroupClassDef.typeDef);

				classDef.FinalizeClass(engine.defaultContext);
				regexMatchClassDef = classDef;
			}

			// ****************************************************************

			// Make sure the List<string> type is registered.
			ClassDef listStringClassDef = engine.defaultContext.RegisterIfUnregisteredList(IntrinsicTypeDefs.STRING);

			// Make sure List<RegexMatch> is registered.
			listMatchClassDef = engine.defaultContext.RegisterIfUnregisteredList(regexMatchClassDef.typeDef);


			// ****************************************************************

			//@ class Regex
			//    Provides static functions that implement regular expression matching for strings. Basically a wrapper for System.Text.RegularExpressions.Regex.
			{
				TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("Regex", null, false);
				ClassDef classDef = engine.defaultContext.CreateClass("Regex", ourType, null, null, true, true);
				classDef.Initialize();

				// ***


				//@ static bool IsMatch(string input, string expression)
				//   Returns true if input matches the given regular expression.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						string a = (string)args[0];
						string b = (string)args[1];
						return Regex.IsMatch(a, b);
					};

					FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
					classDef.AddMemberLiteral("IsMatch", newValue.valType, newValue, true);
				}

				//@ static RegexMatch Match(string input, string pattern);
				//	  Returns the first match of the given pattern in the input string, or null if no match.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						string input = (string)args[0];
						string pattern = (string)args[1];
						Match match = Regex.Match(input, pattern);

						if (match.Success) {
							ClassValue matchInst = regexMatchClassDef.Allocate(context);
							Variable index = matchInst.GetByName("index");
							Variable length = matchInst.GetByName("length");
							Variable value = matchInst.GetByName("value");
							index.value = Convert.ToDouble(match.Index);
							length.value = Convert.ToDouble(match.Length);
							value.value = match.Value;

							return matchInst;
						}

						return null;
					};

					FunctionValue newValue = new FunctionValue_Host(regexMatchClassDef.typeDef, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
					classDef.AddMemberLiteral("Match", newValue.valType, newValue, true);
				}

				//@ static List<RegexMatch> Matches(string input, string pattern);
				//    Returns a list of all the matches of the given regular expression in the input string, or null if no matches found.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						string input = (string)args[0];
						string pattern = (string)args[1];
						MatchCollection matchCol = Regex.Matches(input, pattern);
						if (0 == matchCol.Count)
							return null;

						ClassValue matchListInst = listMatchClassDef.Allocate(context);
						PebbleList pebbleList = matchListInst as PebbleList;

						foreach (Match match in matchCol) { 
							ClassValue matchInst = regexMatchClassDef.Allocate(context);
							Variable index = matchInst.GetByName("index");
							Variable length = matchInst.GetByName("length");
							Variable value = matchInst.GetByName("value");
							index.value = Convert.ToDouble(match.Index);
							length.value = Convert.ToDouble(match.Length);
							value.value = match.Value;

							// Note: In this C# regex library, 0 is always the default group (it is the whole match).
							// That doesn't seem to be a regex standard, and it's entirely rendundant, so I'm only 
							// taking the non-default groups. match.groups is 0 when there are no non-default groups.
							if (match.Groups.Count > 1) {
								ClassValue groupListInst = listRegexGroupClassDef.Allocate(context);
								PebbleList groupList = groupListInst as PebbleList;
								matchInst.GetByName("groups").value = groupListInst;

								for (int ii = 1; ii < match.Groups.Count; ++ii) {
									Group group = match.Groups[ii];

									ClassValue groupInst = regexGroupClassDef.Allocate(context);
									groupInst.GetByName("index").value = Convert.ToDouble(group.Index);
									groupInst.GetByName("length").value = Convert.ToDouble(group.Length);
									groupInst.GetByName("value").value = group.Value;

									groupList.list.Add(new Variable("(made my Regex::Matches)", groupInst.classDef.typeDef, groupInst));
								}
							}

							pebbleList.list.Add(new Variable("(Regex::Matches)", regexMatchClassDef.typeDef, matchInst));
						}

						return matchListInst;
					};

					FunctionValue newValue = new FunctionValue_Host(listMatchClassDef.typeDef, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
					classDef.AddMemberLiteral("Matches", newValue.valType, newValue, true);
				}

				//@ static string Replace(string input, string pattern, string replacement);
				//    Replace any matches of the given pattern in the input string with the replacement string.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						string input = (string)args[0];
						string pattern = (string)args[1];
						string replacement = (string)args[2];
						return Regex.Replace(input, pattern, replacement);
					};

					FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
					classDef.AddMemberLiteral("Replace", newValue.valType, newValue, true);
				}

				//@ static List<string> Split(string input, string pattern);
				//    Splits an input string into an array of substrings at the positions defined by a regular expression match.
				{
					FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
						string input = (string)args[0];
						string pattern = (string)args[1];
						string[] splitArray = Regex.Split(input, pattern);

						ClassValue inst = listStringClassDef.Allocate(context);
						inst.debugName = "(Regex::Split result)";

						PebbleList list = (PebbleList)inst;
						foreach (string str in splitArray)
							list.list.Add(new Variable(null, IntrinsicTypeDefs.STRING, str));

						return inst;
					};

					FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.LIST_STRING, new ArgList { IntrinsicTypeDefs.STRING, IntrinsicTypeDefs.STRING }, eval, false);
					classDef.AddMemberLiteral("Split", newValue.valType, newValue, true);
				}

				// ***

				classDef.FinalizeClass(engine.defaultContext);
			}

			UnitTests.testFuncDelegates.Add("RegexLib", RunTests);
		}

		public static bool RunTests(Engine engine, bool verbose) {

			bool result = true;

			// All the tests are in a unittests pebble file. Just so much easier.
			/*
			engine.Log("\n*** StringLib: Running tests...");

			result &= engine.RunTest("String::Concat(\"A\", \"B\");", "AB", verbose);

			bool sav = engine.logCompileErrors;
			engine.logCompileErrors = false;

			result &= engine.RunCompileFailTest("String::Length(null);", ParseErrorType.TypeMismatch, verbose);

			result &= engine.RunRuntimeFailTest("String::LastIndexOfChar(\"hello\", \"e\", 100);", RuntimeErrorType.ArgumentInvalid, verbose);

			engine.logCompileErrors = sav;
			engine.Log("*** StringLib: Tests " + (result ? "succeeded" : "FAILED"));
			*/

			return result;
		}
	}
}