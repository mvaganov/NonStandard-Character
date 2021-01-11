using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NonStandard.Code;
using System.Text;
using NonStandard.Ui;
public struct Things {
	public int a, b;
}
public class TestData {
	public string name, text;
	public int number;
	public List<float> values;
	public Things things;
}
public class Test2 : TestData {
	public int version;
}

public class Dialog {
	public string text;
	public string image;
	public Direction imageAnchor = Direction.TopLeft;
	public Direction textAnchor = Direction.HorizontalBottom;
	public GUILayoutOption[] options;

	public class Option {
		public string text;
	}
}

public class DialogViewer : MonoBehaviour {

	public TextAsset textAsset;
	void Start () {
		// TODO set enums in CodeConvert, TODO use *wildcard, TODO code execution with reflection
		string text = textAsset.text;
		List<Token> tokens = new List<Token>();
		List<int> rows = new List<int>();
		List<CodeConvert.Err> errors = new List<CodeConvert.Err>();
		CodeParse.Tokens(text, tokens, rows, errors);
		StringBuilder sb = new StringBuilder();
		for(int i = 0; i < tokens.Count; ++i) {
			sb.Append("[").Append(tokens[i]).Append("] ");
		}
		Debug.Log(sb);
		TestData testData;
		bool parsed = CodeConvert.TryParse(text, out testData, errors);
		errors.ForEach(e => Debug.LogError(e));

		Debug.Log(CodeConvert.Stringify(testData));
	}
}
