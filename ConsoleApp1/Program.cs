using NonStandard;
using NonStandard.Data;
using NonStandard.Data.Parse;
using System;
using System.Collections.Generic;

namespace ConsoleApp1 {
	class Program {
		static void Main(string[] args) {
			string testData = "i-2 f3testing12\"3\"-2.9";
			Dictionary<string, float> dict;
			Tokenizer tok = new Tokenizer();
			CodeConvert.TryParse(testData, out dict, null, tok);
			if (tok.errors.Count > 0) {
				Show.Log(tok.errors.Join("\n"));
			}
			Show.Log(tok.DebugPrint());
			Show.Log("result: "+Show.Stringify(dict));
		}
	}
}