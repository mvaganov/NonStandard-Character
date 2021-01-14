using System.Collections.Generic;
using UnityEngine;

namespace NonStandard.Data {

	[System.Serializable] public class SensitiveHashTable_stringfloat : SensitiveHashTable<string, float> { }
	public class Dictionary : MonoBehaviour {
		public SensitiveHashTable_stringfloat dict = new SensitiveHashTable_stringfloat();

#if UNITY_EDITOR
		[TextArea(3,10)]
		public string values;

		bool validating = false;
		void OnValidate() {
			// TODO try to parse the dictionary. if unable to parse
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
		// TODO editing 'values' text area will add to this list, and populate 'dict' on Awake
		[HideInInspector, SerializeField] List<KVP> initialValues = new List<KVP>();
		[System.Serializable] public class KVP { public string key; public float value; }

		void Awake() { }

		void Start() {
			dict.onChange += (k, a, b) => { Debug.Log(k+" : "+a+" -> "+b); ShowChange(); };
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
