using UnityEngine;

namespace NonStandard {
	public class Global : MonoBehaviour {
		private static Global _instance;
		public static Global Instance() {
			if (_instance) { return _instance; }
			_instance = FindObjectOfType<Global>();
			if (!_instance) { _instance = new GameObject("<global>").AddComponent<Global>(); }
			return _instance;
		}
		public static GameObject Get() { return Instance().gameObject; }
		public static T Get<T>() where T : MonoBehaviour {
			T componentInstance = Instance().GetComponent<T>();
			if (componentInstance == null) { componentInstance = _instance.gameObject.AddComponent<T>(); }
			return componentInstance;
		}
		public void Pause() { Clock.Instance.Pause(); }
		public void Unpause() { Clock.Instance.Unpause(); }
		public void TogglePause() { Clock c = Clock.Instance; if(c.IsPaused) { c.Unpause(); } else { c.Pause(); } }
		void Start() { Instance(); }
	}
}