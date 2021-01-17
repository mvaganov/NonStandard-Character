using UnityEngine;
using UnityEngine.Events;

public class OnActiveChange : MonoBehaviour {
	public UnityEvent onEnable, onDisable;
	void OnEnable() { onEnable.Invoke(); }
	void OnDisable() { onDisable.Invoke(); }
}
