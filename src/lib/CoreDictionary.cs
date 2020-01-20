/*
Implementation of Pebble's built-in Dictionary<K,V> type.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class PebbleDictionary : ClassValue {
		public Dictionary<object, Variable> dictionary;
		public int enumeratingCount = 0;

		public PebbleDictionary() {
			dictionary = new Dictionary<object, Variable>();
		}
	}

	public class CoreDictionary {

		public static void Register(Engine engine) {

			//@ class Dictionary<K, V>
			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("Dictionary", new ArgList { IntrinsicTypeDefs.TEMPLATE_0, IntrinsicTypeDefs.TEMPLATE_1 }, false);

			ClassDef classDef = engine.defaultContext.CreateClass("Dictionary", ourType, null, new List<string> { "K", "V" });
			classDef.childAllocator = () => {
				return new PebbleDictionary();
			};
			classDef.Initialize();

			//@ Dictionary<K, V> Add(K key, V value)
			//  Adds a new element to the dictionary.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object key = args[0];
					object value = args[1];

					PebbleDictionary scope = thisScope as PebbleDictionary;
					if (scope.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "Add: Attempt to modify a dictionary that is being enumerated by a foreach loop.");
						return null;
					}

					var dictionary = scope.dictionary;
					var dictionaryType = (TypeDef_Class)scope.classDef.typeDef;
					var valueType = dictionaryType.genericTypes[1];
					if (dictionary.ContainsKey(key)) {
						context.SetRuntimeError(RuntimeErrorType.KeyAlreadyExists, "Dictionary already contains key '" + key + "'.");
						return null;
					}

					dictionary.Add(key, new Variable(null, valueType, value));

					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.TEMPLATE_0, IntrinsicTypeDefs.TEMPLATE_1 }, eval, false, ourType);
				classDef.AddMemberLiteral("Add", newValue.valType, newValue);
			}

			//@ Dictionary<K, V> Clear()
			//  Removes all elements from the dictionary.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebbleDictionary pebDict = thisScope as PebbleDictionary;
					var dictionary = pebDict.dictionary;
					if (pebDict.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "Clear: Attempt to modify a dictionary that is being enumerated by a foreach loop.");
						return null;
					}

					dictionary.Clear();
					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("Clear", newValue.valType, newValue);
			}

			//@ bool ContainsKey(K)
			//  Returns true iff the dictionary contains an element with the given key.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object key = args[0];

					PebbleDictionary pebDict = thisScope as PebbleDictionary;
					var dictionary = pebDict.dictionary;

					return dictionary.ContainsKey(key);
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.TEMPLATE_0 }, eval, false, ourType);
				classDef.AddMemberLiteral("ContainsKey", newValue.valType, newValue);
			}

			//@ num Count()
			//  Returns number of elements in the dictionary.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					var dictionary = (thisScope as PebbleDictionary).dictionary;
					return System.Convert.ToDouble(dictionary.Count);
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("Count", newValue.valType, newValue);
			}

			//@ V Get(K)
			//  Returns the value of the element with the given key.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object key = args[0];

					PebbleDictionary pebDict = thisScope as PebbleDictionary;

					// Bounds checking.
					var dictionary = pebDict.dictionary;
					if (!dictionary.ContainsKey(key)) {
						context.SetRuntimeError(RuntimeErrorType.KeyNotFound, "Get: Key '" + key + "' not in dictionary.");
						return null;
					}

					return dictionary[key].value;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.TEMPLATE_1, new ArgList { IntrinsicTypeDefs.TEMPLATE_0 }, eval, false, ourType);
				classDef.AddMemberLiteral("Get", newValue.valType, newValue);
			}

			//@ Dictionary<K, V> Remove(K key)
			//   Removes element with given key.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object key = args[0];

					PebbleDictionary pebDict = thisScope as PebbleDictionary;
					var dictionary = pebDict.dictionary;
					if (pebDict.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "Remove: Attempt to modify a dictionary that is being enumerated by a foreach loop.");
						return null;
					}

					if (!dictionary.ContainsKey(key)) {
						context.SetRuntimeError(RuntimeErrorType.KeyNotFound, "Remove: Key '" + key + "' not in dictionary.");
						return null;
					}

					dictionary.Remove(key);
					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.TEMPLATE_0 }, eval, false, ourType);
				classDef.AddMemberLiteral("Remove", newValue.valType, newValue);
			}

			//@ Dictionary<K, V> Set(K key, V newValue)
			//   Replaces value of existing element with the given key.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					object key = args[0];
					object value = args[1];

					var dictionary = (thisScope as PebbleDictionary).dictionary;

					// Bounds checking.
					if (!dictionary.ContainsKey(key)) {
						context.SetRuntimeError(RuntimeErrorType.KeyNotFound, "Get: Key '" + key + "' not in dictionary.");
						return null;
					}

					dictionary[key].value = value;

					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.TEMPLATE_0, IntrinsicTypeDefs.TEMPLATE_1 }, eval, false, ourType);
				classDef.AddMemberLiteral("Set", newValue.valType, newValue);
			}

			//@ string ThisToScript(string prefix)
			//   ThisToScript is used by Serialize. A classes' ThisToScript function should return code which can rebuild the class.
			//   Note that it's only the content of the class, not the "new A" part. ie., it's the code that goes in the defstructor.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string result = "";
					string prefix = (string)args[0] + "\t";

					var dictionary = (thisScope as PebbleDictionary).dictionary;
					//bool first = true;
					foreach (KeyValuePair<object, Variable> kvp in dictionary) {
						result += prefix + "Add(" + CoreLib.ValueToScript(context, kvp.Key, prefix + "\t", false) + ", " + CoreLib.ValueToScript(context, kvp.Value.value, prefix + "\t", false) + ");\n";
					}

					return result;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING }, eval, false, ourType);
				classDef.AddMemberLiteral("ThisToScript", newValue.valType, newValue);
			}

			//@ string ToString()
			//   Returns a human readable version of at least the first few elements of the dictionary.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					var dictionary = (thisScope as PebbleDictionary).dictionary;

					string result = "Dictionary(" + dictionary.Count + ")[";
					int count = 0;
					foreach (KeyValuePair<object, Variable> kvp in dictionary) {
						if (count != 0)
							result += ", ";
						result += "(" + CoreLib.ValueToString(context, kvp.Key, true) + ", " + CoreLib.ValueToString(context, kvp.Value.value, true) + ")";

						if (++count >= 4)
							break;
					}
					if (dictionary.Count > 4)
						result += ", ...";
					return result + "]";
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("ThisToString", newValue.valType, newValue);
			}

			bool error = false;
			classDef.FinalizeClass(engine.defaultContext);
			Pb.Assert(!error);
		}

		public static bool RunTests(Engine engine, bool verbose) {
			bool result = true;

			engine.Log("\n*** CoreDictionary: Running tests...");

			result &= engine.RunTest("Dictionary<string, num> dsn = new Dictionary<string, num>;", null, verbose);

			// Add, Count
			result &= engine.RunTest("dsn.Clear(); dsn.Add(\"A\", 1); dsn.Add(\"B\", 2).Add(\"C\", 3); dsn.Count();", 3, verbose);
			// ContainsKey
			result &= engine.RunTest("dsn.Clear(); dsn.Add(\"A\", 1); dsn.ContainsKey(\"A\") && !dsn.ContainsKey(\"B\");", true, verbose);
			// Get
			result &= engine.RunTest("dsn.Clear(); dsn.Add(\"A\", 1).Get(\"A\");", 1, verbose);
			// Set
			result &= engine.RunTest("dsn.Clear(); dsn.Add(\"A\", 1).Set(\"A\", 100).Add(\"B\", 2); 100 == dsn.Get(\"A\") && 2 == dsn.Get(\"B\");", true, verbose);
			// Remove
			result &= engine.RunTest("dsn.Clear(); dsn.Add(\"A\", 1).Add(\"B\", 2).Remove(\"A\"); dsn.Count();", 1, verbose);
			// Clear
			result &= engine.RunTest("dsn.Clear().Count();", 0, verbose);
			//TODO? ToString
			//TODO? ThisToScript

			bool sav = engine.logCompileErrors;
			engine.logCompileErrors = false;

			
			result &= engine.RunCompileFailTest("dsn.Add(null, -1);", ParseErrorType.TypeMismatch, verbose);
			result &= engine.RunCompileFailTest("dsn.Get(null);", ParseErrorType.TypeMismatch, verbose);
			result &= engine.RunCompileFailTest("dsn.ContainsKey(null);", ParseErrorType.TypeMismatch, verbose);

			result &= engine.RunRuntimeFailTest("dsn.Clear(); dsn.Add(\"A\", 1); dsn.Add(\"A\", 2);", RuntimeErrorType.KeyAlreadyExists, verbose);
			result &= engine.RunRuntimeFailTest("dsn.Get(\"B\");", RuntimeErrorType.KeyNotFound, verbose);
			result &= engine.RunRuntimeFailTest("dsn.Set(\"B\", 2);", RuntimeErrorType.KeyNotFound, verbose);
			result &= engine.RunRuntimeFailTest("dsn.Remove(\"B\");", RuntimeErrorType.KeyNotFound, verbose);

			// Test that functions are marked as class functions by calling them within defstructor.
			result &= engine.RunTest("{ Dictionary<string, num> dsn2 = new Dictionary<string, num> { Add(\"hello\", 10); }; dsn2.Get(\"hello\"); }", 10, verbose);

			engine.logCompileErrors = sav;
			engine.Log("*** CoreDictionary: Tests " + (result ? "succeeded" : "FAILED"));

			return result;
		}
	}
}
	