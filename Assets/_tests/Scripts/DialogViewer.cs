using System.Collections.Generic;
using UnityEngine;
using NonStandard.Code;
using NonStandard.Ui;
using UnityEngine.Events;
using System;

[System.Serializable] public class Dialog {
	public string name, image;
	public Direction aImage = Direction.TopLeft;
	public Direction aText = Direction.HorizontalBottom;
	public DialogOption[] options;
	public abstract class DialogOption { }

	[System.Serializable] public class Text : DialogOption { public string text; }
	[System.Serializable] public class Choice : DialogOption { public string text, command; }
}

[System.Serializable] public class UnityEvent_string : UnityEvent<string> { }

public class DialogViewer : MonoBehaviour {
	public TextAsset dialogAsset;
	public UnityEvent_string onDialogCommand;
	public List<Dialog> dialogs;
	Dialog current;
	Dictionary<string, Action<string>> commandListing = new Dictionary<string, Action<string>>();
	private void InitializeCommands() {
		commandListing["done"] = Done;
		commandListing["dialog"] = SetDialog;
	}
	void Start () {
		List<CodeConvert.Err> errors = new List<CodeConvert.Err>();
		CodeConvert.TryParse(dialogAsset.text, out dialogs, errors);
		errors.ForEach(e => Debug.LogError(e));
		Debug.Log(CodeConvert.Stringify(dialogs, true));

		if (dialogs.Count > 0) {
			SetDialog(dialogs[0]);
		}
		InitializeCommands();
	}
	public void SetDialog(Dialog dialog) {
		current = dialog;
		// TODO disable current active dialog options
		// TODO load dialog options into the scroll rect
	}
	public void ParseCommands(string command) {
		int endOfCommand = command.IndexOf(' ');
		if(endOfCommand < 0) { endOfCommand = command.Length; }
		string cmd = command.Substring(0, endOfCommand);
		Action<string> commandToExecute;
		if(commandListing.TryGetValue(cmd, out commandToExecute)) {
			string nextCommand = endOfCommand < command.Length ? command.Substring(endOfCommand + 1) : "";
			commandToExecute.Invoke(nextCommand);
		}
	}
	public void SetDialog(string name) {
		SetDialog(Array.Find(dialogs.ToArray(), d => d.name == name));
	}
	public void Done(string _) {
		// TODO disable current active dialog options
	}
}
