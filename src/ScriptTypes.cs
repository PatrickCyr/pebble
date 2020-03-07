/*
Implementation of Pebble's type system.
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class TypeFactory {
		public static Dictionary<string, ITypeDef> _typeRegistry = new Dictionary<string, ITypeDef>();

		public static TypeDef GetTypeDef(Type type, object defaultValue, bool isConst = false) {
			string name = "";
			if (null == type)
				name = "null";
			else {
				if (typeof(double) == type)
					name = "num";
				else if (typeof(string) == type)
					name = "string";
				else if (typeof(bool) == type)
					name = "bool";
				else
					Pb.Assert(false, "Unknown intrinsic type.");

				if (isConst)
					name = name + " const";
			}

			if (_typeRegistry.ContainsKey(name))
				return _typeRegistry[name] as TypeDef;

			TypeDef newDef = new TypeDef(type, defaultValue, isConst);
			_typeRegistry.Add(name, newDef);
			return newDef;
		}

		public static TypeDef_Function GetTypeDef_Function(ITypeDef retType, List<ITypeDef> argTypes, List<Expr_Literal> defaultValues, bool _varargs, TypeDef_Class classType, bool isConst, bool isStatic) {
			string args = "";
			for (int ii = 0; ii < argTypes.Count; ++ii) {
				string defaultValueString = "";
				if (null != defaultValues && null != defaultValues[ii])
					defaultValueString = " = " + (null != defaultValues[ii].value ? defaultValues[ii].value.ToString() : "null");
				args += (ii == 0 ? "" : ", ") + argTypes[ii] + defaultValueString;
			}

			string classPart = "";
			if (null != classType)
				classPart = ":" + classType.className;

			string name = "function" + classPart + "<" + retType + "(" + args + ")>";
			if (_varargs)
				name = name + " varargs";
			if (isStatic)
				name = name + " static";
			if (isConst)
				name = name + " const";

			if (_typeRegistry.ContainsKey(name))
				return _typeRegistry[name] as TypeDef_Function;

			TypeDef_Function newDef = new TypeDef_Function(retType, argTypes, defaultValues, _varargs, classType, isConst, isStatic);
			_typeRegistry.Add(name, newDef);
			return newDef;
		}

		public static TypeDef_Class GetTypeDef_Class(string className, List<ITypeDef> genericTypes, bool isConst) {
			string name;
			name = className;
			if (genericTypes != null) {
				string genPart = "<";
				for (int ii = 0; ii < genericTypes.Count; ++ii) {
					genPart += (ii == 0 ? "" : ", ") + genericTypes[ii].GetName();
				}
				genPart += ">";
				name += genPart;
			}

			if (isConst)
				name += " const";

			if (_typeRegistry.ContainsKey(name))
				return _typeRegistry[name] as TypeDef_Class;
			TypeDef_Class newDef = new TypeDef_Class(className, genericTypes, isConst);
			_typeRegistry.Add(name, newDef);
			return newDef;
		}

		public static TypeDef_Enum GetTypeDef_Enum(string className, bool isConst) {
			string name;
			name = className;
			if (isConst)
				name += " const";

			if (_typeRegistry.ContainsKey(name)) {
				Pb.Assert(_typeRegistry[name] is TypeDef_Enum, "Messed up enum in typeRegistry.");
				return _typeRegistry[name] as TypeDef_Enum;
			}
			TypeDef_Enum newDef = new TypeDef_Enum(className);
			_typeRegistry.Add(name, newDef);
			return newDef;
		}

		public static ITypeDef GetConstVersion(ITypeDef original) {
			if (original.IsConst())
				return original;

			string constName = original.GetName() + " const";
			if (_typeRegistry.ContainsKey(constName))
				return _typeRegistry[constName];

			ITypeDef result = original.Clone(true);
			_typeRegistry.Add(constName, result);
			return result;
		}

		public static TypeDef_Function GetClassVersion(TypeDef_Function original, TypeDef_Class classType, bool isStatic) {
			return GetTypeDef_Function(original.retType, original.argTypes, original.defaultValues, original.varargs, classType, original.isConst, isStatic);
		}
	}

	public interface ITypeDef {
		ITypeDef Clone(bool isConst);

		bool IsReference();

		object GetDefaultValue(ExecContext context);

		// Returns true if can be compared with other.
		// Used by Expr_Compare at TypeCheck time to see if the types can even be compared. 
		bool Comparable(ExecContext context, ITypeDef other);

		// Returns true if a variable of this type can store a value of valueType.
		bool CanStoreValue(ExecContext context, ITypeDef valueType);

		bool IsNull();

		string GetName();

		bool IsConst();
		
		ITypeDef ResolveTemplateTypes(List<ITypeDef> genericTypes, ref bool modified);
	}

	/* 
		TypeDef is "value type definition".  Every value in the game has a type defined by a ValType.
		Name isn't part of the definition: instead, that comes from the dictionary in the context.
		that maps names	to these definitions.
	*/
	public class TypeDef : ITypeDef {
		protected Type _type;
		protected object _defaultValue;
		protected bool _isConst;

		internal TypeDef(Type type, object defaultValue, bool isConst = false) {
			_type = type;
			_defaultValue = defaultValue;
			_isConst = isConst;
		}

		public virtual ITypeDef Clone(bool isConst) {
			return new TypeDef(_type, _defaultValue, isConst);
		}

		public virtual bool IsConst() {
			return _isConst;
		}

		public virtual bool IsReference() {
			return false;
		}

		public virtual bool IsNull() {
			return null == _type;
		}

		public override bool Equals(object obj) {
			if (obj is TypeDef) {
				TypeDef other = obj as TypeDef;
				if (other._type == null)
					return _type == null;

				return ((TypeDef)obj)._type.Equals(_type);
			}
			return base.Equals(obj);
		}

		public override int GetHashCode() {
			return base.GetHashCode();
		}

		public virtual object GetDefaultValue(ExecContext context) {
			return _defaultValue;
		}

		public virtual bool Comparable(ExecContext context, ITypeDef other) {
			return this.Equals(other);
		}

		public virtual ITypeDef ResolveTemplateTypes(List<ITypeDef> genericTypes, ref bool modified) {
			return this;
		}

		public virtual string GetName() {
			if (null == _type)
				return "null";
			if (typeof(double) == _type)
				return "num";
			if (typeof(string) == _type)
				return "string";
			if (typeof(bool) == _type)
				return "bool";

			//throw new RuntimeError(RuntimeErrorType.Internal, "Unknown intrinsic type?!");
			return "<unknown intrinsic type?!>";
		}

		public override string ToString() {
			string constStr = _isConst ? "const " : "";
			return null != _type ? (constStr + GetName()) : "<null>";
		}

		public virtual TypeDef getTemplateType() {
			return this;
		}

		public virtual bool CanStoreValue(ExecContext context, ITypeDef valueType) {
			return Equals(valueType);
		}

		public Type GetHostType() {
			return _type;
		}
	}

	public class TypeDef_Void : ITypeDef {
		public TypeDef_Void() {
		}

		public virtual ITypeDef Clone(bool isConst) {
			Pb.Assert(false, "Why clone 'void'?");
			return null;
		}

		public virtual bool IsReference() {
			return false;
		}

		public virtual object GetDefaultValue(ExecContext context) {
			Pb.Assert(false, "Can't get default value of 'void'.");
			return null;
		}

		public bool Comparable(ExecContext context, ITypeDef other) {
			Pb.Assert(false, "Can't call Comparable on 'void'.");
			return false;
		}

		public virtual bool CanStoreValue(ExecContext context, ITypeDef valueType) {
			Pb.Assert(false, "Probably shouldn't be trying to ask if you can store to 'void'.");
			return false;
		}

		public virtual bool IsNull() {
			return false;
		}

		public virtual string GetName() {
			Pb.Assert(false, "Can't get name for void.");
			return null;
		}

		public override string ToString() {
			return "void";
		}

		public virtual ITypeDef ResolveTemplateTypes(List<ITypeDef> genericTypes, ref bool modified) {
			return this;
		}

		public virtual bool IsConst() {
			Pb.Assert(false, "Cannot assign to void type, so cannot be checked for const.");
			return false;
		}
	}

	public class TypeDef_Any : ITypeDef {

		internal TypeDef_Any() {
		}

		public virtual ITypeDef Clone(bool isConst) {
			Pb.Assert(false, "Why clone 'Any'?");
			return null;
		}

		public virtual bool IsReference() {
			// hmm...what should this be?
			return false;
		}
		public virtual object GetDefaultValue(ExecContext context) {
			Pb.Assert(false, "Can't get default value of 'Any'.");
			return null;
		}

		public bool Comparable(ExecContext context, ITypeDef other) {
			Pb.Assert(false, "Can't call Comparable on 'Any'.");
			return false;
		}

		public virtual bool CanStoreValue(ExecContext context, ITypeDef valueType) {
			// This function is used to check for valid arguments.
			return true;
		}

		public virtual bool IsNull() {
			return false;
		}

		public virtual string GetName() {
			Pb.Assert(false, "Can't get name for Any.");
			return null;
		}

		public override string ToString() {
			return "#ANY";
		}

		public virtual ITypeDef ResolveTemplateTypes(List<ITypeDef> genericTypes, ref bool modified) {
			return this;
		}

		public virtual bool IsConst() {
			Pb.Assert(false, "Cannot assign to Any type, so cannot be checked for const.");
			return false;
		}
	}


	/**
		I created TypeDef_Template to be a TypeDef which is actually more like a TypeRef: it has to get resolved
		at some point.
		Why not just use TypeRef?  Well, I'm using these for built-in's, and it seemed inefficient to use 
		TypeRefs for them when the vast majority of the time we know exactly what types we want.
		Also, we are writing directly to the SymbolTables which define the class, and those are (at least 
		currently) assumed to be complete and not in need of resolving.
		Which is correct: they do not need to be resolved.  But their children may need to!

		So, that's it, then: they get resolved when a class is derived from a templated class!
	*/
	public class TypeDef_Template : ITypeDef {
		protected int _index;

		internal TypeDef_Template(int index) {
			_index = index;
		}

		public virtual ITypeDef Clone(bool isConst) {
			Pb.Assert(false, "Why are you Cloning a TypeDef_Template?");
			return null;
		}

		public virtual bool IsConst() {
			Pb.Assert(false, "Why are you checking a TypeDef_Template for IsConst?");
			return false;
		}

		public virtual bool IsReference() {
			Pb.Assert(false);
			return false;
		}

		public virtual object GetDefaultValue(ExecContext context) {
			Pb.Assert(false, "Can't GetDefaultValue of Template.");
			return null;
		}

		public virtual bool Comparable(ExecContext context, ITypeDef other) {
			Pb.Assert(false, "Can't call Comparable on Template.");
			return false;
		}

		// Returns true if a variable of this type can store a value of valueType.
		public virtual bool CanStoreValue(ExecContext context, ITypeDef valueType) {
			Pb.Assert(false, "Can't CanStoreValue on Template.");
			return false;
		}

		public virtual bool IsNull() {
			Pb.Assert(false);
			return false;
		}

		public virtual string GetName() {
			return "#Template" + _index + "#";
		}

		public override string ToString() {
			return GetName();
		}

		public virtual ITypeDef ResolveTemplateTypes(List<ITypeDef> genericTypes, ref bool modified) {
			modified = true;
			return genericTypes[_index];
		}
	}

	public class TypeDef_Function : ITypeDef {
		// The type of the return value. It is an ArgType because we need it to be templatable.  Shitty, but...
		public readonly ITypeDef retType;
		// The types of the arguments
		public readonly List<ITypeDef> argTypes;
		// Default values for the args. This list must always be the same length as argTypes, or the list can be null.
		// args without default values will have null in their slot in defaultValues.
		public readonly List<Expr_Literal> defaultValues;
		// If true, function accepts any number of arguments.  There must be at least one defined
		// argument.  The type of the unspecified arguments is the type of the final defined argument.
		public readonly bool varargs;
		public readonly bool isConst;
		// This is the type of the class that we are a member of, or null.
		public readonly TypeDef_Class classType;
		// This is used by CanStoreValue to allow function variables to store references to static class member functions.
		// It's a little wonky to put this in the type, but CanStoreValue needs this information.
		public readonly bool isStaticMember;

		internal TypeDef_Function(ITypeDef _retType, List<ITypeDef> _argTypes, List<Expr_Literal> defaultVals, bool _varargs, TypeDef_Class classTypeIn, bool _isConst, bool _isStaticFunction) {
			retType = _retType;
			argTypes = _argTypes;
			defaultValues = defaultVals;
			varargs = _varargs;
			isConst = _isConst;
			classType = classTypeIn;
			isStaticMember = _isStaticFunction;

			Pb.Assert(!_varargs || null == defaultValues, "internal error: function cannot be both varArgs and have arguments with default values.");
		}

		public virtual ITypeDef Clone(bool _isConst) {
			return new TypeDef_Function(retType, argTypes, defaultValues, varargs, classType, _isConst, isStaticMember);
		}

		public virtual bool IsConst() {
			return isConst;
		}

		public override string ToString() {
			string args = "";
			for (int ii = 0; ii < argTypes.Count; ++ii) {
				string defaultString = "";
				if (null != defaultValues && null != defaultValues[ii])
					defaultString = " = " + (null != defaultValues[ii].value ? defaultValues[ii].value.ToString() : "null");

				args += (ii == 0 ? "" : ", ") + argTypes[ii] + defaultString;
			}

			string classPart = "";
			if (null != classType)
				classPart = ":" + classType.className;

			return (isStaticMember ? "static " : "") + (isConst ? "const " : "") + "function"+classPart+"<" + retType + "(" + args + ")>";
		}

		public string GetDebugString(string name) {
			string result = "";
			result += retType.ToString() + " " + name + (null != classType ? ":" + classType.className : "") + "(";
			for (int iArg = 0; iArg < argTypes.Count; ++iArg) {
				ITypeDef argdef = argTypes[iArg] as ITypeDef;
				if (0 != iArg)
					result += ", ";
				result += argdef.ToString();
			}
			if (varargs)
				result += "[, ...]";
			result += ");\n";
			return result;
		}

		public virtual string GetName() {
			string args = "";
			for (int ii = 0; ii < argTypes.Count; ++ii) {
				string defaultString = "";
				if (null != defaultValues && null != defaultValues[ii])
					defaultString = " = " + defaultValues[ii].value.ToString();
				args += (ii == 0 ? "" : ", ") + argTypes[ii].GetName() + defaultString;
			}

			string classPart = "";
			if (null != classType)
				classPart = ":" + classType.className;

			return "function"+classPart+"<" + retType.GetName() + "(" + args + ")>";
		}

		public virtual bool Comparable(ExecContext context, ITypeDef other) {
			return Equals(other);
		}

		public override bool Equals(object obj) {
			TypeDef_Function other = obj as TypeDef_Function;
			if (!EqualsIgnoreClass(obj))
				return false;

			if (null != classType) {
				if (null == other.classType)
					return false;
				if (classType != other.classType)
					return false;
			} else if (null != other.classType)
				return false;

			return true;
		}

		public bool EqualsIgnoreClass(object obj) {
			TypeDef_Function other = obj as TypeDef_Function;
			if (null == other)
				return false;

			bool argsMatch = true;
			if (argTypes.Count == other.argTypes.Count) {
				for (int ii = 0; ii < argTypes.Count; ++ii) {
					if (argTypes[ii] != other.argTypes[ii])
						argsMatch = false;
				}
			} else
				argsMatch = false;

			if ((null == defaultValues) != (null == other.defaultValues))
				return false;

			// Make sure default values are also the same.
			if (null != defaultValues) {
				for (int ii = 0; ii < defaultValues.Count; ++ii) {
					if (null == defaultValues[ii]) {
						if (null != other.defaultValues[ii])
							return false;
					} else {
						if (null == other.defaultValues[ii])
							return false;
						else {
							if (!defaultValues[ii].value.Equals(other.defaultValues[ii].value))
								return false;
						}
					}
				}
			}

			return (retType == other.retType) && argsMatch && (varargs == other.varargs);
		}

		public override int GetHashCode() {
			return base.GetHashCode();
		}

		public virtual bool IsReference() {
			return true;
		}

		public virtual object GetDefaultValue(ExecContext context) {
			return null;
		}
		public virtual bool CanStoreValue(ExecContext context, ITypeDef valueType) {
			//if (valueType is TypeDef) {
			//	return null == (valueType as TypeDef).IsNull();
			//}
			if (valueType.IsNull())
				return true;

			TypeDef_Function funcValueType = valueType as TypeDef_Function;
			if (null == funcValueType)
				return false;

			if (!retType.Equals(IntrinsicTypeDefs.VOID) && !retType.CanStoreValue(context, funcValueType.retType))
				return false;

			// Not enough arguments.
			if (argTypes.Count > funcValueType.argTypes.Count)
				return false;

			if (argTypes.Count < funcValueType.argTypes.Count) {
				// (Assuming that if this first arg has a default value, all subsequent ones do.)
				if (null == funcValueType.defaultValues || null == funcValueType.defaultValues[argTypes.Count]) {
					// Too many arguments, and they don't have default values.
					return false;
				}
			}

			for (int ii = 0; ii < argTypes.Count; ++ii) {
				if (!funcValueType.argTypes[ii].CanStoreValue(context, argTypes[ii]))
					return false;
			}

			if (null != classType) {
				if (null == funcValueType.classType || !classType.CanStoreValue(context, funcValueType.classType))
					return false;
			// This is the point of isStatic in this class. This is saying, "I can save a reference to a class member function only if it is static."
			} else if (null != funcValueType.classType && !funcValueType.isStaticMember)
				return false;

			return true;
		}

		public virtual bool IsNull() {
			return false;
		}

		public virtual ITypeDef ResolveTemplateTypes(List<ITypeDef> genericTypes, ref bool modified) {
			List<ITypeDef> args = new List<ITypeDef>();
			for (int ii = 0; ii < argTypes.Count; ++ii) {
				args.Add(argTypes[ii].ResolveTemplateTypes(genericTypes, ref modified));
			}
			ITypeDef newRetType = retType.ResolveTemplateTypes(genericTypes, ref modified);
			if (null != classType)
				classType.ResolveTemplateTypes(genericTypes, ref modified);
			return TypeFactory.GetTypeDef_Function(newRetType, args, defaultValues, varargs, classType, isConst, isStaticMember);
		}
	}

	/* This is the Type of a variable which points to reference of a class.
	 * Because Class types never change, all we need is the name of the class.
	 * Also, if the class is generic, the variable *must* specify types for all it's 
	 * generic types.  Consider this:  List<num> myNumList;
	 */
	public class TypeDef_Class : ITypeDef {

		// We use this to look up the SymbolTable that defines the type in the context.
		public readonly string className;

		// This TYPE may be a concrete type.  For example, if _name is "List", but this is null, this TYPE is generic.
		// However, if this list has an entry of "num", then this TYPE is List<num>.
		protected List<ITypeDef> _genericTypes;

		// Cached full name.
		protected string _name = null;

		// isConst here doesn't mean the class is const (that makes no sense in Pebble), but
		// rather that the type of some variable is a const reference to a class.
		protected bool _isConst = false;
		protected object _defaultValue = null;
		protected Type _type;

		internal TypeDef_Class(string classNameIn, List<ITypeDef> genericTypes, bool isConst) {
			className = classNameIn;
			_genericTypes = genericTypes;

			_type = typeof(ClassDef);
			_defaultValue = null;
			_isConst = isConst;
		}

		public virtual ITypeDef Clone(bool isConst) {
			return new TypeDef_Class(className, _genericTypes, isConst);
		}

		public bool IsConst() {
			return _isConst;
		}

		public virtual bool IsReference() {
			return true;
		}

		public override string ToString() {
			return (_isConst ? "const " : "") + GetName();
		}

		public List<ITypeDef> genericTypes { get { return _genericTypes; } } 

		public virtual object GetDefaultValue(ExecContext context) {
			string name = GetName();
			ClassDef parent = context.GetClass(name);
			ClassValue result = parent.Allocate(context);
			if (null == result)
				return null;

			result.debugName = parent.name + " Inst";
			//result.typeDef = this;
			return result;
		}

		public bool Comparable(ExecContext context, ITypeDef other) {
			if (!(other is TypeDef_Class))
				return false;

			if (Equals(other))
				return true;

			TypeDef_Class otherClassType = other as TypeDef_Class;
			return null != context.DetermineAncestor(this, otherClassType);
		}

		public override bool Equals(object obj) {
			if (obj is TypeDef_Class) {
				TypeDef_Class other = obj as TypeDef_Class;
				return other.className == className;
			}
			return false;
		}

		public override int GetHashCode() {
			return base.GetHashCode();
		}

		public virtual bool CanStoreValue(ExecContext context, ITypeDef valueType) {
			if (valueType is TypeDef && null == ((TypeDef)valueType).GetHostType())
				return true;

			TypeDef_Class classValueType = valueType as TypeDef_Class;
			if (null == classValueType)
				return false;

			if (!context.IsChildClass(context.GetClass(className), context.GetClass(classValueType.className)))
				return false;

			// If neither has generic types, we match.
			if (null == classValueType._genericTypes && null == _genericTypes)
				return true;

			// If only one has generic types, we do not.
			if (null == classValueType._genericTypes || null == _genericTypes)
				return false;

			// If they don't have the same number, we do not.
			if (classValueType._genericTypes.Count != _genericTypes.Count)
				return false;

			for (int ii = 0; ii < _genericTypes.Count; ++ii) {
				if (_genericTypes[ii] != classValueType._genericTypes[ii])
					return false;
			}

			return true;
		}

		public bool IsNull() {
			return false;
		}

		public virtual string GetName() {
			if (_name == null) {
				_name = className;
				if (_genericTypes != null) {
					string genPart = "<";
					for (int ii = 0; ii < _genericTypes.Count; ++ii) {
						genPart += (ii == 0 ? "" : ", ") + _genericTypes[ii].GetName();
					}
					genPart += ">";
					_name += genPart;
				}
			}
			return _name;
		}

		public ITypeDef ResolveTemplateTypes(List<ITypeDef> genericTypesIn, ref bool modified) {
			if (null != _genericTypes && genericTypes.Count > 0) {
				modified = true;
				Pb.Assert(_genericTypes.Count == genericTypesIn.Count);
				return TypeFactory.GetTypeDef_Class(className, genericTypesIn, _isConst);
			}
			return this;
		}
	}

	public class TypeDef_Enum : TypeDef_Class {

		internal TypeDef_Enum(string classNameIn, bool isConst = false) : base(classNameIn, null, isConst) {
		}

		public override string GetName() {
			return className;
		}

		public override ITypeDef Clone(bool isConst) {
			return new TypeDef_Enum(className, isConst);
		}

		public override bool IsReference() {
			return false;
		}

		public override object GetDefaultValue(ExecContext context) {
			string name = GetName();
			ClassDef classDef = context.GetClass(name);
			ClassDef_Enum enumDef = classDef as ClassDef_Enum;
			return enumDef.enumDef.GetDefaultValue();
		}

		public override bool CanStoreValue(ExecContext context, ITypeDef valueType) {
			if (valueType is TypeDef_Enum)
				return ((TypeDef_Enum)valueType).className == className;

			return false;
		}
	}

	// ************************************************************************
	// TypeRefs
	// ************************************************************************

	public abstract class ITypeRef {
		public abstract ITypeDef Resolve(ExecContext context, ref bool error);
		public void SetConst(bool isConst) {
			_isConst = isConst;
		}

		protected bool _isConst = false;
	}

	/* 
		The parser produces "references to types": basically a type's name and optional template types.
		TypeRef holds that information.  At TypeCheck type, these are dereferenced to ensure that they 
		point to actual, defined types.

	Note that "static" is not part of something's type. static specifies storage, not type.
	*/
	public class TypeRef : ITypeRef {
		// The two bits of data that are needed to refer to a type.
		public readonly string name;
		protected List<ITypeRef> _templateTypes;
		
		public bool isConst = false;

		// I think we can cache these here because a given TypeRef will only ever point to 
		// a particular type of variable.
		protected ITypeDef _cachedValType;

		public TypeRef(string nameIn, List<ITypeRef> templateTypes = null) {
			Pb.Assert(null != nameIn, "TypeRef constructor passed null name.");
			name = nameIn;
			if (null != templateTypes && templateTypes.Count > 0)
				_templateTypes = templateTypes;
		}

		public List<ITypeRef> templateTypes { get { return _templateTypes; } } 

		public override string ToString() {
			return (_isConst ? "const " : "") + name;
		}

		//public override bool Equals(object obj) {
		//	throw new InvalidProgramException("INTERNAL ERROR: Attempt to use type before it has been evaluated. (Equals)");
		//}

		public override ITypeDef Resolve(ExecContext context, ref bool error) {
			if (name == "void")
				return IntrinsicTypeDefs.VOID;

			ITypeDef def = context.GetTypeByName(name);
			if (null == def) {
				context.engine.LogCompileError(ParseErrorType.TypeNotFound, "Type '" + name + "' not found.");
				error = true;
				return null;
			}

			if (null == _templateTypes) {
				if (_isConst && !def.IsConst())
					return def.Clone(true);
				return def;
			}

			// If we have template types, then we must be a TypeDef_Class.

			List<ITypeDef> genericTypes = new List<ITypeDef>();
			for (int ii = 0; ii < _templateTypes.Count; ++ii) {
				genericTypes.Add(_templateTypes[ii].Resolve(context, ref error));
			}

			if (error)
				return null;


			//TypeDef_Class result = new TypeDef_Class(name, genericTypes, _isConst);
			TypeDef_Class result = TypeFactory.GetTypeDef_Class(name, genericTypes, _isConst);
			if (null == context.RegisterIfUnregisteredTemplate(result))
				error = true;
			return result;
		}
	}

	/*
		Functions defined in code have this type. The parser doesn't know the types of the return
		value and arguments, but it knows their names so they can be looked up.
	*/
	public class TypeRef_Function : ITypeRef {
		public ITypeRef retType;
		public List<ITypeRef> argTypes;
		public List<Expr_Literal> defaultValues;
		public bool varArgs;
		public string className;

		public TypeRef_Function(ITypeRef retTypeIn, List<ITypeRef> argTypesIn, List<Expr_Literal> defaultValuesIn, bool varArgsIn = false, bool isConst = false) {
			retType = retTypeIn;
			argTypes = argTypesIn;
			varArgs = varArgsIn;
			_isConst = isConst;
			defaultValues = defaultValuesIn;
			if (null != defaultValues && 0 == defaultValues.Count)
				defaultValues = null;
		}

		/*
		public override string ToString() {
			string argString = "ARGS";
			return (_isConst ? "const " : "") + "<" + retType + "(" + argString + ")>";
		}
		*/

		public override ITypeDef Resolve(ExecContext context, ref bool error) {
			ITypeDef retValType = retType.Resolve(context, ref error);
			if (null == retValType) {
				context.engine.LogCompileError(ParseErrorType.TypeNotFound, "Type '" + retType + "' not found.");
				error = true;
				return null;
			}

			if (null != defaultValues && defaultValues.Count < argTypes.Count) {
				context.engine.LogCompileError(ParseErrorType.DefaultArgGap, "An argument with a default value is followed by an argument without one.");
				error = true;
				return null;
			}
			if (null != defaultValues) {
				int firstDefault = -1;
				for (int ii = 0; ii < defaultValues.Count; ++ii) {
					if (null != defaultValues[ii]) {
						if (firstDefault < 0)
							firstDefault = ii;
					} else if (firstDefault >= 0) {
						context.engine.LogCompileError(ParseErrorType.DefaultArgGap, "An argument with a default value is followed by an argument without one.");
						error = true;
						return null;
					}
				}
			}

			List<ITypeDef> argValTypes = new List<ITypeDef>();
			for (int ii = 0; ii < argTypes.Count; ++ii) {
				ITypeDef argValType = argTypes[ii].Resolve(context, ref error);
				if (null == argValType) {
					context.engine.LogCompileError(ParseErrorType.TypeNotFound, "Type '" + argTypes[ii] + "' not found.");
					error = true;
					return null;
				}

				argValTypes.Add(argValType);

				if (null != defaultValues && null != defaultValues[ii]) {
					ITypeDef valueType = defaultValues[ii].TypeCheck(context, ref error);
					if (!argValType.CanStoreValue(context, valueType)) {
						context.engine.LogCompileError(ParseErrorType.TypeMismatch, "Argument #" + ii + "'s type (" + argValType + ") can't hold default value of type (" + valueType + ").");
						error = true;
						return null;
					}
				}
			}

			TypeDef_Class classType = null;
			if (null != className) {
				ClassDef classDef = context.GetClass(className);
				if (null == classDef)
					Pb.Assert(false, "Internal error: error resolving class type.");
				classType = classDef.typeDef;
			}

			var ret = TypeFactory.GetTypeDef_Function(retValType, argValTypes, defaultValues, varArgs, classType, _isConst, false);
			return ret;
		}
	}


	///////////////////////////////////////////////////////////////////////////////

	public class IntrinsicTypeDefs {
		public static TypeDef_Any ANY = new TypeDef_Any();

		public static ITypeDef VOID = new TypeDef_Void();
		public static TypeDef NULL = TypeFactory.GetTypeDef(null, false);
		public static TypeDef BOOL = TypeFactory.GetTypeDef(typeof(bool), false);
		public static TypeDef NUMBER = TypeFactory.GetTypeDef(typeof(double), 0.0);
		public static TypeDef STRING = TypeFactory.GetTypeDef(typeof(string), "");
		public static TypeDef CONST_BOOL = TypeFactory.GetTypeDef(typeof(bool), 0.0, true);
		public static TypeDef CONST_NUMBER = TypeFactory.GetTypeDef(typeof(double), 0.0, true);
		public static TypeDef CONST_STRING = TypeFactory.GetTypeDef(typeof(string), "", true);
		public static TypeDef_Template TEMPLATE_0 = new TypeDef_Template(0);
		public static TypeDef_Template TEMPLATE_1 = new TypeDef_Template(1);
		public static TypeDef_Template TEMPLATE_2 = new TypeDef_Template(2);

		public static TypeDef_Class LIST_STRING = TypeFactory.GetTypeDef_Class("List", new ArgList { IntrinsicTypeDefs.STRING }, false);
	}

}
