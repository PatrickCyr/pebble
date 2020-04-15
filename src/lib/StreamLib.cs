/*
The Stream class.
See Copyright Notice in LICENSE.TXT

This library is automatically registered (see CoreLib.cs). Programs don't need to register it manually.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

	public class StreamLib {

		public class PebbleStreamHelper : ClassValue {
			private BinaryWriter writer;
			private TextWriter textWriter;
			private BinaryReader reader;

			public string lastError;

			public bool IsOpen() {
				return null != reader || null != writer || null != textWriter;
			}

			public bool IsReading() {
				return null != reader;
			}

			public bool IsWriting() {
				return null != writer || null != textWriter;
			}

			public bool OpenFileRead(ExecContext context, string path) {
				if (IsOpen()) {
					lastError = "Stream already open.";
					return false;
				}

				try {
					reader = new BinaryReader(File.Open(path, FileMode.Open));
				} catch (Exception e) {
					lastError = "Runtime error attempting to create BinaryReader: " + e.ToString();
					return false;
				}

				return true;
			}

			public bool OpenFileWrite(ExecContext context, string path, bool textMode) {
				if (IsOpen()) {
					lastError = "Stream already open.";
					return false;
				}

				try {
					if (textMode)
						textWriter = new StreamWriter(path);
					else
						writer = new BinaryWriter(File.Open(path, FileMode.Create));
				} catch (Exception e) {
					lastError = "Runtime error attempting to create BinaryWriter: " + e.ToString();
					return false;
				}

				return true;
			}

			public bool Write(ExecContext context, PebbleStreamHelper stream, object value) {
				Pb.Assert(!(value is Variable));

				if (null != textWriter) {
					string s = CoreLib.ValueToString(context, value, false);
					textWriter.Write(s);
					return true;
				}

				if (null == value) {
					writer.Write("null");
				} else if (value is FunctionValue) {
					context.SetRuntimeError(RuntimeErrorType.SerializeUnknownType, "Cannot serialize functions.");
					return false;
				} else if (value is bool) {
					writer.Write((bool)value);
				} else if (value is double) {
					writer.Write((double)value);
				} else if (value is string) {
					writer.Write((string)value);
				} else if (value is PebbleList) {
					PebbleList plist = value as PebbleList;
					// - Serialize full type, ie "List<string>".
					writer.Write(plist.classDef.name);
					// - Serialize count.
					writer.Write(plist.list.Count);
					// - Finally, serialize each object.
					foreach (Variable listvar in plist.list) {
						if (!Write(context, stream, listvar.value))
							return false;
					}
				} else if (value is PebbleDictionary) {
					PebbleDictionary dic = value as PebbleDictionary;
					// - class name
					writer.Write(dic.classDef.name);
					// - count
					writer.Write((Int32)dic.dictionary.Count);
					// - each key, value
					foreach (var kvp in dic.dictionary) {
						if (!Write(context, stream, kvp.Key))
							return false;
						if (!Write(context, stream, kvp.Value.value))
							return false;
					}
				} else if (value is ClassValue_Enum) {
					ClassValue_Enum enumVal = value as ClassValue_Enum;
					writer.Write(enumVal.classDef.name);
					writer.Write(enumVal.GetName());
				} else if (value is ClassValue) {
					ClassValue classVal = value as ClassValue;
					MemberRef serMemRef = classVal.classDef.GetMemberRef("Serialize", ClassDef.SEARCH.NORMAL);
					if (serMemRef.isInvalid) {
						context.SetRuntimeError(RuntimeErrorType.SerializeInvalidClass, "Class '" + classVal.classDef.name + "' cannot be serialized because it doesn't implement a serialization function.");
						return false;
					}

					writer.Write(classVal.classDef.name);

					Variable serVar = classVal.Get(serMemRef);
					FunctionValue serFunc = serVar.value as FunctionValue;
					object result = serFunc.Evaluate(context, new List<object> { stream }, classVal);
					if (context.IsRuntimeErrorSet())
						return false;
					if (result is bool && false == (bool)result) {
						context.SetRuntimeError(RuntimeErrorType.SerializeFailed, "Serialize function of class '" + classVal.classDef.name + "' returned false.");
						return false;
					}
				} else {
					throw new Exception("Internal error: Unexpected type of value in stream Write.");
				}

				return true;
			}

			public bool Read(ExecContext context, PebbleStreamHelper stream, Variable variable) {
				if (variable.type.IsConst()) {
					context.SetRuntimeError(RuntimeErrorType.SerializeIntoConst, "Cannot serialize into const variables.");
					return false;
				}

				if (variable.type.CanStoreValue(context, IntrinsicTypeDefs.BOOL)) {
					variable.value = reader.ReadBoolean();
				} else if (variable.type == IntrinsicTypeDefs.NUMBER) {
					variable.value = reader.ReadDouble();
				} else if (variable.type == IntrinsicTypeDefs.STRING) {
					variable.value = reader.ReadString();
				} else if (variable.type.GetName().StartsWith("List<")) {
					string listTypeName = reader.ReadString();
					if ("null" == listTypeName) {
						variable.value = null;
						return true;
					}

					// Is it possible that the specific generic class isn't registered yet.
					if (!EnsureGenericIsRegistered(context, listTypeName))
						return false;

					ClassDef listDef = context.GetClass(listTypeName);
					if (null == listDef) {
						context.SetRuntimeError(RuntimeErrorType.SerializeUnknownType, "Cannot deserialize list type '" + listTypeName + "' because it is unknown.");
						return false;
					}

					ClassValue listValue = listDef.Allocate(context);
					PebbleList newlist = listValue as PebbleList;
					variable.value = listValue;

					ITypeDef elementType = listDef.typeDef.genericTypes[0];

					int count = reader.ReadInt32();
					for (int ii = 0; ii < count; ++ii) {
						Variable newelem = new Variable(null, elementType);
						if (!Read(context, stream, newelem))
							return false;
						newlist.list.Add(newelem);
					}
				} else if (variable.type.GetName().StartsWith("Dictionary<")) {
					string listTypeName = reader.ReadString();
					if ("null" == listTypeName) {
						variable.value = null;
						return true;
					}

					// Is it possible that the specific generic class isn't registered yet.
					if (!EnsureGenericIsRegistered(context, listTypeName))
						return false;

					ClassDef listDef = context.GetClass(listTypeName);
					if (null == listDef) {
						context.SetRuntimeError(RuntimeErrorType.SerializeUnknownType, "Cannot deserialize list type '" + listTypeName + "' because it is unknown.");
						return false;
					}

					ClassValue listValue = listDef.Allocate(context);
					PebbleDictionary newlist = listValue as PebbleDictionary;
					variable.value = listValue;

					ITypeDef keyType = listDef.typeDef.genericTypes[0];
					ITypeDef valueType = listDef.typeDef.genericTypes[1];

					int count = reader.ReadInt32();
					Variable tempKeyVar = new Variable("tempKeyVar", keyType);
					for (int ii = 0; ii < count; ++ii) {
						if (!Read(context, stream, tempKeyVar))
							return false;

						Variable newelem = new Variable(null, valueType);
						if (!Read(context, stream, newelem))
							return false;
						newlist.dictionary.Add(tempKeyVar.value, newelem);
					}
				} else if (variable.type is TypeDef_Enum) {
					string enumName = reader.ReadString();
					string valueName = reader.ReadString();

					// This happens.
					ITypeDef streamedType = context.GetTypeByName(enumName);
					if (null == streamedType) {
						context.SetRuntimeError(RuntimeErrorType.SerializeUnknownType, "Attempt to load saved enum of unknown type '" + enumName + "'.");
						return false;
					}

					// I can't get this to happen.
					if (!(streamedType is TypeDef_Enum)) {
						context.SetRuntimeError(RuntimeErrorType.SerializeUnknownType, "Type '" + enumName + "' saved as something other than an enum, but attempted to stream into an enum variable.");
						return false;
					}

					ClassDef enumClassDef = context.GetClass(enumName);
					Pb.Assert(null != enumClassDef, "Somehow we got a type for an enum but not the def.");
					ClassDef_Enum enumDef = enumClassDef as ClassDef_Enum;
					Pb.Assert(null != enumClassDef, "Registered type is enum but def is classdef.");

					// This happens.
					ClassValue_Enum cve = enumDef.enumDef.GetValue(valueName);
					if (null == cve) {
						context.SetRuntimeError(RuntimeErrorType.EnumValueNotFound, "Enum '" + enumName + "' does not have saved value '" + valueName + "'.");
						return false;
					}

					variable.value = cve;

				} else if (variable.type is TypeDef_Class) {
					TypeDef_Class varType = variable.type as TypeDef_Class;

					// Get class name.
					string streamClassName = reader.ReadString();

					if ("null" == streamClassName) {
						variable.value = null;
						return true;
					}

					ITypeDef streamedType = context.GetTypeByName(streamClassName);
					if (null == streamedType) {
						context.SetRuntimeError(RuntimeErrorType.SerializeUnknownType, "Serialized type '" + streamClassName + "' not found.");
						return false;
					}

					if (!varType.CanStoreValue(context, streamedType)) {
						context.SetRuntimeError(RuntimeErrorType.SerializeTypeMismatch, "Cannot deserialize a '" + streamClassName + "' into a variable of type '" + varType.GetName() + "'.");
						return false;
					}

					TypeDef_Class streamedClassType = streamedType as TypeDef_Class;
					Pb.Assert(null != streamedClassType, "Somehow a streamed type is not a class but *can* be stored in a class type?!");

					ClassDef streamedClassDef = context.GetClass(streamClassName);
					Pb.Assert(null != streamedClassDef, "Somehow we got a type for a class but not the def.");
					MemberRef serMemRef = streamedClassDef.GetMemberRef("Serialize", ClassDef.SEARCH.NORMAL);
					if (serMemRef.isInvalid) {
						context.SetRuntimeError(RuntimeErrorType.SerializeInvalidClass, "Serialize function of class '" + streamClassName + "' not found.");
						return false;
					}

					ClassValue streamedClassValue = streamedClassDef.Allocate(context);
					Variable serFuncVar = streamedClassValue.Get(serMemRef);
					FunctionValue serFuncVal = serFuncVar.value as FunctionValue;
					serFuncVal.Evaluate(context, new List<object>() { stream }, streamedClassValue);
					if (context.IsRuntimeErrorSet())
						return false;

					variable.value = streamedClassValue;
				} else {
					throw new Exception("Internal error: Unexpected type of value in stream Read.");
				}

				return true;
			}

			public bool Close() {
				if (null != reader) {
					reader.Close();
					reader = null;
					return true;
				}
				if (null != writer) {
					writer.Close();
					writer = null;
					return true;
				}
				if (null != textWriter) {
					textWriter.Flush();
					textWriter.Close();
					return true;
				}

				return false;
			}

			// List<Child> could be written to a binary file, but then List<Parent> read. 
			// If there is no variable with type List<Parent> in the reading script, it
			// wouldn't be in the type registry. 
			// This function insures it's in the registry by parsing a little script which
			// creates a temp variable of the desired type. 
			// It's pretty inefficient to do this, but parsing type names can be very, very
			// tricky. It wouldn't necessarily be a *better* solution to try to replicate
			// the parser's code.
			protected bool EnsureGenericIsRegistered(ExecContext context, string name) {
				if (null != context.GetClass(name))
					return true;

				List<ParseErrorInst> errors = new List<ParseErrorInst>();
				// I don't think there is a way to use a variable name that is *guaranteed* not to already exist, sadly.
				IExpr expr = context.engine.Parse("{ " + name + " otehunstoeunthnsjthntxheui; }", ref errors, false);

				return null != expr && 0 == errors.Count;
			}
		}

		// Note: The main reason there is no OpenReadText is there is no easy way to query for EOF.
		// It might be nice to not have to read large text files all at once, ie. via File::ReadLines, 
		// but OTOH if you are reading files that are THAT big Pebble may not be best tool for
		// the job. If there is enough demand for it can add it later.

		public static void Register(Engine engine) {

			//@ class Stream
			//   Note that it is intentional that there is no OpenReadText method. It's easier to use 
			//   the File library for reading text.
			TypeDef_Class ourType = TypeFactory.GetTypeDef_Class("Stream", null, false);
			ClassDef classDef = engine.defaultContext.CreateClass("Stream", ourType, null, null, false);
			classDef.childAllocator = () => {
				return new PebbleStreamHelper();
			};
			classDef.Initialize();

			//@ bool Close()
			//   Closes the stream.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebbleStreamHelper helper = thisScope as PebbleStreamHelper;
					return helper.Close();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("Close", newValue.valType, newValue, false);
			}

			//@ bool IsReading()
			//   Return true iff the stream is open in read mode.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebbleStreamHelper helper = thisScope as PebbleStreamHelper;
					return helper.IsReading();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("IsReading", newValue.valType, newValue, false);
			}

			//@ bool IsWriting()
			//   Returns true iff the stream is open in write mode.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebbleStreamHelper helper = thisScope as PebbleStreamHelper;
					return helper.IsWriting();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("IsWriting", newValue.valType, newValue, false);
			}

			//@ bool IsOpen()
			//   Returs true iff the stream is open in either read or write mode.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					PebbleStreamHelper helper = thisScope as PebbleStreamHelper;
					return helper.IsOpen();
				};

				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { }, eval, false);
				classDef.AddMemberLiteral("IsOpen", newValue.valType, newValue, false);
			}

			//@ bool OpenReadBinary(string filepath)
			//   Opens the specified file for reading in binary mode.
			//   Returns false if there is an error.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string path = args[0] as string;

					PebbleStreamHelper helper = thisScope as PebbleStreamHelper;
					return helper.OpenFileRead(context, path);
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING }, eval, true);
				classDef.AddMemberLiteral("OpenReadBinary", newValue.valType, newValue, false);
			}

			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string path = args[0] as string;

					PebbleStreamHelper helper = thisScope as PebbleStreamHelper;
					return helper.OpenFileWrite(context, path, false);
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING }, eval, true);
				classDef.AddMemberLiteral("OpenWriteBinary", newValue.valType, newValue, false);
			}

			//@ bool OpenWriteText(string filepath)
			//   Opens the specified file for writing in text mode.
			//   Returns false if there is an error.
			{
				FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
					string path = args[0] as string;

					PebbleStreamHelper helper = thisScope as PebbleStreamHelper;
					return helper.OpenFileWrite(context, path, true);
				};
				FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.BOOL, new ArgList { IntrinsicTypeDefs.STRING }, eval, true);
				classDef.AddMemberLiteral("OpenWriteText", newValue.valType, newValue, false);
			}

			classDef.FinalizeClass(engine.defaultContext);

			UnitTests.testFuncDelegates.Add("StreamLib", RunTests);
		}

		public static bool RunTests(Engine engine, bool verbose) {
			bool result = true;
			engine.Log("\n*** StreamLib: Running tests...");

			result &= engine.RunTest("{ Stream badFileStream = new; bool result = badFileStream.OpenWriteBinary(\"bad/filename\"); !result && !badFileStream.IsOpen(); }", true, verbose);

			bool createResult = engine.RunTest("{ global Stream gstream = new; gstream.OpenWriteBinary(\"StreamLibTest.bin\"); gstream.IsWriting(); }", true, verbose);
			if (!createResult) {
				engine.LogError("Error creating test stream, cannot run all StreamLib tests.");
				return false;
			}

			// Note: I'd like to test EnsureGenericIsRegistered but it requires two runs to test properly, so can't really be 
			// done here.

			bool sav = engine.logCompileErrors;
			engine.logCompileErrors = false;

			result &= engine.RunCompileFailTest("Stream << 4;", ParseErrorType.SyntaxError, verbose);
			result &= engine.RunCompileFailTest("{ num nooo; 4 << nooo; }", ParseErrorType.StreamOnly, verbose);
			result &= engine.RunCompileFailTest("{ num nooo; A anotastream; anotastream << nooo; }", ParseErrorType.StreamOnly, verbose);
			result &= engine.RunCompileFailTest("{ functype<num(num)> func = Math::Floor; gstream << func; }", ParseErrorType.StreamFunction, verbose);
			result &= engine.RunCompileFailTest("{ functype<num(num)> func = null; gstream << func; }", ParseErrorType.StreamFunction, verbose);
			result &= engine.RunCompileFailTest("{ class BadSer { num Serialize(Stream s) { 0; } }; BadSer bs = new; gstream << bs; }", ParseErrorType.StandardFunctionMismatch, verbose);
			result &= engine.RunCompileFailTest("{ class BadSer2 { bool Serialize() { true; } }; BadSer2 bs = new; gstream << bs; }", ParseErrorType.StandardFunctionMismatch, verbose);
			result &= engine.RunCompileFailTest("{ class BadSer3 { bool Serialize(Stream s, num n) { true; } }; BadSer3 bs = new; gstream << bs; }", ParseErrorType.StandardFunctionMismatch, verbose);

			result &= engine.RunRuntimeFailTest("{ class NoSer; List<NoSer> lns = new { Add(new NoSer); }; gstream << lns; }", RuntimeErrorType.SerializeInvalidClass, verbose);
			result &= engine.RunRuntimeFailTest("{ NoSer ns = new; gstream << ns; }", RuntimeErrorType.SerializeInvalidClass, verbose);
			result &= engine.RunRuntimeFailTest("{ num nooo; Stream nullstream; nullstream << nooo; }", RuntimeErrorType.NullAccessViolation, verbose);
			result &= engine.RunRuntimeFailTest("{ class SerFailed { bool Serialize(Stream stream) { false; } }; SerFailed sf = new; gstream << sf; }", RuntimeErrorType.SerializeFailed, verbose);

			result &= engine.RunRuntimeFailTest("{ Stream closedStream = new; closedStream << 4; }", RuntimeErrorType.SerializeStreamNotOpen, verbose);
			// Note: this test changes the test stream to read.
			result &= engine.RunRuntimeFailTest("{ gstream.Close(); gstream.OpenReadBinary(\"StreamLibTest.bin\"); gstream << 4; }", RuntimeErrorType.SerializeReadRequiresLValue, verbose);
			result &= engine.RunRuntimeFailTest("{ const num cn = 10; gstream << cn; }", RuntimeErrorType.SerializeIntoConst, verbose);

			// Clean up.
			result &= engine.RunTest("{ gstream.Close(); File::Delete(\"StreamLibTest.bin\"); }", true, verbose);

			engine.logCompileErrors = sav;
			engine.Log("*** StreamLib: Tests " + (result ? "succeeded" : "FAILED"));
			return result;
		}
	}
}