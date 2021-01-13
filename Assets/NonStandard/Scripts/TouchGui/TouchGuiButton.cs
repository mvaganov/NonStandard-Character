namespace NonStandard.TouchGui {
	public class TouchGuiButton : TouchColliderSensitive {
		public Inputs.KBind.EventSet eventSet;

		public override bool PressDown(TouchCollider tc) {
			eventSet.DoPress();
			return base.PressDown(tc);
		}
		public override bool Hold(TouchCollider tc) {
			eventSet.DoHold();
			return base.Hold(tc);
		}
		public override bool Release(TouchCollider tc) {
			eventSet.DoRelease();
			return base.Release(tc);
		}

		public override void Start() {
			base.Start();
			Ui.PointerTrigger.AddEvent(gameObject, UnityEngine.EventSystems.EventTriggerType.PointerDown, PointerDown);
			Ui.PointerTrigger.AddEvent(gameObject, UnityEngine.EventSystems.EventTriggerType.Drag, PointerDrag);
			Ui.PointerTrigger.AddEvent(gameObject, UnityEngine.EventSystems.EventTriggerType.PointerUp, PointerUp);
		}
	}
}