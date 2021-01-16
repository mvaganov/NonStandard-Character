using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NonStandard.Data {
	public class CodeConvert {
		public struct Err {
			public int row, col;
			public string message;
			public Err(int r, int c, string m) { row = r; col = c; message = m; }
			public Err(Token token, IList<int> rows, string m) {
				CodeParse.FilePositionOf(token, rows, out row, out col);
				message = m;
			}
			public override string ToString() { return "@" + row + "," + col + ": " + message; }
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
			//IDictionary dict;
			KeyValuePair<Type, Type> dictionaryType;
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
				memberType = type.GetIListType();
				isList = memberType != null;
				memberToken.index = -1;
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
							CodeParse.FilePositionOf(tokens[0], rows) + "\n" + e.ToString());
					}
					dictionaryType = type.GetIDictionaryType();
					//UnityEngine.Debug.Log(type + " ~ " + dictionaryType.Key + " : " + dictionaryType.Value);// +"    ["+dict+"]");
					if (dictionaryType.Value != null) { memberType = dictionaryType.Value; }
				}
			}

			private Type FindInternalType() {
				if (tokenIndex >= tokens.Count) return null;
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
						if (t == null) {
							Type[] childTypes = resultType.GetSubClasses();
							string[] typeNames = Array.ConvertAll(childTypes, ty => ty.ToString());
							string nameSearch = !typeName.StartsWith("*") ? "*" + typeName : typeName;
							int index = FindIndexWithWildcard(typeNames, nameSearch, false);
							if (index >= 0) { t = childTypes[index]; }
						}
						if (result == null || result.GetType() != t) {
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
				for (; tokenIndex < tokens.Count; ++tokenIndex) {
					Token token = tokens[tokenIndex];
					//Show.Log(":::" + token + "     " + dictionaryType.Key);
					Context.Entry e = token.AsContextEntry;
					if (e != null && (e.context == Context.CommentLine || e.context == Context.CommentBlock || e.context == Context.XmlCommentLine)) {
						tokenIndex += e.tokenCount - 1; continue;
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
						if (memberToken.index < 0) {// memberType == null) {
							if (!GetMemberNameAndAssociatedType()) { return false; }
							if (memberValue == tokens) { memberValue = null; continue; }
						} else {
							if (!TryGetValue()) { return false; }
							if (memberValue == tokens) { continue; } // this is how TryGetValue communicates value ignore
							if (dictionaryType.Key != null) {
								MethodInfo addMethod = resultType.GetMethod("Add", new Type[] { dictionaryType.Key, dictionaryType.Value });
								object key = memberToken.Resolve();
								if (!memberType.IsAssignableFrom(memberValue.GetType())) {
									if (errors != null) errors.Add(new Err(token, rows, "unable to convert element \"" + key + "\" value \"" + memberValue + "\" to type " + memberType));
								} else {
									//Show.Log(key + " = " + memberValue + " (" + memberValue.GetType() + " vs " + dictionaryType.Value + ")");
									addMethod.Invoke(result, new object[] { key, memberValue });
								}
							} else if (field != null) {
								field.SetValue(result, memberValue);
							} else if (prop != null) {
								prop.SetValue(result, memberValue, null);
							} else {
								throw new Exception("huh? how did we get here?");
							}
							field = null; prop = null; memberType = dictionaryType.Value; memberToken.index = -1;
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
					if (dictionaryType.Key == null) {
						if (e.IsText) {
							str = e.Text;
						} else {
							if (errors != null) errors.Add(new Err(memberToken, rows, "unable to parse member name " + e.BeginToken + " for " + resultType));
						}
					} else {
						str = "dictionary member value will be resolved later";
					}
					tokenIndex += e.tokenCount - 1;
				} else {
					str = memberToken.AsBasicToken;
				}
				if (str == null) { memberToken.index = -1; memberValue = tokens; return true; }
				if (dictionaryType.Key != null) {
					//UnityEngine.Debug.Log("dictionary member [" + memberToken + "]");
					return true;
				}
				int index = FindIndexWithWildcard(fieldNames, str, true);
				if (index < 0) {
					index = FindIndexWithWildcard(propNames, str, true);
					if (index < 0) {
						if (errors != null) {
							StringBuilder sb = new StringBuilder();
							sb.Append("\nvalid possibilities include: ");
							for (int i = 0; i < fieldNames.Length; ++i) {
								if (i > 0) sb.Append(", ");
								sb.Append(fieldNames[i]);
							}
							for (int i = 0; i < propNames.Length; ++i) {
								if (i > 0 || fieldNames.Length > 0) sb.Append(", ");
								sb.Append(propNames[i]);
							}
							errors.Add(new Err(memberToken, rows, "could not find field or property \"" + str + "\" in " + result.GetType() + sb));
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

	}
}
