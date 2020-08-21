/*
The Pb class was created to hold global convenience functions and stuff.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Pebble {
	class Pb {
		public const string FOR_BLOCK_NAME = "__for";
		public const string FOREACH_BLOCK_NAME = "__foreach";

		public static void Assert(bool condition, string msg = "") {
			if (!condition) {
				throw new Exception(msg);
		 	}
		}

		public static float RealtimeSinceStartup() {
			return (float)Stopwatch.GetTimestamp() / (float)Stopwatch.Frequency;
		}

		public static List<string> reservedWords = new List<string> {
			"assert",
			"break",
			"catch",
			"class",
			"const",
			"constructor",
			"continue",
			"else",
			"endif",
			"enum",
			"false",
			"for",
			"foreach",
			"funcdef",
			"functype",
			"global",
			"guarded",
			"if",
			"in",
			"new",
			"override",
			"return",
			"scope",
			"sealed",
			"static",
			"set",
			"this",
			"true",
			"typealias",
			"uninstantiable",
		};
	}

	// This class helps with runtime exceptions, including "returns".
	public class ControlInfo {
		public const uint BREAK = 0x0001;
		public const uint CONTINUE = 0x0002;
		public const uint RETURN = 0x0004;
		public const uint ERROR = 0x8000;

		public uint flags;
		public RuntimeErrorInst runtimeError;
		// If a "Return" flag has been set, this is the value.
		public object result;

		public void Clear() {
			flags = 0;
			result = 0;
			runtimeError = null;
		}

		public void ClearError() {
			flags &= 0x7FFF;
			runtimeError = null;
		}
	}

	// Flags that can modify declarations.
	public class DeclMods {
		public bool _const;
		public bool _static;
		public bool _global;
		public bool _override;
		public bool _guarded;
	};

	public enum ParseErrorType {
		// Used by unit tests to say that any error is acceptable.
		Any,

		// All parse errors are represented by this error.
		SyntaxError,

		ArgCountMismatch,
		AssertNeedsBool,
		AssertNeedsString,
		AssignToNonLValue,
		AssignToConst,
		BreakNotInFor,
		ClassAlreadyDeclared,
		ClassCannotBeChildOfTemplate,
		ClassCanOnlyHaveOneConstructor,
		ClassMemberFunctionsConst,
		ClassMemberGuarded,
		ClassMemberNotFound,
		ClassMembersCannotBeGlobal,
		ClassMemberShadowed,
		ClassNotDeclared,
		ClassParentSealed,
		ClassRequired,
		ClassUninstantiable,
		ConcatStringsOnly,
		ConditionNotBoolean,
		ContinueNotInFor,
		CompareNullToNonReference,
		DefaultArgGap,
		DeserializeStringOnly,
		DotMustBeScope,
		EnumMustHaveValues,
		EnumNameDuplicate,
		ForEachInvalidCollection,
		ForEachInvalidType,
		ForEachIteratorNameTaken,
		ForIteratorNameTaken,
		ForRangeMustBeNumeric,
		FunctionLiteralsAreImplicitlyConst,
		GuardedClassMembersOnly,
		GuardedNonConst,
		IfConditionNotBoolean,
		IndexNotNumeric,
		Internal,
		InvalidSymbolName,
		IsRequiresClass,
		LengthOperatorInvalidOperand,
		MemberOverrideCannotBeStatic,
		MemberOverrideNoParent,
		MemberOverrideNotFound,
		MemberOverrideNotFunction,
		MemberOverrideTypeMismatch,
		//NameShadowing,
		NewTypeCannotBeInferred,
		NullExpression,
		OverrideNonMemberFunction,
		ReferenceOfNonReferenceType,
		RementNonLValue,
		RementOnConst,
		RementOnNumbersOnly,
		ReturnNotInCall,
		ReturnNullInNonVoidFunction,
		ReturnTypeMismatch,
		ReturnValueInVoidFunction,
		ScopeAlreadyDeclared,
		SerializeStringOnly,
		StackOverflow,
		StandardFunctionMismatch,
		StaticClassMembersOnly,
		//StaticImpliesConst,
		StreamFunction,
		StaticMemberEvaluationError,
		StreamNoLValue,
		StreamNoSerializeFunction,
		StreamOnly,
		//StreamSerializeFunctionWrong,
		SymbolAlreadyDeclared,
		SymbolNotFound,
		TemplateCountMismatch,
		TypeAlreadyExists,
		TypeMismatch,
		TypeNotFound,
		TypeNotIndexable,
		TypesUnrelated,
		VoidFunctionsOnly,
	}

	public class ParseErrorInst {
		readonly public ParseErrorType type;
		readonly public string msg;

		public ParseErrorInst(ParseErrorType _type, string _msg) {
			type = _type;
			msg = _msg;
		}

		public override string ToString() {
			return null != msg ? msg : type.ToString();
		}
	}

	public enum RuntimeErrorType {
		//Any,

		Assert,
		ArgumentInvalid,
		ArrayIndexOutOfBounds,
		ConversionInvalid,
		DeserializeScriptHasError,
		DeserializeTypeMismatch,
		DictionaryDoesntContainKey,
		EnumValueNotFound,
		ForeachModifyingContainer,
		ForIndexOutOfBounds,
		KeyAlreadyExists,
		KeyNotFound,
		NativeException,
		NullAccessViolation,
		NumberInvalid,
		SerializeFailed,
		SerializeIntoConst,
		SerializeInvalidClass,
		SerializeReadRequiresLValue,
		SerializeStreamNotOpen,
		SerializeTypeMismatch,
		SerializeUnknownType,
		StackOverflow,
	}

	public class RuntimeErrorInst {
		public readonly RuntimeErrorType type;
		public readonly string msg;

		public RuntimeErrorInst(RuntimeErrorType _type, string _msg) {
			type = _type;
			msg = _msg;
		}

		public override string ToString() {
			return null != msg ? msg : type.ToString();
		}
	}

	// This container holds new entries in a temp buffer until Apply is called to finalize them
	// or Revert is called to toss them. This is used to clean up registered types and stuff
	// in the event of a compilation error.
	public class BufferedDictionary<K, V> {
		private Dictionary<K, V> _main = new Dictionary<K, V>();
		private Dictionary<K, V> _temp = new Dictionary<K, V>();

		// Making this class enumerable is far too complicated when all I want 
		// is this list to print during debugging. Using it for anything else is 
		// naughty.
		public Dictionary<K, V> GetMainForDebugging() {
			return _main;
		}

		public void Apply() {
			foreach (var kvp in _temp) {
				_main.Add(kvp.Key, kvp.Value);
			}
			_temp.Clear();
		}

		public bool HasBuffered() {
			return _temp.Count > 0;
		}

		public int BufferedCount() {
			return _temp.Count;
		}

		public void Revert() {
			_temp.Clear();
		}

		public bool ContainsKey(K key) {
			return _main.ContainsKey(key) || _temp.ContainsKey(key);
		}

		public BufferedDictionary<K, V> Add(K key, V value) {
			if (ContainsKey(key))
				return null;
			_temp.Add(key, value);
			return this;
		}

		public V Get(K key) {
			if (_main.ContainsKey(key))
				return _main[key];
			return _temp[key];
		}

		public void Set(K key, V value) {
			if (_main.ContainsKey(key))
				_main[key] = value;
			else
				_temp[key] = value;
		}

		public V this[K key] {
			get => Get(key);
			set => Set(key, value);
		}
	}

	// A container I made which is a list with a name-index dictionary, too, for both
	// fast integer and string lookups.
	public class DictionaryList<V> {
		private List<V> _list = new List<V>();
		private Dictionary<string, int> _nameToIx = new Dictionary<string, int>();

		public int Count {
			get { return _list.Count; }
		}

		public bool Exists(string name) {
			return null != Get(name);
		}

		public V Get(int index) {
			return _list[index];
		}

		public V Get(string name) {
			if (_nameToIx.ContainsKey(name))
				return _list[_nameToIx[name]];
			return default(V);
		}

		public bool ContainsKey(string key) {
			return _nameToIx.ContainsKey(key);
		}

		public V this[string key] {
			get => Get(key);
			set => Set(key, value);
		}

		public int GetIndex(string name) {
			if (_nameToIx.ContainsKey(name))
				return _nameToIx[name];
			return -1;
		}

		public int Set(string symbol, V value) {
			int ix;
			if (_nameToIx.ContainsKey(symbol)) {
				ix = _nameToIx[symbol];
				_list[ix] = value;
			} else {
				ix = Add(symbol, value);
			}

			return ix;
		}

		public int Add(string symbol, V value) {
			int ix = _list.Count;
			_nameToIx.Add(symbol, ix);
			_list.Add(value);
			return ix;
		}
	}
}
