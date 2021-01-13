using UnityEngine;

namespace NonStandard.TouchGui {
	public class TouchGuiDrag : TouchColliderSensitive {
		public bool Dragged { get; protected set; }
		protected bool surpressDragFollow = false;
		[HideInInspector] public RectTransform rectTransform;
		Vector2 delta;
		int fingerId;

		public override void Start() {
			base.Start();
			rectTransform = GetComponent<RectTransform>();
			// TODO get the UI system working with these events, and see if mobile still works.
			Ui.PointerTrigger.AddEvent(gameObject, UnityEngine.EventSystems.EventTriggerType.PointerDown, PointerDown);
			Ui.PointerTrigger.AddEvent(gameObject, UnityEngine.EventSystems.EventTriggerType.Drag, PointerDrag);
			Ui.PointerTrigger.AddEvent(gameObject, UnityEngine.EventSystems.EventTriggerType.PointerUp, PointerUp);
		}
		public override void PointerDown(UnityEngine.EventSystems.BaseEventData data) {
			UnityEngine.EventSystems.PointerEventData pd = data as UnityEngine.EventSystems.PointerEventData;
			PressDown(pd.position);
			//Debug.Log("down");
		}
		public override void PointerDrag(UnityEngine.EventSystems.BaseEventData data) {
			UnityEngine.EventSystems.PointerEventData pd = data as UnityEngine.EventSystems.PointerEventData;
			Hold(pd.position);
			//Debug.Log("drag");
		}
		public override void PointerUp(UnityEngine.EventSystems.BaseEventData data) {
			UnityEngine.EventSystems.PointerEventData pd = data as UnityEngine.EventSystems.PointerEventData;
			Release(pd.position, null);
			//Debug.Log("release");
		}

		public override bool PressDown(TouchCollider tc) { return PressDown(tc.touch.position); }
		public bool PressDown(Vector2 position) {
			if (Dragged) {
				//Debug.Log("ignored touch before drag finished");
				return false;
			}
			if (triggeringCollider != null) {
				fingerId = triggeringCollider.touch.fingerId;
			} else { fingerId = -1; }
			Dragged = true;
			delta = (Vector2)rectTransform.position - position;//triggeringCollider.touch.position;
			return base.PressDown(null);
		}

		public void FollowDrag() {
			if (surpressDragFollow) return;
			TouchCollider tc = TouchGuiSystem.Instance().GetTouch(fingerId);
			if (tc == null) return;
			FollowDragInternal(tc.touch.position);
		}
		public void FollowDragInternal(Vector2 position) {
			if (!Dragged || surpressDragFollow) { return; }
			rectTransform.position = position + delta;
		}

		public override bool Hold(TouchCollider tc) { return Hold(tc.touch.position); }
		public bool Hold(Vector2 p) {
			FollowDragInternal(p);
			return base.Hold(null);
		}

		public override bool Release(TouchCollider tc) {
			return Release(tc.touch.position, tc);
		}
		public bool Release(Vector2 position, TouchCollider tc) {
			FollowDragInternal(position);
			if (tc == null || tc.touch.phase == TouchPhase.Ended || tc.touch.phase == TouchPhase.Canceled) {
				Dragged = false;
				return base.Release(tc);
			}
			return false;
		}
	}
}