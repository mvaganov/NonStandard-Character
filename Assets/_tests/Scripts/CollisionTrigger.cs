using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class UnityEvent_GameObject : UnityEvent<GameObject> { }

public class CollisionTrigger : MonoBehaviour {
	public UnityEvent_GameObject onTrigger = new UnityEvent_GameObject();
	public string tagLimit;
	void DoActivateTrigger() { if (enabled) onTrigger.Invoke(null); }
	void DoActivateTrigger(GameObject other) { if(enabled) onTrigger.Invoke(other); }
	void OnTriggerEnter(Collider other) {
		if(tagLimit == null || other.gameObject.tag == tagLimit) { DoActivateTrigger(other.gameObject); }
	}
	void OnCollisionEnter(Collision collision) {
		if (tagLimit == null || collision.gameObject.tag == tagLimit) { DoActivateTrigger(collision.gameObject); }
	}

#if UNITY_EDITOR
	void OnValidate() {
		string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
		
	}
#endif
}
