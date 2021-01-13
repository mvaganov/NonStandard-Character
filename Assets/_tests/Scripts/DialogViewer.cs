using System.Collections.Generic;
using UnityEngine;
using NonStandard.Code;
using System;

[Serializable] public class Dialog {
	public string name;
	public DialogOption[] options;
	public abstract class DialogOption { public string text; public TextAnchor anchorText = TextAnchor.UpperLeft; }
	[Serializable] public class Text : DialogOption { }
	[Serializable] public class Choice : DialogOption { public string command; }
}

public class DialogViewer : MonoBehaviour {
	public TextAsset dialogAsset;
	public List<Dialog> dialogs;
	Dictionary<string, Action<string>> commandListing = new Dictionary<string, Action<string>>();

	ListUi listUi;
	ListItemUi prefab_buttonUi, prefab_textUi;
	List<ListItemUi> currentChoices = new List<ListItemUi>();
	ListItemUi closeDialogButton;
	public void InitializeListUi() {
		listUi = GetComponentInChildren<ListUi>();
		prefab_buttonUi = listUi.prefab_item;
		prefab_textUi = Instantiate(prefab_buttonUi.gameObject).GetComponent<ListItemUi>();
		Destroy(prefab_textUi.GetComponent<UnityEngine.UI.Button>());
		Destroy(prefab_textUi.GetComponent<UnityEngine.UI.Image>());
	}
	public ListItemUi AddDialogOption(Dialog.DialogOption option) {
		ListItemUi li = null;
		do {
			Dialog.Text t = option as Dialog.Text;
			if (t != null) {
				li = listUi.AddItem(option, t.text, null, prefab_textUi);
				break;
			}
			Dialog.Choice c = option as Dialog.Choice;
			if (c != null) {
				li = listUi.AddItem(option, c.text, () => ParseCommand(c.command), prefab_buttonUi);
				currentChoices.Add(li);
				break;
			}
		}
		while (false);
		if (li != null) {
			li.text.alignment = option.anchorText;
		}
		return li;
	}
	public void ShowCloseDialogButton() {
		if (closeDialogButton != null) { Destroy(closeDialogButton.gameObject); }
		closeDialogButton = AddDialogOption(new Dialog.Choice { 
			text = "\n<close dialog>\n", command = "hide", anchorText = TextAnchor.MiddleCenter });
		currentChoices.Remove(closeDialogButton);
	}

	void Start () {
		InitializeCommands();
		InitializeListUi();
		List<CodeConvert.Err> errors = new List<CodeConvert.Err>();
		CodeConvert.TryParse(dialogAsset.text, out dialogs, errors);
		errors.ForEach(e => Debug.LogError(e));
		Debug.Log(CodeConvert.Stringify(dialogs, true));
		if (dialogs.Count > 0) {
			SetDialog(dialogs[0], false);
		}
	}
	public void DeactivateDialogChoices() {
		for (int i = 0; i < currentChoices.Count; ++i) {
			currentChoices[i].button.interactable = false;
		}
		currentChoices.Clear();
	}
	public void RemoveDialogElements() {
		DeactivateDialogChoices();
		Transform pt = listUi.transform;
		for (int i = 0; i < pt.childCount; ++i) {
			Transform t = pt.GetChild(i);
			if (t != prefab_buttonUi.transform && t != prefab_textUi.transform) {
				Destroy(t.gameObject);
			}
		}
	}
	public void SetDialog(Dialog dialog, bool removeAllOldElements) {
		if (removeAllOldElements) {
			RemoveDialogElements();
		} else {
			DeactivateDialogChoices();
		}
		if (dialog.options != null) {
			for (int i = 0; i < dialog.options.Length; ++i) {
				AddDialogOption(dialog.options[i]);
			}
		}
	}
	public void ParseCommand(string command) {
		int endOfCommand = command.IndexOf(' ');
		if(endOfCommand < 0) { endOfCommand = command.Length; }
		string cmd = command.Substring(0, endOfCommand);
		Action<string> commandToExecute;
		if(commandListing.TryGetValue(cmd, out commandToExecute)) {
			string nextCommand = endOfCommand < command.Length ? command.Substring(endOfCommand + 1) : "";
			commandToExecute.Invoke(nextCommand);
		}
	}
	private void InitializeCommands() {
		commandListing["dialog"] = SetDialog;
		commandListing["start"] = StartDialog;
		commandListing["done"] = Done;
		commandListing["hide"] = Hide;
		commandListing["show"] = Show;
	}
	public void SetDialog(string name) { SetDialog(Array.Find(dialogs.ToArray(), d => d.name == name), false); }
	public void StartDialog(string name) { SetDialog(Array.Find(dialogs.ToArray(), d => d.name == name), true); }
	public void Done(string _) { DeactivateDialogChoices(); ShowCloseDialogButton(); }
	public void Hide(string _) { gameObject.SetActive(false); }
	public void Show(string _) { gameObject.SetActive(true); }
}
