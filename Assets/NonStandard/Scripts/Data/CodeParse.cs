﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace NonStandard.Data {
	public struct Token : IEquatable<Token>, IComparable<Token> {
		public int index, length; // 32 bits x2
		public object meta; // 64 bits
		public Token(object meta, int i, int len) { this.meta = meta; index = i; length = len; }
		public static Token None = new Token(null, -1, -1);
		public int BeginIndex { get { return index; } }
		public int EndIndex { get { return index + length; } }
		public string ToString(string s) { return s.Substring(index, length); }
		public override string ToString() {
			Context.Entry pce = meta as Context.Entry; if (pce != null) return pce.sourceText.Substring(index, length);
			return Resolve().ToString();
		}
		public object Resolve() {
			if (meta == null) throw new NullReferenceException();
			if (meta is string) return ToString((string)meta);
			TokenSubstitution ss = meta as TokenSubstitution; if (ss != null) return ss.value;
			Delim d = meta as Delim; if (d != null) return d.text;
			Context.Entry pce = meta as Context.Entry; if (pce != null) return pce.Resolve();
			throw new DecoderFallbackException();
		}
		public string AsBasicToken { get { if (meta is string) { return ((string)meta).Substring(index, length); } return null; } }
		public Delim AsDelimiter { get { return meta as Delim; } }
		public Context.Entry AsContextEntry { get { return meta as Context.Entry; } }
		public bool IsContextBeginning { get { Context.Entry ctx = AsContextEntry; if (ctx != null) { return ctx.BeginToken == this; } return false; } }
		public bool IsContextEnding { get { Context.Entry ctx = AsContextEntry; if (ctx != null) { return ctx.EndToken == this; } return false; } }
		public bool IsValid { get { return index >= 0 && length >= 0; } }
		public bool Equals(Token other) { return index == other.index && length == other.length && meta == other.meta; }
		public override bool Equals(object obj) { if (obj is Token) return Equals((Token)obj); return false; }
		public override int GetHashCode() { return meta.GetHashCode() ^ index ^ length; }
		public int CompareTo(Token other) {
			int comp = index.CompareTo(other.index);
			if (comp != 0) return comp;
			return -length.CompareTo(other.length); // bigger one should go first
		}
		public static bool operator ==(Token lhs, Token rhs) { return lhs.Equals(rhs); }
		public static bool operator !=(Token lhs, Token rhs) { return !lhs.Equals(rhs); }
	}
	public struct ParseResult {
		/// <summary>
		/// how much text was resolved (no longer needs to be parsed)
		/// </summary>
		public int lengthParsed;
		/// <summary>
		/// what to replace this delimiter (and all characters until newIndex)
		/// </summary>
		public object replacementValue;
		/// <summary>
		/// null unless there was an error processing this delimeter
		/// </summary>
		public CodeConvert.Err error;
		public bool IsError { get { return !string.IsNullOrEmpty(error.message); } }
		public ParseResult(int length, object value, string err = null, int r = 0, int c = 0) {
			lengthParsed = length; replacementValue = value; error = new CodeConvert.Err(r, c, err);
		}
		public ParseResult AddToLength(int count) { lengthParsed += count; error.col += count; return this; }
		public ParseResult ForceCharSubstitute() { replacementValue = Convert.ToChar(replacementValue); return this; }
		public ParseResult SetError(string errorMessage, int row=0, int col=0) {
			error = new CodeConvert.Err(row, col, errorMessage); return this;
		}
	}
	public class TokenSubstitution {
		public string orig; public object value;
		public TokenSubstitution(string o, object v) { orig = o; value = v; }
	}
	public class DelimCtx : Delim {
		public Context Context {
			get {
				return foundContext != null ? foundContext
: Context.allContexts.TryGetValue(contextName, out foundContext) ? foundContext : null;
			}
		}
		private Context foundContext = null;
		public string contextName;
		public bool isStart, isEnd;
		public DelimCtx(string delim, string name = null, string desc = null, Func<String, int, ParseResult> parseRule = null,
			string ctx = null, bool s = false, bool e = false)
			: base(delim, name, desc, parseRule) {
			contextName = ctx; isStart = s; isEnd = e;
		}
	}
	public class Delim : IComparable<Delim> {
		public string text, name, description;
		public Func<string, int, ParseResult> parseRule = null;
		public Delim(string delim, string name = null, string desc = null, Func<string, int, ParseResult> parseRule = null) {
			this.text = delim; this.name = name; description = desc; this.parseRule = parseRule;
		}
		public bool IsAt(string str, int index) {
			if (index + text.Length > str.Length) { return false; }
			for (int i = 0; i < text.Length; ++i) {
				if (text[i] != str[index + i]) return false;
			}
			return true;
		}
		public override string ToString() { return text; }
		public static implicit operator Delim(string s) { return new Delim(s); }

		public static Delim[] CombineDelims(params Delim[][] delimGroups) {
			List<Delim> delims = new List<Delim>();
			for (int i = 0; i < delimGroups.Length; ++i) { delims.AddRange(delimGroups[i]); }
			delims.Sort();
			return delims.ToArray();
		}
		public int CompareTo(Delim other) {
			if (text.Length > other.text.Length) return -1;
			if (text.Length < other.text.Length) return 1;
			return text.CompareTo(other.text);
		}
		public static ParseResult HexadecimalParse(string str, int index) {
			return NumberParse(str, index + 2, 16, false);
		}
		public static ParseResult NumericParse(string str, int index) {
			return NumberParse(str, index, 10, true);
		}
		public static ParseResult IntegerParse(string str, int index) {
			return NumberParse(str, index, 10, false);
		}
		public static int NumericValue(char c) {
			if (c >= '0' && c <= '9') return c - '0';
			if (c >= 'A' && c <= 'Z') return (c - 'A') + 10;
			if (c >= 'a' && c <= 'z') return (c - 'a') + 10;
			return -1;
		}
		public static bool IsValidNumber(char c, int numberBase) {
			int h = NumericValue(c);
			return h >= 0 && h < numberBase;
		}
		public static int CountDigitsAt(string str, int index, int numberBase) {
			int numDigits = 0;
			while (index + numDigits < str.Length && IsValidNumber(str[index + numDigits], numberBase)) { numDigits++; }
			return numDigits;
		}
		public static ParseResult NumberParse(string str, int index, int numberBase, bool includeDecimal) {
			return NumberParse(str, index, CountDigitsAt(str, index, numberBase), numberBase, includeDecimal);
		}
		public static ParseResult NumberParse(string str, int index, int numDigits, int numberBase, bool includeDecimal) {
			ParseResult pr = new ParseResult(0, null);
			long sum = 0;
			int b = 1, onesPlace = index + numDigits - 1;
			for (int i = 0; i < numDigits; ++i) {
				sum += NumericValue(str[onesPlace - i]) * b;
				b *= numberBase;
			}
			pr.replacementValue = (sum < int.MaxValue) ? (int)sum : sum;
			pr.lengthParsed = numDigits;
			double fraction = 0;
			int numFractionDigits = 0;
			if (includeDecimal && index + numDigits + 1 < str.Length && str[index + numDigits] == '.') {
				int frac = onesPlace + 2;
				while (frac + numFractionDigits < str.Length &&
					IsValidNumber(str[frac + numFractionDigits], numberBase)) { numFractionDigits++; }
				if (numFractionDigits == 0) { pr.SetError("decimal point with no subsequent digits", 0, index); }
				b = numberBase;
				for (int i = 0; i < numFractionDigits; ++i) {
					fraction += NumericValue(str[frac + i]) / (double)b;
					b *= numberBase;
				}
				pr.replacementValue = (fraction + sum);
				pr.lengthParsed = numDigits + 1 + numFractionDigits;
			}
			return pr;
		}
		public static ParseResult CommentEscape(string str, int index) { return UnescapeString(str, index); }

		public static ParseResult UnescapeString(string str, int index) {
			ParseResult r = new ParseResult(0, null); // by default, nothing happened
			if (str.Length <= index) { return r.SetError("invalid arguments"); }
			if (str[index] != '\\') { return r.SetError("expected escape sequence starting with '\\'"); }
			if (str.Length <= index + 1) { return r.SetError("unable to parse escape sequence at end of string", 0,1); }
			char c = str[index + 1];
			switch (c) {
			case '\n': return new ParseResult(index + 2, "");
			case '\r':
				if (str.Length <= index + 2 || str[index + 2] != '\n') {
					return new ParseResult(index, "", "expected windows line ending", 0, 2);
				}
				return new ParseResult(index + 3, "");
			case 'a': return new ParseResult(2, "\a");
			case 'b': return new ParseResult(2, "\b");
			case 'e': return new ParseResult(2, ((char)27).ToString());
			case 'r': return new ParseResult(2, "\r");
			case 'f': return new ParseResult(2, "\f");
			case 'n': return new ParseResult(2, "\n");
			case 't': return new ParseResult(2, "\t");
			case 'v': return new ParseResult(2, "\v");
			case '\\': return new ParseResult(2, "\\");
			case '\'': return new ParseResult(2, "\'");
			case '\"': return new ParseResult(2, "\"");
			case '?': return new ParseResult(2, "?");
			case 'x': return NumberParse(str, index + 2, 2, 16, false).AddToLength(2).ForceCharSubstitute();
			case 'u': return NumberParse(str, index + 2, 4, 16, false).AddToLength(2).ForceCharSubstitute();
			case 'U': return NumberParse(str, index + 2, 8, 16, false).AddToLength(2).ForceCharSubstitute();
			case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': {
				int digitCount = 1;
				do {
					if (str.Length <= index + digitCount + 1) break;
					c = str[index + digitCount + 1];
					if (c < '0' || c > '7') break;
					++digitCount;
				} while (digitCount < 3);
				return NumberParse(str, index + 1, digitCount, 8, false).AddToLength(1);
			}
			}
			return r.SetError("unknown escape sequence", 0,1);
		}

		private static void GiveDesc(Delim[] delims, string desc) {
			for (int i = 0; i < delims.Length; ++i) { if (delims[i].description == null) { delims[i].description = desc; } }
		}
		static Delim() {
			Type t = typeof(Delim);
			MemberInfo[] mInfo = t.GetMembers();
			for (int i = 0; i < mInfo.Length; ++i) {
				MemberInfo mi = mInfo[i];
				FieldInfo fi = mi as FieldInfo;
				if (fi != null && mi.Name.StartsWith("_") && fi.FieldType == typeof(Delim[]) && fi.IsStatic) {
					Delim[] delims = fi.GetValue(null) as Delim[];
					GiveDesc(delims, fi.Name.Substring(1).Replace('_', ' '));
				}
			}
		}
		public static Delim[] _string_delimiter = new Delim[] { new DelimCtx("\"", ctx: "string", s: true, e: true), };
		public static Delim[] _char_delimiter = new Delim[] { new DelimCtx("\'", ctx: "char", s: true, e: true), };
		public static Delim[] _char_escape_sequence = new Delim[] { new Delim("\\", parseRule: UnescapeString) };
		public static Delim[] _expression_delimiter = new Delim[] { new DelimCtx("(", ctx: "()", s: true), new DelimCtx(")", ctx: "()", e: true) };
		public static Delim[] _code_body_delimiter = new Delim[] { new DelimCtx("{", ctx: "{}", s: true), new DelimCtx("}", ctx: "{}", e: true) };
		public static Delim[] _square_brace_delimiter = new Delim[] { new DelimCtx("[", ctx: "[]", s: true), new DelimCtx("]", ctx: "[]", e: true) };
		public static Delim[] _triangle_brace_delimiter = new Delim[] { new DelimCtx("<", ctx: "<>", s: true), new DelimCtx(">", ctx: "<>", e: true) };
		public static Delim[] _ternary_operator_delimiter = new Delim[] { "?", ":", "??" };
		public static Delim[] _instruction_finished_delimiter = new Delim[] { ";" };
		public static Delim[] _list_item_delimiter = new Delim[] { "," };
		public static Delim[] _membership_operator = new Delim[] { new Delim(".", "member"), new Delim("->", "pointee"), new Delim("::", "scope resolution"), new Delim("?.", "null conditional") };
		public static Delim[] _prefix_unary_operator = new Delim[] { "++", "--", "!", "-", "~" };
		public static Delim[] _postfix_unary_operator = new Delim[] { "++", "--" };
		public static Delim[] _binary_operator = new Delim[] { "&", "|", "<<", ">>", "^" };
		public static Delim[] _binary_logic_operatpor = new Delim[] { "==", "!=", "<", ">", "<=", ">=", "&&", "||" };
		public static Delim[] _assignment_operator = new Delim[] { "+=", "-=", "*=", "/=", "%=", "|=", "&=", "<<=", ">>=", "??=", "=" };
		public static Delim[] _lambda_operator = new Delim[] { "=>" };
		public static Delim[] _math_operator = new Delim[] { "+", "-", "*", "/", "%" };
		public static Delim[] _hex_number_prefix = new Delim[] { new DelimCtx("0x", ctx: "0x", parseRule: HexadecimalParse) };
		public static Delim[] _number = new Delim[] {
			new DelimCtx("0",ctx:"number",parseRule:NumericParse),
			new DelimCtx("1",ctx:"number",parseRule:NumericParse),
			new DelimCtx("2",ctx:"number",parseRule:NumericParse),
			new DelimCtx("3",ctx:"number",parseRule:NumericParse),
			new DelimCtx("4",ctx:"number",parseRule:NumericParse),
			new DelimCtx("5",ctx:"number",parseRule:NumericParse),
			new DelimCtx("6",ctx:"number",parseRule:NumericParse),
			new DelimCtx("7",ctx:"number",parseRule:NumericParse),
			new DelimCtx("8",ctx:"number",parseRule:NumericParse),
			new DelimCtx("9",ctx:"number",parseRule:NumericParse) };
		public static Delim[] _block_comment_delimiter = new Delim[] { new DelimCtx("/*", ctx: "/**/", s: true), new DelimCtx("*/", ctx: "/**/", e: true) };
		public static Delim[] _line_comment_delimiter = new Delim[] { new DelimCtx("//", ctx: "//", s: true) };
		public static Delim[] _XML_line_comment_delimiter = new Delim[] { new DelimCtx("///", ctx: "///", s: true) };
		public static Delim[] _end_of_line_comment = new Delim[] { new DelimCtx("\n", ctx: "//", e: true) };
		public static Delim[] _erroneous_end_of_string = new Delim[] { new DelimCtx("\n", ctx: "string", e: true) };
		public static Delim[] _end_of_XML_line_comment = new Delim[] { new DelimCtx("\n", ctx: "///", e: true) };
		public static Delim[] _line_comment_continuation = new Delim[] { new Delim("\\", parseRule: CommentEscape) };
		public static Delim[] _data_keyword = new Delim[] { "null", "true", "false", "bool", "int", "short", "string", "long", "byte",
			"float", "double", "uint", "ushort", "sbyte", "char", "if", "else", "void", "var", "new", "as", };
		public static Delim[] _data_c_sharp_keyword = new Delim[] {
			"abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
			"const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
			"explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
			"implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
			"object", "operator", "out", "override", "params", "private", "protected", "public", "readonly",
			"ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
			"switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
			"ushort", "using", "virtual", "void", "volatile", "while"
		};
		public static Delim[] _DelimitersNone = new Delim[] { };
		public static char[] WhitespaceDefault = new char[] { ' ', '\t', '\n', '\r' };
		public static char[] WhitespaceNone = new char[] { };

		public static Delim[] CharLiteralDelimiters = CombineDelims(_char_escape_sequence, _char_delimiter);
		public static Delim[] StringLiteralDelimiters = CombineDelims(_char_escape_sequence, _string_delimiter, _erroneous_end_of_string);
		public static Delim[] StandardDelimiters = CombineDelims(_string_delimiter, _char_delimiter,
			_expression_delimiter, _code_body_delimiter, _square_brace_delimiter, _ternary_operator_delimiter,
			_instruction_finished_delimiter, _list_item_delimiter, _membership_operator, _prefix_unary_operator,
			_binary_operator, _binary_logic_operatpor, _assignment_operator, _lambda_operator, //_math_operator,
			_block_comment_delimiter, _line_comment_delimiter, _number);
		public static Delim[] LineCommentDelimiters = CombineDelims(_line_comment_continuation, _end_of_line_comment);
		public static Delim[] XmlCommentDelimiters = CombineDelims(_line_comment_continuation,
			_end_of_XML_line_comment);
		public static Delim[] CommentBlockDelimiters = CombineDelims(_block_comment_delimiter);
	}
	public class Context {
		public static Dictionary<string, Context> allContexts = new Dictionary<string, Context>();
		public static Context
			Default = new Context("default"),
			String = new Context("string"),
			Char = new Context("char"),
			Number = new Context("number"),
			Hexadecimal = new Context("0x"),
			Expression = new Context("()"),
			SquareBrace = new Context("[]"),
			GenericArgs = new Context("<>"),
			XmlCommentLine = new Context("///"),
			CommentLine = new Context("//"),
			CommentBlock = new Context("/**/"),
			CodeBody = new Context("{}");
		static Context() {
			XmlCommentLine.delimiters = Delim.XmlCommentDelimiters;
			CommentLine.delimiters = Delim.LineCommentDelimiters;
			CommentBlock.delimiters = Delim.CommentBlockDelimiters;
			String.whitespace = Delim.WhitespaceNone;
			String.delimiters = Delim.StringLiteralDelimiters;
			Number.whitespace = Delim.WhitespaceNone;
		}
		public string name = "default";
		public char[] whitespace = Delim.WhitespaceDefault;
		public Delim[] delimiters = Delim.StandardDelimiters;
		public Func<Entry, object> resolve;
		public Context(string name) {
			this.name = name;
			allContexts[name] = this;
		}
		public int IndexOfDelimeterAt(string str, int index) {
			for (int i = 0; i < delimiters.Length; ++i) {
				if (delimiters[i].IsAt(str, index)) { return i; }
			}
			return -1;
		}
		public Delim GetDelimiterAt(string str, int index) {
			int i = IndexOfDelimeterAt(str, index);
			if (i < 0) return null;
			return delimiters[i];
		}
		public class Entry {
			public Context context = null;
			public Entry parent = null;
			public IList<Token> tokens;
			public int tokenStart, tokenCount = -1;
			public int depth { get { Entry p = parent; int n = 0; while (p != null) { p = p.parent; ++n; } return n; } }
			public string sourceText;
			public string TextRaw { get { return sourceText.Substring(IndexBegin, Length); } }
			public string Text { get { return Unescape(); } }
			public object Resolve() { return (context.resolve != null) ? context.resolve(this) : Unescape(); }
			public bool IsText { get { return context == String || context == Char; } }
			public bool IsEnclosure { get { return context == Expression || context == CodeBody || context == SquareBrace; } }
			public bool IsComment { get { return context == CommentLine || context == XmlCommentLine || context == CommentBlock; } }
			public Token BeginToken { get { return tokens[tokenStart]; } }
			public Token EndToken { get { return tokens[tokenStart + tokenCount - 1]; } }
			public int IndexBegin { get { return BeginToken.BeginIndex; } }
			public int IndexEnd { get { return EndToken.EndIndex; } }
			public int Length { get { return IndexEnd - IndexBegin; } }
			public string Unescape() {
				if (context != Context.String && context != Context.Char) { return TextRaw; }
				StringBuilder sb = new StringBuilder();
				for (int i = tokenStart + 1; i < tokenStart + tokenCount - 1; ++i) {
					sb.Append(tokens[i].ToString());
					//Token t = tokens[i];
					//object meta = t.meta;
					//switch (meta) {
					//case string s: sb.Append(t.ToString(s)); break;
					//case TokenSubstitution s: sb.Append(s.value.ToString()); break;
					//default: throw new Exception("can't escape meta data "+meta);
					//}
				}
				return sb.ToString();
			}
			public int IndexAfter(IList<Token> tokens, int index = 0) {
				if (tokenCount < 0) return tokens.Count;
				int endIndex = IndexEnd;
				while (index + 1 < tokens.Count && tokens[index + 1].index < endIndex) { ++index; }
				return index;
			}
		}
		public Entry GetEntry(IList<Token> tokens, int startTokenIndex, string text, Context.Entry parent = null) {
			Entry e = new Entry { context = this, tokens = tokens, tokenStart = startTokenIndex, sourceText = text, parent = parent };
			return e;
		}
	}
	class CodeParse {
		public static int Tokens(string str, List<Token> tokens, List<int> rows = null, List<CodeConvert.Err> errors = null) {
			return Tokens(str, tokens, null, 0, rows, errors);
		}
		public static int Tokens(string str, List<Token> tokens, Context a_context = null,
			int index = 0, List<int> rows = null, List<CodeConvert.Err> errors = null) {
			if (a_context == null) a_context = Context.Default;
			int tokenBegin = -1, tokenEnd = -1;
			List<Context.Entry> contextStack = new List<Context.Entry>();
			Context currentContext = a_context;
			while (index < str.Length) {
				char c = str[index];
				Delim delim = currentContext.GetDelimiterAt(str, index);
				if (delim != null) {
					Token delimToken = new Token(delim, index, delim.text.Length);
					if (tokenBegin >= 0 && tokenEnd < 0) {
						tokenEnd = index;
						int len = tokenEnd - tokenBegin;
						tokens.Add(new Token(str, tokenBegin, len));
						tokenBegin = tokenEnd = -1;
					}
					if (delim.parseRule != null) {
						ParseResult pr = delim.parseRule.Invoke(str, index);
						if (pr.IsError && errors != null) {
							pr.error.OffsetBy(delimToken, rows);
							errors.Add(pr.error);
						}
						if (pr.replacementValue != null) {
							delimToken.length = pr.lengthParsed;
							delimToken.meta = new TokenSubstitution(str, pr.replacementValue);
						}
						index += pr.lengthParsed - 1;
					} else {
						index += delim.text.Length - 1;
					}
					DelimCtx dcx = delim as DelimCtx;
					if (dcx != null) {
						bool endProcessed = false;
						if (contextStack.Count > 0 && dcx.Context == currentContext && dcx.isEnd) {
							Context.Entry endingContext = contextStack[contextStack.Count - 1];
							delimToken.meta = endingContext;
							endingContext.tokenCount = (tokens.Count - endingContext.tokenStart) + 1;
							contextStack.RemoveAt(contextStack.Count - 1);
							if (contextStack.Count > 0) {
								currentContext = contextStack[contextStack.Count - 1].context;
							} else {
								currentContext = a_context;
							}
							endProcessed = true;
						}
						if (!endProcessed && dcx.isStart) {
							Context.Entry parentContext = (contextStack.Count > 0) ? contextStack[contextStack.Count - 1] : null;
							Context.Entry newContext = dcx.Context.GetEntry(tokens, tokens.Count, str, parentContext);
							currentContext = dcx.Context;
							delimToken.meta = newContext;
							contextStack.Add(newContext);
						}
					}
					tokens.Add(delimToken);
				} else if (Array.IndexOf(currentContext.whitespace, c) < 0) {
					if (tokenBegin < 0) { tokenBegin = index; }
				} else {
					if (tokenEnd < 0 && tokenBegin >= 0) {
						tokenEnd = index;
						int len = tokenEnd - tokenBegin;
						tokens.Add(new Token(str, tokenBegin, len));
						tokenBegin = tokenEnd = -1;
					}
				}
				if (rows != null && c == '\n') { rows.Add(index); }
				++index;
			}
			if (tokenBegin >= 0 && tokenEnd < 0) {
				tokenEnd = index;
				int len = tokenEnd - tokenBegin;
				tokens.Add(new Token(str, tokenBegin, len));
			}
			return index;
		}
		public static void FilePositionOf(Token token, IList<int> indexOfNewRow, out int row, out int col) {
			row = indexOfNewRow.BinarySearchIndexOf(token.index);
			if (row < 0) { row = ~row; }
			int rowStart = row > 0 ? indexOfNewRow[row - 1] : 0;
			col = token.index - rowStart;
			if (row == 0) ++col;
		}
		public static string FilePositionOf(Token token, IList<int> indexOfNewRow) {
			int row, col; FilePositionOf(token, indexOfNewRow, out row, out col);
			return (row + 1) + "," + (col);
		}
	}
}