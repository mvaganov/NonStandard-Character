using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class UnityEvent_GameObject : UnityEvent<GameObject> { }

public class CollisionTrigger : MonoBehaviour {
	public UnityEvent_GameObject onTrigger = new UnityEvent_GameObject();
	public UnityEvent_GameObject onEndTrigger = new UnityEvent_GameObject();
	public string tagLimit;
	void DoActivateTrigger() { if (enabled) onTrigger.Invoke(null); }
	void DoActivateTrigger(GameObject other) { if(enabled) onTrigger.Invoke(other); }
	void DoDeactivateTrigger(GameObject other) { if (enabled) onEndTrigger.Invoke(other); }
	void OnTriggerEnter(Collider other) {
		if(tagLimit == null || other.gameObject.tag == tagLimit) { DoActivateTrigger(other.gameObject); }
	}
	void OnTriggerExit(Collider other) {
		if (tagLimit == null || other.gameObject.tag == tagLimit) { DoDeactivateTrigger(other.gameObject); }
	}
	void OnCollisionEnter(Collision collision) {
		if (tagLimit == null || collision.gameObject.tag == tagLimit) { DoActivateTrigger(collision.gameObject); }
	}
	void OnCollisionExit(Collision collision) {
		if (tagLimit == null || collision.gameObject.tag == tagLimit) { DoDeactivateTrigger(collision.gameObject); }
	}

#if UNITY_EDITOR
	void OnValidate() {
		string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
		// TODO dropdown list of tags?
	}
#endif
}
