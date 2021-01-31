using System;
using System.Collections.Generic;

namespace NonStandard.Data.Parse {
	public class Expression {
		private List<Token> tokens;
		public Expression(List<Token> tokens) { this.tokens = tokens; }
		public override string ToString() {
			return Context.Entry.PrintAll(tokens);
		}
		public string Stringify() { return ToString(); }
		public string DebugPrint(int depth = 0, string indent = "  ") {
			return Tokenizer.DebugPrint(tokens, depth, indent);
		}
		public List<object> Resolve(Tokenizer tok, object scope = null) {
			List<object> results = new List<object>();
			Context.Entry.ResolveTerms(tok, scope, tokens, 0, tokens.Count, results);
			return results;
		}

		public bool TryResolve<T>(out T value, Tokenizer tok, object scope = null) {
			List<object> results = new List<object>();
			//Show.Log(Tokenizer.DebugPrint(tokens));
			Context.Entry.ResolveTerms(tok, scope, tokens, 0, tokens.Count, results);
			//Show.Log(results.Join("]["));
			if(results == null || results.Count != 1) {
				tok.AddError(-1, "missing results");
				value = default(T); return false;
			}
			object obj = results[0];
			if(obj.GetType() == typeof(T)) { value = (T)obj; return true; }
			if(!CodeConvert.TryConvert(ref obj, typeof(T))) {
				tok.AddError(-1, "unable to parse as " + typeof(T).ToString());
				value = default(T);
				return false;
			}
			value = (T)obj;
			return true;
		}
	}
}
