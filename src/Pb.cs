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
	};

	public enum ParseErrorType {
		// Used by unit tests to say that any error is acceptable.
		Any,

		// All parse errors are represented by these two errors.
		SemanticError,
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
		ClassMemberNotFound,
		ClassMembersCannotBeGlobal,
		ClassMemberShadowed,
		ClassNotDeclared,
		ClassParentSealed,
		ClassRequiredForThis,
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
		IfConditionNotBoolean,
		IndexNotNumeric,
		Internal,
		InvalidSymbolName,
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
			return type.ToString() + " - " + msg;
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
			return type + " - " + msg;
		}
	}
}
