using NonStandard;
using NonStandard.Data;
using NonStandard.Data.Parse;
using System;
using System.Collections.Generic;

namespace ConsoleApp1 {
	[System.Serializable]
	[UnambiguousStringify]
	public class SensitiveHashTable_stringfloat : BurlyHashTable<string, object> { }
	class Program {
		static void Main(string[] args) {
			string testData = "name\"the player\"i-2 f3testing12\"3\"2.99";
			SensitiveHashTable_stringfloat dict;
			Tokenizer tok = new Tokenizer();
			CodeConvert.TryParse(testData, out dict, null, tok);
			if (tok.errors.Count > 0) {
				Show.Log(tok.errors.Join("\n"));
			}
			Show.Log(tok.DebugPrint());
			Show.Log("result: "+Show.Stringify(dict,true));
		}
	}
}