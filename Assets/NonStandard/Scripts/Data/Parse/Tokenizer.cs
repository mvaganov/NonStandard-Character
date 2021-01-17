﻿using System;
using System.Collections.Generic;

namespace NonStandard.Data.Parse {
	public class Tokenizer {
		public static int Tokenize(string str, List<Token> tokens, List<int> rows = null, List<ParseError> errors = null) {
			return Tokenize(str, tokens, null, 0, rows, errors);
		}
		public static int Tokenize(string str, List<Token> tokens, Context a_context = null, int index = 0, List<int> rows = null, List<ParseError> errors = null) {
			if (a_context == null) a_context = CodeRules.Default;
			int tokenBegin = -1, tokenEnd = -1;
			List<Context.Entry> contextStack = new List<Context.Entry>();
			Context currentContext = a_context;
			while (index < str.Length) {
				char c = str[index];
				Delim delim = currentContext.GetDelimiterAt(str, index);
				if (delim != null) {
					FinishToken(str, tokens, index, ref tokenBegin, ref tokenEnd);
					HandleDelimiter(delim, tokens, ref index, str, rows, errors, contextStack, ref currentContext, a_context);
				} else if (Array.IndexOf(currentContext.whitespace, c) < 0) {
					if (tokenBegin < 0) { tokenBegin = index; } // handle non-whitespace
				} else {
					FinishToken(str, tokens, index, ref tokenBegin, ref tokenEnd); // handle whitespace
				}
				if (rows != null && c == '\n') { rows.Add(index); }
				++index;
			}
			FinishToken(str, tokens, index, ref tokenBegin, ref tokenEnd); // add the last token that was still being processed
			FinalTokenCleanup(tokens, rows, errors);
			return index;
		}

		private static void FinalTokenCleanup(List<Token> tokens, List<int> rows, List<ParseError> errors) {
			for (int i = 0; i < tokens.Count; ++i) {
				// any unfinished contexts must end. the last place they could end is the end of this string
				Context.Entry e = tokens[i].AsContextEntry;
				if (e != null && e.tokenCount < 0) {
					e.tokenCount = tokens.Count - e.tokenStart;
					if (e.context != CodeRules.CommentLine) { // this is an error, unless it's a comment
						errors.Add(new ParseError(tokens[i], rows, "missing closing token"));
					}
				}
			}
		}

		private static bool FinishToken(string str, List<Token> tokens, int index, ref int tokenBegin, ref int tokenEnd) {
			if (tokenBegin >= 0 && tokenEnd < 0) {
				tokenEnd = index;
				int len = tokenEnd - tokenBegin;
				if (len > 0) { tokens.Add(new Token(str, tokenBegin, len)); }
				tokenBegin = tokenEnd = -1;
				return true;
			}
			return false;
		}
		private static void HandleDelimiter(Delim delim, List<Token> tokens, ref int index, string str, 
			List<int> rows, List<ParseError> errors, List<Context.Entry> contextStack, ref Context currentContext, Context defaultContext) {
			Token delimToken = new Token(delim, index, delim.text.Length);
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
						currentContext = defaultContext;
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
		}
	}
}