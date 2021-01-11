using System;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour {

	public InventoryItemUI prefab_item;
	public string[] itemNames;
	bool refreshing = false;

	public void Refresh() {
		if (refreshing) return;
		refreshing = true;
		NonStandard.Clock.setTimeout(() => {
			refreshing = false;
			LayoutGroup lg = GetComponent<LayoutGroup>();
			lg.enabled = false;
			lg.enabled = true;
		}, 100);
	}

	public void SetItems(object[] items, string[] names, Action[] onButton) {
		ClearItems();
		this.itemNames = names;
		for (int i = 0; i < names.Length; ++i) {
			AddItem(items != null ? items[i] : null, names[i], onButton != null ? onButton[i] : null);
		}
	}

	public void ClearItems() { RemoveItem(null); }

	public InventoryItemUI AddItem(object item, string name, Action onButton) {
		GameObject newItem = Instantiate(prefab_item.gameObject);
		newItem.SetActive(true);
		InventoryItemUI iui = newItem.GetComponent<InventoryItemUI>();
		iui.name = name;
		iui.text.text = name;
		iui.item = item;
		if (onButton != null) {
			iui.button.onClick.AddListener(onButton.Invoke);
		}
		iui.transform.SetParent(transform);
		Refresh();
		return iui;
	}
	public int IndexOf(object item) {
		Transform t = transform;
		for (int i = 0; i < t.childCount; ++i) {
			Transform child = t.GetChild(i);
			if (child != prefab_item.transform && (item == null ||
				child.GetComponent<InventoryItemUI>().item == item ||
				(child.GetComponent<InventoryItemUI>().item == null && child.name == item as string))) {
				return i;
			}
		}
		return -1;
	}
	public InventoryItemUI GetInventoryItemUI(object item) {
		int i = IndexOf(item);
		if (i < 0) { return null; }
		return transform.GetChild(i).GetComponent<InventoryItemUI>();
	}
	public bool RemoveItem(object item) {
		int i = IndexOf(item);
		if (i < 0) { return false; }
		Transform child = transform.GetChild(i);
		Destroy(child.gameObject);
		return true;
	}

	void Awake () {
		if(prefab_item == null) { prefab_item = GetComponentInChildren<InventoryItemUI>(); }
		if(prefab_item.transform.parent == transform) { prefab_item.gameObject.SetActive(false); }
		SetItems(null, itemNames, null);
	}	
}
