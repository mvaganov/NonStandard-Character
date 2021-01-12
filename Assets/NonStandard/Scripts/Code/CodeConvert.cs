using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NonStandard.Code {
	public class CodeConvert {
		public struct Err {
			public int row, col;
			public string message;
			public Err(int r, int c, string m) { row = r; col = c; message = m; }
			public Err(Token token, IList<int> rows, string m) {
				CodeParse.FilePositionOf(token, rows, out row, out col);
				message = m;
			}
			public override string ToString() { return "@"+row+","+col+	": " + message; }
			public static Err None = default(Err);
			public void OffsetBy(Token token, IList<int> rows) {
				int r, c; CodeParse.FilePositionOf(token, rows, out r, out c); row += r; col += c;
			}
		}
		public static bool TryParse<T>(string text, out T data, List<Err> errors = null) {
			object value = null;
			bool result = TryParse(typeof(T), text, ref value, errors);
			data = (T)value;
			return result;
		}
		public static bool TryParse(Type type, string text, ref object data, List<Err> errors = null) {
			List<Token> tokens = new List<Token>();
			List<int> rows = new List<int>();
			CodeParse.Tokens(text, tokens, rows, errors);
			return TryParse(type, tokens, ref data, rows, errors);
		}

		public static T GetNew<T>(string type) { return (T)Activator.CreateInstance(Type.GetType(type)); }
		public static object GetNew(Type t) { return Activator.CreateInstance(t); }

		public class Parser {
			/// current data being parsed
			object memberValue = null;
			/// the object being parsed into, the final result
			public object result;
			string[] fieldNames, propNames;
			FieldInfo[] fields;
			FieldInfo field = null;
			PropertyInfo[] props;
			PropertyInfo prop = null;
			/// the type that the result needs to be
			Type resultType;
			/// the type that the next value needs to be
			Type memberType = null;
			bool isVarPrimitiveType = false, isList;
			List<object> listData = null;
			Token memberToken;
			IDictionary dict;
			IList<Token> tokens;
			IList<int> rows;
			List<Err> errors = null;
			int tokenIndex = 0;
			public void SetResultType(Type type) {
				resultType = type;
				fields = type.GetFields();
				props = type.GetProperties();
				Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));
				Array.Sort(props, (a, b) => a.Name.CompareTo(b.Name));
				fieldNames = Array.ConvertAll(fields, f => f.Name);
				propNames = Array.ConvertAll(props, p => p.Name);
			}
			public void Init(Type type, IList<Token> a_tokens, object dataStructure, IList<int> rows, List<Err> errors) {
				resultType = type;
				tokens = a_tokens;
				result = dataStructure;
				this.rows = rows;
				this.errors = errors;
				SetResultType(type);
				memberType = null;
				isVarPrimitiveType = false;
				memberType = GetIListType(type);
				isList = memberType != null;
				if (isList) {
					isVarPrimitiveType = false;
					if (memberType.IsArray) {
					} else {
						isVarPrimitiveType = IsPrimitiveType(memberType);
					}
					listData = new List<object>();
				} else {
					try {
						Type t = FindInternalType();
						if (t == null && result == null) { result = GetNew(type); }
					} catch (Exception e) {
						throw new Exception("failed to create " + type + " at " +
							CodeParse.FilePositionOf(tokens[0], rows)+"\n"+e.ToString());
					}
					KeyValuePair<Type, Type> kvp = GetIDictionaryType(type);
					if (kvp.Key != null) {
						dict = result as IDictionary;
					}
				}
			}

			private Type FindInternalType() {
				Token token = tokens[tokenIndex];
				if (token.IsContextBeginning) { token = tokens[++tokenIndex]; }
				Delim d = token.AsDelimiter;
				if (d != null) {
					if (d.text == "=" || d.text == ":") {
						++tokenIndex;
						memberType = typeof(string);
						if (!TryGetValue()) { return null; }
						memberType = null;
						++tokenIndex;
						string typeName = memberValue.ToString();
						Type t = Type.GetType(typeName);
						if(t == null) {
							Type[] childTypes = resultType.GetSubClasses();
							string[] typeNames = Array.ConvertAll(childTypes, ty => ty.ToString());
							string nameSearch = !typeName.StartsWith("*") ? "*" + typeName : typeName;
							int index = FindIndexWithWildcard(typeNames, nameSearch, false);
							if(index >= 0) { t = childTypes[index]; }
						}
						if (result  == null || result.GetType() != t) {
							if (t != null) {
								SetResultType(t);
								result = GetNew(resultType);
							} else {
								if (errors != null) errors.Add(new Err(token, rows, "unknown type " + typeName));
							}
						}
						return t;
					} else {
						if (errors != null) errors.Add(new Err(token, rows, "unexpected beginning token " + d.text));
					}
				}
				return null;
			}

			public bool TryParse() {
				FindInternalType();
				Token token = tokens[tokenIndex];
				for (; tokenIndex < tokens.Count; ++tokenIndex) {
					token = tokens[tokenIndex];
					Context.Entry e = token.AsContextEntry;
					if (e != null && (e.context == Context.CommentLine || e.context == Context.CommentBlock || e.context == Context.XmlCommentLine)) {
						tokenIndex += e.tokenCount-1; continue;
					}
					if (token.IsContextBeginning && !token.AsContextEntry.IsText) {
						if (memberType != null && isVarPrimitiveType) {
							if (errors != null) errors.Add(new Err(token, rows, "unexpected beginning of " + token.AsContextEntry.context.name));
							return false;
						}
					}
					if (token.IsContextEnding) {
						//Console.Write("finished parsing " + token.ContextEntry.context.name);
						break;
					}
					if (!isList) {
						if (memberType == null) {
							if (!GetMemberNameAndAssociatedType()) { return false; }
							if(memberValue == tokens) { memberValue = null; continue; }
						} else {
							if (!TryGetValue()) { return false; }
							if (memberValue == tokens) { continue; } // this is how TryGetValue communicates value ignore
							if (dict != null) {
								dict.Add(memberToken.Resolve(), memberValue);
							} else if (field != null) {
								field.SetValue(result, memberValue);
							} else if (prop != null) {
								prop.SetValue(result, memberValue, null);
							} else {
								throw new Exception("huh? how did we get here?");
							}
							field = null; prop = null; memberType = null;
						}
					} else {
						if (!TryGetValue()) { return false; }
						if (memberValue == tokens) { continue; }
						listData.Add(memberValue);
					}
				}
				if (isList) {
					if (resultType.IsArray) {
						Array a = Array.CreateInstance(memberType, listData.Count);
						for (int i = 0; i < listData.Count; ++i) { a.SetValue(listData[i], i); }
						result = a;
					} else {
						result = GetNew(resultType);
						IList ilist = result as IList;
						for (int i = 0; i < listData.Count; ++i) { ilist.Add(listData[i]); }
					}
				}
				return true;
			}

			public bool GetMemberNameAndAssociatedType() {
				memberToken = tokens[tokenIndex];
				string str = null;
				Context.Entry e = memberToken.AsContextEntry;
				if (e != null) {
					// skip comments
					if (dict == null) {
						if (e.IsText) {
							str = e.Text;
						} else {
							if (errors != null) errors.Add(new Err(memberToken, rows, "unable to parse member name "+e.BeginToken+" for " + resultType));
						}
					}
					tokenIndex += e.tokenCount - 1;
				} else {
					str = memberToken.AsBasicToken;
				}
				if (dict != null) { return true; }
				if (str == null) { memberValue = tokens; return true; }
				int index = FindIndexWithWildcard(fieldNames, str, true);
				if (index < 0) {
					index = FindIndexWithWildcard(propNames, str, true);
					if (index < 0) {
						if (errors != null) {
							StringBuilder sb = new StringBuilder();
							sb.Append("\nvalid possibilities include: ");
							for(int i = 0; i < fieldNames.Length; ++i) {
								if (i > 0) sb.Append(", ");
								sb.Append(fieldNames[i]);
							}
							for (int i = 0; i < propNames.Length; ++i) {
								if (i > 0 || fieldNames.Length > 0) sb.Append(", ");
								sb.Append(propNames[i]);
							}
							errors.Add(new Err(memberToken, rows, "could not find field or property \"" + str + "\" in " + resultType+sb));
						}
						return false;
					} else {
						prop = props[index];
						memberType = prop.PropertyType;
					}
				} else {
					field = fields[index];
					memberType = field.FieldType;
				}
				memberValue = null;
				if (memberType.IsArray) {
					isVarPrimitiveType = false;
				} else {
					isVarPrimitiveType = IsPrimitiveType(memberType);
				}
				return true;
			}
			public bool TryGetValue() {
				memberValue = null;
				Token token = tokens[tokenIndex];
				object meta = token.meta;
				Delim delim = meta as Delim;
				if (delim != null) {
					switch (delim.text) {
					// skip these delimiters as though they were whitespace.
					case "=": case ":": case ",": break;
					default:
						if (errors != null) errors.Add(new Err(token, rows, "unexpected delimiter \"" + delim.text + "\""));
						return false;
					}
					memberValue = tokens;
					return true;
				}
				Context.Entry context = meta as Context.Entry;
				if (context != null) {
					int indexAfterContext = tokenIndex + context.tokenCount;
					if (context.IsText) {
						memberValue = context.Text;
					} else if (!CodeConvert.TryParse(memberType, tokens.GetRange(tokenIndex, indexAfterContext - tokenIndex), ref memberValue, rows, errors)) {
						return false;
					}
					tokenIndex = indexAfterContext - 1; // -1 because a for-loop increments tokenIndex right outside this method
					return true;
				}
				string s = meta as string;
				if (s != null) {
					memberValue = token.ToString(s);
					if (!TryConvert(ref memberValue, memberType)) {
						if (errors != null) errors.Add(new Err(token, rows, "unable to convert " + memberValue + " to " + memberType));
						return false;
					}
					return true;
				}
				TokenSubstitution sub = meta as TokenSubstitution;
				if (sub != null) {
					memberValue = sub.value;
					if (!TryConvert(ref memberValue, memberType)) {
						if (errors != null) errors.Add(new Err(token, rows, "unable to convert " + memberValue + " to " + memberType));
						return false;
					}
					return true;
				}
				if (errors != null) errors.Add(new Err(token, rows, "unable to parse token with meta data " + meta));
				return false;
			}
		}

		public static bool TryParse(Type type, IList<Token> tokens, ref object data, IList<int> rows, List<Err> errors = null) {
			Parser p = new Parser();
			p.Init(type, tokens, data, rows, errors);
			bool result = p.TryParse();
			data = p.result;
			return result;
		}

		public static bool IsPrimitiveType(Type typeToGet) {
			switch (Type.GetTypeCode(typeToGet)) {
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
			case TypeCode.Char:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.UInt32:
			case TypeCode.Single:
			case TypeCode.Int64:
			case TypeCode.UInt64:
			case TypeCode.Double:
			case TypeCode.String:
				return true;
			}
			return false;
		}

		public static bool TryConvert(ref object value, Type typeToGet) {
			try {
				if (typeToGet.IsEnum) {
					string str = value as string;
					if (str != null) { return TryGetEnum(typeToGet, str, out value); }
				}
				switch (Type.GetTypeCode(typeToGet)) {
				case TypeCode.Boolean: value = Convert.ToBoolean(value); break;
				case TypeCode.SByte: value = Convert.ToSByte(value); break;
				case TypeCode.Byte: value = Convert.ToByte(value); break;
				case TypeCode.Char: value = Convert.ToChar(value); break;
				case TypeCode.Int16: value = Convert.ToInt16(value); break;
				case TypeCode.UInt16: value = Convert.ToUInt16(value); break;
				case TypeCode.Int32: value = Convert.ToInt32(value); break;
				case TypeCode.UInt32: value = Convert.ToUInt32(value); break;
				case TypeCode.Single: value = Convert.ToSingle(value); break;
				case TypeCode.Int64: value = Convert.ToInt64(value); break;
				case TypeCode.UInt64: value = Convert.ToUInt64(value); break;
				case TypeCode.Double: value = Convert.ToDouble(value); break;
				case TypeCode.String: value = Convert.ToString(value); break;
				default: return false;
				}
			} catch { return false; }
			return true;
		}

		private static bool TryGetEnum(Type typeToGet, string str, out object value) {
			bool startsWith = str.EndsWith("*"), endsWidth = str.StartsWith("*");
			if (startsWith || endsWidth) {
				Array a = Enum.GetValues(typeToGet);
				string[] names = new string[a.Length];
				for (int i = 0; i < a.Length; ++i) { names[i] = a.GetValue(i).ToString(); }
				int index = FindIndexWithWildcard(names, str, false);
				if (index < 0) { value = null; return false; }
				str = names[index];
			}
			value = Enum.Parse(typeToGet, str);
			return true;
		}

		private static int FindIndexWithWildcard(string[] names, string name, bool isSorted) {
			if (name == "*") return 0;
			bool startsWith = name.EndsWith("*"), endsWith = name.StartsWith("*");
			if (startsWith && endsWith) { return Array.FindIndex(names, s => s.Contains(name.Substring(1, name.Length - 2))); }
			if (endsWith) { name = name.Substring(1); return Array.FindIndex(names, s => s.EndsWith(name)); }
			if (startsWith) { name = name.Substring(0, name.Length - 1); }
			int index = isSorted ? Array.BinarySearch(names, name) : (startsWith) 
				? Array.FindIndex(names, s => s.StartsWith(name)) : Array.IndexOf(names, name);
			if (startsWith && index < 0) { return ~index; }
			return index;
		}

		public static Type GetICollectionType(Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>)) {
					return i.GetGenericArguments()[0];
				}
			}
			return null;
		}
		public static Type GetIListType(Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)) {
					return i.GetGenericArguments()[0];
				}
			}
			return null;
		}
		public static KeyValuePair<Type,Type> GetIDictionaryType(Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)) {
					return new KeyValuePair<Type,Type>(i.GetGenericArguments()[0], i.GetGenericArguments()[1]);
				}
			}
			return new KeyValuePair<Type, Type>(null,null);
		}
		public static string Indent(int depth, string indent = "  ") {
			StringBuilder sb = new StringBuilder();
			while (depth-- > 0) { sb.Append(indent); }
			return sb.ToString();
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="pretty"></param>
		/// <param name="includeType">include "=TypeName" if there could be ambiguity because of inheritance</param>
		/// <param name="depth"></param>
		/// <returns></returns>
		public static string Stringify(object obj, bool pretty = false, bool includeType = true, int depth = 0) {
			if (obj == null) return "null";
			Type t = obj.GetType();
			StringBuilder sb = new StringBuilder();
			FieldInfo[] fi = t.GetFields();
			Type iListElement = GetIListType(t);
			bool showTypeHere = includeType; // no need to print type if there isn't type ambiguity
			if (includeType) {
				Type b = t.BaseType; // if the parent class is a base class, there isn't any ambiguity
				if (b == typeof(ValueType) || b == typeof(Object) || b == typeof(Array)) { showTypeHere = false; }
			}
			if(IsPrimitiveType(obj.GetType())) {
				string s = obj as string;
				if (s != null) {
					sb.Append("\"").Append(Escape(s)).Append("\"");
				} else {
					sb.Append(obj.ToString());
				}
			} else if (t.IsArray || iListElement != null) {
				sb.Append("[");
				if (showTypeHere) {
					if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
					sb.Append("=\"" + obj.GetType().ToString() + "\" "+obj.GetType().BaseType);
				}
				IList list = obj as IList;
				if ((iListElement != null && IsPrimitiveType(iListElement)) || IsPrimitiveType(t.GetElementType())) {
					for(int i = 0; i < list.Count; ++i) {
						if (i > 0) { sb.Append(","); if (pretty) sb.Append(" "); }
						sb.Append(Stringify(list[i], pretty, includeType, depth + 1));
					}
				} else {
					for(int i = 0; i < list.Count; ++i) {
						if (i > 0) { sb.Append(","); }
						if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
						sb.Append(Stringify(list[i], pretty, includeType, depth + 1));
					}
					if (pretty) { sb.Append("\n" + Indent(depth)); }
				}
				sb.Append("]");
			} else if (fi.Length > 0) {
				sb.Append("{");
				if (showTypeHere) {
					if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
					sb.Append("=\""+obj.GetType().ToString()+"\"");
				}
				for (int i = 0; i < fi.Length; ++i) {
					if (i > 0 || showTypeHere) { sb.Append(","); }
					if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
					sb.Append(fi[i].Name);
					sb.Append(pretty?" : ":":");
					sb.Append(Stringify(fi[i].GetValue(obj), pretty, includeType, depth + 1));
				}
				if (pretty) { sb.Append("\n" + Indent(depth)); }
				sb.Append("}");
			}
			if(sb.Length == 0) { sb.Append(obj.ToString()); }
			return sb.ToString();
		}

		/// <summary>
		/// converts a string from it's code to it's compiled form, with processed escape sequences
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		// TODO use the actual parsing mechanisms...
		public static string Unescape(string str) {
			ParseResult parse;
			StringBuilder sb = new StringBuilder();
			int stringStarted = 0;
			for (int i = 0; i < str.Length; ++i) {
				char c = str[i];
				if (c == '\\') {
					sb.Append(str.Substring(stringStarted, i - stringStarted));
					parse = Delim.UnescapeString(str, i);
					//if (parse.IsError) {
					//	Console.ForegroundColor = ConsoleColor.Red;
					//	Console.WriteLine("@" + i + ": " + parse.error);
					//}
					if (parse.replacementValue != null) {
						sb.Append(parse.replacementValue);
					}
					//Console.WriteLine("replacing " + str.Substring(i, parse.lengthParsed) + " with " + parse.replacementValue);
					stringStarted = i + parse.lengthParsed;
					i = stringStarted - 1;
				}
			}
			sb.Append(str.Substring(stringStarted, str.Length - stringStarted));
			return sb.ToString();
		}

		// TODO use the actual parsing mechanisms...
		public static string Escape(string str) {
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < str.Length; ++i) {
				char c = str[i];
				switch (c) {
				case '\a': sb.Append("\\a"); break;
				case '\b': sb.Append("\\b"); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': sb.Append("\\r"); break;
				case '\f': sb.Append("\\f"); break;
				case '\t': sb.Append("\\t"); break;
				case '\v': sb.Append("\\v"); break;
				case '\'': sb.Append("\\\'"); break;
				case '\"': sb.Append("\\\""); break;
				case '\\': sb.Append("\\\\"); break;
				default:
					if (c < 32 || (c > 127 && c < 512)) {
						sb.Append("\\").Append(Convert.ToString((int)c, 8));
					} else if (c >= 512) {
						sb.Append("\\u").Append(((int)c).ToString("X4"));
					} else {
						sb.Append(c);
					}
					break;
				}
			}
			return sb.ToString();
		}

	}
}
