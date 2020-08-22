/*
Implementation of all Pebble expressions (and "statements").
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	//////////////////////////////////////////////////////////////////////////////
	// IExpr

	public abstract class IExpr {
		// This is a list of this node's subnodes, ie. it's branches in the abstract syntax tree.
		// This was implemented at one point but then determined it wasn't needed. I'm going to 
		// leave it in in case I need it at some point in the future: ie. if I want to do a custom
		// pass over the tree.
		public List<IExpr> nodes;
		protected ITypeDef _type = null;

		protected string _scriptName = "";
		protected int _line = -1;
		protected int _col = -1;

		// If creating the IExpr during parsing (which is the case 99% of the time), pass it in so we can extract line/col info from it.
		public IExpr(Parser parser) {
			if (null != parser) {
				_scriptName = parser.scriptName;
				_line = parser.t.line;
				_col = parser.t.col;
			}
		}

		virtual public bool RegisterTypes(ExecContext context, ref bool error) {
			return true;
		}

		abstract public ITypeDef TypeCheck(ExecContext context, ref bool error);

		abstract public object Evaluate(ExecContext context);

		abstract public string MyToString(string indent);

		protected ITypeDef SetType(ITypeDef def) {
			//Pb.Assert(null == _type, "IExpr type already set.");
			_type = def;
			return _type;
		}
		public ITypeDef GetTypeDef() {
			Pb.Assert(null != _type, "IExpr has no type (yet?).");
			return _type;
		}

		public string GetFileLineString() {
			string s = null != _scriptName && _scriptName.Length > 0 ? _scriptName + " " : "";
			return s + "[" + _line + ":" + _col + "] ";
		}

		protected void LogCompileErr(ExecContext context, ParseErrorType error, string msg) {
			context.engine.LogCompileError(error, GetFileLineString() + error.ToString() + ": " + msg);
		}

		protected void SetRuntimeError(ExecContext context, RuntimeErrorType error, string msg) {
			context.SetRuntimeError(error, GetFileLineString() + error.ToString() + ": " + msg);
		}
	}

	public abstract class IExpr_LValue : IExpr {
		public IExpr_LValue(Parser parser) : base(parser) { }

		abstract public Variable EvaluateLValue(ExecContext context);
	}

	//////////////////////////////////////////////////////////////////////////////
	// Value - more literal than Literal! These are for values that don't need to be
	// typechecked. I added them specifically for List and Dictionary, because
	// classes are a clusterfuck.

	public class Expr_Value : IExpr {
		public object value;
		protected ITypeDef _typeDef;

		public Expr_Value(Parser parser, object val, ITypeDef typeDef) : base(parser) {
			value = val;
			_typeDef = typeDef;
			SetType(typeDef);
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			Pb.Assert(false);
			return null;
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			return value;
		}

		public override string ToString() {
			return "Value(" + _typeDef.ToString() + ", " + value.ToString() + ")";
		}

		public override string MyToString(string indent) {
			if (_typeDef != null) {
				if (_typeDef is TypeDef) {
					if ("num" == _typeDef.GetName())
						return value.ToString();
					else if ("bool" == _typeDef.GetName())
						return value.ToString();
					else if ("string" == _typeDef.GetName())
						return "\"" + value.ToString() + "\"";
				}
			}

			return "VALUE(" + _typeDef.ToString() + ", " + value.ToString() + ")";
		}
	};

	//////////////////////////////////////////////////////////////////////////////
	// Literal

	public class Expr_Literal : IExpr {
		public object value;
		protected ITypeRef _typeRef; // This is only used by function literals. 
									 // This is the input, unverified typedef.
		protected ITypeDef _typeDef;
		// The owning Expr_Class is responsible for setting this. 
		// static isn't part of something's type so there's no other way of telling, I think.
		public bool isStatic = false;

		public Expr_Literal(Parser parser, object val, ITypeRef typeRef) : base(parser) {
			value = val;
			_typeRef = typeRef;
		}

		public Expr_Literal(Parser parser, object val, ITypeDef typeDef) : base(parser) {
			value = val;
			_typeDef = typeDef;

			// Replace escape characters.
			if (value is string) {
				string str = (string)value;
				str = str.Replace("\\\\", "\\");
				str = str.Replace("\\\"", "\"");
				str = str.Replace("\\n", "\n");
				str = str.Replace("\\t", "\t");

				value = str;
			}
		}

		public void ForceTypeDef(ITypeDef typeDef) {
			Pb.Assert(null == _typeDef);
			_typeDef = typeDef;
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (null == _typeDef) {
				_typeDef = _typeRef.Resolve(context, ref error);
				if (null == _typeDef || error)
					return null;
			}

			if (error)
				return null;

			// If this is a function literal...
			if (value is FunctionValue_Script) {
				FunctionValue_Script inLang = value as FunctionValue_Script;
				inLang.valType = inLang.typeRef.Resolve(context, ref error) as TypeDef_Function;
				if (null == inLang.valType || error) {
					LogCompileErr(context, ParseErrorType.TypeNotFound, "Type not found.");
					error = true;
					return null;
				}

				TypeDef_Function funcType = _typeDef as TypeDef_Function;

				ITypeDef bodyType = null;

				// Push a scope for the function body.
				ClassDef classDef = null;
				bool pushResult = false;
				if (null != funcType.classType) {
					classDef = context.GetClass(funcType.classType.className);
					pushResult = context.stack.PushClassCall_StaticOrTypeCheck(funcType, classDef, false, context);
				} else if (null != inLang.staticClassDef) {
					pushResult = context.stack.PushClassCall_StaticOrTypeCheck(funcType, inLang.staticClassDef, true, context);
				} else {
					pushResult = context.stack.PushCall(funcType, inLang.originalName, null, false, null);
				}

				if (!pushResult) {
					LogCompileErr(context, ParseErrorType.StackOverflow, "Expr_Literal : stack overflow pushing scope for function body.");
					error = true;
					return null;
				}

				{
					// This adds the name of THIS function to THIS function body's scope, so that we may call the function 
					// recursively. We obviously don't need to do that for class members.
					if (null == funcType.classType && null == inLang.staticClassDef)
						context.stack.AddVariable(inLang.originalName, false, funcType, null);

					// Add the function arguments to the body's scope.
					for (int iArg = 0; iArg < funcType.argTypes.Count; ++iArg) {
						if (!context.stack.IsSymbolAvailable(context, inLang.argNames[iArg], true)) {
							// I *think* this is the only situation where an argument name can collide with something.
							LogCompileErr(context, ParseErrorType.SymbolAlreadyDeclared, "Member function argument " + inLang.argNames[iArg] + " is the same as a field of class.");
							error = true;
							return null;
						}

						if (!context.CreateTemp(inLang.argNames[iArg], funcType.argTypes[iArg], false, true).isValid) {
							LogCompileErr(context, ParseErrorType.SymbolAlreadyDeclared, "Unexpected symbol collision on argument name!");
							error = true;
						}
					}

					// Every function written in Pebble is a literal. This is where we type check any default values in Pebble script function arguments for type correctness.
					if (funcType.minArgs < funcType.argTypes.Count) {
						// More thorough gap check, and default value type check.
						if (null != inLang.argDefaultValues) {
							for (int ii = funcType.minArgs; ii < funcType.argTypes.Count; ++ii) {
								ITypeDef defaultType = inLang.argDefaultValues[ii].TypeCheck(context, ref error);
								if (!funcType.argTypes[ii].CanStoreValue(context, defaultType)) {
									LogCompileErr(context, ParseErrorType.TypeMismatch, "Function (" + funcType.GetName() + ") argument #" + ii + " type doesn't match default value type.");
									error = true;
								}
							}
						}
					}

					// The body's exprlist doesn't need to create a scope block, because functions always
					// have a blocking scope applied first.
					var body = inLang.expr as Expr_ExprList;
					body.createScope = false;

					// Finally, type check the function body.
					bodyType = inLang.expr.TypeCheck(context, ref error);
				}
				context.stack.PopScope();

				if (!error &&
					IntrinsicTypeDefs.VOID != inLang.valType.retType &&
					!inLang.valType.retType.CanStoreValue(context, bodyType)) {
					LogCompileErr(context, ParseErrorType.TypeMismatch, "Function body type " + bodyType + " doesn't match function return type " + inLang.valType.retType + ".");
					error = true;
					return null;
				}

				inLang.typeDef = inLang.typeRef.Resolve(context, ref error) as TypeDef_Function;
			}
			return SetType(_typeDef);
		}
		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			return value;
		}
		public override string ToString() {
			if (_typeDef != null) {
				if (_typeDef is TypeDef) {
					if ("num" == _typeDef.GetName())
						return value.ToString();
					else if ("bool" == _typeDef.GetName())
						return value.ToString();
					else if ("string" == _typeDef.GetName())
						return "\"" + value.ToString() + "\"";
				}
			}

			return "LITERAL(" + (null != _typeDef ? _typeDef.ToString() : _typeRef.ToString()) + ", " + (null != value ? value.ToString() : "null") + ")";
		}
		public override string MyToString(string indent) {
			if (_typeDef != null) {
				if (_typeDef is TypeDef) {
					if ("num" == _typeDef.GetName())
						return value.ToString();
					else if ("bool" == _typeDef.GetName())
						return value.ToString();
					else if ("string" == _typeDef.GetName())
						return "\"" + value.ToString() + "\"";
				}
			}

			return "LITERAL(" + (null != _typeDef ? _typeDef.ToString() : _typeRef.ToString()) + ", " + (null != value ? value.ToString() : "null") + ")";
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Symbol
	// - Basically when you see x + y, thats Add(Symbol(x), Symbol(y)).
	//	Symbol is a variable lookup.

	public class Expr_Symbol : IExpr_LValue {
		protected string _symbol;
		//public bool write = false;
		//@ Experiment: Saving variable reference in TypeCheck phase.
		// The variable found during TypeCheck should be the same as the one found during execution, right?! Otherwise, how would you know the type was valid?!
		// Well...it is, but...TypeCheck sometimes check variables in scopes that get destroyed after TypeCheck finishes.  Hmmm.....
		// A bummer, because maybe 2/3rds of this test is taken up by looking up symbols over and over again.
		// THOUGHT: Why am I creating these temp scopes and variables in TypeCheck and then creating them again when I execute?  Maybe because of things
		// like variables created in loops?  But I was thinking I wanted to reuse those anyway, if possible, right?
		//public Variable _value;
		protected VarStackRef _ref;

		public Expr_Symbol(Parser parser, string name) : base(parser) {
			Pb.Assert(null != name, "Expr_Symbol constructor: name = null!");
			_symbol = name;
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (null != _type) return _type;

			if (Pb.reservedWords.Contains(_symbol)) {
				LogCompileErr(context, ParseErrorType.InvalidSymbolName, "'" + _symbol + "' is a reserved word.");
				error = true;
				return null;
			}

			_ref = context.GetVarRefByName(context, _symbol);
			if (!_ref.isValid) {
				LogCompileErr(context, ParseErrorType.SymbolNotFound, "Symbol (" + _symbol + ") not found.");
				error = true;
				return null;
			}

			return SetType(_ref.typeDef);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable result = context.stack.GetVarAtIndex(_ref);
			Pb.Assert(null != result);
			return result.value;
		}

		public override Variable EvaluateLValue(ExecContext context) {
			return context.stack.GetVarAtIndex(_ref);
		}

		public string GetName() { return _symbol; }

		public override string ToString() {
			return _symbol;
		}

		public override string MyToString(string indent) {
			return _symbol;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Dot

	public class Expr_Dot : IExpr_LValue {
		protected IExpr _obj { get { return nodes[0]; } }
		protected string _field;
		//protected VarStackRef _vsr;
		protected MemberRef _fieldRef;

		// When we evaluate we set this variable to the class we found.
		// Then, Expr_Call can use this as the scope of the function.
		// It's shitty, but happens because Call encapsulates Dot in the AST.
		public ClassValue resolvedClassValue;

		public Expr_Dot(Parser parser, IExpr obj, string field) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(obj);

			_field = field;
		}

		public override string ToString() {
			return "DOT(" + _obj + ", " + _field + ")";
		}

		public override string MyToString(string indent) {
			return "DOT(" + _obj.MyToString(indent) + ", " + _field + ")";
		}

		public string GetFieldName() {
			return _field;
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			// Type-check the symbol we are looking up.
			ITypeDef objType = _obj.TypeCheck(context, ref error);
			if (error)
				return null;

			// It *must* be a class: that's what the dot operator does!
			TypeDef_Class scopeType = objType as TypeDef_Class;
			if (null == scopeType) {
				LogCompileErr(context, ParseErrorType.DotMustBeScope, "Left hand side of of dot operator must be of type scope.");
				error = true;
				return null;
			}

			string scopeName = scopeType.GetName();
			ClassDef scope = context.GetClass(scopeName);
			if (null == scope) {
				// I don't think this can ever happen anymore.
				LogCompileErr(context, ParseErrorType.ClassNotDeclared, "An instance of class " + scopeName + " must be new'ed before you can reference any fields on it.");
				error = true;
				return null;
			}

			ITypeDef fieldType = null;
			// Dot operator can only return normal fields. The Scope operator is what returns static members.
			MemberRef fieldRef = scope.GetMemberRef(context, _field, ClassDef.SEARCH.NORMAL, ref fieldType);
			if (fieldRef.isInvalid) {
				LogCompileErr(context, ParseErrorType.ClassMemberNotFound, "Member " + _field + " not found in class " + scopeName + ".");
				error = true;
				return null;
			}

			//_vsr = new VarStackRef(fieldType, ix, true);
			_fieldRef = fieldRef;

			return SetType(fieldType);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable result = EvaluateLValue(context);
			return null != result ? result.value : null;
		}

		public override Variable EvaluateLValue(ExecContext context) {
			object result = _obj.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			if (null == result) {
				SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "Dot: lhs is null.");
				return null;
			}

			resolvedClassValue = result as ClassValue;

			if (null == resolvedClassValue) {
				SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "Dot: object reference is null.");
				return null;
			}

			return resolvedClassValue.Get(_fieldRef); //?
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Scope
	public class Expr_Scope : IExpr_LValue {
		protected string _className;
		protected string _field;

		protected MemberRef _fieldRef;
		protected ClassDef _scope;
		protected VarStackRef _globVarRef;

		public Expr_Scope(Parser parser, string className, string field) : base(parser) {
			_className = className;
			_field = field;
		}

		public override string ToString() {
			return "SCOPE(" + _type + ", " + _field + ")";
		}

		public override string MyToString(string indent) {
			return "SCOPE(" + _type + ", " + _field + ")";
		}

		public string GetName() { return _field; }

		public MemberRef GetMemberRef() { return _fieldRef; }

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			ITypeDef fieldType = null;
			if (null == _className) {
				_globVarRef = context.GetVarRefByName(context, _field, true);
				if (!_globVarRef.isValid) {
					LogCompileErr(context, ParseErrorType.SymbolNotFound, "No global found with name '" + _field + "'.");
					error = true;
					return null;
				}
				fieldType = _globVarRef.typeDef;
			} else {
				_scope = context.GetClass(_className);
				if (null == _scope) {
					LogCompileErr(context, ParseErrorType.ClassNotDeclared, "Class " + _className + " not found.");
					error = true;
					return null;
				}

				// Search first for statics.
				_fieldRef = _scope.GetMemberRef(context, _field, ClassDef.SEARCH.STATIC, ref fieldType);
				if (_fieldRef.isInvalid) {
					ClassDef currentClassDef = context.stack.GetCurrentClassDef();
					if (null != currentClassDef) {
						if (currentClassDef.IsChildOf(_scope)) {
							_fieldRef = _scope.GetMemberRef(context, _field, ClassDef.SEARCH.NORMAL, ref fieldType);
						}
					}
				}

				if (_fieldRef.isInvalid) {
					LogCompileErr(context, ParseErrorType.ClassMemberNotFound, "No static member (" + _field + ") found in class " + _className + ".");
					error = true;
					return null;
				}
			}

#if PEBBLE_TRACESTACK
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("@" + _className + "." + _field + " -> " + _fieldRef.ToString());
			Console.ForegroundColor = ConsoleColor.Gray;
#endif

			return SetType(fieldType);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable result = EvaluateLValue(context);
			return null != result ? result.value : null;
		}

		public override Variable EvaluateLValue(ExecContext context) {
			return _globVarRef.isValid ? context.stack.GetVarAtIndex(_globVarRef) : _scope.GetVariable(_fieldRef);
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Index

	public class Expr_Index : IExpr_LValue {
		protected IExpr _symExpr { get { return nodes[0]; } }
		protected IExpr _ixExpr { get { return nodes[1]; } }

		public Expr_Index(Parser parser, IExpr sym, IExpr ix) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(sym);
			nodes.Add(ix);
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef symType = _symExpr.TypeCheck(context, ref error);
			ITypeDef ixType = _ixExpr.TypeCheck(context, ref error);
			if (error)
				return null;

			TypeDef_Class classType = symType as TypeDef_Class;
			if (null == classType) {
				LogCompileErr(context, ParseErrorType.TypeNotIndexable, "Left hand side of of index operator must be a class (list or dictionary).");
				error = true;
				return null;
			}

			ITypeDef ixTypeDef = ixType as ITypeDef;
			if ("List" == classType.className) {
				if (null == ixTypeDef || !(ixTypeDef is TypeDef) || ((TypeDef)ixTypeDef).GetHostType() != typeof(double)) {
					LogCompileErr(context, ParseErrorType.IndexNotNumeric, "List indices must be numeric.");
					error = true;
					return null;
				}

				return SetType(classType.genericTypes[0]);

			} else if ("Dictionary" == classType.className) {
				if (null == ixTypeDef || !classType.genericTypes[0].CanStoreValue(context, ixTypeDef)) {
					LogCompileErr(context, ParseErrorType.IndexNotNumeric, "Dictionary index must be " + classType.genericTypes[0].GetName() + ".");
					error = true;
					return null;
				}

				return SetType(classType.genericTypes[1]);
			}

			LogCompileErr(context, ParseErrorType.TypeNotIndexable, "Left hand side of of index operator must be a list or dictionary.");
			error = true;
			return null;
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable result = EvaluateLValue(context);
			return null != result ? result.value : null;
		}

		public override Variable EvaluateLValue(ExecContext context) {
			object container = _symExpr.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;
			if (null == container) {
				SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "Attempt to index null container.");
				return null;
			}

			object ixObj = _ixExpr.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			if (container is PebbleList) {
				List<Variable> list = (container as PebbleList).list;

				double ix = (double)ixObj;
				int iix = Convert.ToInt32(ix);
				if (iix < 0)
					iix = list.Count + iix;

				if (iix < 0 || iix >= list.Count) {
					SetRuntimeError(context, RuntimeErrorType.ArrayIndexOutOfBounds, "Index (" + iix + ") out of bounds.");
					return null;
				}
				return list[iix];
			}

			// If here this must be a dictionary, not a list.
			Dictionary<object, Variable> dictionary = (container as PebbleDictionary).dictionary;

			if (!dictionary.ContainsKey(ixObj)) {
				SetRuntimeError(context, RuntimeErrorType.DictionaryDoesntContainKey, "Dictionary doesn't contain key (" + ixObj.ToString() + ").");
				return null;
			}

			return dictionary[ixObj];
		}

		public override string MyToString(string indent) {
			return "INDEX(" + _symExpr.MyToString(indent) + "[" + _ixExpr + "])";
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Call

	public class Expr_Call : IExpr {
		// There are two types of calls.
		// Have to use name here instead of Expr_Symbol because we don't know the *type* of the function 
		// from just it's name.  Kind of shitty.
		protected string _name;

		protected IExpr _funcExpr { get { return nodes[0]; } }
		// Note that are in BOTH args and in nodes, for convenience.
		public List<IExpr> args;

		protected ITypeDef _actualRetType;
		TypeDef_Function fType;

		public static IExpr Create(Parser parser, string name, IExpr arg1 = null, IExpr arg2 = null, IExpr arg3 = null) {
			List<IExpr> args = new List<IExpr>();
			if (null != arg1)
				args.Add(arg1);
			if (null != arg2)
				args.Add(arg2);
			if (null != arg3)
				args.Add(arg3);
			return new Expr_Call(parser, new Expr_Symbol(parser, name), args);
		}

		public Expr_Call(Parser parser, IExpr funcExpr, List<IExpr> argsIn) : base(parser) {
			Pb.Assert(null != funcExpr);
			nodes = new List<IExpr>();
			nodes.Add(funcExpr);
			args = null != argsIn ? argsIn : new List<IExpr>();
			foreach (var arg in args) {
				nodes.Add(arg);
			}
		}

		public override string ToString() {

			string argStr = "";
			if (null != args) {
				foreach (IExpr arg in args) {
					argStr += ", " + arg;
				}
			}

			string name = null != _funcExpr ? _funcExpr.ToString() : _name;
			return "CALL(" + name + argStr + ")";
		}

		public override string MyToString(string indent) {

			string argStr = "";
			if (null != args) {
				foreach (IExpr arg in args) {
					argStr += ", " + arg.MyToString(indent);
				}
			}

			string name = null != _funcExpr ? _funcExpr.MyToString(indent) : _name;
			return "CALL(" + name + argStr + ")";
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {

			// Not type checking, just looking up the name.
			if (_funcExpr is Expr_Symbol)
				_name = ((Expr_Symbol)_funcExpr).GetName();
			else if (_funcExpr is Expr_Dot)
				_name = ((Expr_Dot)_funcExpr).GetFieldName();
			else if (_funcExpr is Expr_Scope)
				_name = ((Expr_Scope)_funcExpr).GetName();
			else {
				LogCompileErr(context, ParseErrorType.SyntaxError, "Internal Error: Expr_Call given an expression of a type it didn't anticipate.");
				error = true;
				return null;
			}

			ITypeDef funcExprType = _funcExpr.TypeCheck(context, ref error);
			if (error) {
				// I'm not sure we can ever get here without already having logged an error, 
				// so this is probably unnecessary.
				//LogCompileErr(context, ParseErrorType.SymbolNotFound, "Function (" + _funcExpr + ") not found.");
				return null;
			}

			// Check that expression's type is function.
			fType = funcExprType as TypeDef_Function;
			if (null == fType) {
				LogCompileErr(context, ParseErrorType.TypeMismatch, "(" + _name + ") is not a function.");
				error = true;
				return null;
			}

			// Arg count.
			if (fType.varargs) {
				if (fType.argTypes.Count > args.Count) {
					LogCompileErr(context, ParseErrorType.ArgCountMismatch, "Function (" + _name + ") requires at least " + fType.argTypes.Count + " args, got " + args.Count + ".");
					error = true;
				}
			} else {
				if (fType.argTypes.Count != args.Count) {
					if (args.Count > fType.argTypes.Count || args.Count < fType.minArgs) {
						LogCompileErr(context, ParseErrorType.ArgCountMismatch, "Function (" + _name + ") requires " + fType.argTypes.Count + " args, got " + args.Count + ".");
						error = true;
					}
				}
			}

			if (error)
				return null;

			// Args.
			for (int iArg = 0; iArg < args.Count; ++iArg) {
				IExpr arg = args[iArg];
				ITypeDef argValType = arg.TypeCheck(context, ref error);
				if (error)
					break;
				if (null == argValType) {
					error = true;
					break;
				}

				ITypeDef unresolvedFuncArgType = iArg >= fType.argTypes.Count ? fType.argTypes[fType.argTypes.Count - 1] : fType.argTypes[iArg];
				//bool modified = false;
				ITypeDef resolvedFuncArgType = unresolvedFuncArgType;
				//ITypeDef resolvedFuncArgType = unresolvedFuncArgType.ResolveTemplateTypes(genericTypes, ref modified);

				if (!resolvedFuncArgType.CanStoreValue(context, argValType)) {
					LogCompileErr(context, ParseErrorType.TypeMismatch, "Expr_Call : Function (" + _name + ") argument #" + iArg + " type mismatch, expected (" + unresolvedFuncArgType.ToString() + ") got (" + argValType.ToString() + ").");
					error = true;
				}
			}

			if (error)
				return null;

			_actualRetType = fType.retType;

			return SetType(_actualRetType);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object result = null;
			if (null != _funcExpr) {
				result = _funcExpr.Evaluate(context);
				if (context.IsRuntimeErrorSet())
					return null;
				if (null == result) {
					SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "Attempt to call a null function variable.");
					return null;
				}
				Pb.Assert(0 == context.control.flags);
				Pb.Assert(result is FunctionValue, "Function expression did not return a function.");

				FunctionValue funcVal = result as FunctionValue;

				// This is important code which handles the complicated issue of calling functions with a scope.
				ClassValue cv = null;
				if (_funcExpr is Expr_Dot)
					cv = ((Expr_Dot)_funcExpr).resolvedClassValue;
				else if (_funcExpr is Expr_Scope) {
					// Don't need a class value if you're looking up a static field, but 
					// if the Scope is looking up a base class version of a function then you do.
					Expr_Scope exprScope = (Expr_Scope)_funcExpr;
					if (ClassDef.MemberType.FUNCTION == exprScope.GetMemberRef().memberType)
						cv = context.stack.GetCurrentClassValue();
				} else if (null != ((TypeDef_Function)_funcExpr.GetTypeDef()).classType) {
					cv = context.stack.GetCurrentClassValue();
				}

				// Evaluate args here. Used to do it within the functions themselves (ie library functions), but
				// why duplicate the shit out of this error checking code?
				List<object> argvals = new List<object>();
				for (int ii = 0; ii < args.Count; ++ii) {
					object argval = args[ii].Evaluate(context);
					if (context.IsRuntimeErrorSet())
						return null;
					argvals.Add(argval);
				}

				// Fill in default values for unprovided arguments.
				/*
				TypeDef_Function tdf = funcVal.valType;
				// Here, we first see if the value's type has a default value, and use that if so.
				// If not, use the variable type's default value.
				for (int ii = args.Count; ii < tdf.argTypes.Count; ++ii) {
					object argVal;
					if (null != tdf.defaultValues && null != tdf.defaultValues[ii])
						argVal = tdf.defaultValues[ii].Evaluate(context);
					else {
						TypeDef_Function varTypeDef = (TypeDef_Function)_funcExpr.GetTypeDef();
						argVal = varTypeDef.defaultValues[ii].Evaluate(context);
					}
					argvals.Add(argVal);
				}
				*/
				for (int ii = args.Count; ii < funcVal.valType.argTypes.Count; ++ii) {
					object argVal;
					argVal = funcVal.argDefaultValues[ii].Evaluate(context);
					if (context.IsRuntimeErrorSet())
						return null;
					argvals.Add(argVal);
				}

				// Finally, call the function!
				result = funcVal.Evaluate(context, argvals, cv);
				if (context.IsRuntimeErrorSet()) {
					// If this is a host function (written in C#), any RuntimeError set will not have a file and line number.
					// So, we check here if it is a C# function and apply our own file and line info.
					if (funcVal is FunctionValue_Host) {
						// This overrides the current runtime error with one just like it, but with the file and line info of this expression added.
						SetRuntimeError(context, context.control.runtimeError.type, context.control.runtimeError.msg);
					}
					return null;
				}

			} /*else if (null != functionValue) {
				// Evaluate args here. Used to do it within the functions themselves (ie library functions), but
				// why duplicate the shit out of this error checking code?
				List<object> argvals = new List<object>();
				for (int ii = 0; ii < args.Count; ++ii) {
					object argval = args[ii].Evaluate(context);
					if (context.IsRuntimeErrorSet())
						return null;
					argvals.Add(argval);
				}

				result = functionValue.Evaluate(context, argvals);
				if (context.IsRuntimeErrorSet())
					return null;
			} */else {
				throw new Exception("Moar function bullshit.");
			}

			if (result is double) {
				double d = (double)result;
				if (Double.IsInfinity(d) || Double.IsNaN(d)) {
					SetRuntimeError(context, RuntimeErrorType.NumberInvalid, "Function returned an invalid number.");
					return null;
				}
			}

			//? This check is pointless if it has a templated return type.  Is it totally necessary anyway?  I don't think so: we've already type checked it in TypeCheck.
			//if (!fType.retType.GetValType(context).IsValueOfType(result)) {
			//	throw new InvalidReturnTypeException("Expr_Call.Evaluate : Return type from function (" + _name + ") doesn't match expected type (" + fType.retType + ")!");
			//}
			return result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// ExprList

	/**
	 * Expression lists must have at least 1 expression so we know what the value of the expression is.
	 * 
	 * This is only an expression because there is currently no way to define a function
	 * that has var args of any but a last parameter of type template.
	 */
	public class Expr_ExprList : IExpr {
		public bool createScope = true;

		public Expr_ExprList(Parser parser) : base(parser) {
			nodes = new List<IExpr>();
		}

		public override bool RegisterTypes(ExecContext context, ref bool error) {
			for (int ii = 0; ii < nodes.Count; ++ii) {
				if (!nodes[ii].RegisterTypes(context, ref error))
					return false;
			}

			return true;
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef lastExprType = null;
			if (createScope) {
				if (!context.stack.PushBlock("__exprlist", context)) {
					LogCompileErr(context, ParseErrorType.StackOverflow, "exprlist: stack overflow pushing block.");
					error = true;
					return null;
				}
			}

			for (int ii = 0; ii < nodes.Count; ++ii) {
				lastExprType = nodes[ii].TypeCheck(context, ref error);
			}

			if (createScope)
				context.stack.PopScope();

			return SetType(lastExprType);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object ret = null;

			if (createScope) {
				if (!context.stack.PushBlock("__exprlist", context)) {
					SetRuntimeError(context, RuntimeErrorType.StackOverflow, "exprlist: stack overflow pushing block");
					return null;
				}
			}

			foreach (IExpr expr in nodes) {
				Pb.Assert(expr != null, "You found an example of a null expression in an ExprList!  Fix it!");

				ret = expr.Evaluate(context);
				if (context.IsRuntimeErrorSet()) {
					if (createScope)
						context.stack.PopScope();

					return null;
				}

				if (context.control.flags != 0)
					break;
			}

			if (createScope)
				context.stack.PopScope();

			return ret;
		}

		public override string ToString() {
			string res = "EXPRLIST(\n";
			foreach (IExpr expr in nodes)
				res += "  " + (null != expr ? expr.ToString() : "<null>") + ";\n";
			res += ")";
			return res;
		}

		public override string MyToString(string indent) {
			string res = "EXPRLIST(\n";
			foreach (IExpr expr in nodes)
				res += indent + "  " + (null != expr ? expr.MyToString(indent + "  ") : "<null>") + ";\n";
			res += indent + ")";
			return res;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Assign

	/**
	 * Assignment returns the value of the symbol.
	 * However, it *should* return a reference to the symbol, to be more like C++, etc.
	 */
	public class Expr_Assign : IExpr {
		public IExpr symbol { get { return (IExpr_LValue)nodes[0]; } }
		public IExpr valueExpr { get { return nodes[1]; } }

		public static IExpr CreateInc(Parser parser, IExpr expr) {
			return new Expr_Assign(parser, expr, new Expr_BinOp(parser, Expr_BinOp.OP.ADD, expr, new Expr_Literal(parser, 1.0, IntrinsicTypeDefs.NUMBER)));
		}

		public static IExpr CreateDec(Parser parser, IExpr expr) {
			return new Expr_Assign(parser, expr, new Expr_BinOp(parser, Expr_BinOp.OP.SUB, expr, new Expr_Literal(parser, 1.0, IntrinsicTypeDefs.NUMBER)));
		}

		public Expr_Assign(Parser parser, IExpr sym, IExpr val) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(sym);
			nodes.Add(val);
		}

		public override string ToString() {
			return "ASSIGN(" + symbol + " = " + valueExpr + ")";
		}

		public override string MyToString(string indent) {
			return "ASSIGN(" + (null != symbol ? symbol.MyToString(indent) : "<null>") + " = " + valueExpr.MyToString(indent) + ")";
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (!(nodes[0] is IExpr_LValue)) {
				LogCompileErr(context, ParseErrorType.AssignToNonLValue, "Assignment: Left expression isn't an L-value.");
				error = true;
				return null;
			}

			ITypeDef symbolType = symbol.TypeCheck(context, ref error);
			if (null == symbolType) {
				error = true;
				return null;
			}

			if (valueExpr is Expr_New) {
				Expr_New eNew = (Expr_New)valueExpr;
				if (null == eNew.typeRef) {
					eNew.parentSetTypeDef = symbolType;
				}
			}

			ITypeDef valueType = valueExpr.TypeCheck(context, ref error);

			if (symbolType.IsConst()) {
				LogCompileErr(context, ParseErrorType.AssignToConst, "Assignment: L-value (" + symbol + ") is read-only.");
				error = true;
			}

			if (error)
				return null;

			if (!symbolType.CanStoreValue(context, valueType)) {
				LogCompileErr(context, ParseErrorType.TypeMismatch, "Assignment: L-value (" + symbol + ") type (" + symbolType + ") doesn't match value type (" + valueType + ").");
				error = true;
			}

			return SetType(valueType);
		}
		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable vr = ((IExpr_LValue)symbol).EvaluateLValue(context);
			if (context.IsRuntimeErrorSet())
				return null;

			object val = valueExpr.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			vr.value = val;
			return val;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Set

	public class Expr_Set : IExpr_LValue {
		public IExpr value { get { return nodes != null ? nodes[0] : null; } }

		public ITypeRef typeRef;
		public string symbol;
		public DeclMods declMods;
		public bool createTemp = true;
		public bool typeCheckValue = true;
		public ClassDef owningClass = null;
		public bool isFunctionLiteral = false;
		public ITypeDef typeDef;
		protected bool _funcInitError = false;
		protected bool _insufficientDefaults = false;

		// This is called by the parser.
		public static IExpr CreateFunctionLiteral(Parser parser, ITypeRef retType, string sym, List<ITypeRef> argTypes, List<Expr_Literal> defaultValues, List<string> argNames, IExpr body, DeclMods mods) {
			int minArgs = argTypes.Count;
			if (null != defaultValues && 0 == defaultValues.Count) 
				defaultValues = null;

			// The default arg checking code scattered everywhere assumes we have enough default values. This code here
			// makes sure we have enough values, but if we don't have enough we flag an error on the Expr_Set.
			// Seemed to make more sense to catch the error immediately.
			List<bool> argHasDefaults = null;
			bool insufficientDefaults = false;
			if (null != defaultValues) {
				Pb.Assert(defaultValues.Count <= argTypes.Count, "internal error: somehow we got more default values than arguments.");
				if (defaultValues.Count < argTypes.Count) {
					insufficientDefaults = true;
					for (int ii = defaultValues.Count; ii < argTypes.Count; ++ii)
						defaultValues.Add(null);
				} 

				argHasDefaults = new List<bool>();
				for (int ii = 0; ii < defaultValues.Count; ++ii) {
					argHasDefaults.Add(null != defaultValues[ii]);
				}
			}

			// all function literals are const
			TypeRef_Function funcTypeRef = new TypeRef_Function(retType, argTypes, argHasDefaults, false, true);

			Expr_Set result = new Expr_Set(parser, funcTypeRef, sym, mods);
			result.isFunctionLiteral = true;
			result._insufficientDefaults = insufficientDefaults;

			// This is not the right ret type.  The actual type is whatever the type of body is, but we won't know that 
			// until the TypeCheck phase.

			FunctionValue_Script funcValue = new FunctionValue_Script(sym, body, retType, argTypes, defaultValues);
			funcValue.argNames = argNames;

			// If this Set is a class member function, the type is all wrong here. The class must fix it.
			result.SetValue(new Expr_Literal(parser, funcValue, funcTypeRef));
			return result;
		}

		public Expr_Set(Parser parser, ITypeRef vType, string sym, DeclMods mods) : base(parser) {
			declMods = mods;
			typeRef = vType;
			symbol = sym;
		}

		public void SetValue(IExpr value) {
			Pb.Assert(null == nodes);
			nodes = new List<IExpr>();
			nodes.Add(value);
		}

		public override string ToString() {
			return "SET(" + typeDef + " " + symbol + (null != value ? " = " + value : "") + ")";
		}

		public override string MyToString(string indent) {
			return "SET(" + typeDef + " " + symbol + (null != value ? " = " + value.MyToString(indent) : "") + ")";
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (_insufficientDefaults) {
				LogCompileErr(context, ParseErrorType.DefaultArgGap, "An argument with a default value is followed by an argument without one.");
				error = true;
				return null;
			}

			if (null == owningClass) {
				if (declMods._static) {
					LogCompileErr(context, ParseErrorType.StaticClassMembersOnly, "Only class members can be static.");
					error = true;
				}
				if (declMods._override) {
					LogCompileErr(context, ParseErrorType.OverrideNonMemberFunction, "Only class member functions can be 'override'.");
					error = true;
				}
				if (declMods._getonly) {
					LogCompileErr(context, ParseErrorType.GetonlyClassMembersOnly, "Only class fields can be 'getonly'.");
					error = true;
				}

				// This is weird. We don't want function literals to be *specified* as const because they are
				// *automatically* const. This code is just checking that the user didn't specify as const.
				// We insure they are const in CreateFunctionLiteral when we create the TypeRef.
				if (isFunctionLiteral && declMods._const) {
					LogCompileErr(context, ParseErrorType.FunctionLiteralsAreImplicitlyConst, "Function literals are implicitly const.");
					error = true;
				}
			} else {
				if (declMods._getonly && declMods._const) {
					LogCompileErr(context, ParseErrorType.GetonlyNonConst, "Variables cannot simultaneously be const and getonly.");
					error = true;
				}

				if (isFunctionLiteral && declMods._getonly) {
					LogCompileErr(context, ParseErrorType.GetonlyClassMembersOnly, "Only class fields can be 'getonly'.");
					error = true;
				}
			}

			if (error)
				return null;

			typeDef = typeRef.Resolve(context, ref error);
			if (null == typeDef) {
				LogCompileErr(context, ParseErrorType.TypeNotFound, "Set: couldn't resolve type " + typeRef + ".");
				error = true;
				return null;
			}

			if (error)
				return null;

			if (!isFunctionLiteral && IntrinsicTypeDefs.VOID.Equals(typeDef)) {
				LogCompileErr(context, ParseErrorType.VoidFunctionsOnly, "Variables cannot have type 'void'.");
				error = true;
				return null;
			}

			if (typeDef is TypeDef_Class) {
				var classTypeDef = typeDef as TypeDef_Class;

				ClassDef classTable = context.GetClass(classTypeDef.className);
				int gotTypeCount = null != classTypeDef.genericTypes ? classTypeDef.genericTypes.Count : 0;
				int requiredTypeCount = null != classTable.genericTypeNames ? classTable.genericTypeNames.Count : 0;

				if (gotTypeCount != requiredTypeCount) {
					LogCompileErr(context, ParseErrorType.TemplateCountMismatch, "Set: provided number of template types " + gotTypeCount + " doesn't match required " + requiredTypeCount + ".");
					error = true;
					return null;
				}
			}

			// This is to handle scope initializers when parent scope already has
			// a variable with that name.
			// Later comment: but this makes it so you can create a global with the same name as a local variable!
			bool canCreateSymbol;
			//if (global)
			//	canCreateSymbol = null == context.GetGlobal(symbol.GetName());
			//else
			//	canCreateSymbol = context.CanCreateSymbol(symbol.GetName());

			canCreateSymbol = declMods._override || context.stack.IsSymbolAvailable(context, symbol, null != owningClass);

			if (!canCreateSymbol) {
				LogCompileErr(context, ParseErrorType.SymbolAlreadyDeclared, "Set: symbol (" + symbol + ") already declared.");
				error = true;
				return null;
			}

			if (null != value && typeCheckValue) {
				ITypeDef valueType = value.TypeCheck(context, ref error);
				if (!error) {
					// Oh boy, we have a value to type check.
					if (!typeDef.CanStoreValue(context, valueType)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, "Set: variable of type (" + typeDef + ") cannot store value of type (" + valueType + ").");
						error = true;
					}
				}
			}

			if (createTemp) {
				if (!context.CreateTemp(symbol, typeDef, declMods._global).isValid) {
					LogCompileErr(context, ParseErrorType.SymbolAlreadyDeclared, "Set: Cannot create variable with name (" + symbol + ").");
					error = true;
				}
			}

			return SetType(typeDef);
		}
		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable result = EvaluateLValue(context);
			return null != result ? result.value : null;
		}

		public override Variable EvaluateLValue(ExecContext context) {
			object val = null;
			if (null == value) {
				if (!typeDef.IsReference())
					val = typeDef.GetDefaultValue(context);
			} else {
				val = value.Evaluate(context);
				if (context.IsRuntimeErrorSet())
					return null;
			}

			Variable valueRef = context.CreateEval(symbol, typeDef, val, declMods._global);
			return valueRef;
		}

	}

	//////////////////////////////////////////////////////////////////////////////
	// If

	public class Expr_If : IExpr {
		public IExpr condition { get { return nodes[0]; } }
		public IExpr trueCase { get { return nodes[1]; } }
		public IExpr falseCase {
			get {
				return nodes.Count > 2 ? nodes[2] : null;
			}
			set {
				Pb.Assert(nodes.Count == 2, "Internal error: setting Expr_If.falseCase twice!");
				nodes.Add(value);
			}
		}

		public Expr_If(Parser parser, IExpr cond, IExpr tCase, IExpr fCase = null) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(cond);
			nodes.Add(tCase);
			if (null != fCase)
				nodes.Add(fCase);
		}

		public override string ToString() {
			return "IF(" + condition + ")\n  " + trueCase + (null != falseCase ? "\nELSE\n  " + falseCase : "") + "\nENDIF";
		}

		public override string MyToString(string indent) {
			string msg = "IF(" + condition.MyToString(indent) + ")\n";
			msg += indent + "  " + trueCase.MyToString(indent + "  ");
			if (null != falseCase)
				msg += "\n" + indent + "ELSE\n" + indent + "  " + falseCase.MyToString(indent + "  ");
			//msg += "\n" + indent + "ENDIF";
			return msg;
		}

		public override bool RegisterTypes(ExecContext context, ref bool error) {
			bool success = trueCase.RegisterTypes(context, ref error);
			if (success && null != falseCase)
				success = falseCase.RegisterTypes(context, ref error);
			return success;
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef conditionType = condition.TypeCheck(context, ref error);
			if (!IntrinsicTypeDefs.BOOL.Equals(conditionType)) {
				LogCompileErr(context, ParseErrorType.IfConditionNotBoolean, "If: condition type (" + conditionType + ") isn't boolean.");
				error = true;
			}

			// If doesn't need scope because 1) it doesn't define an iterator, 2) if a single statement the
			// parser doesn't allow it to be a Decl, and 3) if a block it will have block scope from that.
			trueCase.TypeCheck(context, ref error);
			if (null != falseCase)
				falseCase.TypeCheck(context, ref error);

			return SetType(IntrinsicTypeDefs.BOOL);
		}
		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object ret = condition.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			if ((Boolean)ret == true) {
				trueCase.Evaluate(context);
			} else {
				if (null != falseCase)
					falseCase.Evaluate(context);
			}

			return false;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// For

	/**
	 * Since we have no "none" return type, If expressions evaluate to "false".
	 * For *could* return the value of the body, but why?
	 */
	public class Expr_For : IExpr {
		// Choosing this to be the max range of loop variables.
		// Above a number around 9E15, doubles cease to be able to represent every integer.
		// 1E15 is a nice round number and a safe, upperlimit, but is also a million billion.
		// I did some tests and that could easily result in loops that take decades to complete.
		// So, I'm going to set this to a billion, which would still take like 500s to complete
		// but hey that's better than 30 years.
		public const double MAX = 1E9;

		public string iterator;
		public IExpr start { get { return nodes[0]; } }
		public IExpr end { get { return nodes[1]; } }
		public IExpr step { get { return nodes[2]; } }
		public IExpr body { get { return nodes[3]; } }

		public Expr_For(Parser parser, string it, IExpr s, IExpr e, IExpr step, IExpr b) : base(parser) {
			iterator = it;
			nodes = new List<IExpr>();
			nodes.Add(s);
			nodes.Add(e);
			if (null == step)
				step = new Expr_Literal(parser, 1.0, IntrinsicTypeDefs.NUMBER);
			nodes.Add(step);
			nodes.Add(b);
		}

		public override string ToString() {
			return "FOR(" + iterator + ", " + start + ", " + end + ", " + step + ")\n    " + body;
		}

		public override string MyToString(string indent) {
			return "FOR(" + iterator + ", " + start.MyToString(indent) + ", " + end.MyToString(indent) + ", " + step.MyToString(indent) + ")\n  " + indent + body.MyToString(indent + "  ") + "\n" + indent + ")";
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef startType = start.TypeCheck(context, ref error);
			if (error)
				return SetType(IntrinsicTypeDefs.BOOL);

			if (!startType.Equals(IntrinsicTypeDefs.NUMBER)) {
				LogCompileErr(context, ParseErrorType.ForRangeMustBeNumeric, "For: start type (" + startType + ") isn't number.");
				error = true;
			}
			ITypeDef endType = end.TypeCheck(context, ref error);
			if (!endType.Equals(IntrinsicTypeDefs.NUMBER)) {
				LogCompileErr(context, ParseErrorType.ForRangeMustBeNumeric, "For: end type (" + endType + ") isn't number.");
				error = true;
			}
			ITypeDef stepType = step.TypeCheck(context, ref error);
			if (!stepType.Equals(IntrinsicTypeDefs.NUMBER)) {
				LogCompileErr(context, ParseErrorType.ForRangeMustBeNumeric, "For: step type (" + stepType + ") isn't number.");
				error = true;
			}

			if (!context.stack.PushBlock(Pb.FOR_BLOCK_NAME, null)) {
				LogCompileErr(context, ParseErrorType.StackOverflow, "For: stack overflow pushing block.");
				error = true;
				return null;
			}
			{
				if (!context.CreateTemp(iterator, IntrinsicTypeDefs.CONST_NUMBER, false).isValid) {
					LogCompileErr(context, ParseErrorType.ForIteratorNameTaken, "For: symbol (" + iterator + ") already in use.");
					error = true;
				}

				body.TypeCheck(context, ref error);
			}
			context.stack.PopScope();

			return SetType(IntrinsicTypeDefs.BOOL);
		}
		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object dStartObject = start.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;
			double dStart = (double)dStartObject;
			dStart = Math.Round(dStart);

			object dEndObject = end.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;
			double dEnd = (double)dEndObject;
			dEnd = Math.Round(dEnd);

			object dStepObject = step.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;
			double dStep = (double)dStepObject;
			dStep = Math.Round(dStep);

			if (dStart > MAX) {
				SetRuntimeError(context, RuntimeErrorType.ForIndexOutOfBounds, "For start value above maximum value " + MAX + ".");
				return null;
			}
			if (dStart < -MAX) {
				SetRuntimeError(context, RuntimeErrorType.ForIndexOutOfBounds, "For start value below minimum value -" + MAX + ".");
				return null;
			}
			if (dEnd > MAX) {
				SetRuntimeError(context, RuntimeErrorType.ForIndexOutOfBounds, "For end value above maximum value " + MAX + ".");
				return null;
			}
			if (dEnd < -MAX) {
				SetRuntimeError(context, RuntimeErrorType.ForIndexOutOfBounds, "For end value below minimum value -" + MAX + ".");
				return null;
			}
			if (dStep > MAX) {
				SetRuntimeError(context, RuntimeErrorType.ForIndexOutOfBounds, "For step value above maximum value " + MAX + ".");
				return null;
			}
			if (dStep < -MAX) {
				SetRuntimeError(context, RuntimeErrorType.ForIndexOutOfBounds, "For step value below minimum value -" + MAX + ".");
				return null;
			}

			if (!context.stack.PushBlock(Pb.FOR_BLOCK_NAME, context)) {
				SetRuntimeError(context, RuntimeErrorType.StackOverflow, "For: stack overflow pushing block.");
				return null;
			}
			Variable it = context.CreateEval(iterator, IntrinsicTypeDefs.NUMBER, 0, false);

			// These two blocks need to be IDENTICAL except for the > or < in the for condition.
			if (dStep > 0) {
				for (double ii = dStart; ii <= dEnd; ii += dStep) {
					it.value = ii;
					body.Evaluate(context);
					if (context.IsRuntimeErrorSet()) {
						context.stack.PopScope();
						return null;
					}

					if (0 != (context.control.flags & ControlInfo.CONTINUE)) {
						// All we need to do to continue is just clear the flag.
						context.control.flags -= ControlInfo.CONTINUE;
					}
					if (0 != (context.control.flags & ControlInfo.BREAK)) {
						context.control.flags -= ControlInfo.BREAK;
						break;
					}
					if (0 != (context.control.flags & ControlInfo.RETURN)) {
						// Return isn't for us, so just break out.
						break;
					}
				}
			} else if (dStep < 0) {
				for (double ii = dStart; ii >= dEnd; ii += dStep) {
					it.value = ii;
					body.Evaluate(context);
					if (context.IsRuntimeErrorSet()) {
						context.stack.PopScope();
						return null;
					}

					if (0 != (context.control.flags & ControlInfo.CONTINUE)) {
						// All we need to do to continue is just clear the flag.
						context.control.flags -= ControlInfo.CONTINUE;
					}
					if (0 != (context.control.flags & ControlInfo.BREAK)) {
						context.control.flags -= ControlInfo.BREAK;
						break;
					}
					if (0 != (context.control.flags & ControlInfo.RETURN)) {
						// Return isn't for us, so just break out.
						break;
					}
				}
			}
			context.stack.PopScope();

			return false;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// ForEach

	public class Expr_ForEach : IExpr {
		enum CollectionType {
			INVALID,
			LIST,
			DICTIONARY,
			ENUM,
		};

		public string kSym;
		public string vSym;
		public IExpr collection { get { return nodes[0]; } }
		public IExpr body { get { return nodes[1]; } }

		private CollectionType _collectionType = CollectionType.INVALID;
		protected TypeDef_Class classType;
		protected TypeDef_Enum enumType;
		protected ITypeDef _kIteratorType;
		protected ITypeDef _vIteratorType;

		public Expr_ForEach(Parser parser, IExpr c, string k, string v, IExpr b) : base(parser) {
			kSym = k;
			vSym = v;

			nodes = new List<IExpr>();
			nodes.Add(c);
			nodes.Add(b);
		}

		public override string ToString() {
			return "FOREACH(" + kSym + ',' + vSym + " in " + collection + ")\n    " + body;
		}

		public override string MyToString(string indent) {
			//return "FOREACH(" + kSym.MyToString(indent) + ',' + vSym.MyToString(indent) + " in " + collection.MyToString(indent) + ")\n    " + body.MyToString(indent);
			return "FOREACH(" + kSym + ',' + vSym + " in " + collection.MyToString(indent) + ")\n  " + indent + body.MyToString("  " + indent);
		}

		override public ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (collection is Expr_Symbol) {
				string symName = (collection as Expr_Symbol).GetName();
				ITypeDef type = context.GetTypeByName(symName);
				if (null != type) {
					if (type is TypeDef_Enum) {
						enumType = type as TypeDef_Enum;
						_collectionType = CollectionType.ENUM;
					} else {
						LogCompileErr(context, ParseErrorType.ForEachInvalidType, "foreach: the only *types* that can be iterated over are enums.");
						error = true;
						return null;
					}
				}
			}

			if (_collectionType != CollectionType.ENUM) {
				ITypeDef collectionType = collection.TypeCheck(context, ref error);
				if (null == collectionType)
					return null;

				classType = collectionType as TypeDef_Class;
				if (null == classType) {
					LogCompileErr(context, ParseErrorType.ForEachInvalidCollection, "foreach: collection not a class instance.");
					error = true;
					return null;
				}

				ClassDef classDef = context.GetClass(classType.className);
				if ("List" == classDef.name)
					_collectionType = CollectionType.LIST;
				else if ("Dictionary" == classDef.name)
					_collectionType = CollectionType.DICTIONARY;
				else {
					LogCompileErr(context, ParseErrorType.ForEachInvalidCollection, "foreach: can only iterate over Lists, Dictionarys, and enums.");
					error = true;
					return null;
				}
			}

			if (!context.stack.IsSymbolAvailable(context, kSym)) {
				LogCompileErr(context, ParseErrorType.ForEachIteratorNameTaken, "foreach: symbol (" + kSym + ") already declared.");
				error = true;
				return null;
			}

			if (!context.stack.IsSymbolAvailable(context, vSym)) {
				LogCompileErr(context, ParseErrorType.ForEachIteratorNameTaken, "foreach: symbol (" + vSym + ") already declared.");
				error = true;
				return null;
			}

			if (kSym.Equals(vSym)) {
				LogCompileErr(context, ParseErrorType.ForEachIteratorNameTaken, "foreach: key and value iterators cannot have the same name.");
				error = true;
				return null;
			}

			if (null == body) {
				LogCompileErr(context, ParseErrorType.SyntaxError, "foreach: no body provided.");
				error = true;
				return null;
			}

			if (!context.stack.PushBlock(Pb.FOREACH_BLOCK_NAME, null)) {
				LogCompileErr(context, ParseErrorType.StackOverflow, "foreach: stack overflow pushing block.");
				error = true;
				return null;
			}
			{
				if (_collectionType == CollectionType.LIST) {
					_kIteratorType = IntrinsicTypeDefs.CONST_NUMBER;
					_vIteratorType = TypeFactory.GetConstVersion(classType.genericTypes[0]);
				} else if (_collectionType == CollectionType.ENUM) {
					_kIteratorType = IntrinsicTypeDefs.CONST_NUMBER;
					_vIteratorType = TypeFactory.GetConstVersion(enumType);
				} else {
					_kIteratorType = TypeFactory.GetConstVersion(classType.genericTypes[0]);
					_vIteratorType = TypeFactory.GetConstVersion(classType.genericTypes[1]);
				}

				context.AddLocalVariable(kSym, _kIteratorType, null);
				context.AddLocalVariable(vSym, _vIteratorType, null);

				body.TypeCheck(context, ref error);
			}
			context.stack.PopScope();

			return SetType(IntrinsicTypeDefs.BOOL);
		}
		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			if (_collectionType == CollectionType.LIST) {
				object listObject = collection.Evaluate(context);
				if (null == listObject) {
					SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "foreach list is null.");
					return null;
				}
				PebbleList list = listObject as PebbleList;
				++list.enumeratingCount;

				// Note that collection must be evaluated *before* the foreach block is pushed
				// or the ref looks at the wrong scope.
				if (!context.stack.PushBlock(Pb.FOREACH_BLOCK_NAME, context)) {
					SetRuntimeError(context, RuntimeErrorType.StackOverflow, "foreach: stack overflow pushing block.");
					return null;
				}

				// I think technically the name doesn't need to be known here? Maybe only if there is an ExecInLine? Hmm...
				VarStackRef kRef = context.AddLocalVariable(kSym, _kIteratorType, _kIteratorType.GetDefaultValue(context));
				VarStackRef vRef = context.AddLocalVariable(vSym, _vIteratorType, _vIteratorType.GetDefaultValue(context));
				Variable kVar = context.stack.GetVarAtIndex(kRef);
				Variable vVar = context.stack.GetVarAtIndex(vRef);

				int count = list.list.Count;
				for (int ii = 0; ii < count; ++ii) {
					kVar.value = Convert.ToDouble(ii);
					vVar.value = list.list[ii].value;

					body.Evaluate(context);
					if (context.IsRuntimeErrorSet()) {
						context.stack.PopScope();
						--list.enumeratingCount;
						return null;
					}

					if (0 != (context.control.flags & ControlInfo.CONTINUE)) {
						// All we need to do to continue is just clear the flag.
						context.control.flags -= ControlInfo.CONTINUE;
					}
					if (0 != (context.control.flags & ControlInfo.BREAK)) {
						context.control.flags -= ControlInfo.BREAK;
						break;
					}
					if (0 != (context.control.flags & ControlInfo.RETURN)) {
						// Return isn't for us, so just break out.
						break;
					}
				}

				--list.enumeratingCount;

			} else if (_collectionType == CollectionType.DICTIONARY) {
				object dictObject = collection.Evaluate(context);
				if (null == dictObject) {
					SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "foreach dictionary is null.");
					return null;
				}
				PebbleDictionary dict = dictObject as PebbleDictionary;
				++dict.enumeratingCount;

				// Note that collection must be evaluated *before* the foreach block is pushed
				// or the ref looks at the wrong scope.
				if (!context.stack.PushBlock(Pb.FOREACH_BLOCK_NAME, context)) {
					SetRuntimeError(context, RuntimeErrorType.StackOverflow, "foreach: stack overflow pushing block");
					return null;
				}

				VarStackRef kRef = context.AddLocalVariable(kSym, _kIteratorType, _kIteratorType.GetDefaultValue(context));
				VarStackRef vRef = context.AddLocalVariable(vSym, _vIteratorType, _vIteratorType.GetDefaultValue(context));
				Variable kVar = context.stack.GetVarAtIndex(kRef);
				Variable vVar = context.stack.GetVarAtIndex(vRef);

				foreach (KeyValuePair<object, Variable> pair in dict.dictionary) {
					kVar.value = pair.Key;
					vVar.value = pair.Value.value;

					body.Evaluate(context);
					if (context.IsRuntimeErrorSet()) {
						context.stack.PopScope();
						--dict.enumeratingCount;
						return null;
					}

					if (0 != (context.control.flags & ControlInfo.CONTINUE)) {
						// All we need to do to continue is just clear the flag.
						context.control.flags -= ControlInfo.CONTINUE;
					}
					if (0 != (context.control.flags & ControlInfo.BREAK)) {
						context.control.flags -= ControlInfo.BREAK;
						break;
					}
					if (0 != (context.control.flags & ControlInfo.RETURN)) {
						// Return isn't for us, so just break out.
						break;
					}
				}

				--dict.enumeratingCount;
			} else { // is enum
					 // Note that collection must be evaluated *before* the foreach block is pushed
					 // or the ref looks at the wrong scope.
				if (!context.stack.PushBlock(Pb.FOREACH_BLOCK_NAME, context)) {
					SetRuntimeError(context, RuntimeErrorType.StackOverflow, "foreach: stack overflow pushing block");
					return null;
				}

				VarStackRef kRef = context.AddLocalVariable(kSym, _kIteratorType, _kIteratorType.GetDefaultValue(context));
				VarStackRef vRef = context.AddLocalVariable(vSym, _vIteratorType, _vIteratorType.GetDefaultValue(context));
				Variable kVar = context.stack.GetVarAtIndex(kRef);
				Variable vVar = context.stack.GetVarAtIndex(vRef);

				ClassDef_Enum enumDef = context.GetClass(enumType.className) as ClassDef_Enum;

				int count = enumDef.staticVars.Count;
				for (int ii = 0; ii < count; ++ii) {
					kVar.value = Convert.ToDouble(ii);
					vVar.value = enumDef.staticVars[ii].value;

					body.Evaluate(context);
					if (context.IsRuntimeErrorSet()) {
						context.stack.PopScope();
						return null;
					}

					if (0 != (context.control.flags & ControlInfo.CONTINUE)) {
						// All we need to do to continue is just clear the flag.
						context.control.flags -= ControlInfo.CONTINUE;
					}
					if (0 != (context.control.flags & ControlInfo.BREAK)) {
						context.control.flags -= ControlInfo.BREAK;
						break;
					}
					if (0 != (context.control.flags & ControlInfo.RETURN)) {
						// Return isn't for us, so just break out.
						break;
					}
				}
			}
			context.stack.PopScope();

			return false;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Break

	public class Expr_Break : IExpr {

		public Expr_Break(Parser parser) : base(parser) { }

		public override string ToString() {
			return "BREAK;";
		}

		public override string MyToString(string indent) {
			return "(BREAK)";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			bool inAFor = context.stack.InForStatement();
			if (!inAFor) {
				LogCompileErr(context, ParseErrorType.BreakNotInFor, "Break statement not in an enclosing for loop.");
				error = true;
			}

			return null;
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			context.control.flags |= ControlInfo.BREAK;
			return null;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Continue

	public class Expr_Continue : IExpr {

		public Expr_Continue(Parser parser) : base(parser) { }

		public override string ToString() {
			return "(CONTINUE)";
		}

		public override string MyToString(string indent) {
			return "(CONTINUE)";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			bool inAFor = context.stack.InForStatement();
			if (!inAFor) {
				LogCompileErr(context, ParseErrorType.ContinueNotInFor, "Continue statement not in an enclosing for loop.");
				error = true;
			}

			return null;
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			context.control.flags |= ControlInfo.CONTINUE;
			return null;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Class
	// - Does nothing during Execute pass.

	public class Expr_Class : IExpr {
		protected IExpr _constructorBlock { get { return nodes[0]; } }
		// We keep this members list in addition to nodes for convenience.
		private List<Expr_Set> members = new List<Expr_Set>();

		protected string _symbol;
		public string parent;
		public bool isSealed;
		public bool isUninstantiable;

		protected ClassDef _parentScope;
		protected ClassDef _classDef;
		protected bool _preregError = false;
		protected ClassValue _tempInstance = new ClassValue();

		static protected TypeDef_Function _serializeFunctionType = null;
		static protected TypeDef_Function _tostringFunctionType = null;
		static protected TypeDef_Function _thistoscriptFunctionType = null;

		public Expr_Class(Parser parser, string sym) : base(parser) {
			_symbol = sym;

			nodes = new List<IExpr>();
			// Constructor is always at index 0, whether or not we have one.
			nodes.Add(null);
		}

		public void AddMember(Expr_Set newMember) {
			members.Add(newMember);
			nodes.Add(newMember);
		}

		public void SetConstructor(ExecContext context, IExpr conExpr) {
			Pb.Assert(null != conExpr);

			if (null != _constructorBlock) {
				LogCompileErr(context, ParseErrorType.ClassCanOnlyHaveOneConstructor, "Multiple constructors provided for class (" + _symbol + ").");
				_preregError = true;
				return;
			}

			nodes[0] = conExpr;
		}

		// Registers the class & type with the context.
		// PreRegistering classes allows us to utilize the class type within the class, ie. class Node { Node next; }
		public override bool RegisterTypes(ExecContext context, ref bool error) {
			if (Pb.reservedWords.Contains(_symbol)) {
				LogCompileErr(context, ParseErrorType.InvalidSymbolName, "Class name (" + _symbol + ") is a reserved word.");
				error = true;
				return false;
			}

			if (null != context.GetClass(_symbol)) {
				LogCompileErr(context, ParseErrorType.ClassAlreadyDeclared, "Class (" + _symbol + ") already exists.");
				error = true;
				return false;
			}

			// Can't check for null type. When alias preregisters it registers the name with a null type.
			if (context.DoesTypeExist(_symbol)) {
				LogCompileErr(context, ParseErrorType.TypeAlreadyExists, "Class name (" + _symbol + ") collides with an existing type name.");
				error = true;
				return false;
			}

			if (null != parent) {
				_parentScope = context.GetClass(parent);
				if (null == _parentScope) {
					LogCompileErr(context, ParseErrorType.TypeNotFound, "Class " + _symbol + "'s parent " + parent + " not found.");
					error = true;
					return false;
				}
				if (_parentScope.isSealed) {
					LogCompileErr(context, ParseErrorType.ClassParentSealed, "Class " + _symbol + "'s parent " + parent + " is sealed.");
					error = true;
					return false;
				}
				if (_parentScope.IsGeneric()) {
					LogCompileErr(context, ParseErrorType.ClassCannotBeChildOfTemplate, "Cannot derive from a generic parent.");
					error = true;
					return false;
				}
			}

			TypeDef_Class classType = TypeFactory.GetTypeDef_Class(_symbol, null, false);

			// Phase 1: declare the class type. This originally was to help the parser because
			// the parser was checking to see if identifiers were types. That's not the case
			// anymore, but it's not bad to leave this in.
			_classDef = context.CreateClass(_symbol, classType, _parentScope, null, isSealed, isUninstantiable);
			if (null == _classDef) {
				LogCompileErr(context, ParseErrorType.Any, "Internal Error: failed to create class (" + _symbol + ").");
				error = true;
			}

			return true;
		}

		private void _RegisterMembers(ExecContext context) {
			// Some parser errors can result in this being called without PreRegister being called first,
			// hence the classDef check.
			if (null == _classDef)
				_preregError = true;

			if (_preregError)
				return;

			_classDef.Initialize();

			_tempInstance = new ClassValue();
			_tempInstance.classDef = _classDef;
			if (!context.stack.PushClassScope(_tempInstance, null, "Defining class (StubInMembers)")) {
				LogCompileErr(context, ParseErrorType.StackOverflow, "Expr_Class.RegisterMembers - stack overflow.");
				_preregError = true;
				return;
			}
			{
				foreach (Expr_Set set in members) {
					// This can happen if there is a parsing error.
					if (null == set)
						break;

					set.owningClass = _classDef;
					// This line is very important: it tells Expr_Set.TypeCheck to NOT check the value.
					set.typeCheckValue = false;
					set.createTemp = false;

					if (set.declMods._global) {
						LogCompileErr(context, ParseErrorType.ClassMembersCannotBeGlobal, "Class members cannot be 'global'.");
						_preregError = true;
						continue;
					}

					if (set.isFunctionLiteral) {
						if (set.declMods._const) {
							LogCompileErr(context, ParseErrorType.ClassMemberFunctionsConst, "Class member functions are implicitly const.");
							_preregError = true;
							continue;
						}

						// Functions are implicitly const.
						set.typeRef.SetConst(true);

						// Note: In TypeCheck we also call GetClassVersion, which seems potentially redundant, or at
						// least very messy!

						if (!set.declMods._static)
							((TypeRef_Function)set.typeRef).className = _classDef.name;

						// We also need to do this to the set's VALUE
						if (set.value is Expr_Literal) {
							Expr_Literal lit = (Expr_Literal)set.value;
							if (lit.value is FunctionValue_Script) {
								FunctionValue_Script script = (FunctionValue_Script)lit.value;
								script.typeRef.className = _classDef.name;
								script.typeRef.SetConst(true);
							}
						}

					} else {
						if (set.declMods._override) {
							LogCompileErr(context, ParseErrorType.OverrideNonMemberFunction, "Only class member functions can be overriden.");
							_preregError = true;
							continue;
						}

						/* No longer: I decided that it was better if a class could have const non-static members, because they could be initialized
							to different values at instantiation.
						if (set.declMods._static && set.declMods._const) {
							LogCompileErr(context, ParseErrorType.StaticImpliesConst, "const implies static: just use const.");
							_preregError = true;
							continue;
						}

						// If const, also make sure it's static.
						if (set.declMods._const)
							set.declMods._static = true;
						*/
					}

					// Here, we typecheck the Expr_Set, but NOT THE VALUE. 
					// We need to typecheck the set so that we can get it's type. We need the type to add it as a class member.
					bool setIsStatic = set.declMods._static;
					// Why am I doing this? because Expr_Set.TypeCheck errors out if there is a static, to prevent non-class
					// variables from being static.
					set.declMods._static = false;
					ITypeDef memberTypeDef = set.TypeCheck(context, ref _preregError);
					set.declMods._static = setIsStatic;
					if (_preregError)
						continue;

					if (set.declMods._override) {
						if (set.declMods._static) {
							LogCompileErr(context, ParseErrorType.MemberOverrideCannotBeStatic, "Class (" + _classDef.name + ") static member function (" + set.symbol + ") : Static functions cannot override.");
							_preregError = true;
							continue;
						}
						if (null == _classDef.parent) {
							LogCompileErr(context, ParseErrorType.MemberOverrideNoParent, "Class (" + _classDef.name + ") member function (" + set.symbol + ") marked as override but class has no parent.");
							_preregError = true;
							continue;
						}
						ClassMember parentMember = _classDef.parent.GetMember(set.symbol, set.declMods._static ? ClassDef.SEARCH.STATIC : ClassDef.SEARCH.NORMAL);
						if (null == parentMember) {
							LogCompileErr(context, ParseErrorType.MemberOverrideNotFound, "Class (" + _classDef.name + ") member function (" + set.symbol + ") marked as override but parent has no function with that name.");
							_preregError = true;
							continue;
						}
						if (!(parentMember.typeDef is TypeDef_Function)) {
							LogCompileErr(context, ParseErrorType.MemberOverrideNotFunction, "Class (" + _classDef.name + ") member function (" + set.symbol + ") marked as override but parent member with that name is not a function.");
							_preregError = true;
							continue;
						}

						// Finally, after we've determined it's ok, set the override.
						_classDef.AddFunctionOverride(set.symbol, memberTypeDef, null);

					} else {
						if (!_classDef.AddMember(set.symbol, memberTypeDef, set.value, set.declMods._static, !set.isFunctionLiteral, set.declMods._getonly)) {
							LogCompileErr(context, ParseErrorType.ClassMemberShadowed, "Cannot add field " + set.symbol + " to class " + _classDef.name + " because it would shadow another field.");
							_preregError = true;
							continue;
						}
					}
				}
			}
			context.stack.PopScope();
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			_RegisterMembers(context);

			if (_preregError || error) {
				error = true;
				return null;
			}

			if (!context.stack.PushClassScope(_tempInstance, null, "Defining class (TypeCheck)")) {
				LogCompileErr(context, ParseErrorType.StackOverflow, "Expr_Class.TypeCheck - stack overflow");
				error = true;
				return null;
			}
			{
				// Type check the constructor.
				if (null != _constructorBlock) {
					_constructorBlock.TypeCheck(context, ref error);

					_classDef.constructor = _constructorBlock;
				}

				if (error)
					return null;

				error = !DefineStandardFunctions(context);
				if (error)
					return null;

				// Phase 3: typecheck the ***values***.
				// This is because the values can be function bodies, and those
				// function bodies may reference fields, so all fields must be 
				// already be in the class definition.
				for (int ii = 0; ii < members.Count; ++ii) {
					Expr_Set set = members[ii];
					bool isStatic = set.declMods._static;
					if (null != set.value) {
						ClassMember member = _classDef.GetMember(members[ii].symbol, isStatic ? ClassDef.SEARCH.STATIC : ClassDef.SEARCH.NORMAL);

						// If field is a function...
						if (member.typeDef is TypeDef_Function) {
							TypeDef_Function funcType = member.typeDef as TypeDef_Function;


							if (set.value is Expr_Literal) {
								Expr_Literal lit = set.value as Expr_Literal;

								// We are responsible for telling the function literal if it is static.
								if (set.declMods._static)
									lit.isStatic = true;

								if (lit.value is FunctionValue_Script) {
									FunctionValue_Script inlang = lit.value as FunctionValue_Script;
									// Here we set the name of the class in the value's typeref.
									//inlang.typeRef.className = classDef.name;
									lit.ForceTypeDef(funcType);
									inlang.typeDef = funcType;

									// We also must tell the value that it is literal, as it is responsible for pushing it's own scope.
									if (set.declMods._static)
										inlang.staticClassDef = _classDef;
								}
							}

							if (set.declMods._override) {
								ClassMember parentMember = _classDef.parent.GetMember(set.symbol, isStatic ? ClassDef.SEARCH.STATIC : ClassDef.SEARCH.NORMAL);
								if (!parentMember.typeDef.CanStoreValue(context, member.typeDef)) {
									LogCompileErr(context, ParseErrorType.MemberOverrideTypeMismatch, "Class (" + _classDef.name + ") member override function (" + set.symbol + ") has different signature from base function.");
									error = true;
								}
							}

							member.initializer = set.value;

							// Check signatures of standard functions.

							if ("ThisToString" == member.name) {
								if (!_tostringFunctionType.EqualsIgnoreClass(member.typeDef)) {
									LogCompileErr(context, ParseErrorType.StandardFunctionMismatch, "ThisToString members must return string and take no arguments.");
									error = true;
								}
							}

							if ("ThisToScript" == member.name) {
								if (!_thistoscriptFunctionType.EqualsIgnoreClass(member.typeDef)) {
									LogCompileErr(context, ParseErrorType.StandardFunctionMismatch, "ThisToScript members must return string and one string argument.");
									error = true;
								}
							}

							if ("Serialize" == member.name) {
								if (!_serializeFunctionType.EqualsIgnoreClass(member.typeDef)) {
									LogCompileErr(context, ParseErrorType.StandardFunctionMismatch, "Serialize members must return bool and take a single Stream argument.");
									error = true;
								}
							}
						}

						ITypeDef valueType = set.value.TypeCheck(context, ref error);
						if (error)
							break;

						// Make sure that the field type can store a value of this type.
						if (!member.typeDef.CanStoreValue(context, valueType)) {
							LogCompileErr(context, ParseErrorType.TypeMismatch, "Type of initializer (" + valueType.GetName() + ") of class " + _classDef.name + "'s member " + set.symbol + " doesn't match member's type " + member.typeDef.GetName() + ".");
							error = true;
						}
					}
				}

			}
			context.stack.PopScope();

			if (error)
				return null;

			// Complete class definition.
			if (!_classDef.FinalizeClass(context)) {
				error = true;
				return null;
			}

			return SetType(IntrinsicTypeDefs.BOOL);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);
			/* Expr_New handles all instance initialization.
			   Expr_Class is only defining the type, and that happens during TypeCheck, because
			   the class may have members which refer to the class's own type.
			*/
			return false;
		}

		protected bool DefineStandardFunctions(ExecContext context) {
			bool error = false;
			if (null != _tostringFunctionType)
				return true;

			// ** ToString
			TypeRef_Function tostringFunctionRef = new TypeRef_Function(new TypeRef("string"), new List<ITypeRef>(), null, false, false);
			_tostringFunctionType = tostringFunctionRef.Resolve(context, ref error) as TypeDef_Function;
			if (error || null == tostringFunctionRef) {
				LogCompileErr(context, ParseErrorType.Internal, "Internal error: couldn't resolve TypeDef of ToString function.");
				return false;
			}

			// ** ThisToScript
			TypeRef_Function thistoscriptFunctionRef = new TypeRef_Function(new TypeRef("string"), new List<ITypeRef>() { new TypeRef("string") }, null, false, false);
			_thistoscriptFunctionType = thistoscriptFunctionRef.Resolve(context, ref error) as TypeDef_Function;
			if (error || null == thistoscriptFunctionRef) {
				LogCompileErr(context, ParseErrorType.Internal, "Internal error: couldn't resolve TypeDef of ThisToScript function.");
				return false;
			}

			// *** Serialize
			TypeDef_Class streamClassType = context.GetTypeByName("Stream") as TypeDef_Class;
			if (null == streamClassType) {
				LogCompileErr(context, ParseErrorType.Internal, "Internal error: Stream class not defined.");
				return false;
			}

			TypeRef_Function serFunctionRef = new TypeRef_Function(new TypeRef("bool"), new List<ITypeRef>() { new TypeRef("Stream") }, null);
			_serializeFunctionType = serFunctionRef.Resolve(context, ref error) as TypeDef_Function;
			if (error || null == _serializeFunctionType) {
				LogCompileErr(context, ParseErrorType.Internal, "Internal error: couldn't resolve TypeDef of Serialize function.");
				return false;
			}

			/*
			if (!_serializeFunctionType.EqualsIgnoreClass(serMem.typeDef)) {
				LogCompileErr(context, ParseErrorType.StreamSerializeFunctionWrong, "Serialize member of streamed class must be a function with type <bool(Stream)>.");
				return false;
			}
			*/

			return true;
		}

		public override string MyToString(string indent) {
			return "CLASS(" + _symbol + (null != parent ? ":" + parent : "") + ")";
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// New

	public class Expr_New : IExpr {
		// This is the typeRef given by the parser, if user supplied a type with their new statement.
		public ITypeRef typeRef;
		// compile-only. Expr_Assign sets this.
		public ITypeDef parentSetTypeDef;

		protected IExpr _initializer { get { return null != nodes ? nodes[0] : null; } }

		public Expr_New(Parser parser, ITypeRef type, IExpr initializer) : base(parser) {
			typeRef = type;
			if (null != initializer) {
				Pb.Assert(initializer is Expr_ExprList, "Parser passed an expression that wasn't a list as a new initializer.");

				nodes = new List<IExpr>();
				nodes.Add(initializer);
			}
		}

		public override string ToString() {
			return "NEW(" + (null != _type ? _type.ToString() : "<no type>") + ")";
		}

		public override string MyToString(string indent) {
			return "NEW(" + (null != _type ? _type.ToString() : "<no type>") + (null != _initializer ? ", " + _initializer.MyToString("  " + indent) : "") + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (null == typeRef) {
				if (null == parentSetTypeDef) {
					LogCompileErr(context, ParseErrorType.NewTypeCannotBeInferred, "'new' type can only be inferred in declarations and assignments.");
					error = true;
					return null;
				}
			} else {
				parentSetTypeDef = typeRef.Resolve(context, ref error);
				if (null == parentSetTypeDef) {
					LogCompileErr(context, ParseErrorType.TypeNotFound, "Attempt to 'new' unknown type " + typeRef + ".");
					error = true;
					return null;
				}
			}

			var classType = parentSetTypeDef as TypeDef_Class;
			if (null == classType) {
				LogCompileErr(context, ParseErrorType.ReferenceOfNonReferenceType, "new can only instantiate Class types.");
				error = true;
				return null;
			}

			ClassDef classDef = context.GetClass(classType.GetName());
			if (classDef.isUninstantiable) {
				LogCompileErr(context, ParseErrorType.ClassUninstantiable, "Class '" + classDef.name + "' is uninstantiable.");
				error = true;
				return null;
			}


			if (null != _initializer) {
				ClassValue tempClass = new ClassValue();
				tempClass.classDef = classDef;
				if (!context.stack.PushDefstructorScope(tempClass, null)) {
					LogCompileErr(context, ParseErrorType.StackOverflow, "Expr_New.TypeCheck - stack overflow (defstructor scope).");
					error = true;
					return null;
				}

				_initializer.TypeCheck(context, ref error);

				context.stack.PopScope();
			}

			return SetType(parentSetTypeDef);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);
			ClassValue _newInstance = parentSetTypeDef.GetDefaultValue(context) as ClassValue;
			if (context.IsRuntimeErrorSet())
				return null;

			if (null != _initializer) {
				if (!context.stack.PushDefstructorScope(_newInstance, context)) {
					SetRuntimeError(context, RuntimeErrorType.StackOverflow, "Expr_New.Evaluate - stack overflow (initializer).");
					return null;
				}

				_initializer.Evaluate(context);
				if (context.IsRuntimeErrorSet()) {
					context.stack.PopScope();
					return null;
				}

				context.stack.PopScope();
			}

			return _newInstance;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Compare
	// - This could be merged into BinOp, I guess, but because Compare has a bunch of 
	// code to allow it to work on different types it's cleaner to keep it separate.

	public class Expr_Compare : IExpr {
		protected IExpr _lhs { get { return nodes[0]; } }
		protected IExpr _rhs { get { return nodes[1]; } }
		protected bool _neg;

		public Expr_Compare(Parser parser, IExpr left, IExpr right, bool neg) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(left);
			nodes.Add(right);
			_neg = neg;
		}

		public override string ToString() {
			return "COMPARE(" + _lhs.ToString() + (_neg ? " != " : " == ") + _rhs.ToString() + ")";
		}

		public override string MyToString(string indent) {
			return "COMPARE(" + _lhs.MyToString(indent) + (_neg ? " != " : " == ") + _rhs.MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef lType = _lhs.TypeCheck(context, ref error);
			ITypeDef rType = _rhs.TypeCheck(context, ref error);
			if (!error) {
				if (!lType.Comparable(context, rType)) {
					if (lType.IsNull() || rType.IsNull()) {
						bool lRef = lType.IsNull() || lType.IsReference();
						bool rRef = rType.IsNull() || rType.IsReference();
						if (!lRef || !rRef) {
							LogCompileErr(context, ParseErrorType.CompareNullToNonReference, "Comparison of non-reference to null.");
							error = true;
						}
					} else {
						LogCompileErr(context, ParseErrorType.TypeMismatch, "Comparison of unrelated types (" + lType + ") and (" + rType + ").");
						error = true;
					}
				}
			}

			return SetType(IntrinsicTypeDefs.BOOL);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object val0 = _lhs.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			object val1 = _rhs.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			bool result = Compare(val0, val1);

			if (_neg)
				result = !result;

			return result;
		}

		public static bool Compare(object val0, object val1) {
			bool result;
			if (val0 is bool) {
				result = (bool)val0 == (bool)val1;
			} else if (val0 is double) {
				result = (double)val0 == (double)val1;
			} else if (val0 is string) {
				result = (0 == string.CompareOrdinal((string)val0, (string)val1));
			} else {
				// If not an intrinsic, just compares the reference.
				result = val0 == val1;
			}
			return result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Conditional

	public class Expr_Conditional : IExpr {
		protected IExpr _condition { get { return nodes[0]; } }
		protected IExpr _trueCase { get { return nodes[1]; } }
		protected IExpr _falseCase { get { return nodes[2]; } }

		public Expr_Conditional(Parser parser, IExpr condition, IExpr trueCase, IExpr falseCase) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(condition);
			nodes.Add(trueCase);
			nodes.Add(falseCase);
		}

		public override string ToString() {
			return "(CONDITIONAL " + _condition + "?" + _trueCase + ":" + _falseCase + ")";
		}

		public override string MyToString(string indent) {
			return "CONDITIONAL(" + _condition.MyToString(indent) + " ? " + _trueCase.MyToString(indent) + " : " + _falseCase.MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef conditionType = _condition.TypeCheck(context, ref error);
			ITypeDef trueCaseType = _trueCase.TypeCheck(context, ref error);
			ITypeDef falseCaseType = _falseCase.TypeCheck(context, ref error);

			if (error)
				return null;

			if (!IntrinsicTypeDefs.BOOL.Equals(conditionType)) {
				LogCompileErr(context, ParseErrorType.ConditionNotBoolean, "Conditional: condition type (" + conditionType + ") isn't boolean.");
				error = true;
			}

			ITypeDef resultType = context.GetMostCommonType(trueCaseType, falseCaseType);
			if (null == resultType) {
				LogCompileErr(context, ParseErrorType.TypeMismatch, "Conditional: both true and false cases must have a common type.");
				error = true;
			}

			return SetType(resultType);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object conditionResultObject = _condition.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;
			bool conditionResult = (bool)conditionResultObject;

			return conditionResult ? _trueCase.Evaluate(context) : _falseCase.Evaluate(context);
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Return

	public class Expr_Return : IExpr {
		protected IExpr _value { get { return nodes[0]; } }

		public Expr_Return(Parser parser, IExpr value) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(value);
		}

		public override string ToString() {
			return "(RETURN " + _value + ")";
		}

		public override string MyToString(string indent) {
			return "RETURN(" + _value.MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			// If not in a call, error.
			TypeDef_Function funcType = context.stack.GetEnclosingFunctionType();
			if (null == funcType) {
				LogCompileErr(context, ParseErrorType.ReturnNotInCall, "Return statement not in a function.");
				error = true;
				return null;
			}

			// if in a call, make sure type matches.
			ITypeDef valueType = null;
			if (IntrinsicTypeDefs.VOID.Equals(funcType.retType)) {
				if (null != _value) {
					LogCompileErr(context, ParseErrorType.ReturnValueInVoidFunction, "Cannot return values in void functions.");
					error = true;
				}
				valueType = IntrinsicTypeDefs.NULL;
			} else {
				if (null == _value) {
					LogCompileErr(context, ParseErrorType.ReturnNullInNonVoidFunction, "Return requires a value in non-void functions.");
					error = true;
				} else {
					valueType = _value.TypeCheck(context, ref error);
					if (null == valueType)
						return null;

					if (!funcType.retType.CanStoreValue(context, valueType)) {
						LogCompileErr(context, ParseErrorType.ReturnTypeMismatch, "Return of value " + valueType + " doesn't match function's return type of " + funcType.retType + ".");
						error = true;
					}
				}
			}

			return SetType(valueType);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object result = null;
			if (null != _value) {
				result = _value.Evaluate(context);
				if (context.IsRuntimeErrorSet())
					return null;
			}

			context.control.flags |= ControlInfo.RETURN;
			context.control.result = result;
			return result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// ScriptToValue (<-)

	public class Expr_ScriptToValue : IExpr {
		protected IExpr _lhs { get { return nodes[0]; } }
		protected IExpr _rhs { get { return nodes[1]; } }

		public Expr_ScriptToValue(Parser parser, IExpr left, IExpr right) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(left);
			nodes.Add(right);
		}

		public override string MyToString(string indent) {
			return "SCRIPTOVALUE(" + _lhs.MyToString(indent) + " <- " + _rhs.MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef lType = _lhs.TypeCheck(context, ref error);
			ITypeDef rType = _rhs.TypeCheck(context, ref error);

			if (!(_lhs is IExpr_LValue)) {
				LogCompileErr(context, ParseErrorType.DeserializeStringOnly, "ScriptToValue to lvalue only.");
				error = true;
			}

			if (!IntrinsicTypeDefs.STRING.Equals(rType)) {
				LogCompileErr(context, ParseErrorType.DeserializeStringOnly, "ScriptToValue operator expected string, got (" + rType + ").");
				error = true;
			}

			return SetType(lType);
		}
		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object result = null;

			Variable variable = (_lhs as IExpr_LValue).EvaluateLValue(context);
			if (context.IsRuntimeErrorSet())
				return null;

			object scriptObject = _rhs.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;
			if (scriptObject is Pebble.RuntimeErrorInst)
				return null;
			if (!(scriptObject is string))
				return null;
			string script = (string)scriptObject;

			ClassValue resultInstance = null;
			TypeDef_Class lClassType = variable.type as TypeDef_Class;
			if (null != lClassType && "ScriptResult" == lClassType.className) {
				ClassDef resultClassDef = context.GetClass(lClassType.GetName());
				resultInstance = resultClassDef.Allocate(context);
				variable.value = resultInstance;
			}

			// Parse.
			bool sav = context.engine.logCompileErrors;
			context.engine.logCompileErrors = false;
			List<ParseErrorInst> errors = new List<ParseErrorInst>();
			IExpr scriptExpr = context.engine.Parse(script, ref errors, false);
			context.engine.logCompileErrors = sav;

			if (errors.Count > 0 || scriptExpr == null) {
				string msg = errors.Count == 0 ? "Error in deserialization script." : errors[0].msg;
				if (null == resultInstance) {
					SetRuntimeError(context, RuntimeErrorType.DeserializeScriptHasError, msg);
					return null;
				} else {
					resultInstance.GetByName("error").value = CoreLib.scriptErrorEnum.GetValue(errors[0].type.ToString());
					resultInstance.GetByName("message").value = msg;
					return resultInstance;
				}
			}

			// Check that expression's type matches l-values.
			if (null != resultInstance) {
				if (!lClassType.genericTypes[0].CanStoreValue(context, scriptExpr.GetTypeDef())) {
					SetRuntimeError(context, RuntimeErrorType.DeserializeTypeMismatch, "Script's type cannot be stored in Result template type.");
					return null;
				}

				ClassDef resultClassDef = context.GetClass(lClassType.GetName());
				resultInstance = resultClassDef.Allocate(context);
				variable.value = resultInstance;
			} else {
				if (!variable.type.CanStoreValue(context, scriptExpr.GetTypeDef())) {
					SetRuntimeError(context, RuntimeErrorType.DeserializeTypeMismatch, "Script's type cannot be stored in l-value.");
					return null;
				}
			}

			// Evaluate expression
			context.engine.logCompileErrors = false;
			result = context.engine.EvaluateExpression(scriptExpr);
			context.engine.logCompileErrors = sav;

			// If streaming directly to a value...
			if (null == resultInstance) {
				// ...if there was an error...
				if (result is RuntimeErrorInst) {
					// ...reapply the error so that execution is aborted.
					RuntimeErrorInst rei = result as RuntimeErrorInst;
					SetRuntimeError(context, rei.type, rei.msg);
					return null;
				} else {
					variable.value = result;
					return result;
				}
			} else {
				// ...if there was an error...
				if (result is RuntimeErrorInst) {
					// ...save the error info to the Result, and don't abort execution.
					RuntimeErrorInst rei = result as RuntimeErrorInst;
					resultInstance.GetByName("error").value = CoreLib.scriptErrorEnum.GetValue(rei.type.ToString());
					resultInstance.GetByName("message").value = rei.msg;
				} else {
					resultInstance.GetByName("value").value = result;
				}
				return resultInstance;
			}
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Stream <<

	public class Expr_Stream : IExpr_LValue {
		protected IExpr _lhs { get { return nodes[0]; } }
		protected IExpr _rhs { get { return nodes[1]; } }

		protected static TypeDef_Function _serializeFunctionType = null;

		public Expr_Stream(Parser parser, IExpr left, IExpr right) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(left);
			nodes.Add(right);
		}

		public override string MyToString(string indent) {
			return "STREAM2(" + _lhs.MyToString(indent) + " <- " + _rhs.MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef lType = _lhs.TypeCheck(context, ref error);
			ITypeDef rType = _rhs.TypeCheck(context, ref error);

			if (error)
				return null;

			if (!(lType is TypeDef_Class)) {
				LogCompileErr(context, ParseErrorType.StreamOnly, "Expression to left of << operator must have type Stream.");
				error = true;
				return null;
			} else {
				TypeDef_Class classtype = lType as TypeDef_Class;
				if ("Stream" != classtype.className) {
					LogCompileErr(context, ParseErrorType.StreamOnly, "Expression to left of << operator must be a Stream instance.");
					error = true;
					return null;
				}
			}
			/*
			if (!(_rhs is IExpr_LValue)) {
				LogCompileErr(context, ParseErrorType.StreamNoLValue, "Expression to right of << operator must be an l-value.");
				error = true;
				return null;
			}
			*/
			if (rType is TypeDef_Function) {
				LogCompileErr(context, ParseErrorType.StreamFunction, "Functions cannot be serialized.");
				error = true;
			}

			return SetType(lType);
		}

		public override Variable EvaluateLValue(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable stream = (_lhs as IExpr_LValue).EvaluateLValue(context);
			if (context.IsRuntimeErrorSet())
				return null;
			Pb.Assert(null != stream);
			if (null == stream.value) {
				SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "Left hand side of stream operator is null.");
				return null;
			}

			StreamLib.PebbleStreamHelper helper = stream.value as StreamLib.PebbleStreamHelper;
			if (helper.IsReading()) {
				if (!(_rhs is IExpr_LValue)) {
					SetRuntimeError(context, RuntimeErrorType.SerializeReadRequiresLValue, "When reading, right hand side of stream operator must be an l-value.");
					return null;
				}

				Variable variable = (_rhs as IExpr_LValue).EvaluateLValue(context);
				if (context.IsRuntimeErrorSet())
					return null;
				Pb.Assert(null != variable);

				bool result = helper.Read(context, helper, variable);
				if (context.IsRuntimeErrorSet())
					return null;
				Pb.Assert(result, "When would result be false but no runtime error?!");
			} else if (helper.IsWriting()) {
				object value = _rhs.Evaluate(context);

				bool result = helper.Write(context, helper, value);
				if (context.IsRuntimeErrorSet())
					return null;
				Pb.Assert(result, "When would result be false but no runtime error?!");
			} else {
				SetRuntimeError(context, RuntimeErrorType.SerializeStreamNotOpen, "Cannot serialize to stream that is not open.");
				return null;
			}

			return stream;
		}

		public override object Evaluate(ExecContext context) {
			Variable variable = EvaluateLValue(context);
			if (null != variable)
				return variable.value;
			return null;
		}
	}


	//////////////////////////////////////////////////////////////////////////////
	// Expr_Postrement

	public class Expr_Postrement : IExpr {
		protected IExpr _lvalue { get { return nodes[0]; } }
		protected bool _decrement;

		public Expr_Postrement(Parser parser, IExpr lvalue, bool decrement) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(lvalue);
			_decrement = decrement;
		}

		public override string ToString() {
			return "(" + _lvalue + (_decrement ? " --" : " ++") + ")";
		}

		public override string MyToString(string indent) {
			return "(" + _lvalue + (_decrement ? " --" : " ++") + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (error)
				return null;

			if (_lvalue is IExpr_LValue) {
				ITypeDef valueType = _lvalue.TypeCheck(context, ref error);
				if (null == valueType) {
					error = true;
					return null;
				}

				if (!IntrinsicTypeDefs.NUMBER.Equals(valueType)) {
					error = true;
					LogCompileErr(context, ParseErrorType.RementOnNumbersOnly, "Inc/Decrement operator on numbers only.");
				}

				if (valueType.IsConst()) {
					error = true;
					LogCompileErr(context, ParseErrorType.RementOnConst, "Inc/Decrement operator on constant value.");
				}

			} else {
				error = true;
				LogCompileErr(context, ParseErrorType.RementNonLValue, "Inc/Decrement operator on l-values only.");
			}

			return SetType(IntrinsicTypeDefs.NUMBER);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			Variable var = ((IExpr_LValue)_lvalue).EvaluateLValue(context);
			if (context.IsRuntimeErrorSet())
				return null;

			// Save the value before we modify it.
			double result = (double)var.value;
			if (_decrement)
				var.value = result - 1.0;
			else
				var.value = result + 1.0;

			// Return unmodified result.
			return result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_TypeAlias
	// - Does nothing during Execute pass.

	public class Expr_TypeAlias : IExpr {
		protected string _name;
		protected ITypeRef _typeRef;

		public Expr_TypeAlias(Parser parser, string sym, ITypeRef typeRef) : base(parser) {
			_name = sym;
			_typeRef = typeRef;
		}

		public override string ToString() {
			return "TYPE_ALIAS(" + _name + " = " + _typeRef.ToString() + ")";
		}

		public override string MyToString(string indent) {
			return "TYPE_ALIAS(" + _name + " = " + _typeRef.ToString() + ")";
		}

		/* Note: We don't implement the RegisterTypes pass on this deliberately.
		 * - If we register during RegisterTypes, the entire script will have access to the alias, 
		 *		BUT the type that is being aliased has to be known (declared above in the script).
		 * - If we register during TypeCheck (as we do now), then only the script below has access to the alias, 
		 *		BUT the type that is being referenced can be something that is registered later (such as a class).
		 *		
		 *	The latter is more intuitive, I think, so going with that.
		 */

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (error) 
				return null;

			// Make sure name isn't taken.
			if (context.DoesTypeExist(_name)) {
				LogCompileErr(context, ParseErrorType.SymbolAlreadyDeclared, "Cannot create alias '" + _name + "' because a type already exists with that name.");
				error = true;
				return null;
			}
			if (!context.stack.IsSymbolAvailable(context, _name)) {
				LogCompileErr(context, ParseErrorType.SymbolAlreadyDeclared, "Cannot create alias '" + _name + "' because a variable exists with that name.");
				error = true;
				return null;
			}

			// Make sure that typedef is valid.
			ITypeDef typeDef = _typeRef.Resolve(context, ref error);
			if (null == typeDef) {
				error = true;
				return null;
			}

			// The fact that there are two functions here is a remnant of a time when we used to preregister
			// this type before the TypeCheck pass. Keeping them in case I change my mind and go back to this.
			context.CreateAlias(_name, null);
			context.UpdateAlias(_name, typeDef);

			return SetType(IntrinsicTypeDefs.BOOL);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			return false;
		}
	}


	//////////////////////////////////////////////////////////////////////////////
	// Expr_This

	public class Expr_This : IExpr {
		public Expr_This(Parser parser) : base(parser) {
		}

		public override string ToString() {
			return "THIS";
		}

		public override string MyToString(string indent) {
			return "THIS";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			ClassDef classDef = context.stack.GetCurrentClassDef(true);
			if (null == classDef) {
				error = true;
				LogCompileErr(context, ParseErrorType.ClassRequired, "this keyword must be in class scope.");
				return null;
			}

			return SetType(classDef.typeDef);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			return context.stack.GetCurrentClassValue();
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_Length

	public class Expr_Length : IExpr {
		private IExpr _expr { get { return nodes[0]; } }

		public Expr_Length(Parser parser, IExpr rhs) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(rhs);
		}

		public override string ToString() {
			return "#(" + _expr + ")";
		}

		public override string MyToString(string indent) {
			return "LENGTH(" + _expr.MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			ITypeDef exprType = _expr.TypeCheck(context, ref error);
			if (error)
				return null;

			if (!IntrinsicTypeDefs.STRING.Equals(exprType)) {
				if (exprType is TypeDef_Class) {
					TypeDef_Class classtype = (TypeDef_Class)exprType;
					if ("List" != classtype.className && "Dictionary" != classtype.className)
						error = true;
				} else
					error = true;
			}

			if (error)
				LogCompileErr(context, ParseErrorType.LengthOperatorInvalidOperand, "Length operator can only operate on strings, Lists, or Dictionaries.");

			return SetType(IntrinsicTypeDefs.NUMBER);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object value = _expr.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			if (null == value) {
				SetRuntimeError(context, RuntimeErrorType.NullAccessViolation, "Length operator on null value.");
				return null;
			}

			if (value is string)
				return Convert.ToDouble(((string)value).Length);
			else if (value is PebbleList)
				return Convert.ToDouble(((PebbleList)value).list.Count);
			else if (value is PebbleDictionary)
				return Convert.ToDouble(((PebbleDictionary)value).dictionary.Count);

			return null;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_BinOp
	// num, num: +, -, *, /, %
	// num, bool: <, >, <=, >=
	// bool, bool: ||, &&
	// string, bool: ~=
	// string, string: ..
	public class Expr_BinOp : IExpr {
		public enum OP {
			ADD,
			SUB,
			MULT,
			DIV,
			MOD,
			POW,

			LT,
			GT,
			LEQ,
			GEQ,

			OR,
			AND,

			STREQI,

			CONCAT,
		};

		private OP _op;

		public Expr_BinOp(Parser parser, OP op, IExpr lhs, IExpr rhs) : base(parser) {
			_op = op;

			nodes = new List<IExpr>();
			nodes.Add(lhs);
			nodes.Add(rhs);
		}

		public override string ToString() {
			return _op + "(" + nodes[0] + ", " + nodes[1] + ")";
		}

		public override string MyToString(string indent) {
			return _op + "(" + nodes[0].MyToString(indent) + ", " + nodes[1].MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef ltype = nodes[0].TypeCheck(context, ref error);
			ITypeDef rtype = nodes[1].TypeCheck(context, ref error);

			if (error && (null == ltype || null == rtype))
				return null;

			switch (_op) {
				case OP.ADD:
				case OP.SUB:
				case OP.MULT:
				case OP.DIV:
				case OP.MOD:
				case OP.POW:
					if (!ltype.Equals(IntrinsicTypeDefs.NUMBER) || !rtype.Equals(IntrinsicTypeDefs.NUMBER)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, _op + " operation requires nums.");
						error = true;
					}
					return SetType(IntrinsicTypeDefs.NUMBER);

				case OP.LT:
				case OP.GT:
				case OP.LEQ:
				case OP.GEQ:
					if (!ltype.Equals(IntrinsicTypeDefs.NUMBER) || !rtype.Equals(IntrinsicTypeDefs.NUMBER)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, _op + " operation requires nums.");
						error = true;
					}
					return SetType(IntrinsicTypeDefs.BOOL);

				case OP.OR:
				case OP.AND:
					if (!ltype.Equals(IntrinsicTypeDefs.BOOL) || !rtype.Equals(IntrinsicTypeDefs.BOOL)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, _op + " operation requires bools.");
						error = true;
					}
					return SetType(IntrinsicTypeDefs.BOOL);

				case OP.STREQI:
					if (!ltype.Equals(IntrinsicTypeDefs.STRING) || !rtype.Equals(IntrinsicTypeDefs.STRING)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, _op + " operation requires strings.");
						error = true;
					}

					return SetType(IntrinsicTypeDefs.BOOL);

				case OP.CONCAT:
					if (!ltype.Equals(IntrinsicTypeDefs.STRING) || !rtype.Equals(IntrinsicTypeDefs.STRING)) {
						LogCompileErr(context, ParseErrorType.ConcatStringsOnly, _op + " operation requires strings.");
						error = true;
					}

					return SetType(IntrinsicTypeDefs.STRING);
			}

			LogCompileErr(context, ParseErrorType.Internal, "Unexpected binary operator (" + _op + ").");
			return null;
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object l = nodes[0].Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			// These operations can be resolved before evaluating the right hand side.
			switch (_op) {
				case OP.OR:
					if ((bool)l)
						return true;
					break;
				case OP.AND:
					if (!(bool)l)
						return false;
					break;
			}

			object r = nodes[1].Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			object result = null;
			switch (_op) {
				case OP.ADD:
					result = (double)l + (double)r;
					break;
				case OP.SUB:
					result = (double)l - (double)r;
					break;
				case OP.MULT:
					result = (double)l * (double)r;
					break;
				case OP.DIV:
					result = (double)l / (double)r;
					break;
				case OP.MOD:
					result = (double)l % (double)r;
					break;
				case OP.POW:
					result = Math.Pow((double)l, (double)r);
					break;

				case OP.LT:
					result = (double)l < (double)r;
					break;
				case OP.GT:
					result = (double)l > (double)r;
					break;
				case OP.LEQ:
					result = (double)l <= (double)r;
					break;
				case OP.GEQ:
					result = (double)l >= (double)r;
					break;

				case OP.OR:
					result = (bool)l || (bool)r;
					break;
				case OP.AND:
					result = (bool)l && (bool)r;
					break;

				case OP.STREQI:
					result = 0 == String.Compare((string)l, (string)r, true);
					break;

				case OP.CONCAT:
					result = (string)l + (string)r;
					break;
			}

			if (result is double) {
				double d = (double)result;
				if (Double.IsInfinity(d) || Double.IsNaN(d)) {
					SetRuntimeError(context, RuntimeErrorType.NumberInvalid, "Operator returned an invalid number.");
					return null;
				}
			}

			return result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_UnOp
	// num, num: +, -
	// bool, bool: !
	public class Expr_UnOp : IExpr {
		public enum OP {
			INVALID,

			POS,
			NEG,
			NOT,
			TOSTRING,
		};

		private OP _op;

		public Expr_UnOp(Parser parser, OP op, IExpr expr) : base(parser) {
			_op = op;

			nodes = new List<IExpr>();
			nodes.Add(expr);
		}

		public override string ToString() {
			return _op + "(" + nodes[0] + ")";
		}

		public override string MyToString(string indent) {
			return _op + "(" + nodes[0].MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef type = nodes[0].TypeCheck(context, ref error);
			if (error && null == type)
				return null;

			switch (_op) {
				case OP.POS:
				case OP.NEG:
					if (!type.Equals(IntrinsicTypeDefs.NUMBER)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, _op + " operation requires num.");
						error = true;
					}
					return SetType(IntrinsicTypeDefs.NUMBER);

				case OP.NOT:
					if (!type.Equals(IntrinsicTypeDefs.BOOL)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, _op + " operation requires bool.");
						error = true;
					}
					return SetType(IntrinsicTypeDefs.BOOL);

				case OP.TOSTRING:
					return SetType(IntrinsicTypeDefs.STRING);
			}

			LogCompileErr(context, ParseErrorType.Internal, "Unexpected unary operator (" + _op + ").");
			return null;
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			object l = nodes[0].Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			object result = null;
			switch (_op) {
				case OP.POS:
					result = (double)l;
					break;
				case OP.NEG:
					result = -(double)l;
					break;

				case OP.NOT:
					result = !(bool)l;
					break;

				case OP.TOSTRING:
					result = CoreLib.ValueToString(context, l, false);
					break;
			}

			return result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_Catch

	public class Expr_Catch : IExpr {
		private IExpr _block { get { return nodes[0]; } }

		private ClassDef _classDef;

		public Expr_Catch(Parser parser, IExpr block) : base(parser) {
			nodes = new List<IExpr>();
			nodes.Add(block);
		}

		public override string ToString() {
			return "CATCH(" + _block + ")";
		}

		public override string MyToString(string indent) {
			return "CATCH(" + _block.MyToString(indent) + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {

			ITypeDef exprType = _block.TypeCheck(context, ref error);
			if (error)
				return null;

			// Type of this expression is Result<exprType>.
			List<ITypeDef> genericTypes = new ArgList();
			genericTypes.Add(exprType);
			TypeDef_Class resultTypeDef = TypeFactory.GetTypeDef_Class("ScriptResult", genericTypes, false);
			_classDef = context.engine.defaultContext.RegisterIfUnregisteredTemplate(resultTypeDef);

			return SetType(_classDef.typeDef);
		}

		public override object Evaluate(ExecContext context) {
			Pb.Assert(0 == context.control.flags);

			ClassValue result = _classDef.Allocate(context);
			Variable status = result.GetByName("error");
			Variable value = result.GetByName("value");
			Variable message = result.GetByName("message");

			int callStackDepthAtStart = context.stack.GetCallStackDepth();

			object blockValue = _block.Evaluate(context);
			if (context.IsRuntimeErrorSet()) {
				status.value = CoreLib.scriptErrorEnum.GetValue(context.control.runtimeError.type.ToString());
				message.value = context.control.runtimeError.ToString();
				context.control.ClearError();

				// This check is very important as the likelihood that there is a piece of code 
				// somewhere that doesn't clean up the stack when it detects a runtime error is
				// high. I'm not 100% sure we should be asserting but we do need to blow up somehow.
				int endingDepth = context.stack.GetCallStackDepth();
				Pb.Assert(endingDepth == callStackDepthAtStart, "Internal Error: Something in the catch block didn't clean itself off the stack. Starting depth = " + callStackDepthAtStart + ", ending depth = " + endingDepth + ".");

			} else {
				status.value = CoreLib.scriptErrorEnum_noErrorValue;
				value.value = blockValue;
				message.value = "";
			}

			return result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_Enum

	public class Expr_Enum : IExpr {

		private class EnumValue {
			public string name;
			public IExpr initializer;
		};

		private string _enumName;
		private ITypeRef _enumTypeRef;
		private ITypeDef _enumTypeDef;
		private List<EnumValue> _valueList;
		private PebbleEnum _pe;

		public Expr_Enum(Parser parser, string name, ITypeRef enumType) : base(parser) {
			_enumName = name;
			_enumTypeRef = enumType;
		}

		public void AddValue(string name, IExpr initializer) {
			if (null == _valueList)
				_valueList = new List<EnumValue>();
			EnumValue newValue = new EnumValue();
			newValue.name = name;
			newValue.initializer = initializer;
			_valueList.Add(newValue);
		}

		public override string ToString() {
			return "ENUM(" + _enumName + ")";
		}

		public override string MyToString(string indent) {
			return "ENUM(" + _enumName + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			_enumTypeDef = _enumTypeRef.Resolve(context, ref error);
			if (error)
				return null;

			if (context.DoesTypeExist(_enumName) || !context.stack.IsSymbolAvailable(context, _enumName)) {
				LogCompileErr(context, ParseErrorType.SymbolAlreadyDeclared, "Symbol '" + _enumName + "' already in use.");
				error = true;
				return null;
			}

			_pe = new PebbleEnum(context, _enumName, _enumTypeDef);

			Dictionary<string, bool> memberNames = new Dictionary<string, bool>() { { "name", true }, { "value", true } };

			if (null == _valueList || _valueList.Count == 0) {
				LogCompileErr(context, ParseErrorType.EnumMustHaveValues, "Enum '" + _enumName + "' has no values.");
				error = true;
				return null;
			}

			foreach (EnumValue val in _valueList) {
				// check for duplicates.
				if (memberNames.ContainsKey(val.name)) {
					LogCompileErr(context, ParseErrorType.EnumNameDuplicate, "Enum has multiple values with name '" + val.name + "'.");
					error = true;
					return null;
				}
				memberNames.Add(val.name, true);

				if (null != val.initializer) {
					// If the initializer is a new expression, tell the new what type of thing we are new'ing.
					// This is what allows the user to leave it off.
					if (val.initializer is Expr_New)
						((Expr_New)val.initializer).parentSetTypeDef = _enumTypeDef;

					ITypeDef valType = val.initializer.TypeCheck(context, ref error);
					if (error)
						return null;
					if (!_enumTypeDef.CanStoreValue(context, valType)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, "Enum with type (" + _enumTypeDef.ToString() + ") cannot be initialized with value type (" + valType.ToString() + ").");
						error = true;
						return null;
					}

					_pe.AddValue_Expr(context, val.name, val.initializer);
				} else {
					_pe.AddValue_Default(context, val.name);
				}
			}

			_valueList.Clear();

			//! TODO: clean up enum when we abort due to error

			return null;
		}

		public override object Evaluate(ExecContext context) {
			return _pe.EvaluateValues(context);
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_Assert

	public class Expr_Assert : IExpr {
		private IExpr _expectedValue { get { return nodes[0]; } }
		private IExpr _message { get { return nodes[1]; } }
		private IExpr _block { get { return nodes[2]; } }

		private ParseErrorInst _pei;
		private bool _result = true;

		public static FunctionValue handler = null;

		public Expr_Assert(Parser parser, IExpr expectedValue, IExpr message, IExpr block, bool throwExceptionOnFail) : base(parser) {
			nodes = new List<IExpr>() {
				expectedValue,
				message,
				block
			};
		}

		public override string ToString() {
			return "ASSERT(" + _expectedValue + (null != _message ? ", " + _message : "") + ")";
		}

		public override string MyToString(string indent) {
			return "ASSERT(" + _expectedValue.MyToString(indent) + (null != _message ? ", " + _message.MyToString(indent) : "") + ")";
		}

		public override bool RegisterTypes(ExecContext context, ref bool error) {
			if (null != _block) {
				bool sav = context.engine.logCompileErrors;
				context.engine.logCompileErrors = false;
				bool retval = _block.RegisterTypes(context, ref error);
				context.engine.logCompileErrors = sav;

				if (error) {
					_pei = context.engine.GetParseErrorAndClear();
					error = false;
				}
			}

			return true;
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			if (!error) {

				_expectedValue.TypeCheck(context, ref error);
				if (error)
					return null;

				if (null != _message) {
					ITypeDef messageType = _message.TypeCheck(context, ref error);
					if (!IntrinsicTypeDefs.CONST_STRING.Comparable(context, messageType)) {
						LogCompileErr(context, ParseErrorType.TypeMismatch, "2nd (optional) argument of test expression must be a string.");
						error = true;
					}
				}

				if (!error && null != _block && null == _pei) {
					bool sav = context.engine.logCompileErrors;
					context.engine.logCompileErrors = false;
					_block.TypeCheck(context, ref error);
					context.engine.logCompileErrors = sav;

					if (error) {
						_pei = context.engine.GetParseErrorAndClear();
						error = false;
					}
				}

				_result = true;
			}

			return IntrinsicTypeDefs.BOOL;
		}

		public override object Evaluate(ExecContext context) {

			object expectedValue = _expectedValue.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			_result = true;
			string reason = "";

			object actualValue;
			if (null != _block) {
				// If we got a parse error...
				if (null != _pei) {
					_result = false;

					// ...and we expected one...
					if (expectedValue is ClassValue_Enum) {
						ClassValue_Enum expectedValueEnum = (ClassValue_Enum)expectedValue;
						string expectedName = (string)expectedValueEnum.Get(PebbleEnum.mrName).value;
						if (expectedValueEnum.classDef == CoreLib.scriptErrorEnum._classDef) {
							// ...and it's the right one.
							if (_pei.type.ToString() == expectedName || "Any" == expectedName) {
								// ... then it's no error.
								_result = true;
							} else {
								reason = "Expected " + expectedName + ", got " + _pei.ToString();
							}
						} else {
							reason = "Expected " + expectedName + ", got " + _pei.ToString();
						}
					} else {
						reason = "Unexpected " + _pei.type.ToString();
					}
					// ... else we got no parse error.
				} else {
					actualValue = _block.Evaluate(context);

					if (expectedValue is ClassValue_Enum) {
						ClassValue_Enum expectedValueEnum = (ClassValue_Enum)expectedValue;

						// If we expect a runtime error...
						if (expectedValueEnum.classDef == CoreLib.scriptErrorEnum._classDef) {
							string expectedErrorName = (string)expectedValueEnum.Get(PebbleEnum.mrName).value;
							// ...and we got one...
							if (context.IsRuntimeErrorSet()) {
								// ...if it isn't the right error that's a failed test.
								if ("Any" == expectedErrorName || context.control.runtimeError.type.ToString() != expectedErrorName) {
									reason = "Expected " + expectedErrorName + " error but got " + context.control.runtimeError.type.ToString() + ".";
									_result = false;
								}
							} else {
								// ...we didn't get an error but expected one, so that's a failed test.
								reason = "Expected " + expectedErrorName + " error but got no error.";
								_result = false;
							}
						} else {
							// We didn't expect an error but got one.
							if (context.IsRuntimeErrorSet()) {
								reason = "Unexpected error: " + context.control.runtimeError.ToString();
								_result = false;
							}
						}
					} else {
						_result = Expr_Compare.Compare(actualValue, expectedValue);
					}

					context.control.ClearError();
				}
			// ...we got no block, so the expected value is just a bool.
			} else {
				_result = (bool)expectedValue;
			}

			string msg = "";
			if (!_result) {
				if (null != _message) {
					object msgObject = _message.Evaluate(context);
					// Assert isn't responsible for handling exceptions when evaluating the message argument! 
					if (context.IsRuntimeErrorSet())
						return null;
					msg = (string)msgObject;
				}
			}

			if (null != handler) {
				object oresult = handler.Evaluate(context, new List<object> { _result, msg });
				if (context.IsRuntimeErrorSet())
					return null;
				_result = (bool)oresult;
			}

			if (!_result)
				SetRuntimeError(context, RuntimeErrorType.Assert, "ASSERT FAILED : " + reason + " " + msg);

			return _result;
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_Is

	public class Expr_Is : IExpr {
		private IExpr _expr { get { return nodes[0]; } }
		private string _symbol;

		private ClassDef _lhsClassDef;
		private ClassDef _rhsClassDef;
		private bool _automaticallyTrue = false;

		public Expr_Is(Parser parser, IExpr expr, string symbol) : base(parser) {
			nodes = new List<IExpr>() {
				expr,
			};
			_symbol = symbol;
		}

		public override string ToString() {
			return "IS(" + _expr + ": " + _symbol + ")";
		}

		public override string MyToString(string indent) {
			return "IS(" + _expr.MyToString(indent) + ": " + _symbol + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef exprTypeDef = _expr.TypeCheck(context, ref error);
			if (error)
				return null;

			if (!(exprTypeDef is TypeDef_Class)) {
				LogCompileErr(context, ParseErrorType.IsRequiresClass, "is: Left hand side of is operator must be an expression of type class.");
				error = true;
				return null;
			}
			TypeDef_Class exprClassType = exprTypeDef as TypeDef_Class;
			_lhsClassDef = context.GetClass(exprClassType.className);

			_rhsClassDef = context.GetClass(_symbol);
			if (null == _rhsClassDef) {
				LogCompileErr(context, ParseErrorType.ClassNotDeclared, "is: Class not found with name '" + _symbol + "'.");
				error = true;
				return null;
			}

			if (context.IsChildClass(_rhsClassDef, _lhsClassDef)) {
				_automaticallyTrue = true;
			} else if (!context.IsChildClass(_lhsClassDef, _rhsClassDef)) {
				LogCompileErr(context, ParseErrorType.TypesUnrelated, "is: Left hand expression type is not a parent of right hand type.");
				error = true;
				return null;
			}

			return IntrinsicTypeDefs.BOOL;
		}

		public override object Evaluate(ExecContext context) {
			object exprResult = _expr.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			if (null == exprResult)
				return false;

			if (_automaticallyTrue)
				return true;

			ClassValue classValue = exprResult as ClassValue;
			return context.IsChildClass(_rhsClassDef, classValue.classDef);
		}
	}

	//////////////////////////////////////////////////////////////////////////////
	// Expr_As

	public class Expr_As : IExpr {
		private IExpr _expr { get { return nodes[0]; } }
		private string _symbol;

		private ClassDef _lhsClassDef;
		private ClassDef _rhsClassDef;
		private bool _automaticallyTrue = false;

		public Expr_As(Parser parser, IExpr expr, string symbol) : base(parser) {
			nodes = new List<IExpr>() {
				expr,
			};
			_symbol = symbol;
		}

		public override string ToString() {
			return "AS(" + _expr + ": " + _symbol + ")";
		}

		public override string MyToString(string indent) {
			return "AS(" + _expr.MyToString(indent) + ": " + _symbol + ")";
		}

		public override ITypeDef TypeCheck(ExecContext context, ref bool error) {
			ITypeDef exprTypeDef = _expr.TypeCheck(context, ref error);
			if (error)
				return null;

			if (!(exprTypeDef is TypeDef_Class)) {
				LogCompileErr(context, ParseErrorType.ClassRequired, "as: Left hand side of as operator must be an expression of type class.");
				error = true;
				return null;
			}
			TypeDef_Class exprClassType = exprTypeDef as TypeDef_Class;
			_lhsClassDef = context.GetClass(exprClassType.className);

			_rhsClassDef = context.GetClass(_symbol);
			if (null == _rhsClassDef) {
				LogCompileErr(context, ParseErrorType.ClassNotDeclared, "as: Class not found with name '" + _symbol + "'.");
				error = true;
				return null;
			}

			if (context.IsChildClass(_rhsClassDef, _lhsClassDef)) {
				_automaticallyTrue = true;
			} else if (!context.IsChildClass(_lhsClassDef, _rhsClassDef)) {
				LogCompileErr(context, ParseErrorType.TypesUnrelated, "as: Left hand expression type is not a parent of right hand type.");
				error = true;
				return null;
			}

			return _rhsClassDef.typeDef;
		}

		public override object Evaluate(ExecContext context) {
			object exprResult = _expr.Evaluate(context);
			if (context.IsRuntimeErrorSet())
				return null;

			if (null == exprResult)
				return null;

			if (_automaticallyTrue)
				return exprResult;

			ClassValue classValue = exprResult as ClassValue;
			if (context.IsChildClass(_rhsClassDef, classValue.classDef))
				return exprResult;

			return null;
		}
	}
}
