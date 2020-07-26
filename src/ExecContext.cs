/*
ExecContext holds all of the engine's variables: the defined types and classes, the stack, etc.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

namespace Pebble {

	/**
	 * Holds all the variable parts of the engine: globals, the stack, registered types and classes, and control flags.
	 * The idea was that the same engine could have multiple contexts and run scripts on them independently, though
	 * I've never found any reason to do that.
	 * TODO: Should it hold TypeFactory as well? Probably.
	 */
	public class ExecContext {

		// Used to save state while compiling so we can undo any changes that may happen if there's an error.
		// Note that the TypeLibrary is not reverted. I spent some time trying to come up with a scenario
		// where that would become a problem but I came up empty. Not positive there isn't a way to do it, just 
		// don't know what it could be.
		private class PreCompileState {
			public StackState stackState;
		};
		private PreCompileState _preCompileState;

		public readonly Engine engine;

		// This contains globals & the stack. 
		// Currently there is no heap: we utilize C#'s memory manager for our heap.
		public VariableStack stack = new VariableStack();

		// This is where "type variables" are saved.  They work much like regular variables,
		// but are syntatically specified very differently, so we need to store them separately.
		// User defined types (classes) are stored here as well.
		private BufferedDictionary<string, ITypeDef> _types = new BufferedDictionary<string, ITypeDef>();

		// This is where we store the user-defined classes. Note that the class's *type* will
		// also be in _types.
		private BufferedDictionary<string, ClassDef> _classes = new BufferedDictionary<string, ClassDef>();

		// This holds information about control elements, like continue, break, and return.
		public ControlInfo control = new ControlInfo();

		// **************************************************************************
		// System Methods
		// **************************************************************************

		public ExecContext(Engine engineIn) {
			engine = engineIn;
			_types.Add("bool", IntrinsicTypeDefs.BOOL);
			_types.Add("num", IntrinsicTypeDefs.NUMBER);
			_types.Add("string", IntrinsicTypeDefs.STRING);
		}

		public void SetRuntimeError(RuntimeErrorType errorType, string msg) {
			control.flags |= ControlInfo.ERROR;
			control.runtimeError = new RuntimeErrorInst(errorType, msg);
		}

		public bool IsRuntimeErrorSet() {
			return 0 != (ControlInfo.ERROR & control.flags);
		}

		public string GetRuntimeErrorString() {
			if (!IsRuntimeErrorSet())
				return null;

			return control.runtimeError.ToString();
		}

		public void BeginCompile() {
			Pb.Assert(null == _preCompileState);
			_preCompileState = new PreCompileState();
			_preCompileState.stackState = stack.GetState();
			_types.Apply();
			_classes.Apply();
		}

		public void FinishCompile(bool revert) {
			stack.RestoreState(_preCompileState.stackState);
			if (revert) {
#if PEBBLE_TRACETYPES
				engine.Log("Reverting " + _types.BufferedCount() + " types.");
				engine.Log("Reverting " + _classes.BufferedCount() + " classes.");
#endif
				_types.Revert();
				_classes.Revert();
			} else {
#if PEBBLE_TRACETYPES
				engine.Log("Applying " + _types.BufferedCount() + " types.");
				engine.Log("Applying " + _classes.BufferedCount() + " classes.");
#endif
				_types.Apply();
				_classes.Apply();
			}
			control.Clear();

			_preCompileState = null;
		}

		// Used by the parser.
		public bool IsType(string name) {
			return _types.ContainsKey(name);
		}

		public override string ToString() {
			string msg = "";

			//msg += _globals;
			msg += stack;

            /*
			for (int iTier = _scopes.Count - 1; iTier >= 0; --iTier) {
				SymbolTable prev = null;
				msg += "" + iTier + "[";
				for (int iLevel = _scopes[iTier].Count - 1; iLevel >= 0; --iLevel) {
					IScope table = _scopes[iTier][iLevel];
					if (null != prev)
						msg += table.nonterminal ? " <- " : " | ";
					msg += table.name + "(" + table.table.Count + (table.locked ? "L" : "") + ")";
					prev = table;
				}
				msg += "]  ";
			}
            */
			
			return msg;
		}

		public string DumpValue(object value, string indent) {
			//object val = (value is Variable) ? ((Variable)value).value : value;

			//if (val == null)
			//	return "<null>";
			//else if (val is string)
			//	return "\"" + ((string)val) + "\"";
			//else if (val is SymbolTable)
			//	return ((SymbolTable)val).Dump(this, indent + "  ");
			//else
			//	return val.ToString();
			return "";
		}

		public string GetDebugTypeString(bool classes = true, bool types = true, bool registry = true) {
			string output = "";

			if (classes) {
				output += "\nClasses:\n";
				foreach (var x in _classes.GetMainForDebugging())
					output += "  " + x.Key + "\n";
			}

			if (types) {
				output += "\nTypes:\n";
				foreach (var x in _types.GetMainForDebugging())
					output += "  " + x.Key + "\n";
			}

			if (registry) {
				output += "\nRegistry:\n";
				foreach (var x in TypeFactory._typeRegistry) {
					output += "  " + x.Key + "\n";
				}
			}

			return output;
		}
		

		// ***************************************************************************
		// Variable functions.
		//  Note - These are nearly-trivial wrappers for stack functions.
		//         These could be removed at some point.
		// ***************************************************************************

		public VarStackRef GetVarRefByName(string symbol, bool globalOnly = false) {
			VarStackRef index;
			if (globalOnly)
				index = stack.GetGlobalVarIndexByName(symbol);
			else
				index = stack.GetVarIndexByName(symbol);
#if PEBBLE_TRACESTACK
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine(symbol + " -> " + index.ToString());
			Console.ForegroundColor = ConsoleColor.Gray;
#endif
			return index;
		}

		// Adds a variable to the top of the stack
		public VarStackRef AddLocalVariable(string symbol, ITypeDef type, object value) {
			return stack.AddVariable(symbol, false, type, value);
		}

		// set uses this to 1) check to see if symbol already exists, and 2) caches the variable for use during evaluate
		//		set can also be used to set global variables.
		// for uses this to 1) check to make sure symbol doesn't already exist, 2) push var on the stack for type checking
		public VarStackRef CreateTemp(string symbol, ITypeDef type, bool global = false, bool doesntCollideWithGlobal = false) {
			if (null != GetTypeByName(symbol))
				return new VarStackRef(false);

			if (Pb.reservedWords.Contains(symbol))
				return new VarStackRef(false);

			VarStackRef existingRef = stack.GetVarIndexByName(symbol);
			//if (stack.VariableExists(symbol, global)) {
			if (existingRef.isValid) {
				if (!(doesntCollideWithGlobal && existingRef.isGlobal))
					return new VarStackRef(false);
			}

			return stack.AddVariable(symbol, global, type, null);
		}

		public bool CreateGlobal(string symbol, ITypeDef type, object value = null) {
			Variable existing = stack.GetVariable(symbol, true); // existence check
			if (null != existing)
				return false;

			VarStackRef vsr = stack.AddVariable(symbol, true, type, value);
			return null != stack.GetVarAtIndex(vsr); // DoesVarExist
		}

		// Call this one to create a variable *during execution*.
		// Doesn't do error checking, doesn't care if variable already exists.
		// If you aren't sure what you are doing, you probably want to use CreateGlobal.
		public Variable CreateEval(string symbol, ITypeDef type, object value = null, bool isGlobal = false) {
			VarStackRef vsr = stack.AddVariable(symbol, isGlobal, type, value);
			return stack.GetVarAtIndex(vsr);
		}


		// ***************************************************************************
		// Type functions.
		// ***************************************************************************

		public bool CreateAlias(string alias, ITypeDef typeDefIn) {
			if (_types.ContainsKey(alias))
				return false;
			_types.Add(alias, typeDefIn);
			return true;
		}

		public bool UpdateAlias(string alias, ITypeDef typeDefIn) {
			if (!_types.ContainsKey(alias))
				return false;
			_types[alias] = typeDefIn;
			return true;
		}

		// When creating a new class, call CreateClass XOR RegisterClass. Use the latter if you already have a ClassDef, otherwise
		// call the former and it will create one for you.
		public ClassDef CreateClass(string nameIn, TypeDef_Class typeDef, ClassDef par, List<string> genericTypeNames = null, bool isSealed = false, bool isUninstantiable = false) {
			ClassDef def = new ClassDef(nameIn, typeDef, par, genericTypeNames, isSealed, isUninstantiable);
			RegisterClass(def);
			return def;
		}

		public void RegisterClass(ClassDef def) {
			// AddClass()
			Pb.Assert(!_classes.ContainsKey(def.name), "ExecContext::AddClass - Class already exists!");
			_classes.Add(def.name, def);
			_types.Add(def.name, TypeFactory.GetTypeDef_Class(def.name, null, false));
		}

		// Register a template type, creating the ClassDef if necessary.
		public ClassDef RegisterIfUnregisteredTemplate(TypeDef_Class classType) {
			string fullName = classType.GetName();
			if (_classes.ContainsKey(fullName))
				return _classes[fullName];

			ClassDef parent = _classes[classType.className];
			Pb.Assert(null != parent, "Parent template not registered?");
			if (parent.isSealed) {
				engine.LogCompileError(ParseErrorType.ClassParentSealed, "Cannot derive from sealed class.");
				return null;
			}

			// NOTE: All this code is assuming that the generic parent has no parents.
			// This is fine as long as 1) all generic classes are only defined in C#, and 
			// 2) none of them have parents.

			if (parent.genericTypeNames.Count != classType.genericTypes.Count) {
				engine.LogCompileError(ParseErrorType.TemplateCountMismatch, "Template count mismatch: expected " + parent.genericTypeNames.Count + ", got " + classType.genericTypes.Count + ".");
				return null;
			}

			ClassDef newClass = new ClassDef(classType.GetName(), classType, parent);
			_classes.Add(newClass.name, newClass);
			newClass.Initialize();
			newClass.FinalizeClass(this);

			return newClass;
		}

		public ClassDef GetClass(string name) {
			return _classes.ContainsKey(name) ? _classes[name] : null;
		}

		public bool DoesTypeExist(string symbol) {
			return _types.ContainsKey(symbol);
		}

		public ITypeDef GetTypeByName(string symbol) {
			if (_types.ContainsKey(symbol))
				return _types[symbol];

			return null;
		}

		// Returns true if parent is in child's ancestor list.
		public bool IsChildClass(ClassDef parent, ClassDef child) {
			ClassDef test = child;
			while (null != test) {
				if (test == parent)
					return true;
				test = test.parent;
			}

			return false;
		}

		// D B A
		// G E B A
		// This is specifically for the conditional operator.
		//   typeof(true?D:G) is B
		public ITypeDef GetMostCommonType(ITypeDef typeA, ITypeDef typeB) {

			bool aIsClass = typeA is TypeDef_Class;
			if (aIsClass) {
				bool bIsClass = typeB is TypeDef_Class;
				if (bIsClass) {
					TypeDef_Class classTypeA = typeA as TypeDef_Class;
					TypeDef_Class classTypeB = typeB as TypeDef_Class;
					return GetMostCommonType(classTypeA, classTypeB);
				}
			}

			// If they aren't both classes then they must be equal or they 
			// have no commonality.
			if (typeA.Equals(typeB))
				return typeA;

			return null;
		}
		
		private ITypeDef GetMostCommonType(TypeDef_Class typeA, TypeDef_Class typeB) {
			ClassDef a = _classes[typeA.className];
			while (a != null) {
				ClassDef b = _classes[typeB.className];
				while (b != null) {
					if (a == b)
						return a.typeDef;
					b = b.parent;
				}
				a = a.parent;
			}

			return null;
		}

		// Given two classes, return which is the ancestor of the other, or null.
		// If neither is the direct ancestor of the other, returns null.  If you 
		// want to determine teh nearest common ancestor, use GetMostCommonType.
		public TypeDef_Class DetermineAncestor(TypeDef_Class typeA, TypeDef_Class typeB) {
			ClassDef a = _classes[typeA.className];
			ClassDef b = _classes[typeB.className];
			while (b != null) {
				if (a == b)
					return typeA;
				b = b.parent;
			}

			b = _classes[typeB.className];
			while (a != null) {
				if (a == b)
					return typeB;
				a = a.parent;
			}

			return null;
		}

	}

}
