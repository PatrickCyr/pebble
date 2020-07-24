/*
A bunch of general unit tests for Pebble. Library specific tests are usually in the library files themselves.
See Copyright Notice in LICENSE.TXT
*/

using System.IO;
using System.Collections.Generic;

namespace Pebble {	

	public class UnitTests {

		public delegate bool TestFuncDelegate(Engine engine, bool verbose);

		public static Dictionary<string, TestFuncDelegate> testFuncDelegates = new Dictionary<string, TestFuncDelegate>();

		public static void RunTests(Engine engine, bool verbose) {
			Dictionary<string, object> evaluationTests = new Dictionary<string, object> {
				// Classes used by a variety of tests.
				{"{ class A { string name = \"A\"; }; class B : A { constructor { name = \"B\"; } }; class C : A; }", null},
				{"class M;", false},
				{@"class N { num x = 7; num y = 4; string s = ""Hello!""; bool b = true; const num c = 7; };", false},
				{@"class O : N { bool eqFunc(num a1, num b1) { a1 == b1; } num square() { x * y; } };", false},
				{"{ class P { num x = 5; }; }", false},
				{"N n = new N;", null},
				{"O o = new O;", null},

				// *** Type conversion
				{"ToBool(0);", false},
				{"ToBool(1);", true},
				{"ToBool(null);", false},
				{"ToBool(\"false\");", false},
				{"ToBool(\"true\");", true},
				{"ToBool(n);", true},

				{@"ToString(""Hello"");", "Hello"},
				{"ToString(1);", "1"},
				{"ToString(-0.7);", "-0.7"},
				{"ToString(true);", "true"},
				{"ToString(false);", "false"},
				{"ToString(null);", "null"},
				//{"ToString(n);", "Pebble.ClassValue"},
				{"ToString(true,1.5,\"hi\");", "true1.5hi"},

				{"ToNum(-3.14);", -3.14},
				{"ToNum(true);", 1},
				{"ToNum(false);", 0},
				{@"ToNum(""-3.14"");", -3.14},
				{"ToNum(null);", 0},
				{"ToNum(new List<num>);", 1},

				// *** Null
				// - Assignment
				{"N nullInstance = null;", null},
				// - Comparison
				{"null == null;", true},
				{"nullInstance == null;", true},
				{"null == nullInstance;", true},
				{"{ nullInstance = new N; null == n; }", false},

				// Global
				{"{ { global num glob_x = 3; } glob_x; } glob_x; ", 3},

				// -- Scopeless expression list.
				{" num scopeless_x; string scopeless_str; bool scopeless_bBool; ", false},

				{"class ZZ { const num c = 5; };", null},

				// Assign
				{"{ num x; num y; x = y = 3; y == 3 && x == 3; }", true},

				// Conditional
				// -- Groups as true ? (true ? 1 : 0) : 2
				{"true ? true ? 1 : 0 : 2;", 1},
				{"true ? false ? 1 : 0 : 2;", 0},
				{"false ? true ? 1 : 0 : 2;", 2},
				// -- Groups as true ? (true ? 1 : 0) : (true ? 2 : 3);
				{"true ? true ? 1 : 0 : true ? 2 : 3;", 1},
				{"false ? true ? 1 : 0 : true ? 2 : 3;", 2},
				{"false ? true ? 1 : 0 : false ? 2 : 3;", 3},
				// -- The type of a conditional expression is most common type of types of the true and false expressions.
				{"{ B bChildOfA = new B; C cChildOfA = new C; A aIsCommonAncestorOfAAndB = true ? bChildOfA : cChildOfA; aIsCommonAncestorOfAAndB.name; }", "B"},

				// ||
				{"true || true || false;", true},
				{"false || true || false;", true},
				{"false || false || true;", true},
				// Don't evaluate right expression if left is already true.
				{"true || (0 == 3/0);", true},

				// &&
				{"false && true && true;", false},
				{"true && false && true;", false},
				{"true && true && false;", false},
				// Don't evaluate right expression if left is already true.
				{"false && (0 == 3/0);", false},

				// && has precedence over ||
				{"false || true && false;", false},
				{"true || true && false;", true},
				{"false || true && true;", true},
				{"false || false && true;", false},
				{"true && false || false;", false},
				{"true && false || true;", true},

				// Logical operators have preedence over conditional.
				{"false || true ? 1 : 2;", 1},
				{"false ? false : true || false;", true},

				// Equality
				{"true == false == true;", false},
				{"true == true == false;", false},
				{"true == true == true;", true},

				// == has precedence over logical || and &&.
				{"true && false == false && true;", true},
				{"false && false == true && true;", false},
				
				// ..
				{"\"a\"..\"b\";", "ab"},
				{"{string a = \"a\"; a..a..a..\"b\"..a;}", "aaaba"},

				// Const
				{"{ const functype<bool()> constf = null; true; }", true},
				{"{ const num con = 3; con; }", 3},
				{"{ const num constNum = 3; }", 3},
				//-- const has no impact on comparison
				{"{ const num constNum = 3; num nonConst = 3; constNum == nonConst && nonConst == constNum; }", true},


				// More detailed scope tests
				// -- symbols don't persist outside of their blocks
				{"{ { num someVar; } { num someVar; }; true; }", true},
				// -- A variety of scopes.
				{@"{ 
					string xs; 
					class D { 
						num d(num xs) { 
							-xs; 
						}
					}; 
					D d = new D; 
					d.d(3); 
				}", -3},
				// -- block scope shadows
				//{"{ bool bbb = true; { bool bbb = false; }; bbb; }", true},
				// --for block has it's own scope with access to higher ones.
				{@"{ 
					bool bb = true; 
					for (i = 1, 2) { 
						bool zzz = bb; 
					}; 
					true;
				}", true},
				// -- block scope terminates
				{@"{ 
					{ bool bbb = true; } 
					bool bbb = true; 
				}", true},

				// Literals.
				{"true;", true},
				{"false;", false},
				{"42.8;", 42.8},
				// -- every escape sequence
				{"\"\\t\\n\\\"\\\\Hello, world!\";", "\t\n\"\\Hello, world!"},
				// Compound statements
				{"{ true; 19; }", 19},

				// Numeric literals.
				{"3;", 3},
				{"3.14;", 3.14},
				{".14;", .14},
				{"3E+10;", 3E+10},
				{"3E-10;", 3E-10},
				{"3.14E+10;", 3.14E+10},
				{"3.14E-10;", 3.14E-10}, 

				// Nested statements
				{"{ true; { 3.1415; } }", 3.1415},

				// Var decl
				{"num x;", 0},
				{"num y = 10;", 10},
				{"bool tf = true;", true},

				// Symbol lookup.
				{"y;", 10},

				// Assignment.
				{"y = 32;", 32},

				// Unops
				{"!true;", false},
				{"!false;", true},
				{"-4;", -4},
				{"-y;", -32},
				{"++y;", 33},
				{"--y;", 32},

				// Binops
				{"3 + 4;", 7},
				{"3 - 4;", -1},
				{"3 * 4;", 12},
				{"y / 8;", 4},
				{"24 % 5;", 4},
				{"3 < 4;", true},
				{"3 > 4;", false},
				{"4 <= 4;", true},
				{"5 <= 4;", false},
				{"4 >= 4;", true},
				{"5 >= 4;", true},
				{"num mynum = 0;", 0},
				{"mynum += 5;", 5},
				{"mynum -= 3;", 2},
				{"mynum *= 3;", 6},
				{"mynum /= 2;", 3},
				{"{ string mystr = \"h\"; mystr ..= \"i\"; mystr; }", "hi"},

				// Logical ops
				{"true && false;", false},
				{"true || false;", true},
				//{"true ? 10 : 5;", 10},
				{"false ? 10 : 5;", 5},

				// Equality ops
				{"true == false;", false},
				{"false == false;", true},
				{"4 == 4.0;", true},
				{"4 == 4.1;", false},
				{"\"hi\" == \"hi\";", true},
				{"\"HI\" == \"hi\";", false},
				{"true != false;", true},
				{"\"hi\" ~= \"hi\";", true},
				{"\"HI\" ~= \"hi\";", true},
				{"\"HI\" ~= \"h\";", false},

				// Operator precedence.
				{"3 + 4 * 5;", 23},
				{"3 * 4 + 5;", 17},
				// Need tons more here.
				{"(3 + 4) * 5;", 35},
				{"3 * (4 - 5);", -3},

				// Decreasing precedence:
				// 

				// Function calls
				// - varargs
				{"String::Concat(\"He\", \"llo\", \" \", \"World!\");", "Hello World!"},
				{"ToString(\"42 = \", 42);", "42 = 42"},
				// -- no shadowing in args
				{"{ num somevar = 2; num fun(num somevar) { somevar = 3; } fun(5); somevar; }", 2},
				{"{ num somevar = 2; num fun() { num somevar = 3; } fun(); somevar; }", 2},
				// -- optional arguments
				{"{ num OF(num x, num y = 10) { x + y; } (11 == OF(1) && 21 == OF(1, 20)); }", true},
				{"{ num OF2(num x = 1, num y = 10) { x + y; } (11 == OF2() && 20 == OF2(10) && 21 == OF2(1, 20)); }", true},
				{"{ num OF3(num x = 1) { x * 2; } (2 == OF3() && 20 == OF3(10)); }", true},
				// -- default value in function making it fit into variable with fewer arguments
				{"{	functype<num()> fnn; num FNN(num x = 4) { x * 2; } fnn = FNN; fnn(); }", 8},
				{"{	functype<num(num?)> fnn; num FNN(num x = 4) { x * 2; } fnn = FNN; fnn(); }", 8},
				// * class default values can only be null because null is the only literal class value
				{"{ bool OF4(List<num> ln = null) { ln == null; } OF4(); }", true},
				// -- argument promotion: bFunc can only be called with Bs, which are always As
				{"{ functype<string(B)> bFunc; string AFunc(A a) { a.name; } bFunc = AFunc; bFunc(new B); }", "B"},
				// -- function arguments don't collide with globals
				{"{ global num argGlobal = 2; num IDontCollide(num argGlobal) { argGlobal * ::argGlobal; } IDontCollide(3); }", 6},
				// -- function variable default value is null
				{"{ functype<void()> nullfunc; nullfunc == null; }", true},
				// -- function variables with var args: this is ok fbb will always be guaranteed to be called with an argument
				{"{ functype<bool(bool)> fbb; functype < bool(bool ?) > fbbo; fbb = fbbo; true; }", true},

				

				// Return
				{"{ num poop() { return 99; 42; } poop(); }", 99},
				{"{ num poop() { for (nnn=1,2) { return 99; } 42; } poop(); }", 99},
				{"{ num poop() { for (nnn=1,2) { if (nnn == 3) return 99; } 42; } poop(); }", 42},				
				// -- Make sure return can be used in unary form (ie an if statement with no block).
				{"{ void poop() { if (true) return; true; } true; }", true},
				{"{ void poop() { for (i = 0, 1) return; true; } true; }", true},
				{"class VoidClass { void VF() { true; } };", false},


				// If
				{"{ if (true) y = 1; y; }", 1},
				{"{ if (false) y = 1; else y = 2; y; }", 2},
				{"{ if (false) { y = 1; } else { y = 2; } y; }", 2},
				// -- else always chooses nearest if.
				{"{ bool ok; if (false) if (false) if (false) 0; else 0; else 0; else ok = true; ok; }", true},
				{"{ bool ok; if (true) if (false) if (false) 0; else 0; else ok = true; else 0; ok; }", true},
				{"{ bool ok; if (true) if (true) if (false) 0; else ok = true; else 0; else 0; ok; }", true},

				// For
				{"{ num z; for (xx = 1, 10) z = z + 10; z; }", 100},
				{"{ num count = 0; for (xx = 0, -9, -1) ++count; count; }", 10},
				// -- This tests that for scope resets each loop.
				{"{ num res = 0; for (xx = 1, 5) { num yy = 0; yy += 1; res += yy; }; res; }", 5},
				// -- break;
				{"{ num res; for (zzz = 1, 10) { res = zzz; if (zzz > 5) break; }; res; }", 6},
				// -- continue
				{"{ num res = 0; for (zzz = 1, 10) { if (zzz > 8) ++res; }; res; }", 2},
				// -- limits
				{"{ num count = 0; for (i = FORMAX-1, FORMAX) { ++count; } count; }", 2},
				{"{ num min = -FORMAX; num count = 0; for (i = min, min) { ++count; } count; }", 1},
				// --rounding
				{"{ num count = 0; for (i = 0.4, 0.6, 1.4) { ++count; } count; }", 2},
				{"{ num count = 0; for (i = -0.4, -9.1, -1.6) { ++count; } count; }", 5},

				// Function type
				{"{ functype<bool(string, string)> mystreqi = String::EqualsI; mystreqi(\"hi\", \"HI\"); }", true},
				{"{ num numFunc(num a, num b) { a + b; } numFunc(3, 7); }", 10},
				{"{ num pi() { 3.14; } pi(); }", 3.14},
				// Function call scope.
				// --- this is actually ok.  x is a global,
				//{"function num poop() { num x = 3; };", false},
				// -- function return type doesn't have to match exactly
				{"{ functype<A()> aFunc; B BFunc() { new B; } aFunc = BFunc; aFunc().name; }", "B"},


				// *** Lists
				
				{"List<num> ln;", null},
				{"ln = new List<num>;", null},
				{"null != ln;", true},
				{"ln != null;", true},
				{"ln.Count() == 0;", true},
				// Can no longer store references to class members. (Was surely borked anyway, as pebble doesn't have closures.)
				//{"{ functype<List<num>(num)> insertPtr = ln.Push; true; }", true},
				{"{ ln.Push(42); ln.Get(0); }", 42},

				{"ln.Push(43).Count();", 2},
				// This test is important, as it tests order of argument evaluation.
				// I've screwed it up before, so don't remove it.
				{"ln.Push(44).Get(ln.Count() - 1);", 44},
				{"ln.Get(0);", 42},
				{"ln.Get(1);", 43},
				{"ln.Get(2);", 44},

				// Make sure list instances are truly separate!
				{"{ List<num> ln2 = new List<num>; ln2.Count() == 0 && ln.Count() == 3; }", true},

				// Index operator.
				{"List<num> numList = new List<num>;", null},
				{"numList.Add(3); numList.Add(4);", null},
				{"numList[0] = 5;", 5},
				{"numList[0];", 5},
				{"numList[0.5];", 5},
				{"numList[0.50001];", 4},
				{"4 == numList[-1] && 5 == numList[-2];", true},
				{"{ List<N> nlist = new List<N>; N n1 = new N; nlist.Add(n1); nlist[0].x; }", 7},
				{"{ List<N> nlist = new List<N>; for (i = 0, 9) { N n1 = new N; n1.x = i; nlist.Add(n1); }; num sum = 0; for (i = 0, 9) { sum += nlist[i].x; }; sum; }", 45},

				{"ln[2];", 44},
				{"{ num test = ln[2]; ln[2] = 99; test == 44 && ln.Get(2) == 99; }", true},
				{"ln.RemoveAt(0).Get(0);", 43},
				{"ln.Set(0, 45).Get(0);", 45},
				{"ln.Clear().Count();", 0},
				{"true ? 10 : 5;", 10},

				// -- list sorting
				{@"{	num comparator(num a, num b) { a - b; }
						List<num> l = new List<num>; 
						l.Push(5.3); 
						l.Push(3.5); 
						l.Sort(comparator); 
						l[0] == 3.5 && l[1] == 5.3 && l.Count() == 2; }", true},

				// -- valid modifications in foreach
				{"{ foreach (k, v in ln) { ln.Count(); } true; }", true},
				{"{ foreach (k, v in ln) { ln[k]; } true; }", true}, // get
				{"{ foreach (k, v in ln) { ln[k] = k; } true; }", true}, // set
				{"{ num comparator(num a, num b) { a - b; } foreach (k, v in ln) { ln.Sort(comparator); } true; }", true},

				// -- foreach collection can be an expression, not just a symbol
				{"{ List<Dictionary<string, num>> ldsn = new List<Dictionary<string, num>>; ldsn.Add(new Dictionary<string, num>); num count; foreach(k, v in ldsn[0]) { ++count; }; count; }", 0},

				// -- foreach on enums
				{"{ enum<num> TestEnum { a, b }; string cat; foreach (ix, e in TestEnum) { cat = ToString(cat, ix, e.name); } cat; }", "0a1b"},

				// *** Dictionaries
				{"Dictionary<string, num> dict = new Dictionary<string, num>;", null},
				{"dict.Count();", 0},
				{@"{ dict.Add(""first"", 100); dict.Count(); }", 1},
				{"dict.Get(\"first\");", 100},
				{"{ 200 == dict.Set(\"first\", 200).Get(\"first\") && dict.Count() == 1; }", true},
				{"dict.Add(\"second\", 102).Count();", 2},
				// -- Indexing
				{"dict[\"second\"];", 102},
				// -- foreach
				{@"{ string result; foreach (k, v in dict) { result = String::Concat(result, k, ToString(v)); } result; }", "first200second102"},
				{@"dict.Remove(""first"").Count();", 1},
				{"dict.Add(\"poo\", 10).Clear().Count();", 0},
				// -- valid modifications in foreach
				{"{ foreach (k, v in dict) { dict.Count(); } true; }", true},
				{"{ foreach (k, v in dict) { dict.Get(k); } true; }", true},
				{"{ foreach (k, v in dict) { dict.Set(k, v); } true; }", true},
				// -- complex keys
				{"{ Dictionary<N, bool> dnb = new; O ooo = new; dnb.Add(ooo, true); dnb[ooo]; }", true},

				// foreach
				{"{ dict.Add(\"first\", 100); dict.Add(\"second\", 200); }", null},
				{"foreach (k, v in dict) { k; }", null},
				{"{ string firstKey; foreach (k, v in dict) { firstKey = k; break; } firstKey; }", "first"},
				{@"{ num secondVal; foreach (k, v in dict) { if (k == ""second"") { secondVal = v; continue; } } secondVal; }", 200},

				// foreach on lists
				{"{ List<num> lsn = new List<num>; lsn.Add(100); lsn.Add(200); num sum; foreach(i, v in lsn) { sum += i + v; } sum; }", 301},


				// *** Classes
				// -- can only be declared at the top level, within blocks under the top level, or in assert blocks.
				{"class TopLevel;", null}, // can't really test the value of this, sadly.
				{"{ class InBlock; true; }", true},
				{"{ assert( ScriptError::SymbolNotFound ) { class InAssert; oeuoeuaoeu; } true; }", true},
				// -- classes can reference (but not inherit from) classes further down in the file
				{"{ class FirstDeclared { SecondDeclared ref; void SetRef(SecondDeclared s) { ref = s; } }; class SecondDeclared; true; }", true},

				// -- make sure that initializers are inherited. 
				// To test this properly, both classes must be declared in the same script.
				{"{ class InitA { num x = 5; }; class InitB : InitA { }; InitB initB = new; initB.x; }", 5},
				// -- make sure constructors override initializers
				{"{ class InitA2 { num x = 5; }; class InitB2 : InitA2 { constructor{ x = 7;} }; InitB2 initB = new; initB.x; }", 7},

				// m is a reference to a scope of type M.
				//{"{scope<M> m; 77; }", 77},
				{"n.s;", "Hello!"},
				{"{ n.b; }", true},
				{"{ x = 2; y = 2; o.eqFunc(x, y); }", true},
				{"o.square();", 28},
				// Child members can reference parent fields.
				{"class OO : N { num ff() { x; } };", false},

				// Polymorphism.
				{"{N nButStoresO = o; nButStoresO.b;}", true},
				
				// Assignment to const class member.
				{"class Co { const num x = 3; num y = 5; };", null},
				{"Co coop = new Co;", null},
				{"coop.y = 7;", 7},
				{"coop.y;", 7},

				// Calls with class arguments.
				{"{ num carg(Co c) { c.y; } carg(coop); }", 7},

				// make sure classes don't bleed into each other
				{"class Aa { num x; num y = 1; bool f() {false;} };", null},
				{"class Ba : Aa { Aa a; };", null},
				{"Aa a = new Ba;", null},
				{"a.x == 0;", true},
				{"a.y == 1;", true},
				{"a = new Aa;", null},
				{"Ba b;", null},
				{"{ b = new Ba; true; }", true},
				{"{ b.y = 2; b.y == 2 && a.y == 1; }", true},
				{"{ a.y = 3; b.y == 2 && a.y == 3; }", true},
				{"a.f();", false},

				{"{ class ZZZ {num x;}; ZZZ zzz = new ZZZ; zzz.x = 7; zzz.x; }", 7},
				{"{ class YYY : ZZZ { constructor {x = 4;} }; YYY yyy = new YYY; yyy.x; }", 4},

				// class members don't conflict with globals, plus global scope syntax
				{"{ global num aGlobal = 1; class NoShadow { num aGlobal = 2; num F() { aGlobal + ::aGlobal; } }; NoShadow ns = new; ns.F(); }", 3},

				// this
				{"{ class This { num x = 3; static num y = 4; num z; This dis; num F() { this.x + This::y; } constructor { z = this.F(); dis = this; } }; This t = new; t.dis.z; }", 7},

				// Override.
				{"class Ca : Ba { override bool f() { true; } };", null},
				{"{ Aa aa = new Aa; Ba bb = new Ba; Ca cc = new Ca; false == aa.f() && false == bb.f() && true == cc.f(); }", true},

				// Initializers
				{"{ num localVar = 77; N nn = new N { x = localVar; }; nn.x; }", 77},
				{"{ global num gnum = 1000; ZZZ zzz = new ZZZ { x = gnum; }; zzz.x; }", 1000},

				// All the many ways you can use new.
				{"{ Ca c = new; Ca c2 = new Ca; Ba bb = new Ca; Ca c3 = new { x = 3; }; c3.x; }", 3},
				{"{ class NewInDef { List<num> ln = new; List<num> ln2 = new { Add(10); }; }; NewInDef nid = new; nid.ln2[0]; }", 10},

				// Different type of field initializers.
				{"{ class Variety { num literal = 3; bool funccall = true || false; List<num> newexpr = new List<num>; }; Variety v = new Variety; v.literal == 3 && v.funccall == true && v.newexpr.Count() == 0; }", true},

				// No collision between arguments of static functions and non-static fields.
				{"{ class ArgumentFieldNoShadowing2 { num x = 2; static num F(num x) { x; } }; ArgumentFieldNoShadowing2::F(3); }", 3},

				// Make sure member indexes are set properly in override functions.
				{@"{ class AOver { num f() { 1; } string g() { ""one""; } }; 
					 class BOver : AOver { override num f() { 2; } override string g() { ""two""; } }; 
					 BOver bOver = new BOver; assert(2 == bOver.f() && ""two"" == bOver.g()); }", true},

				// If members aren't properly told what class they are a part of, this will explode.
				{@"{ class AM { num f1() { f2(); } num f2() { f3(); } num f3() { 42; } }; AM am = new; am.f1(); }", 42},

				// class ThisToString member
				{"{ class TTS1 { num x; string ThisToString() { \"TTS1\"..ToString(x); } }; TTS1 tts1 = new { x = 42; }; ToString(tts1); }", "TTS142"},
				{"{ class TTS2 : TTS1 { override string ThisToString() { \"TTS2:\"..TTS1::ThisToString(); } }; TTS2 tts2 = new { x = 52; }; ToString(tts2); }", "TTS2:TTS152"},

				// Using class type within the class.
				{"{ class SelfRef { num x = 10; SelfRef ref; SelfRef F() { SelfRef result = new; result.ref = result; result; }}; SelfRef selfRef = new; selfRef.F().ref.x; }", 10},

				// new inferrence in declarations
				{"{ TTS1 tts1 = new; tts1.x; }", 0},
				{"{ TTS1 tts1 = new { x = 10; }; tts1.x; }", 10},
				// new inferrence in assignments
				{"{ TTS1 tts1; tts1 = new; tts1.x; }", 0},
				{"{ TTS1 tts1; tts1 = new { x = 10; }; tts1.x; }", 10},

				// member function variables are not class functions
				{ "{ class MF { functype<num()> fvar; }; MF mf = new; num somefunc() { 10; } mf.fvar = somefunc; functype<num()> fvar = mf.fvar; mf.fvar == fvar && 10 == mf.fvar(); }", true},


				// *** Static
				// Define class and regular static lookup.
				{"{ class StaticOK { num x = 1; static num sx = 10; num X() { x; } static num SX() { sx; } }; StaticOK::sx; }", 10},
				// static function call.
				{"StaticOK::SX();", 10},
				// Static usage within class functions.
				{@"{ class StaticOK2 { 
						num x = 1; 
						static num sx = 10; 
						num X() { 
							x + StaticOK::sx + StaticOK2::sx + StaticOK::SX() + StaticOK2::SX() + sx + SX();
						} 
						static num SX() { 
							sx + StaticOK::sx + StaticOK::SX(); }
						static num SX2() { 
							1000 + SX(); 
						} 
					}; 
					StaticOK2::SX2(); }", 1030},
				// -- Statics can be const.
				{"{ class StaticConst { static const num x = 2; const static num y = 3; }; StaticConst::x * StaticConst::y; }", 6},

				// *** Referencing base class implementations.
				{"{ class Par { num F() { 1; } }; class Chi : Par { override num F() { Par::F() * 2; } }; Chi chi = new; chi.F(); }", 2},

				// ***

				// Console call
				//{@"#print ""Hello!""", "Hello!"},

				// For a function-already-declared compile test.
				{"num numFunc(num a, num b) { a + b; }", null},

				// *** Stream
				// -- <- operator
				{"num streamNum <- \"2*3;\";", 6},
				{"streamNum <- \"3*5;\";", 15},
				{@"{ N newN <- ""new N;""; newN.x; }", 7},
				{"{ bool bSer <- ToScript(true); }", true},
				{"streamNum <- ToScript(11);", 11},
				// -- Result form
				{"{ ScriptResult<num> resultNum <- \"2+3;\"; 5 == resultNum.value && resultNum.error == ScriptError::NoError; }", true},
				// -- parse error handled
				{"{ ScriptResult<num> resultNum <- \"oeuonth;\"; resultNum.error != ScriptError::NoError; }", true},
				// -- runtime error handled
				{"{ ScriptResult<num> resultNum <- \"3/0;\"; !resultNum.IsSuccess(); }", true},
				// -- other forms
				{"{ ScriptResult<bool> resultBool; resultBool <- \"true;\"; resultBool.value; }", true},
				{"{ ScriptResult<bool> resultBool = new; resultBool <- \"true;\"; resultBool.value; }", true},

				// *** typealias
				{"{ typealias DSS = Dictionary<string, string>; DSS newdss = new DSS; newdss.Count(); }", 0},
				{"{ typealias NumFunc = functype<num(num,num)>; List<NumFunc> lnf = new List<NumFunc>; num fff(num a, num b) { a*b; }; lnf.Add(fff); lnf.Count(); }", 1},
				{"{ typealias ZZZalias = ZZZ; ZZZalias alias = new ZZZalias; true; }", true},
				{"typealias numalias = num;", null},
				// -- typealiases are global
				{"{ NumFunc aNumFunc; true; }", true},

				// *** Comments and empty expressions
				// These should return null but have no compile errors.
				{"", null},
				{";", null},
				{"// i am a comment", null},
				{"/* i am a comment */", null},

				// *** assert
				{"assert(true);", true},
				{"assert(true, \"User assert message.\");", true},
				{"assert(true, ToString(1 / 3));", true},
				// We had a parser error, so this tests that the parser picks up the immediate block after the condition instead of skipping one.
				{"{ assert(ScriptError::NumberInvalid) { 3 / 0;	} { 1; } }", 1},

				// *** void
				// just testing that it compiles. the actual return value should be undefined,
				// but in practice it's just whatever the function returns
				{"{ void VF() { true; }; VF(); }", null},
				{"{ void VF() { true; }; functype<void()> vf = VF; vf(); }", null},

				// *** length
				{"#\"hello\";", 5},
				{"{ string str = \"hi\"; #str + 3; }", 5},
				{"{ List<num> ln2 = new; #ln2; }", 0},
				{"{ List<num> ln3 = new {Add(10);}; #ln3; }", 1},
				{"{ Dictionary<string, num> dsn2 = new; #dsn2; }", 0},
				{"{ Dictionary<string, num> dsn3 = new {Add(\"hi\", 10);}; #dsn3; }", 1},
				{"{ List<string> ls2 = new { Add(\"h\").Add(\"ello\"); }; #ls2[1]; }", 4},
				{"{ string F() { \"hi\"; } #F(); }", 2},

				// *** ToString operator.
				{"{ List<bool> lbn; $lbn; }", "null"},
				{"$(2 + 3);", "5"},
				{"$(1 == 2);", "false"},
				{"$(new List<num>);", "List(0)[]"},

				// *** ++, --, $, and # are the only operators which can be chained (because they are prefixes)
				{"#$3;", 1}, // length of "3" is 1
				{"$#$3;", "1"},
				{"{ num inc = 10; $++inc; }", "11"},
				{"{ num inc = 10; #$++inc; }", 2},
				{"{ num inc = 10; \"10\" == $inc++ && 11 == inc; }", true},

				// *** Non-class functions should add the class name as the first variable in call scope.
				// There's lots of ways for this to go wrong, so these tests aim to make sure it works properly.
				// -- Normal function
				{"{ string F(bool go = true) { string result = \"F\"; if (go) return result..F(false); return result; } F(); }", "FF"},
				// -- Normal and static class functions
				{@"	{
						class FuncTest { 
							static string sx = ""S"";
							string x = ""N"";

							string  NormalFunc(bool go = true) {
								string result = x;
								if (go)
									return result..NormalFunc(false);
								return result;
							}

							static string StaticFunc(bool go = true) {
								string result = sx;
								if (go)
									return result..StaticFunc(false);
								return result;
							}
						};
					
						FuncTest ft = new;
						ft.NormalFunc()..FuncTest::StaticFunc();
					}", "NNSS"},

				// Static class functions can be stored by function variables.
				{"{ functype<string(bool)> fun = FuncTest::StaticFunc; fun(false); }", "S"},
				// Function variables can store refs to library functions because lib functions are
				// not classified as class functions due to the way they are made.
				{"{ functype<num(num,num)> fun = Math::Min; fun(3, 1); }", 1},

				// *** catch
				{"{ ScriptResult<num> rn = catch { ToNum(\"e\"); }; rn.IsSuccess(); }", false},
				{"{ ScriptResult<num> rn = catch { ToNum(\"-7.2\"); }; -7.2 == rn.value && rn.IsSuccess(); }", true},
				{"{ catch { ToNum(\"-7.2\"); }.value; }", -7.2},
				// We need to test every way that code which pushes calls onto the stack might fail to clean them up when an 
				// error happens. Without the assert in Expr_Catch these tests will not exhibit an error even if the stack
				// is messed up.
				// * exprlist
				{"catch { { ToNum(\"x\"); } }.IsSuccess();", false},
				// * for
				{"catch { for (i = 1, 1) { ToNum(\"x\"); } }.IsSuccess();", false},
				// * foreach, list
				{"catch { List<num> lnc = new { Add(1); }; foreach (ix, nlnc in lnc) { ToNum(\"x\"); } }.IsSuccess();", false},
				// * foreach, dictionary
				{"catch { Dictionary<string, num> dsn = new { Add(\"A\", 1); }; foreach (s, ndsn in dsn) { ToNum(\"x\"); } }.IsSuccess();", false},
				// * defstructor (div by zero)
				{"catch { List<num> lndbz = new { Add(3 / 0); }; }.IsSuccess();", false},
				// * function call (null access)
				{"{ num GetResult() { List<num> nulllist; #nulllist; } catch { GetResult(); }.IsSuccess(); }", false},

				// *** enum
				// * trailing commas, and numeric default values
				{"{ enum<num> TrailingComma { a, b, }; 0 == TrailingComma::a.value; }", true},
				{"{ enum<num> TrailingComma2 { a, }; 0 == TrailingComma2::a.value; }", true},
				{"{ enum<num> NoTrailingComma { a, b }; 1 == NoTrailingComma::b.value; }", true},
				{"{ enum<num> NoTrailingComma2 { a }; 0 == NoTrailingComma2::a.value; }", true},
				// * enums are global
				{"NoTrailingComma::a.name;", "a"},
				// * default string values
				{"{ enum<string> StringEnum { a }; StringEnum::a.name == StringEnum::a.value && StringEnum::a.value == \"a\"; }" , true},
				// * default num values
				{"{ enum<num> NumEnum { Zeroth, First, Third = 3, Fourth, Third2 = 3, Fourth2 }; 1 == NumEnum::First.value && 4 == NumEnum::Fourth.value && 4 == NumEnum::Fourth2.value; }", true},
				// * initializing class values
				{"{ enum<B> BEnum { bval, cval = new { name = \"C\"; },}; BEnum::bval.value.name == \"B\" && BEnum::cval.value.name == \"C\"; }", true},
				// * enum variables
				{"{ ScriptError re = ScriptError::NumberInvalid; re == ScriptError::NumberInvalid && re.value == ScriptError::NumberInvalid.value; }", true},
				// * text serialization
				{"{ ScriptError se = ScriptError::NumberInvalid; string txt = ToScript(se); txt; }", "ScriptError::NumberInvalid;" },
				{"{ ScriptError se <- \"ScriptError::NumberInvalid;\"; se == ScriptError::NumberInvalid; }", true},
			};

			Dictionary<string, RuntimeErrorType> executionFailTests = new Dictionary<string, RuntimeErrorType> {
				//"5 / 0;", // returns infinity

				// Null access.
					// --	This is an odd one. Without a "new" preceeding an attempt to use the dot operator on a class,
					//		the system returns a ClassNotDeclared compile error. It's a little weird but it should be
					//		clear to the user what they need to fix, which is all that matters.
					// "{ List<bool> lb; lb.Count(); }", 
				{"{ A anull; anull.name; }", RuntimeErrorType.NullAccessViolation},
				{"{ A anull; \"hi\"..anull.name; }", RuntimeErrorType.NullAccessViolation},
				{"{ functype<void()> nullfunc; nullfunc(); }", RuntimeErrorType.NullAccessViolation},
				{"{ List<num> lnn; lnn[0]; }", RuntimeErrorType.NullAccessViolation},
				{"{ List<num> lnn; #lnn; }", RuntimeErrorType.NullAccessViolation},
				{"{ List<num> lnn; lnn.Clear(); }", RuntimeErrorType.NullAccessViolation},
				{"{ Dictionary<string, num> dsnn; dsnn[\"hi\"]; }", RuntimeErrorType.NullAccessViolation},
				{"{ Dictionary<string, num> dsnn; #dsnn; }", RuntimeErrorType.NullAccessViolation},
				{"{ Dictionary<string, num> dsnn; dsnn.Clear(); }", RuntimeErrorType.NullAccessViolation},

				// Lists
				// -- out of bounds checks
				{"{ List<bool> lb = new List<bool>; lb.Get(0); }", RuntimeErrorType.ArrayIndexOutOfBounds},
				{"{ List<bool> lb = new List<bool>; lb.Set(-1, true); }", RuntimeErrorType.ArrayIndexOutOfBounds},
				{"{ List<bool> lb = new List<bool>; lb.RemoveAt(0); }", RuntimeErrorType.ArrayIndexOutOfBounds},
				{"{ List<bool> lb = new { Add(true, true); }; lb[-3]; }", RuntimeErrorType.ArrayIndexOutOfBounds},
				// -- modify in foreach
				{"{ ln.Add(1); foreach(k, v in ln) { ln.Add(666); } }", RuntimeErrorType.ForeachModifyingContainer},
				{"{ ln.Add(1); foreach(k, v in ln) { ln.RemoveAt(0); } }", RuntimeErrorType.ForeachModifyingContainer},
				{"{ ln.Add(1); foreach(k, v in ln) { ln.Clear(); } }", RuntimeErrorType.ForeachModifyingContainer},

				// Dictionaries.
				// -- Add existing item.
				{"{ Dictionary<string, bool> dic = new Dictionary<string, bool>; dic.Add(\"a\", true); dic.Add(\"a\", true); }", RuntimeErrorType.KeyAlreadyExists},
				// -- Read element that doesn't exist.
				{"{ Dictionary<string, bool> dic = new Dictionary<string, bool>; dic.Get(\"hello\"); }", RuntimeErrorType.KeyNotFound},
				// -- modify in foreach
				{"{ foreach(k, v in dict) { dict.Add(\"poo\", 666); } }", RuntimeErrorType.ForeachModifyingContainer},
				{"{ foreach(k, v in dict) { dict.Remove(\"poo\"); } }", RuntimeErrorType.ForeachModifyingContainer},
				{"{ foreach(k, v in dict) { dict.Clear(); } }", RuntimeErrorType.ForeachModifyingContainer},

				// StackOverflow
				// - Ideally we would check that every construct that pushes a "call" onto the stack handles overflow correctly, but there are many
				//   and these tests are difficult to do correctly.
				{"{ bool recurse(bool bArg) { recurse(bArg); }; recurse(true); }", RuntimeErrorType.StackOverflow},
				{"{ bool recurse(bool bArg) { if (true) { if (true) { if (true) { if (true) { if (true) { recurse(bArg); }}}}} }; recurse(true); }", RuntimeErrorType.StackOverflow},
				{"{ bool recurse(bool bArg) { for (i0 = 1, 1) { for (i1 = 1, 1) { for (i2 = 1, 1) { for (i3 = 1, 1) { for (i4 = 1, 1) { recurse(bArg); }}}}} }; recurse(true); }", RuntimeErrorType.StackOverflow},
				{"{ class Recurse { constructor { new Recurse; } }; new Recurse; }", RuntimeErrorType.StackOverflow},

				// Exceptions thrown in a variety of places.
				// -- argument to built-in function
				{"{ num badfunc() { N n; n.x; }; 3 * badfunc(); }", RuntimeErrorType.NullAccessViolation},

				// For
				// -- limits
				{"for (ii = -FORMAX-1, 0, -1) { }", RuntimeErrorType.ForIndexOutOfBounds},
				{"for (ii = FORMAX+1, 0, 1) { }", RuntimeErrorType.ForIndexOutOfBounds},
				{"for (ii = 0, -FORMAX-1, 1) { }", RuntimeErrorType.ForIndexOutOfBounds},
				{"for (ii = 0, FORMAX+1, -1) { }", RuntimeErrorType.ForIndexOutOfBounds},
				{"for (ii = 0, 1, -FORMAX-1) { }", RuntimeErrorType.ForIndexOutOfBounds},
				{"for (ii = 1, 0, FORMAX+1) { }", RuntimeErrorType.ForIndexOutOfBounds},

				// Math
				{"3 / 0;", RuntimeErrorType.NumberInvalid},
				{"0 / 0;", RuntimeErrorType.NumberInvalid},
				{"2.0E+256 * 2.0E+256;", RuntimeErrorType.NumberInvalid},

				// Assert
				{"assert(false);", RuntimeErrorType.Assert},
				{"assert(false, \"User assert message.\");", RuntimeErrorType.Assert},
				// -- assert doesn't catch exceptions while evaluating parameters
				{"assert(0 == (1 / 0));", RuntimeErrorType.NumberInvalid},
				{"assert(false, ToString(1 / 0));", RuntimeErrorType.NumberInvalid},
				{"{ A nulla; assert(false, nulla.name); }", RuntimeErrorType.NullAccessViolation},

				// Length operator
				{"{ List<num> nullln; #nullln; }", RuntimeErrorType.NullAccessViolation},

				// <- operator
				{"num baddes <- \"bad code\";", RuntimeErrorType.DeserializeScriptHasError},
				{"num baddes <- \"3/0;\";", RuntimeErrorType.NumberInvalid},
				{"num baddes <- \"true;\";", RuntimeErrorType.DeserializeTypeMismatch},
				{"Result<bool> rb <- \"3;\";", RuntimeErrorType.DeserializeTypeMismatch},

				// ?: operator
				{"{ List<num> nulllist; #nulllist == 0 ? 0 : 1; }", RuntimeErrorType.NullAccessViolation},

				// Class
				// -- Error evaluating non-static initializer.
				{"{ class BadInitializer { num x = 3 / 0; }; BadInitializer bi = new; }", RuntimeErrorType.NumberInvalid},
			};

			// Do syntax/type checks separately.
			Dictionary<string, ParseErrorType> compileFailureTests = new Dictionary<string, ParseErrorType> {

				// *** Type conversion

				// *** General syntax.
				// Missing closing semicolon.
				{"true", ParseErrorType.SyntaxError},
				// funcref is not a keyword
				{"funcref<num(num)> cubeFunc;", ParseErrorType.SymbolNotFound},

				// *** Operators
				{"++5;", ParseErrorType.AssignToNonLValue},
				{"{ const num hmmm; hmmm++; }", ParseErrorType.RementOnConst},
				{"true || 5;", ParseErrorType.TypeMismatch},
				{"true..\"hello\";", ParseErrorType.ConcatStringsOnly},
				{"{ string sss; sss ..= 3; }", ParseErrorType.ConcatStringsOnly},

				// *** Set
				// -- duplicate variable definition.
				{"bool y;", ParseErrorType.SymbolAlreadyDeclared},
				{"{ bool y; bool y; }", ParseErrorType.SymbolAlreadyDeclared},
				// -- bad variable names
				{"num const;", ParseErrorType.SyntaxError},
				{"num override;", ParseErrorType.SyntaxError},
				{"num class;", ParseErrorType.SyntaxError }, // ParseErrorType.InvalidSymbolName}, -- now that we no longer make class symbols available to code above the declaration, the error changed
				{"num for;", ParseErrorType.SyntaxError},

				// *** Assignment.
				// Type check.
				{"y = true;", ParseErrorType.TypeMismatch},
				// Syntax
				{"y = = 3;", ParseErrorType.SyntaxError},
				{"y = 3 = 4;", ParseErrorType.Any},
				// Assignment to library functions.
				{"Exec = null;", ParseErrorType.AssignToConst},

				// *** Var Decl
				// Bad type. 
				{"int x;", ParseErrorType.TypeNotFound},
				// Type as symbol name.
				{"num string;", ParseErrorType.SymbolAlreadyDeclared},
				// Malformed.
				{"num xo = num;", ParseErrorType.SymbolNotFound},
				{"x num = 42;", ParseErrorType.TypeNotFound},
				

				// Functions
				// - already declared
				{"num numFunc(num a, num b) { a + b; }", ParseErrorType.SymbolAlreadyDeclared},
				// - arg type mismatch
				{"numFunc(1, true); ", ParseErrorType.TypeMismatch},
				// - too few args
				{"numFunc(1); ", ParseErrorType.ArgCountMismatch},
				// - too many args
				{"numFunc(1, 2, 3); ", ParseErrorType.ArgCountMismatch},
				// - return value mismatch
				{"bool failBool = numFunc(1, 2);", ParseErrorType.TypeMismatch},
				// - type mismatch
				{"{ num numFunc2(num a) { -a; } functype<num(num, num)> mismatch = numFunc2; }", ParseErrorType.TypeMismatch},
				// - type checking inside def.
				{"num numFunc3(num a) { a = true; }", ParseErrorType.TypeMismatch},
				// -- gaps in default argument values
				{"num OA(num x, num y = 10, num z) { x; }", ParseErrorType.DefaultArgGap},
				{"num OA2(num y = 10, num z) { x; }", ParseErrorType.DefaultArgGap},
				{"num OA3(num y = 10, num gap, num a = 20) { x; }", ParseErrorType.DefaultArgGap},
				// -- default value type mismatch
				{"num O4(num x = true) { x; }", ParseErrorType.TypeMismatch},
				// -- too few args for function with default values
				{"{ num O5(num x, num y = 3) { x + y; } O5(); }", ParseErrorType.ArgCountMismatch},
				// -- attempting to call variable with more arguments than it has, even though it's value could handle them
				{"{ num O6(num x, num y = 3) { x + y; } functype<num(num)> oneArgFunc = O6; oneArgFunc(1, 2); }", ParseErrorType.ArgCountMismatch},
				// -- something other than a literal for a default value
				{"{ num O7(num x = 3 + 2); }", ParseErrorType.SyntaxError},
				{"{ num O8(List<num> ln = new List<num>); }", ParseErrorType.SyntaxError},
				// -- function literals cant be const.
				{"const num ConstFunc() { 0; }", ParseErrorType.FunctionLiteralsAreImplicitlyConst},
				{"class ClassWithConstFunc { const num ConstFunc() { 0; } };", ParseErrorType.ClassMemberFunctionsConst},
				// -- function literals are implicitly const
				{"{ num ConstFunc() { 0; } ConstFunc = null; }", ParseErrorType.AssignToConst},
				{"{ class ClassWithConstFunc2 { num ConstFunc() { 0; } }; ClassWithConstFunc2 cf2 = new; cf2.ConstFunc = null; }", ParseErrorType.AssignToConst},
				// -- arguments cannot be const
				{"void ConstArg(const num n) {}", ParseErrorType.SyntaxError},
				// -- function variables with var args: this is not ok because fbbo may be called with 0 arguments, but fbb may hold a variable that requires at least 1.
				{"{ functype<bool(bool)> fbb; functype < bool(bool ?) > fbbo; fbbo = fbb; }", ParseErrorType.TypeMismatch},

				// -- errors in default values in function types
				{"functype<num(num ?, num)> nope;", ParseErrorType.DefaultArgGap},
				{"functype<num(num ?, num, num ?)> nope;", ParseErrorType.DefaultArgGap},

				// -- return types cannot be promoted
				{"{ functype<B()> bFunc; A AFunc() { new A; } bFunc = AFunc; }", ParseErrorType.TypeMismatch},
				// -- bad argument promotion: BFunc has to have a B, but aFunc can only guarantee an A
				{"{ functype<num(A)> aFunc; num BFunc(B b) { 1; } aFunc = BFunc; }", ParseErrorType.TypeMismatch},

				// Return
				{"return true;", ParseErrorType.SyntaxError},
				{"{ return true; }", ParseErrorType.SyntaxError},
				{"{ if (true) return true; }", ParseErrorType.ReturnNotInCall},
				{"{ for (i = 1, 1) return true; }", ParseErrorType.ReturnNotInCall},
				{"{ num poop() { return true; }; }", ParseErrorType.ReturnTypeMismatch},
				{"{ num poop() { string poopy() { return 0; }; }; }", ParseErrorType.ReturnTypeMismatch},
				{"class RetTest { return null; };", ParseErrorType.SyntaxError},
				{"void poop() { return true; };", ParseErrorType.ReturnValueInVoidFunction},
				{"num poop() { return; };", ParseErrorType.ReturnNullInNonVoidFunction},


				// Class
				// -- many invalid places to attempt to declare classes
				{"{ if (true) class InIf; }", ParseErrorType.SyntaxError}, 
				{"{ if (true) { class InIfBlock; } }", ParseErrorType.SyntaxError},
				{"{ if (true) { { class InIfBlockBlock; } } }", ParseErrorType.SyntaxError},
				{"{ for (i=1,1) class InFor; }", ParseErrorType.SyntaxError}, 
				{"{ for (i=1,1) { class InForBlock; } }", ParseErrorType.SyntaxError},
				{"{ for (i=1,1) { { class InForBlockBlock; } } }", ParseErrorType.SyntaxError},
				{"{ new A { class InsideDefstructor; }; }", ParseErrorType.SyntaxError},
				{"{ new A { { class InsideDefstructorBlock; } }; }", ParseErrorType.SyntaxError},
				{"{ void ClassContainingFunc() { class InFuncBody; } }", ParseErrorType.SyntaxError},
				{"{ void ClassContainingFunc() { { class InFuncBodyBlock; } } }", ParseErrorType.SyntaxError},
				{"class NestedInConstructor { constructor { class ImInAConstructor; } };", ParseErrorType.SyntaxError},
				{"catch { class InCatch; }", ParseErrorType.SyntaxError},
				// - classes can reference (though not inherit from) classes that are declared below them
				{"{ class First : Second; class Second; }", ParseErrorType.SymbolNotFound},
				// - derived class member shadowing
				{@"class OY : N { num x = 5; };", ParseErrorType.SymbolAlreadyDeclared},
				// - argument b shadows inherited property b.
				{@"class OX : N { bool eqFunc(num a, num b) { a == b; } num square() { x * y; } };", ParseErrorType.SymbolAlreadyDeclared},
				// -- Function scope not fully lexical.
				{"num noAccess() { x; }", ParseErrorType.SymbolNotFound},
				// -- Assignment to member
				{"{ o.square = null; true; }", ParseErrorType.AssignToConst},
				{"class OXX { num xy = true; };", ParseErrorType.TypeMismatch},
				// -- Symbol with class name.
				{"num N;", ParseErrorType.SymbolAlreadyDeclared},
				// -- Member functions have special type that makes them unable to be saved in non-class variables.
				{"{ functype<bool()> f = a.f; f(); }", ParseErrorType.TypeMismatch},
				// -- Invalid parents
				{"class ListChild : List;", ParseErrorType.ClassCannotBeChildOfTemplate},
				{"class DictionaryChild : Dictionary;", ParseErrorType.ClassCannotBeChildOfTemplate},
				{"class ResultChild : Result;", ParseErrorType.ClassCannotBeChildOfTemplate},
				{"class ListNumChild : List<num>;", ParseErrorType.SyntaxError}, // ParseErrorType.ClassCannotBeChildOfTemplate},
				{"class NumChild : num;", ParseErrorType.SymbolNotFound},
				// -- sealed
				{"{ sealed class SealedClass; class ChildOfSealed : SealedClass; }", ParseErrorType.ClassParentSealed},
				// -- uninstantiable
				{"{ uninstantiable class Uclass; new Uclass; }", ParseErrorType.ClassUninstantiable},
				// -- Make sure we can't name a class after an existing type.
				{"class num {  };", ParseErrorType.TypeAlreadyExists},
				{"{ typealias alias = O; class alias {}; }", ParseErrorType.TypeAlreadyExists},
				// -- multiple constructors
				{"class MultCons{ constructor {} constructor {} };", ParseErrorType.ClassCanOnlyHaveOneConstructor},
				// -- Error evaluating static initializer.
				{"class BadStaticInitializer { static num x = 3 / 0; };", ParseErrorType.StaticMemberEvaluationError},
				// -- member function argument shadows member field.
				{"class ArgumentFieldShadowing { num x; num F(num x) { x; } };", ParseErrorType.SymbolAlreadyDeclared},
				{"class ArgumentStaticFieldShadowing { static num x; static num F(num x) { x; } };", ParseErrorType.SymbolAlreadyDeclared},
				
				// this
				// - Using "this" outside of class scope.
				{"this;", ParseErrorType.ClassRequiredForThis},
				{"num ThisFunc() { this; }", ParseErrorType.ClassRequiredForThis},
				// - Using "this" to reference statics
				{"class BadThis { static num a; num GetA() { this.a; } };", ParseErrorType.ClassMemberNotFound},
				{"class BadThis2 { static BadThis2 GetThis() { this; } };", ParseErrorType.ClassRequiredForThis},
				{"class BadThis3 { num n; static num F() { this.n; } };", ParseErrorType.ClassRequiredForThis},

				// Polymorphism
				// assigning value of parent type to child variable
				{"O noGood = n;", ParseErrorType.TypeMismatch},

				// Bad uses of new.
				{"{ num notnew; notnew = new; }", ParseErrorType.ReferenceOfNonReferenceType},
				{"{ void FFFF(O o) { }  FFFF(new); }", ParseErrorType.NewTypeCannotBeInferred},
				{"List<M> lm = new List<N>;", ParseErrorType.TypeMismatch},

				// Initializers
				{"{ N nn = new N { num strandedNum; }; strandedNum; }", ParseErrorType.SymbolNotFound},

				// Const.
				{"{ const num co = 5; co = 3; }", ParseErrorType.AssignToConst},
				{"{ class Coo { const num x = 3; }; Coo coo = new; coo.x = 7; }", ParseErrorType.AssignToConst},
				{"class Const { const num f() { 0; } };", ParseErrorType.ClassMemberFunctionsConst},
				{"const class ConstClass;", ParseErrorType.SyntaxError},
				{"functype<const num()>", ParseErrorType.SyntaxError},
				{"functype<num(const num)>", ParseErrorType.SyntaxError},
				{"List<const num> lcn;", ParseErrorType.SyntaxError},
				{"StaticConst::x = 3;", ParseErrorType.AssignToConst},
				{"{ const N m = new; m = new; }", ParseErrorType.AssignToConst},

				// Static
				{"{ static num sc; }", ParseErrorType.StaticClassMembersOnly},
				{"{ static const num sc; }", ParseErrorType.StaticClassMembersOnly},
				{"{ const static num sc; }", ParseErrorType.StaticClassMembersOnly},
				//{"class StaticConst { static const num x = 3; };", ParseErrorType.StaticImpliesConst},
				//{"class StaticConst2 { const static num x = 3; };", ParseErrorType.StaticImpliesConst},
				//{"class Static { static num f() { 0; } };", ParseErrorType.StaticFieldsOnly},
				// Access static in normal. <- language was changed to allow this, makes it more like C#.
				//{"class StaticBad { static num sx; num F() { sx; } };", ParseErrorType.SymbolNotFound},
				//{"class StaticBad2 { static num SF() { 1; } num F() { SF(); } };", ParseErrorType.SymbolNotFound},
				// Access normal in static.
				{"class StaticBad3 { num sx; static num F() { sx; } };", ParseErrorType.SymbolNotFound},
				{"class StaticBad4 { num F() { 1; } static num SF() { F(); } };", ParseErrorType.SymbolNotFound},
				// Using scope operator on normal.
				{"class StaticBad5 { num sx; num F() { StaticBad5::sx; } };", ParseErrorType.ClassMemberNotFound},
				{"class StaticBad6 { num sx; static num F() { StaticBad6::sx; } };", ParseErrorType.ClassMemberNotFound},
				{"class StaticBad7 { num E() { 1; } num F() { StaticBad7::E(); } };", ParseErrorType.ClassMemberNotFound},
				{"class StaticBad8 { num E() { 1; } static num F() { StaticBad8::E(); } };", ParseErrorType.ClassMemberNotFound},
				// Attempting to override static.
				{"class StaticGood { static num F() { 1; } }; class StaticBad9 : StaticGood { static override num F() { 2; } };", ParseErrorType.MemberOverrideCannotBeStatic},
				// Same but different keyword order.
				{"class StaticGood2 { static num F() { 1; } }; class StaticBad10 : StaticGood2 { override static num F() { 2; } };", ParseErrorType.MemberOverrideCannotBeStatic},
				// Access static field without @.
				{"{ StaticOK sok = new; sok.sx; }", ParseErrorType.ClassMemberNotFound},
				// Access static function without @.
				{"{ StaticOK sok = new; sok.SX(); }", ParseErrorType.ClassMemberNotFound},

				// Global
				{"{ { const global num globNum = 42; } globNum = 3; }", ParseErrorType.AssignToConst},
				{"{ static global num sc; }", ParseErrorType.StaticClassMembersOnly},
				{"{ global static num sc; }", ParseErrorType.StaticClassMembersOnly},
				{"class Global { global num f() { 0; } };", ParseErrorType.ClassMembersCannotBeGlobal},
				{"class Global2 { global num n; };", ParseErrorType.ClassMembersCannotBeGlobal},

				// Scope
				// -- variables block scope terminates
				{"{ { bool bbb = true; } bbb = true; }", ParseErrorType.SymbolNotFound},
				{"{ if (true) { bool bbb = true; } bbb = true; }", ParseErrorType.SymbolNotFound},
				{"{ for (xxx = 1, 10) { bool bbb = true; } bbb = true; }", ParseErrorType.SymbolNotFound},
				{"{ foreach (k, v in dict) { bool bbb = true; } bbb = true; }", ParseErrorType.SymbolNotFound},
				// -- iterators don't leave scope
				{"{ for (xxx = 1, 10) { } xxx = true; }", ParseErrorType.SymbolNotFound},
				{"{ foreach (k, v in dict) { } k; }", ParseErrorType.SymbolNotFound},
				// -- shadowing
				{"{ num someVar; { num someVar; }; }", ParseErrorType.SymbolAlreadyDeclared},
				{"{ num someVar; for(it=1, 10) { num someVar; }; }", ParseErrorType.SymbolAlreadyDeclared},
				{"{ num someVar; for(someVar=1, 10) { }; }", ParseErrorType.ForIteratorNameTaken},
				{"{ num someVar; if(true) { num someVar; } }", ParseErrorType.SymbolAlreadyDeclared},
				// -- iterator shadowing
				{"{ for(someVar=1, 10) { bool someVar; }; }", ParseErrorType.SymbolAlreadyDeclared},
				{"{ for(xxx=1, 10) { num xxx; }; }", ParseErrorType.SymbolAlreadyDeclared},
				{"{ foreach (k, v in dict) { bool k; } }", ParseErrorType.SymbolAlreadyDeclared},
				{"{ foreach (k, v in dict) { bool v; } }", ParseErrorType.SymbolAlreadyDeclared},
				// -- function shielding
				{"{ num somevar; num fun() { somevar; }; }", ParseErrorType.SymbolNotFound},

				// If
				// -- allocation body (not embeddable statement)
				{"{ if (true) bool allocInTrueCase = true; }", ParseErrorType.SyntaxError},
				// -- class definition body
				{"{ if (true) class Nooo; }", ParseErrorType.SyntaxError},
				// -- allocation in if condition
				{"{ if(bool bbbbb = true) { }; }", ParseErrorType.SyntaxError},
				// -- condition not boolean
				{"if (0) { };", ParseErrorType.IfConditionNotBoolean},
				// -- make sure allocation in if block doesn't bleed out
				{"{ if (true) { num allocInBlock; } numAllocInBlock; }", ParseErrorType.SymbolNotFound},

				// Lists
				// -- list type mismatch.
				{"{ List<string> ls = ln.Push(42); }", ParseErrorType.TypeMismatch},
				// -- Assignment to static member.
				{"{ List<string> ls; ls.Clear = null; }", ParseErrorType.AssignToConst}, // doesnt happen anymore ErrorType.ClassNotDeclared}, 
				{"{ List<string> ls = new List<string>; ls.Clear = null; }", ParseErrorType.AssignToConst},
				//{"{ List<num> ls = new List<num>; ls.Clear = ls.Clear2; }", ErrorType.AssignToConst},
				// -- Invalid number of template types.
				{"{ List<> ls; }", ParseErrorType.SyntaxError},
				{"{ List<string, string> ls; }", ParseErrorType.TemplateCountMismatch},
				// -- Template type not a type.
				{"{ List<y> ls; }", ParseErrorType.TypeNotFound},
				
				// Index 
				// -- attempt to use on non-list
				{"a[0];", ParseErrorType.TypeNotIndexable},
				{"true[0];", ParseErrorType.TypeNotIndexable},
				// -- non numeric index
				{"ln[true];", ParseErrorType.IndexNotNumeric},

				// Dictionaries
				// -- Invalid number of template types.
				{"Dictionary<num> bad;", ParseErrorType.TemplateCountMismatch},
				{"Dictionary<num, num, num> bad;", ParseErrorType.TemplateCountMismatch},
				// -- Template type not a type!
				{"Dictionary<num, x> bad;", ParseErrorType.TypeNotFound},

				// for
				// -- loop indexer const
				{"{ for(xxx=1, 10) { xxx = 1; }; }", ParseErrorType.AssignToConst},
				// -- loop indexer shadowing
				{"{ for(xxx=1, 10) { num xxx = 1; }; }", ParseErrorType.SymbolAlreadyDeclared},

				// foreach
				{"foreach(k v in dict) { }", ParseErrorType.SyntaxError},
				{"foreach(k, in dict) { }", ParseErrorType.SyntaxError},
				{"foreach(k, v dict) { }", ParseErrorType.SyntaxError},
				{"foreach(k, v, z dict) { }", ParseErrorType.SyntaxError},
				{"foreach(k, v in dict)", ParseErrorType.SyntaxError},
				{"foreach(k, v in x) { }", ParseErrorType.ForEachInvalidCollection},
				// -- loop indexer invalid
				{"foreach(x, v in dict) { }", ParseErrorType.ForEachIteratorNameTaken},
				{"foreach(k, y in dict) { }", ParseErrorType.ForEachIteratorNameTaken},
				{"foreach(k, k in dict) { }", ParseErrorType.ForEachIteratorNameTaken},
				{"foreach(dict, k in dict) { }", ParseErrorType.ForEachIteratorNameTaken},
				{"foreach(k, dict in dict) { }", ParseErrorType.ForEachIteratorNameTaken},
				// -- loop indexer const
				{"{ foreach (k, v in dict) { k = \"hello\"; } }", ParseErrorType.AssignToConst},
				{"{ foreach (k, v in dict) { v = 42; } }", ParseErrorType.AssignToConst},
				// -- cannot foreach on a enum value
				{"{ TestEnum e; foreach (a, b in e) {} }", ParseErrorType.ForEachInvalidCollection},
				// -- cannot foreach on any type other than an enum.
				{"{ foreach (a, b in A) {} }", ParseErrorType.ForEachInvalidType},
				{"{ foreach (a, b in n) {} }", ParseErrorType.ForEachInvalidCollection},

				// Break & continue
				{"break;", ParseErrorType.SyntaxError},
				{"continue;", ParseErrorType.SyntaxError},
				{"if (true) { break; }", ParseErrorType.BreakNotInFor},
				{"if (true) { continue; }", ParseErrorType.ContinueNotInFor},
				{"if (true) break;", ParseErrorType.BreakNotInFor},
				{"if (true) continue; ", ParseErrorType.ContinueNotInFor},
				{"{ for (yyy=1, 2) { num xxx() { break; 0; }; } }", ParseErrorType.BreakNotInFor},

				// Globals
				{"global num glob_no; num glob_no;", ParseErrorType.SymbolAlreadyDeclared},

				// Typealias
				{"typealias num = string;", ParseErrorType.SymbolAlreadyDeclared},
				{"typealias badalias = qjxbxbr;", ParseErrorType.TypeNotFound},
				{"typealias badalias2 = 2;", ParseErrorType.SyntaxError},
				// -- Let's try to break variable declaration by using an alias.
				{"num numalias = 3;", ParseErrorType.SymbolAlreadyDeclared},
				{"for(numalias = 0, 1) { };", ParseErrorType.ForIteratorNameTaken},
				{"{ List<num> lnn = new; foreach (ix, numalias in lnn) { }; }", ParseErrorType.ForEachIteratorNameTaken},
				{"{ List<num> lnn = new; foreach (numalias, val in lnn) { }; }", ParseErrorType.ForEachIteratorNameTaken},

				// ..
				{"\"a\"..0;", ParseErrorType.ConcatStringsOnly},
				{"{string aaa; bool bbb; aaa..bbb;}", ParseErrorType.ConcatStringsOnly},
				{"{string aaa; bool bbb; bbb..aaa;}", ParseErrorType.ConcatStringsOnly},

				// Void
				{"void variablesCantBeVoid;", ParseErrorType.VoidFunctionsOnly},
				{"bool BVF(void v) { true; };", ParseErrorType.SyntaxError},
				{"{ void GVF() {}; void BVF2() { return GVF(); }; }", ParseErrorType.ReturnValueInVoidFunction},
				{"List<void> lv;", ParseErrorType.SyntaxError},
				{"class ClassWithVoidField { void v; };", ParseErrorType.VoidFunctionsOnly},

				// override
				{"override num overrideNonClass;", ParseErrorType.OverrideNonMemberFunction},

				// Let's make sure the parser busts on all these.
				{"class override {};", ParseErrorType.SyntaxError}, // ParseErrorType.InvalidSymbolName},
				{"override class OverrideClass {};", ParseErrorType.SyntaxError},
				{"const static;", ParseErrorType.SyntaxError},
				{"override static void FFF() {}", ParseErrorType.StaticClassMembersOnly},
				{"functype<override()> fff;", ParseErrorType.SyntaxError},
				{"functype<num(void) fff;", ParseErrorType.SyntaxError},
				{"override functype<num()> fff;", ParseErrorType.OverrideNonMemberFunction},
				{"num global;", ParseErrorType.SyntaxError},

				// Length operator
				{"#10;", ParseErrorType.LengthOperatorInvalidOperand},
				{"#List<string>;", ParseErrorType.SyntaxError},
				{"{ functype<string()> fvar; #fvar; }", ParseErrorType.LengthOperatorInvalidOperand},

				// ++ and -- cant be chained with eachother because they require l-values but don't return them
				{"{ num inc = 10; ++++inc; }", ParseErrorType.AssignToNonLValue},
				{"{ num inc = 10; ----inc; }", ParseErrorType.AssignToNonLValue},
				{"{ num inc = 10; --++inc; }", ParseErrorType.AssignToNonLValue},
				{"{ num inc = 10; ++$inc; }", ParseErrorType.AssignToNonLValue},
				{"{ num inc = 10; ++#inc; }", ParseErrorType.AssignToNonLValue},

				// catch
				// - no block
				{"{ ScriptResult<num> rn = catch 3/0; }", ParseErrorType.SyntaxError},

				// enum
				// - extra commas
				{"enum<num> TooManyCommas { a,,b };", ParseErrorType.SyntaxError},
				{"enum<num> TooManyCommas { a,b,, };", ParseErrorType.SyntaxError},
				// - everything is readonly
				{"ScriptError::NumberInvalid = ScriptError::NullAccessViolation;", ParseErrorType.AssignToConst},
				{"ScriptError::NumberInvalid.name = \"hello\";", ParseErrorType.AssignToConst},
				{"ScriptError::NumberInvalid.value = \"hello\";", ParseErrorType.AssignToConst},
				// - cannot be null.
				{"ScriptError se = null;", ParseErrorType.TypeMismatch},
				// - cannot have no values
				{"enum<num> NoValues { };", ParseErrorType.EnumMustHaveValues},
				// - cannot have duplicate values
				{"enum<num> DupeValues { A, B, A };", ParseErrorType.EnumNameDuplicate},
				// -- cannot be instantiated
				{"ScriptError se = new;", ParseErrorType.ClassUninstantiable},
				// -- cannot be parent
				{"class EnumChild : ScriptError {};", ParseErrorType.ClassParentSealed},
			};

			if (!engine.defaultContext.stack.PushTerminalScope("<test scope>", null)) {
				engine.LogError("stack overflow attempting to push scope for unit tests.");
				return;
			}
			{
				float startTime = Pb.RealtimeSinceStartup();

				// Runs the tests defined above.
				RunTests(engine, evaluationTests, compileFailureTests, executionFailTests, verbose);

				// Run test functions provided by registered libraries.
				foreach(var kvp in testFuncDelegates) {
					kvp.Value(engine, verbose);
				}

				engine.Log("\nTests took " + (Pb.RealtimeSinceStartup() - startTime) + " seconds.");
			}
			engine.defaultContext.stack.PopScope();
		}

		public static bool RunTests(Engine engine, Dictionary<string, object> evaluationTests, Dictionary<string, ParseErrorType> compileFailureTests, Dictionary<string, RuntimeErrorType> executionFailTests, bool verbose = false) {

			int evaluationTestsFailed = 0;
			int compileFailureTestsFailed = 0;
			int executionFailTestsFailed = 0;

			engine.Log("*** General: Running tests...");

			string msg = "";

			if (null != evaluationTests) {
				engine.Log("* Running evaluation tests...");

				foreach (var pair in evaluationTests) {
					if (!engine.RunTest(pair.Key, pair.Value, verbose))
						++evaluationTestsFailed;
				}

				msg += "\n" + evaluationTestsFailed + "/" + evaluationTests.Count + " evaluation tests failed. ";
			}

			// ***

			engine.logCompileErrors = false;
			if (null != compileFailureTests) {
				engine.Log("* Running compile failure tests...");
				foreach (var kvp in compileFailureTests) {
					if (!engine.RunCompileFailTest(kvp.Key, kvp.Value, verbose))
						++compileFailureTestsFailed;
				}

				msg += "\n" + compileFailureTestsFailed + "/" + compileFailureTests.Count + " compile failure tests failed. ";
			}

			// ***

			if (null != executionFailTests) {
				engine.Log("* Running execution failure tests...");

				foreach (var kvp in executionFailTests) {
					if (!engine.RunRuntimeFailTest(kvp.Key, kvp.Value, verbose))
						++executionFailTestsFailed;
				}

				msg += "\n" + executionFailTestsFailed + "/" + executionFailTests.Count + " execution failure tests failed. ";
			}
			engine.logCompileErrors = true;

			// ***

			const string unittestPath = "UnitTests";
			int filesSucceeded = 0;
			int filesFailed = 0;
			if (Directory.Exists(unittestPath)) {
				List<ParseErrorInst> errors = new List<ParseErrorInst>();
				string[] testFiles = Directory.GetFiles(unittestPath);
				if (testFiles.Length > 0) {
					engine.Log("* Checking files in '" + unittestPath + "' directory (expected result = true)...");

					foreach (string testFile in testFiles) {
						string testFileData = File.ReadAllText(testFile);

						object result = engine.RunScript(testFileData, ref errors, false, testFile);
						if (!(result is bool) || true != (bool)result) {
							engine.LogError("  ^ " + testFile + " failed with " + errors.Count + " errors.");
							++filesFailed;
						} else
							++filesSucceeded;
					}

					msg += "\n" + filesFailed + "/" + (filesFailed + filesSucceeded) + " files in unittests directory failed.";
				}
			}

			// ***

			bool success = 0 == evaluationTestsFailed && 0 == compileFailureTestsFailed && 0 == executionFailTestsFailed && 0 == filesFailed;
			if (success)
				engine.Log(msg + "\nNo errors!");
			else
				engine.LogError(msg);

			return success;
		}
	}
}