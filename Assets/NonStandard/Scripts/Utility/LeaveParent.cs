using UnityEngine;

namespace NonStandard.Utility {
	public class LeaveParent : MonoBehaviour {
		public Transform whereToGo = null;
		public void DoActivateTrigger() { transform.SetParent(whereToGo); }
		void Start () { DoActivateTrigger(); }
	}
}