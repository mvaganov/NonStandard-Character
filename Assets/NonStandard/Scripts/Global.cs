using UnityEngine;

namespace NonStandard {
	public class Global : MonoBehaviour {
		private static Global _instance;
		public static Global Instance() {
			return (_instance) ? _instance : _instance = new GameObject("<global>").AddComponent<Global>();
		}
		public static GameObject Get() { return Instance().gameObject; }
		public static T Get<T>() where T : MonoBehaviour {
			T instance = _instance.GetComponent<T>();
			if (instance == null) { instance = _instance.gameObject.AddComponent<T>(); }
			return instance;
		}
		public void Pause() { Clock.Instance.Pause(); }
		public void Unpause() { Clock.Instance.Unpause(); }
		public void TogglePause() { Clock c = Clock.Instance; if(c.IsPaused) { c.Unpause(); } else { c.Pause(); } }
	}
}