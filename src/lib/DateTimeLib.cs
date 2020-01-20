/*
DateTime library (date and time functions).
See Copyright Notice in LICENSE.TXT
*/

using System;
using System.Collections.Generic;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class PebDateTime : ClassValue {
		public DateTime dt;
	}

	public class DateTimeLib {

		public static void Register(Engine engine) {

			//@ class DateTime
			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("DateTime", null, false);
			ClassDef classDef = engine.defaultContext.CreateClass("DateTime", ourType, null, null, true);
			classDef.childAllocator = () => {
				return new PebDateTime();
			};

			classDef.Initialize();

			//@ DateTime Clone()
			//   Returns a copy of 'this' DateTime.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime thisDT = thisScope as PebDateTime;
					PebDateTime pdt = classDef.Allocate(context) as PebDateTime;
					pdt.dt = new DateTime(thisDT.dt.Ticks);
					return pdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("Clone", newValue.valType, newValue, false);
			}

			//@ static DateTime Create(num year, num month, num day, num hour, num minute, num second, num millisecond)
			//   Creates a new DateTime with the given values.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double year = (double)args[0];
					double month = (double)args[1];
					double day = (double)args[2];
					double hour = (double)args[3];
					double minute = (double)args[4];
					double second = (double)args[5];
					double millisecond = (double)args[6];

					PebDateTime pdt = classDef.Allocate(context) as PebDateTime;
					try {
						pdt.dt = new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day), Convert.ToInt32(hour), Convert.ToInt32(minute), Convert.ToInt32(second), Convert.ToInt32(millisecond));
					} catch (ArgumentOutOfRangeException aoor) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, aoor.ToString());
						return null;
					} catch (Exception e) {
						context.SetRuntimeError(RuntimeErrorType.NativeException, e.ToString());
						return null;
					}
					return pdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false);
				classDef.AddMemberLiteral("Create", newValue.valType, newValue, true);
			}

			//@ bool Equals(DateTime other)
			//   Returns true iff both this and the other DateTime have the same date and time exactly.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {

					PebDateTime pdt = thisScope as PebDateTime;
					PebDateTime otherPdt = args[0] as PebDateTime;

					if (null == otherPdt) {
						context.SetRuntimeError(RuntimeErrorType.NullAccessViolation, "DateTime::Equals - Argument is null.");
						return null;
					}

					return	pdt.dt.Year == otherPdt.dt.Year &&
							pdt.dt.Month == otherPdt.dt.Month &&
							pdt.dt.Day == otherPdt.dt.Day &&
							pdt.dt.Hour == otherPdt.dt.Hour &&
							pdt.dt.Minute == otherPdt.dt.Minute &&
							pdt.dt.Second == otherPdt.dt.Second &&
							pdt.dt.Millisecond == otherPdt.dt.Millisecond;
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { ourType }, eval, false, ourType);
				classDef.AddMemberLiteral("Equals", newValue.valType, newValue, false);
			}

			//@ static DateTime GetNow()
			//   Returns a new DateTime with the current date and time.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {

					PebDateTime pdt = classDef.Allocate(context) as PebDateTime;
					pdt.dt = DateTime.Now;
					return pdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetNow", newValue.valType, newValue, true);
			}

			//@ bool IsDateSame(DateTime other)
			//   Returns true iff both this and the other DateTime have the same date. Time is ignored.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {

					PebDateTime pdt = thisScope as PebDateTime;
					PebDateTime otherPdt = args[0] as PebDateTime;

					if (null == otherPdt) {
						context.SetRuntimeError(RuntimeErrorType.NullAccessViolation, "DateTime::IsDateSame - Argument is null.");
						return null;
					}

					return 
						pdt.dt.Year == otherPdt.dt.Year &&
						pdt.dt.Month == otherPdt.dt.Month &&
						pdt.dt.Day == otherPdt.dt.Day;
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { ourType }, eval, false, ourType);
				classDef.AddMemberLiteral("IsDateSame", newValue.valType, newValue, false);
			}

			//@ DateTime Set(num year, num month, num day, num hour, num minute, num second, num millisecond)
			//   Gives this DateTime the given values
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					double year = (double)args[0];
					double month = (double)args[1];
					double day = (double)args[2];
					double hour = (double)args[3];
					double minute = (double)args[4];
					double second = (double)args[5];
					double millisecond = (double)args[6];

					PebDateTime pdt = thisScope as PebDateTime;
					DateTime newdt;
					try {
						newdt = new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day), Convert.ToInt32(hour), Convert.ToInt32(minute), Convert.ToInt32(second), Convert.ToInt32(millisecond));
					} catch (ArgumentOutOfRangeException aoor) {
						context.SetRuntimeError(RuntimeErrorType.ArgumentInvalid, aoor.ToString());
						return null;
					} catch (Exception e) {
						context.SetRuntimeError(RuntimeErrorType.NativeException, e.ToString());
						return null;
					}
					pdt.dt = newdt;
					return pdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER, IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("Set", newValue.valType, newValue, false);
			}

			//@ string ThisToScript(string prefix)
			//   ThisToScript is used by Serialize. A classes' ThisToScript function should return code which can rebuild the class.
			//   Note that it's only the content of the class, not the "new A" part. ie., it's the code that goes in the defstructor.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					string result = (string)args[0] + "\tSet(" + pdt.dt.Year + ", " + pdt.dt.Month + ", " + pdt.dt.Day + ", " + pdt.dt.Hour + ", " + pdt.dt.Minute + ", " + pdt.dt.Second + ", " + pdt.dt.Millisecond + ");\n";
					return result;
				};

				FunctionValue_Host newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { IntrinsicTypeDefs.STRING }, eval, false, ourType);
				classDef.AddMemberLiteral("ThisToScript", newValue.valType, newValue);
			}

			//@ string ToString()
			//   Returns a string representation of this DateTime.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return pdt.dt.ToString();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("ThisToString", newValue.valType, newValue, false);
			}

			//@ num GetYear()
			//   Returns the DateTime's year.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return Convert.ToDouble(pdt.dt.Year);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetYear", newValue.valType, newValue, false);
			}
			//@ num GetMonth()
			//   Returns the DateTime's month.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return Convert.ToDouble(pdt.dt.Month);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetMonth", newValue.valType, newValue, false);
			}

			//@ num GetDay()
			//   Returns the DateTime's day.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return Convert.ToDouble(pdt.dt.Day);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetDay", newValue.valType, newValue, false);
			}

			//@ num GetHour()
			//   Returns the DateTime's hour.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return Convert.ToDouble(pdt.dt.Hour);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetHour", newValue.valType, newValue, false);
			}

			//@ num GetMinute()
			//   Returns the DateTime's minute.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return Convert.ToDouble(pdt.dt.Minute);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetMinute", newValue.valType, newValue, false);
			}

			//@ num GetSecond()
			//   Returns the DateTime's second.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return Convert.ToDouble(pdt.dt.Second);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetSecond", newValue.valType, newValue, false);
			}

			//@ num GetMillisecond()
			//   Returns the DateTime's millisecond.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					return Convert.ToDouble(pdt.dt.Millisecond);
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.NUMBER, new ArgList { }, eval, false, ourType);
				classDef.AddMemberLiteral("GetMillisecond", newValue.valType, newValue, false);
			}

			//@ DateTime AddYears(num years)
			//   Returns a new DateTime that is 'years' (rounded to nearest integer) years ahead of this DateTime. Can be negative.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					double d = (double) args[0];
					PebDateTime newpdt = classDef.Allocate(context) as PebDateTime;
					newpdt.dt = pdt.dt.AddYears(Convert.ToInt32(d));
					return newpdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("AddYears", newValue.valType, newValue, false);
			}

			//@ DateTime AddMonths(num months)
			//   Returns a new DateTime that is 'months' (rounded to nearest integer) months ahead of this DateTime. Can be negative.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					double d = (double)args[0];
					PebDateTime newpdt = classDef.Allocate(context) as PebDateTime;
					newpdt.dt = pdt.dt.AddMonths(Convert.ToInt32(d));
					return newpdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("AddMonths", newValue.valType, newValue, false);
			}

			//@ DateTime AddDays(num days)
			//   Returns a new DateTime that is 'days' (rounded to nearest integer) days ahead of this DateTime. Can be negative.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					double d = (double)args[0];
					PebDateTime newpdt = classDef.Allocate(context) as PebDateTime;
					newpdt.dt = pdt.dt.AddDays(Convert.ToInt32(d));
					return newpdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("AddDays", newValue.valType, newValue, false);
			}

			//@ DateTime AddHours(num hours)
			//   Returns a new DateTime that is 'hours' (rounded to nearest integer) hours ahead of this DateTime. Can be negative.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					double d = (double)args[0];
					PebDateTime newpdt = classDef.Allocate(context) as PebDateTime;
					newpdt.dt = pdt.dt.AddHours(Convert.ToInt32(d));
					return newpdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("AddHours", newValue.valType, newValue, false);
			}

			//@ DateTime AddMinutes(num minutes)
			//   Returns a new DateTime that is 'minutes' (rounded to nearest integer) minutes ahead of this DateTime. Can be negative.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					double d = (double)args[0];
					PebDateTime newpdt = classDef.Allocate(context) as PebDateTime;
					newpdt.dt = pdt.dt.AddMinutes(Convert.ToInt32(d));
					return newpdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("AddMinutes", newValue.valType, newValue, false);
			}

			//@ DateTime AddSeconds(num seconds)
			//   Returns a new DateTime that is 'seconds' (rounded to nearest integer) seconds ahead of this DateTime. Can be negative.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					double d = (double)args[0];
					PebDateTime newpdt = classDef.Allocate(context) as PebDateTime;
					newpdt.dt = pdt.dt.AddSeconds(Convert.ToInt32(d));
					return newpdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("AddSeconds", newValue.valType, newValue, false);
			}

			//@ DateTime AddMilliseconds(num ms)
			//   Returns a new DateTime that is 'ms' (rounded to nearest integer) milliseconds ahead of this DateTime. Can be negative.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebDateTime pdt = thisScope as PebDateTime;
					double d = (double)args[0];
					PebDateTime newpdt = classDef.Allocate(context) as PebDateTime;
					newpdt.dt = pdt.dt.AddMilliseconds(Convert.ToInt32(d));
					return newpdt;
				};

				FunctionValue newValue = new FunctionValue_Host(ourType, new ArgList { IntrinsicTypeDefs.NUMBER }, eval, false, ourType);
				classDef.AddMemberLiteral("AddMilliseconds", newValue.valType, newValue, false);
			}

			classDef.FinalizeClass(engine.defaultContext);
		}

		public static bool RunTests(Engine engine, bool verbose) {

			bool result = true;

			engine.Log("\n*** DateTimeLib: Running tests...");

			// Create
			result &= engine.RunTest("{ global DateTime dttest = DateTime::Create(2000, 3, 15, 12, 30, 45, 0); true; }", true, false);

			// Clone (and Get___s)
			result &= engine.RunTest("{ DateTime clone = dttest.Clone(); dttest.GetYear() == clone.GetYear() && dttest.GetMonth() == clone.GetMonth() && dttest.GetDay() == clone.GetDay() && dttest.GetHour() == clone.GetHour() && dttest.GetMinute() == clone.GetMinute() && dttest.GetSecond() == clone.GetSecond() && dttest.GetMillisecond() == clone.GetMillisecond() && dttest != clone; }", true, verbose);
			// Equals
			result &= engine.RunTest("{ DateTime dt = dttest.Clone(); dt.Equals(dttest) && dt != dttest; }", true, verbose);
			// ::GetNow
			result &= engine.RunTest("{ DateTime now = DateTime::GetNow(); null != now; }", true, verbose);
			// IsDateSame (and AddHours)
			result &= engine.RunTest("{ DateTime dt = dttest.AddHours(-4); DateTime dt2 = dttest.AddHours(24); dt.IsDateSame(dttest) && dttest.IsDateSame(dt) && !dt2.IsDateSame(dttest); }", true, verbose);
			// ToString

			bool sav = engine.logCompileErrors;
			engine.logCompileErrors = false;

			result &= engine.RunCompileFailTest("class DateTimeSealed : DateTime;", ParseErrorType.ClassParentSealed, verbose);

			//tested ok: result &= engine.RunRuntimeFailTest("DateTime::Create(2000, 0, 1, 1, 1, 1, 1);", RuntimeErrorType.ArgumentInvalid, verbose);
			result &= engine.RunRuntimeFailTest("dttest.Equals(null);", RuntimeErrorType.NullAccessViolation, verbose);
			result &= engine.RunRuntimeFailTest("dttest.IsDateSame(null);", RuntimeErrorType.NullAccessViolation, verbose);

			engine.logCompileErrors = sav;
			engine.Log("*** DateTimeLib: Tests " + (result ? "succeeded" : "FAILED"));

			return result;
		}
	}
}