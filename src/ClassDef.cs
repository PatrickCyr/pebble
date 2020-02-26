/*
Classes used in the implementation of Pebble Classes.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	using MemberList = DictionaryList<ClassMember>;

	// Stores definition of a class member.
	public class ClassMember {
		public string name;
		public ITypeDef typeDef;
		public int index;
		public IExpr initializer;

		public ClassMember(string name, ITypeDef typeDef, IExpr initializer) {
			this.name = name;
			this.typeDef = typeDef;
			this.initializer = initializer;
		}
	}

	// Structure created during compile-time which allows us to find class members
	// without doing string look-ups at runtime.  This is needed for the dot operator.
	// It's also currently being used for the :: (scope) operator but it needn't be.
	public struct MemberRef {
		public readonly ClassDef.MemberType memberType;
		public readonly int ix;
		public readonly ClassDef classDef;

		public bool isInvalid { get { return -1 == ix; } }

		// Type 1: a reference to a non-static member.
		// * NORMAL type is implied (functions can be looked up from the ClassDef)
		// * ix is index into _fields 
		public MemberRef(int ixIn) {
			memberType = ClassDef.MemberType.NORMAL;
			ix = ixIn;
			classDef = null;
		}

		// Type 2: a reference to a static member of a class.
		// * type is STATIC or FUNCTION (FUNCTION implying non-static function)
		// * ix is index into the given ClassDef's _statics or _memberFuncs
		public MemberRef(ClassDef staticOwnerIn, ClassDef.MemberType typeIn, int ixIn) {
			Pb.Assert(ClassDef.MemberType.STATIC == typeIn || ClassDef.MemberType.FUNCTION == typeIn);
			memberType = typeIn;
			ix = ixIn;
			classDef = staticOwnerIn;
		}

		// Type 0: Use this to indicate an invalid MemberRef. Used as error return values.
		// Don't create these, just use "invalid".
		private MemberRef(bool bad) {
			memberType = ClassDef.MemberType.NORMAL;
			ix = -1;
			classDef = null;
		}

		public static MemberRef invalid = new MemberRef(true);

		public override string ToString() {
			return "MemberRef " + memberType + "[" + ix + "]" + (null != classDef ? " @" + classDef.name : "");
		}
	}

	/* Class definition. This is also where the values of static members are stored.
		Normal fields: Not overridable. Stored in instance.
			Can be templates: resolve type at ClassDef creation.
		Member Functions: Overridable. Stored in vftable in ClassDef.
			These can be templates, ie. the return value of List<num>.Get: resolve type at ClassDef creation.
		Statics: Not overidable. Stored in ClassDef.
			To avoid going crazy, these can't be templates.
	*/
	public class ClassDef {

		public enum MemberType {
			NORMAL = 0,
			FUNCTION = 1,
			STATIC = 2,
		}

		public readonly string name;
		public readonly TypeDef_Class typeDef;
		public readonly ClassDef parent;
		public readonly List<string> genericTypeNames;
		public readonly bool isSealed;
		public readonly bool isUninstantiable;


		public IExpr constructor;

		private MemberList _fields;
		// vftable is different from normals in that the ClassDef has a value for each
		// function. Only class instances have values for normal fields.
		// When typechecking a class we ideally only want to typecheck the functions
		// that were added by *this* class. Inherited functions should already be checked.
		private MemberList _memberFuncs;
		// A consequence of the way this is done is statics can't be overriden.
		private MemberList _statics;
		public List<Variable> vftableVars;
		public List<Variable> staticVars;

		public enum SEARCH {
			NORMAL,
			STATIC,
			EITHER,
		};

		///////////////////////////

		// DO NOT CALL THIS DIRECTLY
		// Use context.CreateClass instead.
		public ClassDef(string nameIn, TypeDef_Class typeDefIn, ClassDef par, List<string> genericTypeNamesIn = null, bool isSealedIn = false, bool isUninstantiableIn = false) {
			name = nameIn;
			typeDef = typeDefIn;
			parent = par;
			genericTypeNames = genericTypeNamesIn;
			if (null != par && null != par.childAllocator)
				childAllocator = par.childAllocator;
			isSealed = isSealedIn;
			isUninstantiable = isUninstantiableIn;
		}

		public string GetDebugString() {
			string result = "class " + name + ":\n";
			for (int ii = 0; ii < _statics.Count; ++ ii) {
				ClassMember member = _statics.Get(ii);
				result += "  static ";
				if (member.typeDef is TypeDef_Function)
					result += ((TypeDef_Function)member.typeDef).GetDebugString(member.name);
				else
					result += member.typeDef.ToString() + " " + member.name + " = " + (null == staticVars[ii].value ? "null" : staticVars[ii].value) + ";\n";
			}
			for (int ii = 0; ii < _fields.Count; ++ii) {
				ClassMember member = _fields.Get(ii);
				result += "  " + member.typeDef.ToString() + " " + member.name + ";\n";
			}
			for (int ii = 0; ii < _memberFuncs.Count; ++ii) {
				ClassMember member = _memberFuncs.Get(ii);
				result += "  " + ((TypeDef_Function)member.typeDef).GetDebugString(member.name);
			}

			return result;
		}

		// Only called by the context.
		public void Initialize() {
			if (null != _fields)
				return;

			// 1) fields.
			_fields = new MemberList();
			if (null != parent) {
				for (int ii = 0; ii < parent._fields.Count; ++ii) {
					ClassMember member = parent._fields.Get(ii);
					bool modified = false;
					ITypeDef resolvedType = parent.IsGeneric() ? member.typeDef.ResolveTemplateTypes(typeDef.genericTypes, ref modified) : member.typeDef;
					ClassMember newMember = new ClassMember(member.name, resolvedType, member.initializer);
					newMember.index = _fields.Add(member.name, newMember);
				}
			}

			// 2) Functions
			_memberFuncs = new MemberList();
			if (null != parent) {
				for (int ii = 0; ii < parent._memberFuncs.Count; ++ii) {
					ClassMember member = parent._memberFuncs.Get(ii);
					bool modified = false;
					ITypeDef resolvedType = parent.IsGeneric() ? member.typeDef.ResolveTemplateTypes(typeDef.genericTypes, ref modified) : member.typeDef;
					ClassMember newMember = new ClassMember(member.name, resolvedType, member.initializer);
					newMember.index = _memberFuncs.Add(member.name, newMember);
				}

				vftableVars = new List<Variable>(parent.vftableVars);
			} else {
				vftableVars = new List<Variable>();
			}

			// 3) Statics aren't copied.
			_statics = new MemberList();
			staticVars = new List<Variable>();
		}

		// Call this after creating the class and adding members to it.
		public bool FinalizeClass(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			// Only initializing the members that this class has added is a cool idea, but
			// leaves us fucked when it comes to overrridden functions.
			// So, I'm being lazy here and initializing them all.
			for (int ii = 0; ii < _memberFuncs.Count; ++ii) {
				ClassMember member = _memberFuncs.Get(ii);

				if (null != member.initializer && (member.initializer is Expr_Literal || member.initializer is Expr_Value)) {

					// Make sure vftableVars has a slot for this function.
					while (vftableVars.Count < ii + 1)
						vftableVars.Add(null);

					object initValue = member.initializer.Evaluate(context);
					if (context.IsRuntimeErrorSet())
						return false;

					// Create the variable for the function.
					vftableVars[ii] = new Variable(member.name, member.typeDef, initValue);
				}
			}

			// Initialize the static members. Populates the staticVars list.
			for (int ii = 0; ii < _statics.Count; ++ii) {
				ClassMember member = _statics.Get(ii);
				object initValue = null;
				if (null != member.initializer) {
					initValue = member.initializer.Evaluate(context);
					if (context.IsRuntimeErrorSet()) {
						context.engine.LogCompileError(ParseErrorType.StaticMemberEvaluationError, name + "::" + member.name + " - " + context.GetRuntimeErrorString());
						return false;
					}
				} 
				staticVars[ii] = new Variable(member.name, member.typeDef, initValue);
			}

			return true;
		}

		public bool IsGeneric() {
			return null != genericTypeNames && genericTypeNames.Count > 0;
		}

		// Returns true if it succeeds, false if there was a name conflict.
		public bool AddMember(string name, ITypeDef typeDef, IExpr initializer = null, bool isStatic = false, bool isFunctionVariable = false) {
			// Fail if a member with that name already exists.
			var mr = GetMemberRef(name, SEARCH.EITHER);
			if (!mr.isInvalid) {
				return false;
			}

			var member = new ClassMember(name, typeDef, initializer);

			// This logic is weird but works because the language currently 
			// doesn't allow static member functions.
			if (isStatic) {
				member.index = _statics.Add(member.name, member);
				staticVars.Add(new Variable(member.name, typeDef, null));
			} else if (!isFunctionVariable && typeDef is TypeDef_Function) {
				member.index = _memberFuncs.Add(member.name, member);
			} else {
				member.index = _fields.Add(member.name, member);
			}

			return true;
		}

		// Convenience function for adding members when you know their literal
		// value at compile time.
		public bool AddMemberLiteral(string name, ITypeDef typeDef, object value, bool isStatic = false) {
			Pb.Assert(null != typeDef);
			return AddMember(name, typeDef, new Expr_Value(null, value, typeDef), isStatic);
		}

		/* Todo, perhaps: Add function which allows C# to add static functions later. Could help C#-Pebble communication.
		public Variable AddStaticLiteralAfterFinalization(ExecContext context, string name, ITypeDef typeDef, object value) {
			Variable variable = new Variable(name, typeDef);
			staticVars.Add(variable);
			variable.value = value;
			return variable;
		}
		*/

		// Use to add an override of a member function.
		// Caller is responsible for checking that...
		//	1) we have a parent
		//	2) it has a function with this name.
		public void AddFunctionOverride(string name, ITypeDef typeDef, IExpr initializer = null) {
			var member = new ClassMember(name, typeDef, initializer);
			//var mr = GetMemberRef(name);
			member.index = _memberFuncs.Set(name, member);
		}

		// Compile-time function for looking up a member.
		public ClassMember GetMember(string name, SEARCH searchType) {
			MemberRef memRef = GetMemberRef(name, searchType);
			if (memRef.isInvalid)
				return null;
			
			switch (memRef.memberType) {
				case MemberType.NORMAL:
					return _fields.Get(memRef.ix);

				case MemberType.FUNCTION:
					return _memberFuncs.Get(memRef.ix);

				case MemberType.STATIC:
					return memRef.classDef._statics.Get(memRef.ix);
			}
			return null;
		}

		// Compile-time lookup of MemberRef.
		public MemberRef GetMemberRef(string name, SEARCH searchType, ref ITypeDef typeDef) {
			// If there is an error during compliation then we can have an uninitialized
			// class on the stack. This check prevents us from throwing an exception
			// when that happens. 
			// Kind of a kludge but handling errors during compilation is pretty chaotic
			// and I don't know what I could do other than to just abort on the first error.
			if (null == _fields)
				return MemberRef.invalid;

			if (SEARCH.NORMAL == searchType || SEARCH.EITHER == searchType) {
				if (_fields.Exists(name)) {
					var member = _fields.Get(name);
					typeDef = member.typeDef;
					return new MemberRef(member.index);
				}

				if (_memberFuncs.Exists(name)) {
					var member = _memberFuncs.Get(name);
					typeDef = member.typeDef;
					return new MemberRef(this, MemberType.FUNCTION, member.index);
				}
			}

			if (SEARCH.STATIC == searchType || SEARCH.EITHER == searchType) {
				ClassDef def = this;
				while (null != def) {
					if (def._statics.Exists(name)) {
						ClassMember member = def._statics.Get(name);
						typeDef = member.typeDef;
						return new MemberRef(def, MemberType.STATIC, member.index);
					}
					def = def.parent;
				}
			}

			return MemberRef.invalid;
		}

		public MemberRef GetMemberRef(string name, SEARCH searchType) {
			ITypeDef typeDef = null;
			return GetMemberRef(name, searchType, ref typeDef);
		}

		// Runtime access of static variable, having only the ClassDef.
		public Variable GetVariable(MemberRef memRef) {
			switch (memRef.memberType) {
				case MemberType.FUNCTION:
					return vftableVars[memRef.ix];

				case MemberType.STATIC:
					return memRef.classDef.staticVars[memRef.ix];

				default:
					Pb.Assert(false, "Attempt to get Variable for nonstatic field from ClassDef.");
					break;
			}

			return null;
		}

		public bool IsChildOf(ClassDef other) {
			ClassDef par = parent;
			while (null != par) {
				if (par.name == other.name)
					return true;
				par = par.parent;
			}

			return false;
		}

		// *** Allocation

		// Used by List & Dictionary.
		public delegate ClassValue ChildAllocatorDelegate();
		public ChildAllocatorDelegate childAllocator = () => {
			return new ClassValue();
		};

		// Create a new instance of the class.
		public ClassValue Allocate(ExecContext context) {
			//! We can still do it internally, though...
			//Pb.Assert(!isUninstantiable, "Internal error: attempt to instantiate an uninstantiable class.");

			ClassValue result = childAllocator();
			result.classDef = this;
			result.debugName = name + " Inst";

			bool scopePushed = false;

			for (int ii = 0; ii < _fields.Count; ++ii) {
				ClassMember member = _fields.Get(ii);

				object value = null;
				if (null != member.initializer) {
					if (!scopePushed) {
						scopePushed = true;
						if (!context.stack.PushClassScope(result, context)) {
							context.SetRuntimeError(RuntimeErrorType.StackOverflow, "ClassValue.Allocate - stack overflow.");
							return null;
						}
					}

					value = member.initializer.Evaluate(context);
				} else if (!member.typeDef.IsReference())       // don't instantiate class refs automatically
					value = member.typeDef.GetDefaultValue(context);

				Variable newVar = new Variable(member.name, member.typeDef, value);
				result.fieldVars.Add(newVar);
			}

			if (null != constructor) {
				if (context.IsRuntimeErrorSet()) return null;

				if (!scopePushed) {
					scopePushed = true;
					if (!context.stack.PushClassScope(result, context)) {
						context.SetRuntimeError(RuntimeErrorType.StackOverflow, "ClassValue.Allocate - stack overflow.");
						return null;
					}
				}

				constructor.Evaluate(context);
			}

			if (scopePushed)
				context.stack.PopScope();

			return result;
		}

	}

	/*
	public class EnumValue<T> {
		public string enumName;
		public string name;
		public T value;
	}
	*/

	// An instance of a class. Has storage for it's normal member's variables.
	public class ClassValue {
		public string debugName;
		public ClassDef classDef;
		public List<Variable> fieldVars = new List<Variable>();

		public Variable Get(MemberRef mr) {
			if (mr.isInvalid)
				return null;

			switch (mr.memberType) {
				case ClassDef.MemberType.NORMAL:
					return fieldVars[mr.ix];
				case ClassDef.MemberType.FUNCTION:
					return classDef.vftableVars[mr.ix];
				case ClassDef.MemberType.STATIC:
					return mr.classDef.staticVars[mr.ix];
			}

			return null;
		}

		public Variable GetByName(string name) {
			// Because this class is Value, I think that implies we are only searching for normal fields.
			MemberRef mr = classDef.GetMemberRef(name, ClassDef.SEARCH.NORMAL);
			if (mr.isInvalid) {
				return null;
			}

			return Get(mr);
		}

		public virtual string ToString(ExecContext context) {
			Variable var = GetByName("ThisToString");
			if (null != var && var.type is TypeDef_Function) {
				TypeDef_Function tdf = (TypeDef_Function)var.type;
				if (null != var.value && tdf.retType.Equals(IntrinsicTypeDefs.STRING) && tdf.argTypes.Count == 0) {
					FunctionValue funcVal = (FunctionValue)var.value;
					return (string) funcVal.Evaluate(context, new List<object>(), this);
				}
			}

			return "[" + classDef.name + " instance]";
		}
	}


	///////////////////////////////////////////////////////////////////////////

	public class ClassDef_Enum : ClassDef{
		public readonly PebbleEnum enumDef;

		public ClassDef_Enum(PebbleEnum enumDefIn, string nameIn, TypeDef_Class typeDefIn) : base(nameIn, typeDefIn, null, null, true, true) {
			enumDef = enumDefIn;
		}
	}

	public class ClassValue_Enum : ClassValue {
		public IExpr initializer;
		public bool generateValue = false;

		public string GetName() {
			return (string) Get(PebbleEnum.mrName).value;
		}

		public object GetValue() {
			return Get(PebbleEnum.mrValue).value;
		}

		public override string ToString() {
			return classDef.name + "::" + GetName();
		}
	};

	public class PebbleEnum {
		private class EnumValue {
			public string name;
			public IExpr initializer;
			public object literalValue;
		}

		public readonly string enumName;
		public readonly TypeDef_Enum enumType;
		public readonly ITypeDef valueType;
		public readonly ClassDef _classDef;

		private List<EnumValue> _values = new List<EnumValue>();
		private Dictionary<string, bool> _names = new Dictionary<string, bool>();

		public static MemberRef mrName = MemberRef.invalid;
		public static MemberRef mrValue = MemberRef.invalid;

		public PebbleEnum(ExecContext context, string _enumName, ITypeDef valueTypeIn) {
			enumName = _enumName;
			valueType = valueTypeIn;

			// Apparently need to register non-const first.
			ITypeDef nonConstEnumType = TypeFactory.GetTypeDef_Enum(_enumName, false);
			enumType = (TypeDef_Enum) TypeFactory.GetConstVersion(nonConstEnumType);
			_classDef = new ClassDef_Enum(this, _enumName, enumType);
			context.RegisterClass(_classDef);

			_classDef.childAllocator = () => {
				return new ClassValue_Enum();
			};
			_classDef.Initialize();


			//don't think this is needed _classDef.AddMemberLiteral("enumName", IntrinsicTypeDefs.CONST_STRING, _enumName, false);
			_classDef.AddMember("name", IntrinsicTypeDefs.CONST_STRING);
			_classDef.AddMember("value", valueType.Clone(true), null, false, true);

			{
				FunctionValue_Host.EvaluateDelegate eval = (_context, args, thisScope) => {
					ClassValue cv = thisScope as ClassValue;
					return enumName + "::" + cv.Get(mrName).value;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false, _classDef.typeDef);
				_classDef.AddMemberLiteral("ThisToString", newValue.valType, newValue, false);
			}

			_classDef.FinalizeClass(context);

			if (mrName.isInvalid) {
				mrName = _classDef.GetMemberRef("name", ClassDef.SEARCH.NORMAL);
				mrValue = _classDef.GetMemberRef("value", ClassDef.SEARCH.NORMAL);
			}
		}

		public bool AddValue_Default(ExecContext context, string valueName) {
			if (_names.ContainsKey(valueName))
				return false;
			_names.Add(valueName, true);

			_classDef.AddMember(valueName, enumType, null, true);

			EnumValue ev = new EnumValue();
			ev.name = valueName;
			_values.Add(ev);
			return true;
		}
		
		public bool AddValue_Literal(ExecContext context, string valueName, object litValue) {
			if (_names.ContainsKey(valueName))
				return false;
			_names.Add(valueName, true);

			_classDef.AddMember(valueName, enumType, null, true);

			EnumValue ev = new EnumValue();
			ev.name = valueName;
			ev.literalValue = litValue;
			_values.Add(ev);
			return true;
		}

		public bool AddValue_Expr(ExecContext context, string valueName, IExpr initializer) {
			if (_names.ContainsKey(valueName))
				return false;
			_names.Add(valueName, true);

			_classDef.AddMember(valueName, enumType, null, true);

			EnumValue ev = new EnumValue();
			ev.name = valueName;
			ev.initializer = initializer;
			_values.Add(ev);
			return true;
		}

		//! This currently does inefficient searching.
		public ClassValue_Enum GetValue(string name) {
			foreach (Variable var in _classDef.staticVars) {
				if (var.name == name)
					return (ClassValue_Enum) var.value;
			}
			return null;
		}

		public object EvaluateValues(ExecContext context) {
			bool isNum = valueType.CanStoreValue(context, IntrinsicTypeDefs.NUMBER);
			double nextInteger = 0;
			bool isString = valueType.CanStoreValue(context, IntrinsicTypeDefs.STRING);

			for (int ii = 0; ii < _values.Count; ++ii) { 
				ClassValue_Enum val = (ClassValue_Enum)_classDef.Allocate(context);
				val.Get(mrName).value = _values[ii].name;
				_classDef.staticVars[ii].value = val;

				if (null == _values[ii].initializer) {
					if (isNum) {
						// If number, use next consecutive integer.
						val.Get(mrValue).value = nextInteger;
						nextInteger = Math.Floor(nextInteger + 1);
					} else if (isString) {
						// If string, just use name.
						val.Get(mrValue).value = _values[ii].name;
					} else {
						// Use the default value.
						val.Get(mrValue).value = valueType.GetDefaultValue(context);
					}
				} else {
					object init = _values[ii].initializer.Evaluate(context);
					if (context.IsRuntimeErrorSet())
						return null;
					val.Get(mrValue).value = init;

					if (isNum)
						nextInteger = Math.Floor((double)init + 1);
				}
			}

			return false;
		}

		public ClassValue_Enum GetDefaultValue() {
			Pb.Assert(_classDef.staticVars.Count > 0);
			return _classDef.staticVars[0].value as ClassValue_Enum;
		}
	}
}