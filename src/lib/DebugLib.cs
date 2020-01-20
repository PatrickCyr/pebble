/*
Debug library (functions for helping to debug Pebble programs).
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class DebugLib {

		static Stopwatch timer = null;

		public static void Register(Engine engine) {

			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("Debug", null, false);
			ClassDef classDef = engine.defaultContext.CreateClass("Debug", ourType, null, null, true);
			classDef.Initialize();

			// string DumpClass(string className = "")
			//   Returns a string dump of the fields of a class with the given name.
			//   If no argument is given (or string is empty), instead returns a list of all classes.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string className = (string)args[0];

					if ("" == className) {
						return context.GetDebugTypeString(true, false, false);
					} else {
						ClassDef def = context.GetClass(className);
						if (null == def)
							return "Class '" + className + "' not found.";

						return def.GetDebugString();
					}
				};

				// Note: Here is an example of how you provide default argument values for a host function.
				List<Expr_Literal> defaultArgVals = new List<Expr_Literal>();
				defaultArgVals.Add(new Expr_Literal("", IntrinsicTypeDefs.STRING));

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING }, eval, false, null, false, defaultArgVals);
				classDef.AddMemberLiteral("DumpClass", newValue.valType, newValue, true);
			}

			// string DumpStack()
			//   Returns a string printout of the stack at the point the function is called.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					return context.ToString();
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("DumpStack", newValue.valType, newValue, true);
			}

			// string DumpTypes()
			//   Returns a string printout of the registered types.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					return context.GetDebugTypeString();
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("DumpTypes", newValue.valType, newValue, true);
			}

			// string GetTotalMemory()
			//   Wraps GC.GetTotalMemory, which returns the number of bytes estimated to be allocated by C#.
			//   Note that this is NOT just memory allocated by Pebble.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					bool forceFullCorrection = (bool) args[0];

					return Convert.ToDouble(GC.GetTotalMemory(forceFullCorrection));
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.BOOL }, eval, false);
				classDef.AddMemberLiteral("GetTotalMemory", newValue.valType, newValue, true);
			}

			// functype<bool(bool, string)> SetAssertCallback(functype<bool(bool, string)>)
			//   Sets callback for assert results, returns previous callback.
			//   Callback gets success as first variable, message as second. If returns false, system throws an Assert runtime exception.
			{
				TypeRef_Function handlerTypeRef = new TypeRef_Function(new TypeRef("bool"), new List<ITypeRef>() { new TypeRef("bool"), new TypeRef("string") }, null);
				bool error = false;
				TypeDef_Function handlerTypeDef = (TypeDef_Function) handlerTypeRef.Resolve(engine.defaultContext, ref error);
				Pb.Assert(!error, "Internal error: SetAssertHandler initialization.");

				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					FunctionValue newHandler = (FunctionValue)args[0];
					FunctionValue oldHandler = Expr_Assert.handler;
					Expr_Assert.handler = newHandler;
					return oldHandler;
				};
				FunctionValue newValue = new FunctionValue_Host(handlerTypeDef, new ArgList { handlerTypeDef }, eval, false);
				classDef.AddMemberLiteral("SetAssertCallback", newValue.valType, newValue, true);
			}

			//@ bool SetLogCompileErrors(bool log)
			//   Sets whether or not compile errors should be logged to the Engine's log function.
			//   Returns the previous value.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					bool oldValue = context.engine.logCompileErrors;
					bool newVal = (bool)args[0];
					context.engine.logCompileErrors = newVal;
					return oldValue;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.BOOL }, eval, false);
				classDef.AddMemberLiteral("SetLogCompileErrors", newValue.valType, newValue, true);
			}

			// void TimerStart()
			//   Starts a debug timer. If one is already running it will be set to this new start time.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					timer = Stopwatch.StartNew();
					return null;
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.VOID, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("TimerStart", newValue.valType, newValue, true);
			}

			// num TimerGet()
			//   Returns elapsed ms since TimerStart called, or -1 if TimerStart wasn't previously called.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					if (null == timer)
						return -1.0;
					return Convert.ToDouble(timer.ElapsedMilliseconds);

				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("TimerGet", newValue.valType, newValue, true);
			}

			classDef.FinalizeClass(engine.defaultContext);
		}
	}
}