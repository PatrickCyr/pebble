/*
Implementation of Function values.
See Copyright Notice in LICENSE.TXT
*/

using System.Collections.Generic;

namespace Pebble {

	public abstract class FunctionValue {

		public TypeDef_Function valType;
		public FunctionValue_Host.EvaluateDelegate Evaluate;

		// Note that this is a list of Expr_Literals. A null entry means no default value.
		// To make a default value of null, create an Expr_Literal with value null.
		public List<Expr_Literal> argDefaultValues;
		public int minArgs;
		public List<bool> argHasDefaults = null;

		protected void BuildArgHasDefaults(int argCount) {
			minArgs = argCount;

			if (null != argDefaultValues && 0 == argDefaultValues.Count)
				argDefaultValues = null;
			if (null == argDefaultValues)
				return;

			Pb.Assert(argDefaultValues.Count == argCount, "Default values array length doesn't match arg array length.");

			bool defaultFound = false;
			argHasDefaults = new List<bool>();
			for (int ii = 0; ii < argCount; ++ii) {
				argHasDefaults.Add(null != argDefaultValues[ii]);
				if (!defaultFound && null != argDefaultValues[ii]) {
					defaultFound = true;
					minArgs = ii;
				}
			}
		}
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
			argDefaultValues = defaultArgVals;
			BuildArgHasDefaults(_argTypes.Count);

			valType = TypeFactory.GetTypeDef_Function(_retType, _argTypes, minArgs, _varargs, classType, true, isStatic);
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
			argDefaultValues = defaultVals;
			BuildArgHasDefaults(_argTypes.Count);

			originalName = name;
			expr = _expr;
			typeRef = new TypeRef_Function(_retType, _argTypes, argHasDefaults, false);
			
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
				}
				context.stack.PopScope();

				if (context.IsRuntimeErrorSet())
					return null;

				if (0 != (context.control.flags & ControlInfo.RETURN)) {
					result = context.control.result;
					context.control.result = null;
					context.control.flags -= ControlInfo.RETURN;
				}

				Pb.Assert(0 == context.control.flags);

				return result;
			};
		}

		private object _EvaluateInternal(ExecContext context) {
			return expr.Evaluate(context);
		}
	}
}