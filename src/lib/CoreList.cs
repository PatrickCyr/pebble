/*
Implementation of Pebble's built-in List<V> type.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {
	public class PebbleList : ClassValue {
		public List<Variable> list = new List<Variable>();
		public int enumeratingCount = 0;

		private static ClassDef _listClassDef = null;

		public static PebbleList AllocateListString(ExecContext context, string debugName = "(List<string> inst)") {
			if (null == _listClassDef)
				_listClassDef = context.GetClass("List<string>");
			PebbleList listinst = (PebbleList) _listClassDef.childAllocator();
			listinst.classDef = _listClassDef;
			listinst.debugName = debugName;
			return listinst;
		}

		// Todo - Could this be generic without PebbleList being generic?
		public List<string> GetNativeList() {
			List<string> result = new List<string>();
			foreach (Variable v in list) {
				result.Add((string)v.value);
			}
			return result;
		}
	}

	public class CoreList {

		public static void Register(Engine engine) {

			//@ class List<T>
			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("List", new ArgList { IntrinsicTypeDefs.TEMPLATE_0 }, false);
			ClassDef classDef = engine.defaultContext.CreateClass("List", ourType, null, new List<string> { "T" });
			classDef.childAllocator = () => {
				return new PebbleList();
			};
			classDef.Initialize();

			//@ List<T> Add(T newValue, ...) or List<T> Push(T newValue, ...)
			//   Adds one or more elements to the end of the list.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {

					PebbleList scope = thisScope as PebbleList;
					if (scope.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "Add: Attempt to modify a list that is being enumerated by a foreach loop.");
						return null;
					}

					var list = scope.list;
					var listType = (TypeDef_Class)scope.classDef.typeDef;
					var elementType = listType.genericTypes[0];
					for (int ii = 0; ii < args.Count; ++ii) {
						object ret = args[ii];
						list.Add(new Variable(null, elementType, ret));
					}

					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.TEMPLATE_0 }, eval, true, ourType);
				classDef.AddMemberLiteral("Add", newValue.valType, newValue);
				classDef.AddMemberLiteral("Push", newValue.valType, newValue);
			}

			//@ List<T> Clear()
			//   Removes all elements from the list.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebbleList pebList = thisScope as PebbleList;
					if (pebList.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "Clear: Attempt to modify a list that is being enumerated by a foreach loop.");
						return null;
					}

					var list = pebList.list;
					list.Clear();
					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("Clear", newValue.valType, newValue);
			}

			//@ num Count()
			//   Returns the number of elements in the list.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					var list = (thisScope as PebbleList).list;
					return System.Convert.ToDouble(list.Count);
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("Count", newValue.valType, newValue);
			}

			//@ T Get(num index)
			//   Returns the value of the element of the list at the given index.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double dix = (double)args[0];
					int ix = (int)dix;

					var list = (thisScope as PebbleList).list;

					// Bounds checking.
					if (ix < 0 || ix >= list.Count) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "Get: Index " + ix + " out of bounds of array of length " + list.Count + ".");
						return null;
					}

					return list[ix].value;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.TEMPLATE_0, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("Get", newValue.valType, newValue);
			}

			//@ List<T> Insert(num index, T item)
			//   Inserts a new element into the list at the given index. Existing elements at and after the given index are pushed further down the list.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {

					PebbleList scope = thisScope as PebbleList;
					if (scope.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "Insert: Attempt to modify a list that is being enumerated by a foreach loop.");
						return null;
					}

					var list = scope.list;
					var listType = (TypeDef_Class)scope.classDef.typeDef;
					var elementType = listType.genericTypes[0];
					var indexDouble = (double)args[0];
					var item = args[1];
					var index = Convert.ToInt32(indexDouble);
					if (index < 0 || index > list.Count) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "Insert: array index out of bounds.");
						return null;
					}

					list.Insert(index, new Variable(null, elementType, item));

					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.TEMPLATE_0 }, eval, false, ourType);
				classDef.AddMemberLiteral("Insert", newValue.valType, newValue);
			}

			//@ T Pop()
			//   Returns the value of the last element of the list and removes it from the list.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebbleList pebList = thisScope as PebbleList;
					if (pebList.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "Pop: Attempt to remove an element from a list that is being enumerated in a foreach loop.");
						return null;
					}

					var list = pebList.list;
					int ix = list.Count - 1;
					if (ix < 0) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "Pop: List is empty.");
						return null;
					}

					var result = list[ix].value;
					list.RemoveAt(ix);
					return result;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.TEMPLATE_0, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("Pop", newValue.valType, newValue);
			}

			//@ List<T> RemoveAt(num index)
			//   Removes element at the given index, and returns the list.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double dix = (double)args[0];
					int ix = (int)dix;

					PebbleList pebList = thisScope as PebbleList;
					if (pebList.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "RemoveAt: Attempt to modify a list that is being enumerated by a foreach loop.");
						return null;
					}

					var list = pebList.list;
					if (ix < 0 || ix >= list.Count) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "RemoveAt: Index " + ix + " out of bounds of array of length " + list.Count + ".");
						return null;
					}

					list.RemoveAt(ix);
					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("RemoveAt", newValue.valType, newValue);
			}

			//@ List<T> RemoveRange(num start, num count)
			//   Removes elements in the given range of indices, and returns the list.
			//   Cannot be used in a foreach loop.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double dstart = (double)args[0];
					int start = (int)dstart;
					double dcount = (double)args[1];
					int count = (int)dcount;

					PebbleList pebList = thisScope as PebbleList;
					if (pebList.enumeratingCount > 0) {
						context.SetRuntimeError(RuntimeErrorType.ForeachModifyingContainer, "RemoveRange: Attempt to modify a list that is being enumerated by a foreach loop.");
						return null;
					}

					var list = pebList.list;
					if (start < 0 || start >= list.Count) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "RemoveRange: Start " + start + " out of bounds of array of length " + list.Count + ".");
						return null;
					}
					if (count < 0) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "RemoveRange: Count (" + count + ") cannot be negative.");
						return null;
					}
					if ((start + count) >= list.Count) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "RemoveRange: Count " + count + " exceeds array length (" + list.Count + ").");
						return null;
					}

					list.RemoveRange(start, count);
					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("RemoveRange", newValue.valType, newValue);
			}


			//@ List<T> Set(num index, T newValue)
			//   Changes the value of the element at the given index, and returns the list.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double dix = (double)args[0];
					int ix = (int)dix;

					object value = args[1];

					var list = (thisScope as PebbleList).list;

					// Bounds checking.
					if (ix < 0 || ix >= list.Count) {
						context.SetRuntimeError(RuntimeErrorType.ArrayIndexOutOfBounds, "Set: Index " + ix + " out of bounds of array of length " + list.Count + ".");
						return null;
					}

					list[ix].value = value;

					return thisScope;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.TEMPLATE_0 }, eval, false, ourType);
				classDef.AddMemberLiteral("Set", newValue.valType, newValue);
			}

			//@ List<T> Sort(functype<num(T, T>)> comparator)
			//   Sorts the list using the given comparator function. 
			//   The comparator should behave the same as a C# Comparer. The first argument should be earlier in the
			//   list than the second, return a number < 0. If It should be later, return a number > 0. If their order
			//   is irrelevant, return 0.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					var list = (thisScope as PebbleList).list;

					if (null == args[0]) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Sort: comparator may not be null.");
						return null;
					}

					FunctionValue comparator = (FunctionValue)args[0];

					List<object> argvals = new List<object>();
					argvals.Add(0);
					argvals.Add(0);

					Comparison<Variable> hostComparator = new Comparison<Variable>(
						(a, b) => {

							argvals[0] = a.value;
							argvals[1] = b.value;

							// Note we use null instead of thisScope here. There is no way the sort function could be a 
							// class member because Sort's signature only takes function's whose type has no class.
							double result = (double)comparator.Evaluate(context, argvals, null);
							return Convert.ToInt32(result);
						}
					);

					list.Sort(hostComparator);
					return thisScope;
				};

				TypeDef_Function comparatorType = TypeFactory.GetTypeDef_Function(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.TEMPLATE_0, IntrinsicTypeDefs.TEMPLATE_0 }, -1, false, null, false, false);

				FunctionValue_Host newValue = new FunctionValue_Host(ourType, new ArgList { comparatorType }, eval, false, ourType);
				classDef.AddMemberLiteral("Sort", newValue.valType, newValue);
			}

			//@ string ThisToScript(string prefix)
			//   ThisToScript is used by Serialize. A classes' ThisToScript function should return code which can rebuild the class.
			//   Note that it's only the content of the class, not the "new A" part. ie., it's the code that goes in the defstructor.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string result = "";
					string prefix = (string)args[0] + "\t";

					var list = (thisScope as PebbleList).list;
					for (int ii = 0; ii < list.Count; ++ii) {
						result += prefix + "Add(" + CoreLib.ValueToScript(context, list[ii].value, prefix + "\t", false) + ");\n";
					}

					return result;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING }, eval, false, ourType);
				classDef.AddMemberLiteral("ThisToScript", newValue.valType, newValue);
			}

			//@ string ToString()
			//   Returns a string representation of at least the first few elements of the list.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					
					var list = (thisScope as PebbleList).list;
					string result = "List(" + list.Count + ")[";
					for (int ii = 0; ii < Math.Min(4, list.Count); ++ii) {
						if (ii > 0)
							result += ", ";
						result += CoreLib.ValueToString(context, list[ii].value, true);
					}
					if (list.Count > 4)
						result += ", ...";
					return result + "]";
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("ThisToString", newValue.valType, newValue);
			}

			classDef.FinalizeClass(engine.defaultContext);
		}

		public static bool RunTests(Engine engine, bool verbose) {
			bool result = true;

			engine.Log("\n*** CoreList: Running tests...");

			// Add/Push
			result &= engine.RunTest("{ List<num> testln = new List<num>; 100 == testln.Add(100).Get(0) && 1 == testln.Count(); }", true, verbose);
			result &= engine.RunTest("{ List<num> testln = new List<num>; testln.Add(1000, 2000, 3000, 4000, 5000); 5 == testln.Count() && 3000 == testln[2]; };", true, verbose);
			// Clear
			result &= engine.RunTest("{ List<num> testln = new List<num>; testln.Add(1000).Add(2000); 0 == testln.Clear().Count(); }", true, verbose);
			// Count
			// Get
			// Insert
			result &= engine.RunTest("{ List<num> testln = new List<num>; testln.Insert(0, 2000).Insert(0, 1000); 1000 == testln.Get(0) && 2000 == testln.Get(1) && 2 == testln.Count(); }", true, verbose);
			// Pop
			result &= engine.RunTest("{ List<num> testln = new List<num>; testln.Add(1000).Add(2000); 2000 == testln.Pop() && 1000 == testln.Get(0) && 1 == testln.Count(); }", true, verbose);
			// Set
			result &= engine.RunTest("{ List<num> testln = new List<num>; testln.Add(1000).Add(2000); testln.Set(0, 1).Set(1, 2); 1 == testln.Get(0) && 2 == testln.Get(1) && 2 == testln.Count(); }", true, verbose);
			// RemoveAt
			result &= engine.RunTest("{ List<num> testln = new List<num>; testln.Add(1000).Add(2000); 2000 == testln.RemoveAt(0).Get(0) && 1 == testln.Count(); }", true, verbose);
			// RemoveRange
			result &= engine.RunTest("{ List<num> testln = new List<num>; testln.Add(1000).Add(2000).Add(3000).Add(4000); 4000 == testln.RemoveRange(1, 2)[1] && 2 == #testln; }", true, verbose);
			// Sort
			result &= engine.RunTest(@"{
				// Note: This works because the String static functions don't have class type, because they don't need to.
				functype<num(string, string)> alphaComp = String::CompareTo;
				List<string> ls = new List<string>;
				ls.Add(""cat"").Add(""dog"").Add(""bat"").Add(""ant"");
				ls.Sort(alphaComp); 
				ls[0] == ""ant"" && ls[1] == ""bat"" && ls[2] == ""cat"" && ls[3] == ""dog"" && ls.Count() == 4; 
			}", true, verbose);
			//TODO? ToString
			//TODO? ThisToScript

			// Make sure we can call class functions from within defstructor. This requires that the functions are marked as belonging to the class.
			result &= engine.RunTest("{ List<num> testln = new List<num> { Add(10); Clear(); Add(20).Add(30); }; testln.Get(0); }", 20, verbose);

			bool sav = engine.logCompileErrors;
			engine.logCompileErrors = false;

			result &= engine.RunCompileFailTest("{ List<num> testln = new List<num>; testln.Add(); }", ParseErrorType.ArgCountMismatch, verbose);

			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.Clear(); testln.Get(0); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.Insert(-1, 0); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.Insert(1, 0); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.Pop(); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.Set(0, 100); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.Set(-1, 100); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.RemoveAt(0); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.RemoveAt(-1); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num> { Add(0); }; testln.RemoveRange(0, 100); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num> { Add(0); }; testln.RemoveRange(0, -1); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num> { Add(0); }; testln.RemoveRange(1, 0); }", RuntimeErrorType.ArrayIndexOutOfBounds, verbose);
			result &= engine.RunRuntimeFailTest("{ List<num> testln = new List<num>; testln.Sort(null); }", RuntimeErrorType.ArgumentInvalid, verbose);

			engine.logCompileErrors = sav;
			engine.Log("*** CoreList: Tests " + (result ? "succeeded" : "FAILED"));

			return result;
		}
	}
}
	