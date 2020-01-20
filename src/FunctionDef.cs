/*
Implementation of Function values.
See Copyright Notice in LICENSE.TXT
*/


using System.Collections.Generic;

//using ArgList = System.Collections.Generic.List<Pebble.TypeRef>;

namespace Pebble {

	public class FunctionValue {
		//public delegate object EvaluateDelegate(ExecContext context, List<IExpr> args);

		
		public TypeDef_Function valType;
		public FunctionValue_Host.EvaluateDelegate Evaluate;
	}


	// This is for functions which are defined in the host language (C#).
	public class FunctionValue_Host : FunctionValue {
		public delegate object EvaluateDelegate(ExecContext context, List<object> args, ClassValue thisScope = null);

		public FunctionValue_Host(TypeDef_Function _valType, EvaluateDelegate eval) {
			valType = _valType;
			Evaluate = eval;
		}

		// NOTE: isStatic = true has never been tested. Library functions are static but are neither flagged as static nor as class members,
		// so references can be saved to them by users already.
		public FunctionValue_Host(ITypeDef _retType, List<ITypeDef> _argTypes, EvaluateDelegate _Evaluate, bool _varargs = false, TypeDef_Class classType = null, bool isStatic = false, List<Expr_Literal> defaultArgVals = null) {
			valType = TypeFactory.GetTypeDef_Function(_retType, _argTypes, defaultArgVals, _varargs, classType, true, isStatic);
			Evaluate = _Evaluate;
		}
	}

	// This is for functions which are defined in Pebble code.
	// * These are NEVER generic!
	// * These are never varArgs (aka infinite args)
	public class FunctionValue_Script : FunctionValue {
		public IExpr expr;
		// This is the name as originally defined.  This is the symbol that the function's body knows the function by.
		public string originalName; 
		public List<string> argNames;
		public TypeRef_Function typeRef;
		public TypeDef_Function typeDef;

		public ClassDef staticClassDef;

		public FunctionValue_Script(string name, IExpr _expr, ITypeRef _retType, List<ITypeRef> _argTypes, List<Expr_Literal> defaultVals = null) {
			originalName = name;
			expr = _expr;
			typeRef = new TypeRef_Function(_retType, _argTypes, defaultVals, false);
			
			Evaluate = (context, args, thisScope) => {
				Pb.Assert(null != originalName);
				Pb.Assert(args.Count == _argTypes.Count, "Internal error: Don't have enough arguments!");

				if (context.IsRuntimeErrorSet())
					return null;

				object result;

				bool pushResult = false;
				if (null != staticClassDef) {
					pushResult = context.stack.PushClassCall_StaticOrTypeCheck(typeDef, staticClassDef, true, context);
				} else {
					pushResult = context.stack.PushCall(typeDef, originalName, thisScope, false, context);
				}

				if (!pushResult) {
					context.SetRuntimeError(RuntimeErrorType.StackOverflow, "FunctionValue_Script.Evaluate : stack overflow");
					return null;
				}

				{
					// Add name of this function as first variable in function scope.
					// This condition loosely mirrors the check in Expr_Literal. It's shitty that the code that
					// runs during TypeCheck is so far removed from this code that runs during Execute.
					if (null == typeDef.classType)
						context.stack.AddVariable(originalName, false, typeDef, this);

					// Add argument variables to function scope.
					if (null != args) {
						for (int ii = 0; ii < args.Count; ++ii) {
							context.AddLocalVariable(argNames[ii], valType.argTypes[ii], args[ii]);
						}
					}

					result = _EvaluateInternal(context);
					if (context.IsRuntimeErrorSet()) {
						context.stack.PopScope();
						return null;
					}
				}
				context.stack.PopScope();

				return result;
			};
		}

		private object _EvaluateInternal(ExecContext context) {
			return expr.Evaluate(context);
		}
	}
}