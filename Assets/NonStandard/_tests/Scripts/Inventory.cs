using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour {

	public List<GameObject> items;
	public ListUi inventoryUi;

	public ListItemUi AddItem(GameObject itemObject) {
		items.Add(itemObject);
		itemObject.SetActive(false);
		itemObject.transform.SetParent(transform);
		if (inventoryUi == null) { return null; }
		InventoryItem item = itemObject.GetComponent<InventoryItem>();
		string name = item != null ? item.itemName : null;
		if(string.IsNullOrEmpty(name)) { name = itemObject.name; }
		return inventoryUi.AddItem(itemObject, name, () => {
			RemoveItem(itemObject);
			inventoryUi.RemoveItem(itemObject);
		});
	}

	public void RemoveItem(GameObject item) {
		items.Remove(item);
		item.SetActive(true);
		item.transform.SetParent(null);
		Rigidbody rb = item.GetComponent<Rigidbody>();
		if(rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
	}
}
