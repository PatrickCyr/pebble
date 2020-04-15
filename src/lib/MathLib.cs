/*
Implementation of Pebble's Math library.
See Copyright Notice in LICENSE.TXT

This library is automatically registered (see CoreLib.cs). Programs don't need to register it manually.
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class MathLib {

		private static Random rand;

		public static void Register(Engine engine) {

			//@ class Math
			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("Math", null, false);
			ClassDef classDef = engine.defaultContext.CreateClass("Math", ourType, null, null, true);
			classDef.Initialize();

			//@ static const num pi;
			//   pi is the ratio of a circle's circumpherence to its diameter. 3.1415...
			classDef.AddMemberLiteral("pi", IntrinsicTypeDefs.CONST_NUMBER, Math.PI, true);
			//@ static const num e;
			//   e is the base of the natural logarithm. 2.71828...
			classDef.AddMemberLiteral("e", IntrinsicTypeDefs.CONST_NUMBER, Math.E, true);
			//@ static const num tau;
			//   tau is the ratio of a circle's circumpherence to its radius. tau = 2 * pi
			classDef.AddMemberLiteral("tau", IntrinsicTypeDefs.CONST_NUMBER, Math.PI * 2, true);

			// Note on error handling:
			// The native math functions don't throw exceptions when inputs are invalid. Rather, the functions return one of C#'s "invalid" number values. 
			// These values are illegal in Pebble, and there is code in Expr_Call to detect if a function has returned one of those values and which 
			// generates a script runtime error if it does. Thus, there a lot of the MathLib functions don't do any argument or return value checking
			// themselves, but rather rely on Expr_Call to detect if a user fed a bad input into a function (ie. Sqrt(-1)).

			//@ static num Abs(num radians)
			//   Returns the absolute value of the input.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Abs(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Abs", newValue.valType, newValue, true);
			}

			//@ static num Acos(num)
			//   Returns the arccosine of the input in radians.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Acos(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Acos", newValue.valType, newValue, true);
			}

			//@ static num Asin(num)
			//   Returns the arcsine of the input in radians
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Asin(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Asin", newValue.valType, newValue, true);
			}

			//@ static num Atan(num)
			//   Returns the arctangent of the input in radians.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Atan(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Atan", newValue.valType, newValue, true);
			}

			//@ static num Ceiling(num)
			//   If an exact integer, returns the integer, otherwise returns the next highest integer. aka, it rounds up.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Ceiling(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Ceiling", newValue.valType, newValue, true);
			}

			//@ static num Cos(num radians)
			//   Returns the cosine of the input.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Cos(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Cos", newValue.valType, newValue, true);
			}

			//@ static num Exp(num power)
			//   Returns e raised to the given power.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Exp(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Exp", newValue.valType, newValue, true);
			}

			//@ static num Floor(num)
			//   If an exact integer, returns the integer, otherwise returns the next lower integer. aka, it rounds down.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Floor(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Floor", newValue.valType, newValue, true);
			}

			//@ static num Log(num)
			//   Returns the natural logarithm of the input.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					double b = (double)args[1];
					return Math.Log(a, b);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Log", newValue.valType, newValue, true);
			}

			//@ static num Min(num, num)
			//   Returns the lesser of the two inputs.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					double b = (double)args[1];
					return Math.Min(a, b);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Min", newValue.valType, newValue, true);
			}

			//@ static num Max(num)
			//   Returns the greater of the two inputs.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					double b = (double)args[1];
					return Math.Max(a, b);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Max", newValue.valType, newValue, true);
			}

			//@ static num Pow(num b, num p)
			//   Returns b raised to the power p.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					double b = (double)args[1];
					return Math.Pow(a, b);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Pow", newValue.valType, newValue, true);
			}

			//@ static num Rand()
			//   Returns a random number between 0 and 1.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					if (null == rand)
						rand = new Random();
					return rand.NextDouble();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("Rand", newValue.valType, newValue, true);
			}

			//@ static void RandSeed(num seed)
			//   Seeds the random number generator with the given value (converted to a signed 32-bit integer).
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double dseed = (double) args[0];
					if (dseed > Int32.MaxValue || dseed < Int32.MinValue) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "Math::RandSeed seed argument must be within the range of a C# Int32.");
						return null;
					}
					int seed = Convert.ToInt32(dseed);
					rand = new Random(seed);
					return null;
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.VOID, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("RandSeed", newValue.valType, newValue, true);
			}

			//@ static num Round(num n[, num decimals])
			//   Rounds the input to the nearest integer, or optionally to the nearest specified decimal point.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					double b = (double)args[1];
					int decimals = Convert.ToInt32(b);
					if (decimals < 0) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, "2nd argument to Math.Round cannot be negative.");
						return null;
					}
					return Math.Round(a, decimals);
				};

				// Note: Here is an example of how you provide default argument values for a host function.
				List<Expr_Literal> defaultArgVals = new List<Expr_Literal>();
				defaultArgVals.Add(null);
				defaultArgVals.Add(new Expr_Literal(null, 0.0, IntrinsicTypeDefs.NUMBER));

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false, null, false, defaultArgVals);
				classDef.AddMemberLiteral("Round", newValue.valType, newValue, true);
			}

			//@ static num Sin(num radians)
			//   Returns the sine of the input.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Sin(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Sin", newValue.valType, newValue, true);
			}

			//@ static num Sqrt(num)
			//   Returns the square root of the input. 
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Sqrt(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Sqrt", newValue.valType, newValue, true);
			}

			//@ static num Tan(num radians)
			//   Returns the tangent of the input.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double a = (double)args[0];
					return Math.Tan(a);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Tan", newValue.valType, newValue, true);
			}

			classDef.FinalizeClass(engine.defaultContext);

			UnitTests.testFuncDelegates.Add("MathLib", RunTests);
		}

		public static bool RunTests(Engine engine, bool verbose) {
			bool result = true;

			engine.Log("\n*** MathLib: Running tests...");

			result &= engine.RunTest("Math::Abs(-7);", 7, verbose);
			result &= engine.RunTest("Math::tau / 4 == Math::Acos(0);", true, verbose);
			result &= engine.RunTest("Math::tau / 4 == Math::Asin(1);", true, verbose);
			result &= engine.RunTest("Math::tau / 8 == Math::Atan(1);", true, verbose);
			result &= engine.RunTest("Math::Ceiling(6.5);", 7, verbose);
			result &= engine.RunTest("1 == Math::Cos(0) && 0 == Math::Round(Math::Cos(Math::pi / 2), 10);", true, verbose);
			result &= engine.RunTest("Math::Round(Math::e * Math::e - Math::Exp(2), 5);", 0, verbose);
			result &= engine.RunTest("Math::Floor(7.5);", 7, verbose);
			result &= engine.RunTest("Math::Log(100, 10);", 2, verbose);
			result &= engine.RunTest("Math::Min(7, 8);", 7, verbose);
			result &= engine.RunTest("Math::Min(8, 7);", 7, verbose);
			result &= engine.RunTest("Math::Max(7, 6);", 7, verbose);
			result &= engine.RunTest("Math::Max(6, 7);", 7, verbose);
			result &= engine.RunTest("Math::Pow(2, 3);", 8, verbose);
			result &= engine.RunTest("Math::Floor(Math::Rand());", 0, verbose);
			result &= engine.RunTest("7 == Math::Round(7.1) && 7 == Math::Round(6.9);", true, verbose);
			result &= engine.RunTest("7 == Math::Round(7.1, 0) && 7 == Math::Round(6.9, 0);", true, verbose);
			result &= engine.RunTest("3.14 == Math::Round(Math::pi, 2) && 3.142 == Math::Round(Math::pi, 3);", true, verbose);
			result &= engine.RunTest("0 == Math::Sin(0) && 1 == Math::Sin(Math::pi / 2);", true, verbose);
			result &= engine.RunTest("Math::Sqrt(49);", 7, verbose);
			result &= engine.RunTest("Math::Round(Math::Tan(Math::tau / 8), 4);", 1, verbose);
			result &= engine.RunTest("{ Math::RandSeed(1); Math::Rand() - 0.24866858 < 0.0001; }", true, verbose);

			bool sav = engine.logCompileErrors;
			engine.logCompileErrors = false;

			result &= engine.RunCompileFailTest("class MathChild : Math;", ParseErrorType.ClassParentSealed, verbose);
			result &= engine.RunCompileFailTest("Math::Abs(1, 2);", ParseErrorType.ArgCountMismatch, verbose);
			result &= engine.RunCompileFailTest("Math::e = 99;", ParseErrorType.AssignToConst, verbose);
			result &= engine.RunCompileFailTest("Math::pi = 99;", ParseErrorType.AssignToConst, verbose);
			result &= engine.RunCompileFailTest("Math::tau = 99;", ParseErrorType.AssignToConst, verbose);

			result &= engine.RunRuntimeFailTest("Math::Acos(2);", RuntimeErrorType.NumberInvalid, verbose);
			result &= engine.RunRuntimeFailTest("Math::Asin(2);", RuntimeErrorType.NumberInvalid, verbose);
			result &= engine.RunRuntimeFailTest("Math::RandSeed(3000000000);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("Math::RandSeed(-3000000000);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("Math::Round(1.99, -1);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("Math::Sqrt(-1);", RuntimeErrorType.NumberInvalid, verbose);

			engine.logCompileErrors = sav;
			engine.Log("*** MathLib: Tests " + (result ? "succeeded" : "FAILED"));


			return result;
		}
	}
}