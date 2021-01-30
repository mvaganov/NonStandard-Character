using NonStandard.Data.Parse;
using System.Collections.Generic;
using UnityEngine;

namespace NonStandard.Data {

	[System.Serializable, UnambiguousStringify]
	public class SensitiveHashTable_stringfloat : SensitiveHashTable<string, float> { }
	public class DictionaryKeeper : MonoBehaviour {
		protected SensitiveHashTable_stringfloat dict = new SensitiveHashTable_stringfloat();
		public SensitiveHashTable_stringfloat Dictionary { get { return dict; } }
#if UNITY_EDITOR
		[TextArea(3,10)]
		public string values;
		[TextArea(1, 10)]
		public string parseResults;

		bool validating = false;
		void OnValidate() {
			List<ParseError> errors = new List<ParseError>();
			CodeConvert.TryFill(values, ref dict, errors);
			if (errors.Count > 0) {
				parseResults = string.Join("\n", errors.ConvertAll(e => e.ToString()).ToArray());
			} else {
				//parseResults = dict.Show(true);
				parseResults = Show.Stringify(dict, true);
			}
		}
		void ShowChange() {
			if (!validating) {
				validating = true;
				Clock.setTimeout(() => { values = dict.Show(true); validating = false; }, 0);
			}
		}
#else
		void ShowChange(){}
#endif
		[System.Serializable] public class KVP { public string key; public float value; }

		void Awake() { }

		void Start() {
			#if UNITY_EDITOR
			dict.onChange += (k, a, b) => { ShowChange(); };
#endif
			dict.FunctionAssignIgnore();
			string[] mainStats = new string[] { "str", "con", "dex", "int", "wis", "cha" };
			int[] scores = { 8, 8, 18, 12, 9, 14 };
			for(int i = 0; i < mainStats.Length; ++i) {
				dict[mainStats[i]] = scores[i];
			}
			for (int i = 0; i < mainStats.Length; ++i) {
				string s = mainStats[i];
				dict.Set("_"+s, ()=>CalcModifier(dict, s));
			}
			dict["cha"] += 4;
		}
		private int CalcModifier(SensitiveHashTable<string, float> d, string s) { return (int)Mathf.Floor((d[s] - 10) / 2); }
	}
}
