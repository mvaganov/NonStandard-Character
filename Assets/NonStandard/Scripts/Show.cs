﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif
namespace NonStandard {
	public class Show {
		public static Action<string> onLog;
		public static Action<string> onError;
		public static Action<string> onWarning;

		public static void Log(object obj) { onLog.Invoke(obj.ToString()); }
		public static void Log(string str) { onLog.Invoke(str); }
		public static void Error(object obj) { onError.Invoke(obj.ToString()); }
		public static void Error(string str) { onError.Invoke(str); }
		public static void Warning(object obj) { onWarning.Invoke(obj.ToString()); }
		public static void Warning(string str) { onWarning.Invoke(str); }

		static Show() {
#if UNITY_5_3_OR_NEWER
			onLog += Debug.Log;
			onError += Debug.LogError;
			onWarning += Debug.LogWarning;
#else
			onLog += Console.WriteLine;
			onError += Console.WriteLine;
			onWarning += Console.WriteLine;
#endif
		}

		public static string Indent(int depth, string indent = "  ") {
			StringBuilder sb = new StringBuilder();
			while (depth-- > 0) { sb.Append(indent); }
			return sb.ToString();
		}

		/// <summary>
		/// stringifies an object using custom NonStandard rules
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="pretty"></param>
		/// <param name="includeType">include "=TypeName" if there could be ambiguity because of inheritance</param>
		/// <param name="depth"></param>
		/// <param name="recursionStack">used to prevent recursion stack overflows</param>
		/// <returns></returns>
		public static string Stringify(object obj, bool pretty = false, bool includeType = true, int depth = 0, List<object> recursionStack = null) {
			if (obj == null) return "null";
			Type t = obj.GetType();
			StringBuilder sb = new StringBuilder();
			Type iListElement = t.GetIListType();
			bool showTypeHere = includeType; // no need to print type if there isn't type ambiguity
			if (includeType) {
				Type b = t.BaseType; // if the parent class is a base class, there isn't any ambiguity
				if (b == typeof(ValueType) || b == typeof(System.Object) || b == typeof(Array)) { showTypeHere = false; }
			}
			string s = obj as string;
			if (s != null || t.IsPrimitive || t.IsEnum) {
				if (s != null) {
					sb.Append("\"").Append(Escape(s)).Append("\"");
				} else {
					sb.Append(obj.ToString());
				}
				return sb.ToString();
			}
			if (recursionStack == null) { recursionStack = new List<object>(); }
			int recursionIndex = recursionStack.IndexOf(obj);
			if (recursionIndex >= 0) {
				sb.Append("/* recursed " + (recursionStack.Count - recursionIndex) + " */");
				return sb.ToString();
			}
			recursionStack.Add(obj);
			if (t.IsArray || iListElement != null) {
				sb.Append("[");
				if (showTypeHere) {
					if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
					sb.Append("=\"" + obj.GetType().ToString() + "\" " + obj.GetType().BaseType);
				}
				IList list = obj as IList;

				if (iListElement != null && iListElement.IsPrimitive) {
					for (int i = 0; i < list.Count; ++i) {
						if (i > 0) { sb.Append(","); if (pretty) sb.Append(" "); }
						sb.Append(Stringify(list[i], pretty, includeType, depth + 1, recursionStack));
					}
				} else {
					for (int i = 0; i < list.Count; ++i) {
						if (i > 0) { sb.Append(","); }
						if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
						sb.Append(Stringify(list[i], pretty, includeType, depth + 1, recursionStack));
					}
					if (pretty) { sb.Append("\n" + Indent(depth)); }
				}
				sb.Append("]");
			} else {
				bool isDict = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
				sb.Append("{");
				if (showTypeHere) {
					if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
					sb.Append("=\"" + obj.GetType().ToString() + "\"");
				}
				if (!isDict) {
					FieldInfo[] fi = t.GetFields();
					for (int i = 0; i < fi.Length; ++i) {
						if (i > 0 || showTypeHere) { sb.Append(","); }
						if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
						sb.Append(fi[i].Name);
						sb.Append(pretty ? " : " : ":");
						sb.Append(Stringify(fi[i].GetValue(obj), pretty, includeType, depth + 1, recursionStack));
					}
				} else {
					MethodInfo getEnum = t.GetMethod("GetEnumerator", new Type[] { });
					MethodInfo getKey = null, getVal = null;
					object[] noparams = new object[] { };
					IEnumerator e = getEnum.Invoke(obj, noparams) as IEnumerator;
					bool somethingPrinted = false;
					while (e.MoveNext()) {
						object o = e.Current;
						if (getKey == null) { getKey = o.GetType().GetProperty("Key").GetGetMethod(); }
						if (getVal == null) { getVal = o.GetType().GetProperty("Value").GetGetMethod(); }
						if (somethingPrinted || showTypeHere) { sb.Append(","); }
						if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
						object k = getKey.Invoke(o, noparams);
						object v = getVal.Invoke(o, noparams);
						sb.Append(k);
						sb.Append(pretty ? " : " : ":");
						sb.Append(Stringify(v, pretty, includeType, depth + 1, recursionStack));
						somethingPrinted = true;
					}
				}
				if (pretty) { sb.Append("\n" + Indent(depth)); }
				sb.Append("}");
			}
			if (sb.Length == 0) { sb.Append(obj.ToString()); }
			return sb.ToString();
		}

		/// <summary>
		/// converts a string from it's code to it's compiled form, with processed escape sequences
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string Unescape(string str) {
			StringBuilder sb = new StringBuilder();
			int stringStarted = 0;
			for (int i = 0; i < str.Length; ++i) {
				char c = str[i];
				if (c == '\\') {
					if (stringStarted != i) { sb.Append(str.Substring(stringStarted, i - stringStarted)); }
					++i;
					if (i >= str.Length) { break; }
					c = str[i];
					string replacement;
					switch (c) {
					case 'a': replacement = "\a"; ++i; break;
					case 'b': replacement = "\b"; ++i; break;
					case 't': replacement = "\t"; ++i; break;
					case 'n': replacement = "\n"; ++i; break;
					case 'r': replacement = "\r"; ++i; break;
					default: replacement = c.ToString(); ++i; break;
					}
					if (replacement != null) { sb.Append(replacement); }
					stringStarted = i;
					--i; // the for-loop is about to increment the iterator again
				}
			}
			sb.Append(str.Substring(stringStarted, str.Length - stringStarted));
			return sb.ToString();
		}

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