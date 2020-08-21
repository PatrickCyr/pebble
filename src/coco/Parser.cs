/*----------------------------------------------------------------------
Pebble
Copyright (c) 2019 Patrick Cyr
See LICENSE.TXT for license information.

This file was generated by Coco/R.
-----------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using BoolList = System.Collections.Generic.List<bool>;
using ExprList = System.Collections.Generic.List<Pebble.IExpr>;
using StrList = System.Collections.Generic.List<string>;
using TypeRefList = System.Collections.Generic.List<Pebble.ITypeRef>;
using LiteralList = System.Collections.Generic.List<Pebble.Expr_Literal>;

namespace Pebble {



// This is only here because Coco tries to create one of these,
// though it doesn't try to use it. 
public class CodeGenerator {
}

public class Parser {
	// *** constants *************************************************************
	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _number = 2;
	public const int _StringLiteral = 3;
	public const int maxT = 75;

	// *** constants end *********************************************************

	const bool _T = true;
	const bool _x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;
	public string scriptName;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

	// *** declarations *******************************************************
const int // types
	  undef = 0, integer = 1, boolean = 2;

	const int // object kinds
	  var = 0, proc = 1;

	public CodeGenerator gen;
	public IExpr _headExpr;
	public ExecContext context;

	//------------------ token sets ------------------------------------
	/*
	static BitArray
	startOfTypeName = NewBitArray(_const, _volatile, _void, _char, _short, _int, _long,
		                _double, _signed, _unsigned, _struct, _union, _enum),
	startOfDecl     = NewBitArray(_typedef, _extern, _static, _auto, _register, _const, 
	                  _volatile, _void, _char, _short, _int, _long, _double, _signed, 
	                  _unsigned, _struct, _union, _enum);

	private static BitArray NewBitArray(params int[] val) {
		BitArray s = new BitArray(128);
		foreach (int x in val) s[x] = true;
		return s;
	}
	*/
	//---------- LL(1) conflict resolvers ------------------------------

	// Return the n-th token after the current lookahead token
	Token Peek (int n) {
		scanner.ResetPeek();
		Token x = la;
		while (n > 0) { x = scanner.Peek(); n--; }
		return x;
	}

	private bool IsTypeName(Token x) {
		return context.IsType(x.val);
	}

	bool IsDecl() { // return true if followed by Decl
		if ("override" == la.val 
				|| "void" == la.val
				|| "global" == la.val 
				|| "const" == la.val 
				|| "functype" == la.val 
				|| "funcdef" == la.val 
				|| "static" == la.val
				|| "guarded" == la.val)
			return true;
		scanner.ResetPeek();
		Token laa = scanner.Peek();
		if (null == laa)
			return false;

		// Anywhere we have two identifiers in sequence, it must be a declaration.
		if (1 == la.kind && 1 == laa.kind)
			return true;

		// That isn't sufficient, though, in the case of templates, so here's the old check,
		// which also makes sure this isn't a ::.
		// Note this code is probably broken if there are ever static template members, ie. A<num>::x; 
		// I'm not positive that's needed. I suppose it depends on the order these productions are checked for.
		return IsTypeName(la) && null != laa && "::" != laa.val;
	}

	bool IsClassName() {
		return context.IsType(la.val);
	}

	bool IsScopeOpStart() {
		scanner.ResetPeek();
		Token laa = scanner.Peek();
		// Here I'm assuming type 1 means "identifier". That seems to be the case but unsure why.
		// This would not work for static in templates, btw.
		return 1 == la.kind && null != laa && "::" == laa.val;
	}

	// True, if the comma is not a trailing one, like the last one in: a, b, c,      
	bool NotFinalComma () {
		string peek = Peek(1).val;
		return la.val == "," && peek != "]" && peek != "}";
	}

/*****************************************************************************/
	
	// *** declarations end ***************************************************

	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(scriptName, la.line, la.col, n);
		errDist = 0;
	}

	/*
	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(scriptName, t.line, t.col, msg);
		errDist = 0;
	}
	*/
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }
			// *** pragmas ****************************************************

			// *** pragmas end ************************************************
			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	// *** productions ********************************************************
	void TypeSpecifier(ref ITypeRef type) {
		bool isConst = false; 
		if (la.kind == 8) {
			FunctionType(ref type);
		} else if (la.kind == 1) {
			TypeSpecifierNoFunc(ref type);
		} else SynErr(76);
		if (isConst) type.SetConst(true); 
	}

	void FunctionType(ref ITypeRef varType) {
		ITypeRef retType = null; TypeRefList args = new TypeRefList(); BoolList argHasDefaults = new BoolList(); bool isConst = false; 
		Expect(8);
		Expect(4);
		if (la.kind == 1 || la.kind == 8) {
			TypeSpecifier(ref retType);
		} else if (la.kind == 9) {
			Get();
			retType = new TypeRef("void"); 
		} else SynErr(77);
		Expect(10);
		if (la.kind == 1 || la.kind == 8) {
			FunctionTypeArg(ref args, ref argHasDefaults);
			while (la.kind == 5) {
				Get();
				FunctionTypeArg(ref args, ref argHasDefaults);
			}
		}
		Expect(11);
		Expect(6);
		varType = new TypeRef_Function(retType, args, argHasDefaults, false, isConst); 
	}

	void TypeSpecifierNoFunc(ref ITypeRef type) {
		string className = null; ITypeRef genType = null; TypeRefList genericTypes = new TypeRefList(); 
		Expect(1);
		className = t.val; 
		if (la.kind == 4) {
			Get();
			TypeSpecifier(ref genType);
			genericTypes.Add(genType); 
			while (la.kind == 5) {
				Get();
				TypeSpecifier(ref genType);
				genericTypes.Add(genType); 
			}
			Expect(6);
		}
		type = new TypeRef(className, genericTypes); 
	}

	void FunctionTypeArg(ref TypeRefList argTypes, ref BoolList argHasDefaults) {
		ITypeRef argType = null; 
		TypeSpecifier(ref argType);
		argTypes.Add(argType); 
		if (la.kind == 7) {
			Get();
			while (argHasDefaults.Count < argTypes.Count - 1)
			argHasDefaults.Add(false);
			argHasDefaults.Add(true); 
			
		}
	}

	void FunctionDeclArg(ref TypeRefList argTypes, ref StrList argNames, ref LiteralList defaultValues) {
		ITypeRef argType = null; IExpr litValue = null; 
		TypeSpecifier(ref argType);
		Expect(1);
		argTypes.Add(argType); argNames.Add(t.val); 
		if (la.kind == 12) {
			Get();
			Literal(ref litValue);
			while (defaultValues.Count < argTypes.Count)
			defaultValues.Add(null);
			defaultValues[defaultValues.Count - 1] = (Expr_Literal) litValue; 
		}
	}

	void Literal(ref IExpr expr) {
		if (la.kind == 55) {
			Get();
			expr = new Expr_Literal(this, true, IntrinsicTypeDefs.BOOL); 
		} else if (la.kind == 56) {
			Get();
			expr = new Expr_Literal(this, false, IntrinsicTypeDefs.BOOL); 
		} else if (la.kind == 57) {
			Get();
			expr = new Expr_Literal(this, null, IntrinsicTypeDefs.NULL); 
		} else if (la.kind == 2) {
			Get();
			expr = new Expr_Literal(this, Convert.ToDouble(t.val), IntrinsicTypeDefs.NUMBER); 
		} else if (la.kind == 3) {
			Get();
			expr = new Expr_Literal(this, t.val.Substring(1, t.val.Length - 2), IntrinsicTypeDefs.STRING); 
		} else SynErr(78);
	}

	void Decl(ref IExpr expr) {
		string sym = null; ITypeRef type = null; IExpr init = null; TypeRefList argTypes = new TypeRefList(); StrList argNames = new StrList(); LiteralList defaultValues = new LiteralList(); IExpr body = null; IExpr script = null; DeclMods mods = new DeclMods(); 
		while (StartOf(1)) {
			if (la.kind == 13) {
				Get();
				mods._const = true; 
			} else if (la.kind == 14) {
				Get();
				mods._global = true; 
			} else if (la.kind == 15) {
				Get();
				mods._static = true; 
			} else if (la.kind == 16) {
				Get();
				mods._override = true; 
			} else {
				Get();
				mods._guarded = true; 
			}
		}
		if (la.kind == 1 || la.kind == 8) {
			TypeSpecifier(ref type);
		} else if (la.kind == 9) {
			Get();
			type = new TypeRef("void"); 
		} else SynErr(79);
		if (null != type) type.SetConst(mods._const); 
		Ident(ref sym);
		if (la.kind == 12 || la.kind == 18 || la.kind == 19) {
			expr = new Expr_Set(this, type, sym, mods); 
			if (la.kind == 12 || la.kind == 18) {
				if (la.kind == 12) {
					Get();
					AssignExpr(ref init);
					((Expr_Set)expr).SetValue(init); 
					if (init is Expr_New && null == ((Expr_New)init).typeRef)
					((Expr_New)init).typeRef = type;
					
				} else {
					Get();
					Expr(ref script);
					expr = new Expr_ScriptToValue(this, expr, script); 
				}
			}
			Expect(19);
		} else if (la.kind == 10) {
			Get();
			if (la.kind == 1 || la.kind == 8) {
				FunctionDeclArg(ref argTypes, ref argNames, ref defaultValues);
				while (la.kind == 5) {
					Get();
					FunctionDeclArg(ref argTypes, ref argNames, ref defaultValues);
				}
			}
			Expect(11);
			EmbeddedStatementBlock(ref body);
			expr = Expr_Set.CreateFunctionLiteral(this, type, sym, argTypes, defaultValues, argNames, body, mods); 
		} else SynErr(80);
	}

	void Ident(ref string id) {
		Expect(1);
		id = t.val; 
	}

	void AssignExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null; 
		CondExpr(ref expr1);
		expr = expr1; 
		if (StartOf(2)) {
			switch (la.kind) {
			case 12: {
				Get();
				Expr(ref expr2);
				expr = new Expr_Assign(this, expr1, expr2); 
				break;
			}
			case 20: {
				Get();
				Expr(ref expr2);
				expr = new Expr_Assign(this, expr1, new Expr_BinOp(this, Expr_BinOp.OP.ADD, expr1, expr2)); 
				break;
			}
			case 21: {
				Get();
				Expr(ref expr2);
				expr = new Expr_Assign(this, expr1, new Expr_BinOp(this, Expr_BinOp.OP.SUB, expr1, expr2)); 
				break;
			}
			case 22: {
				Get();
				Expr(ref expr2);
				expr = new Expr_Assign(this, expr1, new Expr_BinOp(this, Expr_BinOp.OP.MULT, expr1, expr2)); 
				break;
			}
			case 23: {
				Get();
				Expr(ref expr2);
				expr = new Expr_Assign(this, expr1, new Expr_BinOp(this, Expr_BinOp.OP.DIV, expr1, expr2)); 
				break;
			}
			case 24: {
				Get();
				Expr(ref expr2);
				expr = new Expr_Assign(this, expr1, new Expr_BinOp(this, Expr_BinOp.OP.CONCAT, expr1, expr2)); 
				break;
			}
			case 25: {
				Get();
				Expr(ref expr2);
				expr = new Expr_Stream(this, expr1, expr2); 
				break;
			}
			}
		}
	}

	void Expr(ref IExpr expr) {
		AssignExpr(ref expr);
	}

	void EmbeddedStatementBlock(ref IExpr exprBlock) {
		Expr_ExprList block = new Expr_ExprList(this);
		exprBlock = block;
		IExpr expr = null; 
		
		Expect(65);
		while (StartOf(3)) {
			EmbeddedStat(ref expr);
			if (null != expr) block.nodes.Add(expr); expr = null; 
		}
		Expect(67);
	}

	void CondExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null, expr3 = null; 
		LogOrExpr(ref expr1);
		expr = expr1; 
		if (la.kind == 7) {
			Get();
			Expr(ref expr2);
			Expect(26);
			CondExpr(ref expr3);
			expr = new Expr_Conditional(this, expr1, expr2, expr3); 
		}
	}

	void LogOrExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null; 
		LogAndExpr(ref expr1);
		expr = expr1; 
		while (la.kind == 27) {
			Get();
			LogAndExpr(ref expr2);
			expr = new Expr_BinOp(this, Expr_BinOp.OP.OR, expr, expr2); 
		}
	}

	void LogAndExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null; 
		EqlExpr(ref expr1);
		expr = expr1; 
		while (la.kind == 28) {
			Get();
			EqlExpr(ref expr2);
			expr = new Expr_BinOp(this, Expr_BinOp.OP.AND, expr, expr2); 
		}
	}

	void EqlExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null; string sym = null; 
		RelExpr(ref expr1);
		expr = expr1; 
		while (StartOf(4)) {
			if (la.kind == 29) {
				Get();
				RelExpr(ref expr2);
				expr = new Expr_Compare(this, expr, expr2, false); 
			} else if (la.kind == 30) {
				Get();
				RelExpr(ref expr2);
				expr = new Expr_Compare(this, expr, expr2, true);  
			} else if (la.kind == 18) {
				Get();
				RelExpr(ref expr2);
				expr = new Expr_ScriptToValue(this, expr, expr2);	
			} else {
				Get();
				Ident(ref sym);
				expr = new Expr_Is(this, expr1, sym); 
			}
		}
	}

	void RelExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null; 
		AddExpr(ref expr1);
		expr = expr1; 
		while (StartOf(5)) {
			if (la.kind == 4) {
				Get();
				AddExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.LT, expr, expr2); 
			} else if (la.kind == 6) {
				Get();
				AddExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.GT, expr, expr2); 
			} else if (la.kind == 32) {
				Get();
				AddExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.LEQ, expr, expr2); 
			} else if (la.kind == 33) {
				Get();
				AddExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.GEQ, expr, expr2); 
			} else {
				Get();
				AddExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.STREQI, expr, expr2); 
			}
		}
	}

	void AddExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null; 
		MultExpr(ref expr1);
		expr = expr1; 
		while (la.kind == 35 || la.kind == 36 || la.kind == 37) {
			if (la.kind == 35) {
				Get();
				MultExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.ADD, expr, expr2); 
			} else if (la.kind == 36) {
				Get();
				MultExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.SUB, expr, expr2); 
			} else {
				Get();
				MultExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.CONCAT, expr, expr2); 
			}
		}
	}

	void MultExpr(ref IExpr expr) {
		IExpr expr1 = null, expr2 = null; 
		CastExpr(ref expr1);
		expr = expr1; 
		while (StartOf(6)) {
			if (la.kind == 38) {
				Get();
				CastExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.MULT, expr, expr2); 
			} else if (la.kind == 39) {
				Get();
				CastExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.DIV, expr, expr2); 
			} else if (la.kind == 40) {
				Get();
				CastExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.MOD, expr, expr2); 
			} else {
				Get();
				CastExpr(ref expr2);
				expr = new Expr_BinOp(this, Expr_BinOp.OP.POW, expr, expr2); 
			}
		}
	}

	void CastExpr(ref IExpr expr) {
		IExpr expr1 = null; string sym = null; 
		UnaryExpr(ref expr1);
		expr = expr1; 
		if (la.kind == 42) {
			Get();
			Ident(ref sym);
			expr = new Expr_As(this, expr1, sym); 
		}
	}

	void UnaryExpr(ref IExpr expr) {
		List<int> li = new List<int>(); 
		while (StartOf(7)) {
			if (la.kind == 43) {
				Get();
				li.Add(0); 
			} else if (la.kind == 44) {
				Get();
				li.Add(1); 
			} else if (la.kind == 45) {
				Get();
				li.Add(2); 
			} else {
				Get();
				li.Add(3); 
			}
		}
		UnaryPost(ref expr);
		for (int i = li.Count - 1; i >= 0; --i) {
		if (0 == li[i])
		expr = Expr_Assign.CreateInc(this, expr); 
		else if (1 == li[i])
		expr = Expr_Assign.CreateDec(this, expr); 
		else if (2 == li[i])
		expr = new Expr_Length(this, expr); 
		else if (3 == li[i])
		expr = new Expr_UnOp(this, Expr_UnOp.OP.TOSTRING, expr);	
		}
		
	}

	void UnaryPost(ref IExpr expr) {
		
		if (StartOf(8)) {
			PostfixExpr(ref expr);
		} else if (la.kind == 35) {
			Get();
			CastExpr(ref expr);
			expr = new Expr_UnOp(this, Expr_UnOp.OP.POS, expr); 
		} else if (la.kind == 36) {
			Get();
			CastExpr(ref expr);
			expr = new Expr_UnOp(this, Expr_UnOp.OP.NEG, expr); 
		} else if (la.kind == 47) {
			Get();
			CastExpr(ref expr);
			expr = new Expr_UnOp(this, Expr_UnOp.OP.NOT, expr); 
		} else SynErr(81);
	}

	void PostfixExpr(ref IExpr expr) {
		ExprList args = null; IExpr indexExpr = null; 
		Primary(ref expr);
		while (StartOf(9)) {
			if (la.kind == 48) {
				Get();
				Expr(ref indexExpr);
				Expect(49);
				expr = new Expr_Index(this, expr, indexExpr); 
			} else if (la.kind == 50) {
				Get();
				Expect(1);
				expr = new Expr_Dot(this, expr, t.val); 
			} else if (la.kind == 10) {
				Get();
				if (StartOf(10)) {
					ArgExprList(ref args);
				}
				Expect(11);
				expr = new Expr_Call(this, expr, args); args = null; 
			} else if (la.kind == 43) {
				Get();
				expr = new Expr_Postrement(this, expr, false); 
			} else {
				Get();
				expr = new Expr_Postrement(this, expr, true); 
			}
		}
	}

	void Primary(ref IExpr expr) {
		string className = null; IExpr exprBlock = null; ITypeRef valType = null; IExpr initializer = null; 
		if (IsScopeOpStart()) {
			Ident(ref className);
			Expect(51);
			Expect(1);
			expr = new Expr_Scope(this, className, t.val); 
		} else if (la.kind == 51) {
			Get();
			Expect(1);
			expr = new Expr_Scope(this, null, t.val); 
		} else if (la.kind == 1) {
			Get();
			expr = new Expr_Symbol(this, t.val); 
		} else if (la.kind == 52) {
			Get();
			expr = new Expr_This(this); 
		} else if (la.kind == 53) {
			Get();
			EmbeddedStatementBlock(ref exprBlock);
			expr = new Expr_Catch(this, exprBlock); 
		} else if (StartOf(11)) {
			Literal(ref expr);
		} else if (la.kind == 10) {
			Get();
			Expr(ref expr);
			Expect(11);
		} else if (la.kind == 54) {
			Get();
			if (la.kind == 1) {
				TypeSpecifierNoFunc(ref valType);
			}
			if (la.kind == 65) {
				EmbeddedStatementBlock(ref initializer);
			}
			expr = new Expr_New(this, valType, initializer); 
		} else SynErr(82);
	}

	void ArgExprList(ref ExprList args) {
		args = new ExprList(); IExpr arg1 = null, arg2 = null; 
		AssignExpr(ref arg1);
		args.Add(arg1); 
		while (la.kind == 5) {
			Get();
			AssignExpr(ref arg2);
			args.Add(arg2); 
		}
	}

	void ForExpr(ref IExpr expr) {
		string sym = null; IExpr minExpr = null; IExpr maxExpr = null; IExpr body = null; IExpr stepExpr = null; 
		Expect(58);
		Expect(10);
		Ident(ref sym);
		Expect(12);
		Expr(ref minExpr);
		Expect(5);
		Expr(ref maxExpr);
		if (la.kind == 5) {
			Get();
			Expr(ref stepExpr);
		}
		Expect(11);
		ForOrIfStat(ref body);
		expr = new Expr_For(this, sym, minExpr, maxExpr, stepExpr, body); 
	}

	void ForOrIfStat(ref IExpr expr) {
		Expr_If ifExpr = null; IExpr cond = null; IExpr trueCase = null; IExpr falseCase = null; 
		switch (la.kind) {
		case 65: {
			EmbeddedStatementBlock(ref expr);
			break;
		}
		case 1: case 2: case 3: case 10: case 35: case 36: case 43: case 44: case 45: case 46: case 47: case 51: case 52: case 53: case 54: case 55: case 56: case 57: {
			Expr(ref expr);
			Expect(19);
			break;
		}
		case 70: {
			Get();
			Expect(10);
			Expr(ref cond);
			Expect(11);
			EmbeddedStat(ref trueCase);
			ifExpr = new Expr_If(this, cond, trueCase); expr = ifExpr; 
			if (la.kind == 71) {
				Get();
				EmbeddedStat(ref falseCase);
				ifExpr.falseCase = falseCase; 
			}
			break;
		}
		case 58: {
			ForExpr(ref expr);
			break;
		}
		case 59: {
			ForEachExpr(ref expr);
			break;
		}
		case 72: {
			Get();
			Expect(19);
			expr = new Expr_Break(this); 
			break;
		}
		case 73: {
			Get();
			Expect(19);
			expr = new Expr_Continue(this); 
			break;
		}
		case 74: {
			Get();
			if (StartOf(10)) {
				Expr(ref cond);
			}
			Expect(19);
			expr = new Expr_Return(this, cond); 
			break;
		}
		case 19: {
			Get();
			break;
		}
		default: SynErr(83); break;
		}
	}

	void ForEachExpr(ref IExpr expr) {
		string kIdent = null, vIdent = null; IExpr sym = null, body = null; 
		Expect(59);
		Expect(10);
		Ident(ref kIdent);
		Expect(5);
		Ident(ref vIdent);
		Expect(60);
		Expr(ref sym);
		Expect(11);
		ForOrIfStat(ref body);
		Expr_ForEach forEachExpr = new Expr_ForEach(this, sym, kIdent, vIdent, body); expr = forEachExpr; 
	}

	void TypeAliasStat(ref IExpr expr) {
		ITypeRef typeRef = null; string ident = null; 
		Expect(61);
		Ident(ref ident);
		Expect(12);
		TypeSpecifier(ref typeRef);
		Expr_TypeAlias taExpr = new Expr_TypeAlias(this, ident, typeRef); expr = taExpr; 
	}

	void Class(ref IExpr expr) {
		Expr_Class scope = null; bool isSealed = false; bool isUninstantiable = false; IExpr memberDec = null; IExpr block = null; 
		while (la.kind == 62 || la.kind == 63) {
			if (la.kind == 62) {
				Get();
				isSealed = true; 
			} else {
				Get();
				isUninstantiable = true; 
			}
		}
		Expect(64);
		Expect(1);
		scope = new Expr_Class(this, t.val); expr = scope; scope.isSealed = isSealed; scope.isUninstantiable = isUninstantiable; 
		if (la.kind == 26) {
			Get();
			Expect(1);
			scope.parent = t.val; 
		}
		if (la.kind == 65) {
			Get();
			while (StartOf(12)) {
				if (StartOf(13)) {
					Decl(ref memberDec);
					scope.AddMember((Expr_Set) memberDec); 
				} else {
					Get();
					EmbeddedStatementBlock(ref block);
					scope.SetConstructor(context, block); 
				}
			}
			Expect(67);
		}
		Expect(19);
	}

	void Enum(ref IExpr expr) {
		IExpr initializer = null; Expr_Enum e = null; ITypeRef enumType = null; string exprName; string valName = null;
		Expect(68);
		Expect(4);
		TypeSpecifier(ref enumType);
		Expect(6);
		Expect(1);
		exprName = t.val; e = new Expr_Enum(this, exprName, enumType); expr = e; 
		Expect(65);
		if (la.kind == 1) {
			Ident(ref valName);
			if (la.kind == 12) {
				Get();
				Expr(ref initializer);
			}
			e.AddValue(valName, initializer); initializer = null; 
			while (NotFinalComma()) {
				Expect(5);
				Ident(ref valName);
				if (la.kind == 12) {
					Get();
					Expr(ref initializer);
				}
				e.AddValue(valName, initializer); initializer = null; 
			}
		}
		if (la.kind == 5) {
			Get();
		}
		Expect(67);
	}

	void Assert(ref IExpr expr) {
		IExpr conditionExpr = null; IExpr messageExpr = null; IExpr block = null; 
		Expect(69);
		Expect(10);
		Expr(ref conditionExpr);
		while (la.kind == 5) {
			Get();
			Expr(ref messageExpr);
		}
		Expect(11);
		if (la.kind == 65) {
			StatBlock(ref block);
		} else if (la.kind == 19) {
			Get();
		} else SynErr(84);
		#if PEBBLE_ASSERTOFF
			expr = new Expr_Literal(this, true, IntrinsicTypeDefs.BOOL);
		#else
			expr = new Expr_Assert(this, conditionExpr, messageExpr, block, true);
		#endif
		
	}

	void StatBlock(ref IExpr exprBlock) {
		Expr_ExprList block = new Expr_ExprList(this);
		exprBlock = block;
		IExpr expr = null; 
		
		Expect(65);
		while (StartOf(14)) {
			Stat(ref expr);
			if (null != expr) block.nodes.Add(expr); expr = null; 
		}
		Expect(67);
	}

	void Stat(ref IExpr expr) {
		Expr_If ifExpr = null; IExpr cond = null; IExpr trueCase = null; IExpr falseCase = null; 
		switch (la.kind) {
		case 65: {
			StatBlock(ref expr);
			break;
		}
		case 62: case 63: case 64: {
			Class(ref expr);
			break;
		}
		case 68: {
			Enum(ref expr);
			break;
		}
		case 70: {
			Get();
			Expect(10);
			Expr(ref cond);
			Expect(11);
			ForOrIfStat(ref trueCase);
			ifExpr = new Expr_If(this, cond, trueCase); expr = ifExpr; 
			if (la.kind == 71) {
				Get();
				ForOrIfStat(ref falseCase);
				ifExpr.falseCase = falseCase; 
			}
			break;
		}
		case 58: {
			ForExpr(ref expr);
			break;
		}
		case 59: {
			ForEachExpr(ref expr);
			break;
		}
		case 69: {
			Assert(ref expr);
			break;
		}
		case 72: {
			Get();
			Expect(19);
			expr = new Expr_Break(this); 
			break;
		}
		case 73: {
			Get();
			Expect(19);
			expr = new Expr_Continue(this); 
			break;
		}
		case 74: {
			Get();
			if (StartOf(10)) {
				Expr(ref cond);
			}
			Expect(19);
			expr = new Expr_Return(this, cond); 
			break;
		}
		case 61: {
			TypeAliasStat(ref expr);
			Expect(19);
			break;
		}
		case 1: case 2: case 3: case 8: case 9: case 10: case 13: case 14: case 15: case 16: case 17: case 35: case 36: case 43: case 44: case 45: case 46: case 47: case 51: case 52: case 53: case 54: case 55: case 56: case 57: {
			if (IsDecl()) {
				Decl(ref expr);
			} else {
				Expr(ref expr);
				Expect(19);
			}
			break;
		}
		case 19: {
			Get();
			break;
		}
		default: SynErr(85); break;
		}
	}

	void EmbeddedStat(ref IExpr expr) {
		Expr_If ifExpr = null; IExpr cond = null; IExpr trueCase = null; IExpr falseCase = null; 
		switch (la.kind) {
		case 65: {
			EmbeddedStatementBlock(ref expr);
			break;
		}
		case 1: case 2: case 3: case 8: case 9: case 10: case 13: case 14: case 15: case 16: case 17: case 35: case 36: case 43: case 44: case 45: case 46: case 47: case 51: case 52: case 53: case 54: case 55: case 56: case 57: {
			if (IsDecl()) {
				Decl(ref expr);
			} else {
				Expr(ref expr);
				Expect(19);
			}
			break;
		}
		case 70: {
			Get();
			Expect(10);
			Expr(ref cond);
			Expect(11);
			ForOrIfStat(ref trueCase);
			ifExpr = new Expr_If(this, cond, trueCase); expr = ifExpr; 
			if (la.kind == 71) {
				Get();
				ForOrIfStat(ref falseCase);
				ifExpr.falseCase = falseCase; 
			}
			break;
		}
		case 58: {
			ForExpr(ref expr);
			break;
		}
		case 59: {
			ForEachExpr(ref expr);
			break;
		}
		case 69: {
			Assert(ref expr);
			break;
		}
		case 72: {
			Get();
			Expect(19);
			expr = new Expr_Break(this); 
			break;
		}
		case 73: {
			Get();
			Expect(19);
			expr = new Expr_Continue(this); 
			break;
		}
		case 74: {
			Get();
			if (StartOf(10)) {
				Expr(ref cond);
			}
			Expect(19);
			expr = new Expr_Return(this, cond); 
			break;
		}
		case 19: {
			Get();
			break;
		}
		default: SynErr(86); break;
		}
	}

	void Pebble() {
		Expr_ExprList list = null; IExpr _nextExpr = null; 
		while (StartOf(14)) {
			Stat(ref _nextExpr);
			if (null != _nextExpr) {
			if (null == _headExpr)
				_headExpr = _nextExpr;
			else {
				if (null == list) {
					list = new Expr_ExprList(this);
					list.createScope = false;
					list.nodes.Add(_headExpr);
					_headExpr = list;
				}
				list.nodes.Add(_nextExpr);
			}
			_nextExpr = null;
			}
			
		}
	}


	// *** productions end ****************************************************

	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		// *** parseRoot ******************************************************
		Pebble();
		Expect(0);

		// *** parseRoot end **************************************************
	}
	
	static readonly bool[,] set = {
		// *** initialization *************************************************
		{_T,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _T,_x,_x,_x, _x,_x,_x,_x, _T,_T,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _T,_T,_T,_x, _x,_T,_T,_T, _T,_T,_x,_T, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_x,_x,_x, _x,_x,_x,_T, _T,_T,_T,_T, _x,_x,_x,_T, _T,_T,_T,_T, _T,_T,_T,_T, _x,_x,_x,_x, _x,_T,_x,_x, _x,_T,_T,_x, _T,_T,_T,_x, _x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_T,_T, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_x,_x,_x, _T,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _T,_T,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_T,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_T,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_x,_x,_x, _T,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_x,_x,_x, _x,_x,_x,_T, _T,_T,_T,_T, _x,_x,_x,_T, _T,_T,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_x,_T,_T, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_T,_x,_x, _x,_x,_x,_x, _T,_T,_x,_x, _x,_T,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_T,_x,_x, _x,_x,_x,_x, _T,_T,_x,_x, _x,_T,_T,_T, _T,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _T,_T,_T,_x, _x,_T,_T,_T, _T,_T,_x,_T, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_x,_x,_x, _x,_x,_x,_T, _T,_T,_T,_T, _x,_x,_x,_T, _T,_T,_T,_T, _T,_T,_T,_T, _x,_T,_T,_T, _T,_T,_x,_x, _T,_T,_T,_x, _T,_T,_T,_x, _x}

		// *** initialization end *********************************************
	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	public List<ParseErrorInst> errors = new List<ParseErrorInst>();

	public virtual void SynErr (string scriptName, int line, int col, int n) {
		string s;
		switch (n) {
			// *** errors *****************************************************
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "number expected"; break;
			case 3: s = "StringLiteral expected"; break;
			case 4: s = "\"<\" expected"; break;
			case 5: s = "\",\" expected"; break;
			case 6: s = "\">\" expected"; break;
			case 7: s = "\"?\" expected"; break;
			case 8: s = "\"functype\" expected"; break;
			case 9: s = "\"void\" expected"; break;
			case 10: s = "\"(\" expected"; break;
			case 11: s = "\")\" expected"; break;
			case 12: s = "\"=\" expected"; break;
			case 13: s = "\"const\" expected"; break;
			case 14: s = "\"global\" expected"; break;
			case 15: s = "\"static\" expected"; break;
			case 16: s = "\"override\" expected"; break;
			case 17: s = "\"guarded\" expected"; break;
			case 18: s = "\"<-\" expected"; break;
			case 19: s = "\";\" expected"; break;
			case 20: s = "\"+=\" expected"; break;
			case 21: s = "\"-=\" expected"; break;
			case 22: s = "\"*=\" expected"; break;
			case 23: s = "\"/=\" expected"; break;
			case 24: s = "\"..=\" expected"; break;
			case 25: s = "\"<<\" expected"; break;
			case 26: s = "\":\" expected"; break;
			case 27: s = "\"||\" expected"; break;
			case 28: s = "\"&&\" expected"; break;
			case 29: s = "\"==\" expected"; break;
			case 30: s = "\"!=\" expected"; break;
			case 31: s = "\"is\" expected"; break;
			case 32: s = "\"<=\" expected"; break;
			case 33: s = "\">=\" expected"; break;
			case 34: s = "\"~=\" expected"; break;
			case 35: s = "\"+\" expected"; break;
			case 36: s = "\"-\" expected"; break;
			case 37: s = "\"..\" expected"; break;
			case 38: s = "\"*\" expected"; break;
			case 39: s = "\"/\" expected"; break;
			case 40: s = "\"%\" expected"; break;
			case 41: s = "\"**\" expected"; break;
			case 42: s = "\"as\" expected"; break;
			case 43: s = "\"++\" expected"; break;
			case 44: s = "\"--\" expected"; break;
			case 45: s = "\"#\" expected"; break;
			case 46: s = "\"$\" expected"; break;
			case 47: s = "\"!\" expected"; break;
			case 48: s = "\"[\" expected"; break;
			case 49: s = "\"]\" expected"; break;
			case 50: s = "\".\" expected"; break;
			case 51: s = "\"::\" expected"; break;
			case 52: s = "\"this\" expected"; break;
			case 53: s = "\"catch\" expected"; break;
			case 54: s = "\"new\" expected"; break;
			case 55: s = "\"true\" expected"; break;
			case 56: s = "\"false\" expected"; break;
			case 57: s = "\"null\" expected"; break;
			case 58: s = "\"for\" expected"; break;
			case 59: s = "\"foreach\" expected"; break;
			case 60: s = "\"in\" expected"; break;
			case 61: s = "\"typealias\" expected"; break;
			case 62: s = "\"sealed\" expected"; break;
			case 63: s = "\"uninstantiable\" expected"; break;
			case 64: s = "\"class\" expected"; break;
			case 65: s = "\"{\" expected"; break;
			case 66: s = "\"constructor\" expected"; break;
			case 67: s = "\"}\" expected"; break;
			case 68: s = "\"enum\" expected"; break;
			case 69: s = "\"assert\" expected"; break;
			case 70: s = "\"if\" expected"; break;
			case 71: s = "\"else\" expected"; break;
			case 72: s = "\"break\" expected"; break;
			case 73: s = "\"continue\" expected"; break;
			case 74: s = "\"return\" expected"; break;
			case 75: s = "??? expected"; break;
			case 76: s = "invalid TypeSpecifier"; break;
			case 77: s = "invalid FunctionType"; break;
			case 78: s = "invalid Literal"; break;
			case 79: s = "invalid Decl"; break;
			case 80: s = "invalid Decl"; break;
			case 81: s = "invalid UnaryPost"; break;
			case 82: s = "invalid Primary"; break;
			case 83: s = "invalid ForOrIfStat"; break;
			case 84: s = "invalid Assert"; break;
			case 85: s = "invalid Stat"; break;
			case 86: s = "invalid EmbeddedStat"; break;

			// *** errors end *************************************************
			default: s = "error " + n; break;
		}
		//errorStream.WriteLine(errMsgFormat, line, col, s);
		// PRC: I made this!
		errors.Add(new ParseErrorInst(ParseErrorType.SyntaxError, scriptName + " [" + line + ":" + col + "] " + ParseErrorType.SyntaxError.ToString() + ": " + s));
		count++;
	}

	/* PRC: I'm not using these.
	public virtual void SemErr (string scriptName, int line, int col, string s) {
		//errorStream.WriteLine(errMsgFormat, line, col, s);
		// PRC: I made this!
		errors.Add(new ParseErrorInst(ParseErrorType.SemanticError, String.Format(errMsgFormat, scriptName, line, col, ParseErrorType.SemanticError.ToString(), s)));
		count++;
	}
	
	public virtual void SemErr (string scriptName, string s) {
		//errorStream.WriteLine(s);
		// PRC: I made this!
		errors.Add(new ParseErrorInst(ParseErrorType.SemanticError, scriptName + " " + ParseErrorType.SemanticError.ToString() + ": " + s));
		count++;
	}
	
	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
	*/
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
}