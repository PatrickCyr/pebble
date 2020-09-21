/*
Implementation of Pebble's variables and variable stack.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

namespace Pebble {

	/*	
	Represents a variable.  In Pebble, variables have static type, meaning their
	type can never change.  However, they may point to a values of a different types
	when it comes to classes and child classes.
	*/
	public class Variable {
		// Note that none of these are readonly because we reuse Variable instances.
		public string name;
		public ITypeDef type;
		public object value;
		public bool unique;

		public Variable(string nameIn, ITypeDef typeIn, object valueIn = null) {
			Set(nameIn, typeIn, valueIn);
		}

		public Variable Set(string nameIn, ITypeDef typeIn, object valueIn = null) {
			Pb.Assert(null != typeIn, "Can't have a variable without a type.");
			name = nameIn;
			type = typeIn;
			value = valueIn;
			unique = false;
			return this;
		}

		public override string ToString() {
			return "Var<" + name + " " + type + "=" + value + ">";
		}
	}

	// This holds information about each scope pushed onto the scope stack.
	// Scopes are caused by function calls and by expression lists.
	/* Types of scopes:
		1) Non-terminal, non class: bare expression lists, for, if etc.
		2) Terminal, non-class: used by the engine for things like file scope.
		3) Non-terminal, non-function class scope: defstructors.
		4) Terminal, non-function class scope: used when defining classes.
		5) Terminal, class function calls.
		6) Terminal, class static function calls.
	*/
	public class StackScope {
		// Cannot make these readonly because instances of StackScope are reused.

		private int _varStackStart;
		private string _name;
		private bool _terminal;
		private bool _hardTerminal;

		private ClassValue _classInstance;
		private ClassDef _classDef;
		private TypeDef_Function _funcType;
		private bool _isStatic;

		public int varStackStart { get { return _varStackStart; } }
		public string name { get { return _name; } }
		public bool terminal { get { return _terminal; } }
		public bool hardTerminal { get { return _hardTerminal; } }
		public TypeDef_Function funcType { get { return _funcType; } }
		public ClassValue classInstance { get { return _classInstance; } }
		public ClassDef classDef { get { return _classDef; } }
		public bool isStatic { get { return _isStatic; } }

		// This is for non-terminal blocks: for, if, and expr-lists. NOT fuction calls.
		public void Set(int depth, string name = "<scope>") {
			_varStackStart = depth;
			_name = name;
			_terminal = false;
			_hardTerminal = false;
			_classInstance = null;
			_classDef = null;
			_funcType = null;
			_isStatic = false;
		}

		// This is for terminal blocks. Used only by the engine itself (like to 
		// push a top-level terminal block on the stack before starting the script).
		public void SetTerminal(int depth, string name = "<terminal scope>") {
			_varStackStart = depth;
			_name = name;
			_terminal = true;
			_hardTerminal = false;
			_classInstance = null;
			_classDef = null;
			_funcType = null;
			_isStatic = false;
		}

		// Terminal should be true in the "usual" case, which seems to be class member
		// functions. Should be false in the case of defstructors, because those aren't
		// real functions and thus not closures: they're just little blocks of code
		// inside the surrounding code that have access to class members.
		public void Set(int depth, ClassValue classInstance, bool terminalIn = true) {
			_varStackStart = depth;
			_name = "class (" + classInstance.classDef.name + ")";
			_terminal = terminalIn;
			_hardTerminal = false;
			_classInstance = classInstance;
			_classDef = null;
			_funcType = null;
			_isStatic = false;
		}

		// This is for static function calls, or for class member function calls during typecheck
		// when we don't have or need an instance.
		public void Set(int depth, TypeDef_Function funcType, ClassDef classDef, bool isStaticIn) {
			_varStackStart = depth;
			_name = "class (" + classDef.name + ")";
			_terminal = true;
			_hardTerminal = false;
			_classInstance = null;
			_classDef = classDef;
			_funcType = funcType;
			_isStatic = isStaticIn;
		}

		// This is for function calls, both regular calls and class member calls.
		public void Set(int depth, string name, TypeDef_Function funcType, ClassValue classInstance, bool isStaticIn) {
			_varStackStart = depth;
			_name = "function call (" + name + ")";
			_terminal = true;
			_hardTerminal = false;
			_classInstance = classInstance;
			_classDef = null;
			_funcType = funcType;
			_isStatic = isStaticIn;
		}

		// ATM, this is only used by Exec.
		public void SetHardTerminal(int depth, string name = "<hardTerminal>") {
			_varStackStart = depth;
			_name = "hardTerminal";
			_terminal = true;
			_hardTerminal = true;
			_classInstance = null;
			_classDef = null;
			_funcType = null;
			_isStatic = false;
		}
	}

	public struct StackState {
		public readonly int varCount;
		public readonly int callCount;

		public StackState(int varCountIn, int callCountIn) {
			varCount = varCountIn;
			callCount = callCountIn;
		}
	}

	// VariableStack passes these back as a fast way (no string look-ups) to reference variables.
	// NOTE: isValid is super goofy. structs can't have parameterless constructors, but they *do* have
	// a default constructor which sets all fields to their default value. So, isValid defaults to false
	// in that parameterless constructor, meaning that anyone that creates a VSR that way will 
	// create an invalid one.
	public struct VarStackRef {
		public enum ErrorType {
			NOTINITIALIZED,
			None,
			NotFound,
			AlreadyExists,
			ReservedSymbol,
			NonUnique,
		};
		public readonly ErrorType errorType;

		public bool isValid { get { return ErrorType.None == errorType; } }

		// This is the number you add to _callCount to get the index into _callStack.
		// For examle, -1 is the top of the call stack.
		public readonly int callIndexOffset;
		// Index into _varStack *relative to the call's top*.
		public readonly int varIndex;
		public readonly bool isGlobal;
		public readonly MemberRef memberRef;
		public readonly ITypeDef typeDef;
		// This is for references to unique variables, like globals.
		public readonly Variable variable;

		// This creates an invalid VarStackRef.
		public VarStackRef(ErrorType error) {
			Pb.Assert(error != ErrorType.None);
			errorType = error;
			callIndexOffset = -9000;
			varIndex = -1;
			isGlobal = false;
			memberRef = MemberRef.invalid;
			typeDef = null;
			variable = null;
		}

		// This is for uniques, both globals and those on the stack.
		public VarStackRef(Variable uniqueVariable, bool global) {
			errorType = ErrorType.None;
			callIndexOffset = -9000;
			varIndex = -1;
			isGlobal = global;
			memberRef = MemberRef.invalid;
			typeDef = uniqueVariable.type;
			variable = uniqueVariable;
			//!Pb.Assert(variable.persistent == true);
			variable.unique = true;
		}

		// Use this one for non-unique variables on the varStack.
		public VarStackRef(ITypeDef typeDefIn, int callIx, int ix) {
			errorType = ErrorType.None;
			typeDef = typeDefIn;
			callIndexOffset = callIx;
			varIndex = ix;
			memberRef = MemberRef.invalid;
			isGlobal = false;
			variable = null;
		}

		// Use this one for class members, both regular and static.
		//! statics are unique but we aren't referencing them that way atm.
		public VarStackRef(ITypeDef typeDefIn, int callIx, MemberRef memRef) {
			errorType = ErrorType.None;
			typeDef = typeDefIn;
			callIndexOffset = callIx;
			varIndex = -1;
			memberRef = memRef;
			isGlobal = false;
			variable = null;
		}

		public override string ToString() {
			if (!isValid)
				return "<invalid>";
			else if (null != variable)
				return "unique[" + variable.type + " " + variable.name + "]";
			else if (isGlobal)
				return "global[" + varIndex + "]";
			else
				return "call[" + callIndexOffset + "] " + (!memberRef.isInvalid ? memberRef.ToString() : "stack[" + varIndex + "]");
		}
	}

	// Despite the name, this stores the stack *and* globals. Because there is no heap,
	// this is where all script-managed variables live. (We utilize the C# heap for our new'ed vars.)
	public class VariableStack {
		// NOTE: This number is pretty arbitrary and may be changed to suit your needs. The only downside to making it 
		// bigger is that it takes more memory. The only downside to making it smaller is you run the risk that a program
		// will exceed the call stack depth. 
		// Because "calls" are used for scopes as well, you use these faster than you might think. For example, a 
		// single function call might result in quite a few "calls" being pushed onto the stack.
		// Still, unless you are doing recursion you shouldn't be using hundreds and hundreds of these.
		private const int CALLSTACKMAXDEPTH = 256;

		// All globals live here.
		protected DictionaryList<Variable> _globals = new DictionaryList<Variable>();

		// There are one of these for each scope pushed.
		protected List<StackScope> _callStack = new List<StackScope>();
		protected int _callCount;

		// This is the variable stack.
		protected List<Variable> _varStack = new List<Variable>();
		protected int _varCount;


		// ********************************************************************

		// Checks stack, class, and global.
		public bool IsSymbolAvailable(ExecContext context, string symbol, bool ignoreGlobals = false) {
			if (context.DoesTypeExist(symbol))
				return false;
			VarStackRef index = GetVarIndexByName(null, symbol, true);
			return !index.isValid || (ignoreGlobals && index.isGlobal);
		}
		/*
		public bool VariableExists(string symbol, bool global) {
			if (global)
				return _globals.Exists(symbol);

			return GetVarIndexByName(symbol).isValid;
		}
		*/

		// Note that in the specific case of global variables we can look them up without a context.
		public Variable GetGlobalVariable(string symbol) {
			return _globals.Get(symbol);
		}

		public VariableStack() {
			for (int ii = 0; ii < CALLSTACKMAXDEPTH; ++ii) {
				_callStack.Add(new StackScope());
			}
		}

		public int GetCallStackDepth() {
			return _callCount;
		}

		public override string ToString() {
			string res = "";

			int count = _globals.Count;
			res += "GLOBALS:\n";
			for (int ii = 0; ii < count; ++ii) {
				Variable var = _globals.Get(ii);
				if (var.type is TypeDef_Function)
					res += "  " + ((TypeDef_Function)var.type).GetDebugString(var.name);
				else
					res += GetVariableString(var);
			}
				
			res += "STACK: varCount = "+_varCount+", callCount = "+_callCount+"\n";
			if (_callCount > 0) {
				for (int iVar = 0; iVar <= _callStack[0].varStackStart - 1; ++iVar) {
					Variable var = _varStack[iVar];
					res += GetVariableString(var);
				}

				for (int iCall = 0; iCall < _callCount; ++iCall) {
					StackScope scope = _callStack[iCall];
					if (scope.terminal)
						res += "  *** " + scope.name + " ***\n";
					else
						res += "  --- " + scope.name + " ---\n";

					int startIndex = scope.varStackStart;
					int endIndex;
					if (iCall < _callCount - 1)
						endIndex = _callStack[iCall + 1].varStackStart - 1;
					else
						endIndex = _varCount - 1;

					for (int iVar = startIndex; iVar <= endIndex; ++iVar) {
						Variable var = _varStack[iVar];
						res += "  " + GetVariableString(var);
					}
				}
			} else {
				for (int iVar = 0; iVar < _varCount; ++iVar) {
					Variable var = _varStack[iVar];
					res += "  " + GetVariableString(var);
				}
			}

			return res;
		}

		private string GetVariableString(Variable var) {
			if (null == var)
				return "<null>";
			return "  " + var.type.ToString() + " " + var.name + " = " + CoreLib.ValueToString(null, var.value, true) + "\n";
		}

		// This pushes a non-terminal scope ("block") onto the stack.
		// Use for "if", "for", and exrpession lists.
		public bool PushBlock(string name, ExecContext context) {
			if (CALLSTACKMAXDEPTH == _callCount)
				return false;

			_callStack[_callCount].Set(_varCount, name);
			++_callCount;

#if PEBBLE_TRACESTACK
			TraceScope("PushBlock " + name);
#endif

			return true;
		}

		public bool PushTerminalScope(string name, ExecContext context, bool hard = false) {
			if (CALLSTACKMAXDEPTH == _callCount)
				return false;

			if (hard)
				_callStack[_callCount].SetHardTerminal(_varCount, name);
			else
				_callStack[_callCount].SetTerminal(_varCount, name);
			++_callCount;

#if PEBBLE_TRACESTACK
			TraceScope("PushTerminalScope" + (hard ? "(hard)" : ""));
#endif

			return true;
		}

		public bool PushCall(TypeDef_Function funcType, string funcName, ClassValue instance, bool isStatic, ExecContext context) {
			if (CALLSTACKMAXDEPTH == _callCount)
				return false;

			_callStack[_callCount].Set(_varCount, funcName, funcType, instance, isStatic);
			++_callCount;

#if PEBBLE_TRACESTACK
			TraceScope("PushCall");
#endif

			return true;
		}

		public bool PushClassCall_StaticOrTypeCheck(TypeDef_Function funcType, ClassDef classDef, bool isStatic, ExecContext context) {
			if (CALLSTACKMAXDEPTH == _callCount)
				return false;

			_callStack[_callCount].Set(_varCount, funcType, classDef, isStatic);
			++_callCount;

#if PEBBLE_TRACESTACK
			TraceScope("PushClassCall_StaticOrTypeCheck(" + classDef.name + ", " + (isStatic ? "static)" : "type check)"));
#endif

			return true;
		}

		public bool PushClassScope(ClassValue instance, ExecContext context, string msg = "") {
			if (CALLSTACKMAXDEPTH == _callCount)
				return false;

			_callStack[_callCount].Set(_varCount, instance, true);
			++_callCount;

#if PEBBLE_TRACESTACK
			TraceScope("PushClassScope");
#endif
			return true;
		}

		public bool PushDefstructorScope(ClassValue instance, ExecContext context) {
			Pb.Assert(_callCount < CALLSTACKMAXDEPTH);
			if (CALLSTACKMAXDEPTH == _callCount)
				return false;

#if PEBBLE_TRACESTACK
			TraceLog("PushDefstructorScope " + instance.classDef.name + " '" + instance.debugName + "'");
#endif
			_callStack[_callCount].Set(_varCount, instance, false);
			++_callCount;

			return true;
		}

		public void PopScope() {
#if PEBBLE_DEBUG
			Pb.Assert(_callCount > 0, "PopScope called but no scopes to pop.");
#endif

			int lastToRemove = _callStack[_callCount - 1].varStackStart;
			while (_varCount > lastToRemove) {
				Variable var = _varStack[_varCount - 1];
				if (null != var) {
					if (var.unique)
						_varStack[_varCount - 1] = null;
					else {
						var.name = "<deleted>";
						var.value = null;
					}
				}
				--_varCount;
			}

			--_callCount;
#if PEBBLE_TRACESTACK
			TraceLog("PopScope");
#endif
		}

		public void PopToCallDepth(int length) {
#if PEBBLE_DEBUG
			Pb.Assert(length > _callCount, "Why are you setting length to something longer than the current length?");
#endif

			_varStack.RemoveRange(length, _varStack.Count - length);
		}

		public VarStackRef AddVariable(string name, bool global, ITypeDef type, object value = null) {
			Variable var;

			if (global) {
				var = new Variable(name, type, value);
				_globals.Set(name, var);
				return new VarStackRef(var, true);
			}
			
			if (_varStack.Count == _varCount) {
				var = new Variable(name, type, value);
				_varStack.Add(var);
			} else {
				var = _varStack[_varCount];
				if (null == var) {
					var = new Variable(name, type, value);
					_varStack[_varCount] = var;
				} else
					var.Set(name, type, value);
			}
			++_varCount;

#if PEBBLE_TRACESTACK
			TraceLog("AddVariable " + type.ToString() + " " + name + "[" + (_varCount - 1) + "] = <" + value + ">");
#endif

			return new VarStackRef(type, -1, _varCount - _callStack[_callCount - 1].varStackStart - 1);
		}

		public VarStackRef AddExistingVariable(string name, bool global, Variable variable) {
			if (global) {
				_globals.Set(name, variable);
				return new VarStackRef(variable, global);
			}

			if (_varStack.Count == _varCount) {
				_varStack.Add(variable);
			} else {
				_varStack[_varCount] = variable;
			}
			++_varCount;

#if PEBBLE_TRACESTACK
			TraceLog("AddExistingVariable " + variable.type.ToString() + " " + name + "[" + (_varCount - 1) + "] = <" + variable.value + ">");
#endif

			return new VarStackRef(variable, global);
		}

		public ClassDef GetCurrentClassDef(bool noStatics = false) {
			int iCall = _callCount;
			while (--iCall >= 0) {
				if (null != _callStack[iCall].classDef) {
					if (_callStack[iCall].isStatic && noStatics)
						return null;
					return _callStack[iCall].classDef;
				} else if (null != _callStack[iCall].classInstance)
					return _callStack[iCall].classInstance.classDef;
				else if (_callStack[iCall].terminal)
					return null;
			}

			return null;
		}

		public ClassValue GetCurrentClassValue() {
			int iCall = _callCount;
			while (--iCall >= 0) {
				if (null != _callStack[iCall].classInstance)
					return _callStack[iCall].classInstance;
				else if (_callStack[iCall].terminal)
					return null;
			}

			return null;
		}

		// BIG IMPORTANT FUNCTION.
		// Searches the current call stack for a variable with the given name and returns a VarStackRef, 
		// which can be used by GetVarAtIndex to find the variable, usually later at execution time.
		// This saves us from having to do string searches for variables at execution time, but also
		// adds a lot of complexity to the system. Since the idea is to create these Refs during
		// TypeCheck, any discrepancy between the order scopes are pushed/popped and variables created
		// between TypeCheck and Evaluate WILL result in a grave error.
		public VarStackRef GetVarIndexByName(ExecContext context, string name, bool stopAtTerminals = false) {

			bool onlyUnique = false;
			int callIx = _callCount;
			int varIx = _varCount - 1;
			while (--callIx >= 0) {
				StackScope scope = _callStack[callIx];

				if (!onlyUnique && null != scope.classInstance) {
					// If this scope is a class function call, search that class for 
					// a member with that name.
					ITypeDef typeDef = null;
					MemberRef memRef = _callStack[callIx].classInstance.classDef.GetMemberRef(context, name, scope.isStatic ? ClassDef.SEARCH.STATIC : ClassDef.SEARCH.EITHER, ref typeDef);
					if (!memRef.isInvalid) {
						return new VarStackRef(typeDef, callIx - _callCount, memRef);
					}
				}

				if (!onlyUnique && null != scope.classDef) {
					ITypeDef typeDef = null;
					MemberRef memRef = _callStack[callIx].classDef.GetMemberRef(context, name, scope.isStatic ? ClassDef.SEARCH.STATIC : ClassDef.SEARCH.EITHER, ref typeDef);
					if (!memRef.isInvalid) {
						return new VarStackRef(typeDef, callIx - _callCount, memRef);
					}
				}

				// Otherwise, search the variable stack for it.
				while (varIx >= scope.varStackStart) {
					if (null != _varStack[varIx] && _varStack[varIx].name == name) {
						Variable var = _varStack[varIx];
						if (var.unique)
							return new VarStackRef(var, false);
						else if (!onlyUnique)
							return new VarStackRef(_varStack[varIx].type, callIx - _callCount, varIx - scope.varStackStart);
						else
							return new VarStackRef(VarStackRef.ErrorType.NonUnique);
					}
					--varIx;
				}

				if (scope.hardTerminal)
					break;

				if (scope.terminal) {
					if (stopAtTerminals)
						break;
					else
						onlyUnique = true;
				}
			}

			// Search globals last. Globals are always in scope.
			int globIx = _globals.GetIndex(name);
			if (globIx >= 0) {
				Variable var = _globals.Get(globIx);
				return new VarStackRef(var, true);
			}

			return new VarStackRef(VarStackRef.ErrorType.NotFound);
		}

		public VarStackRef GetGlobalVarIndexByName(string name) {
			int globIx = _globals.GetIndex(name);
			if (globIx >= 0) {
				Variable var = _globals.Get(globIx);
				return new VarStackRef(var, true);
			}

			return new VarStackRef(VarStackRef.ErrorType.NotFound);
		}

		// BIG IMPORTANT FUNCTION.
		// This is how VarStackRefs are decoded and the Variables they referenced are returned.
		public Variable GetVarAtIndex(VarStackRef stackRef) {
			if (!stackRef.isValid)
				return null;

			if (null != stackRef.variable)
				return stackRef.variable;

			if (!stackRef.memberRef.isInvalid) {
				if (stackRef.memberRef.memberType == ClassDef.MemberType.NORMAL) {
					int callIx = _callCount + stackRef.callIndexOffset;
					return _callStack[callIx].classInstance.Get(stackRef.memberRef);
				} else {
					return stackRef.memberRef.classDef.GetVariable(stackRef.memberRef);
				}
			} else if (stackRef.isGlobal) {
				return _globals.Get(stackRef.varIndex);
			}

			return _varStack[_callStack[_callCount + stackRef.callIndexOffset].varStackStart + stackRef.varIndex];
		}

		public bool InForOrWhileStatement() {
			for (int ii = _callCount - 1; ii >= 0; --ii) {
				string name = _callStack[ii].name;
				if (name == Pb.FOR_BLOCK_NAME || name == Pb.FOREACH_BLOCK_NAME || name == Pb.WHILE_BLOCK_NAME) {
					return true;
				} else if (_callStack[ii].terminal)
					break;
			}

			return false;
		}

		public TypeDef_Function GetEnclosingFunctionType() {
			for (int ii = _callCount - 1; ii >= 0; --ii) {
				var funcType = _callStack[ii].funcType;
				if (null != funcType)
					return funcType;
				else if (_callStack[ii].terminal)
					break;
			}

			return null;
		}

		public StackState GetState() {
			return new StackState(_varCount, _callCount);
		}

		public bool RestoreState(StackState state) {
#if PEBBLE_DEBUG
			Pb.Assert(state.varCount <= _varCount || state.callCount <= _callCount, "TODO: StackState invalid.");
#endif

			while (_varCount > state.varCount) {
				Variable var = _varStack[_varCount - 1];
				if (null != var) {
					if (var.unique)
						_varStack[_varCount - 1] = null;
					else {
						var.value = null;
						var.name = "<deleted>";
					}
				}
				--_varCount;
			}

			while (state.callCount < _callCount)
				PopScope();

#if PEBBLE_DEBUG
			Pb.Assert(state.callCount == _callCount, "whoops, call count different.");
#endif

#if PEBBLE_TRACESTACK
			TraceLog("RestoreState");
			TraceLog("");
#endif
			return true;
		}

		// This clears all variables from the stack and pops all calls.
		// Global variables are not affected.
		public void ClearStack() {
			StackState state = new StackState(0, 0);
			RestoreState(state);
		}

#if PEBBLE_TRACESTACK
		protected void TraceLog(string msg) {
			string lead = new String(' ', _callCount * 2);
			Console.WriteLine(lead + "C" + _callCount + " V" + _varCount + " - " + msg);
		}

		protected void TraceScope(string msg) {
			string lead = new String(' ', (_callCount - 1) * 2);
			StackScope scope = _callStack[_callCount - 1];
			string output = lead + "Scope[" + (_callCount - 1) + "] VSS=" + scope.varStackStart + (scope.terminal ? " TERMINAL" : "") + " - Name='" + scope.name + "'";
			if (null != scope.funcType)
				output += " functype(" + scope.funcType.ToString() + ")";
			else if (null != scope.classInstance)
				output += " @(" + scope.classInstance.classDef.name + ")";
			Console.WriteLine(output + " by='" + msg + "'");
			//Console.WriteLine(lead + "C" + _callCount + " - " + msg);
		}
#endif
	}

}