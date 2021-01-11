using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace NonStandard.Ui {
	public class DragWithMouse : MonoBehaviour {
		protected RectTransform rt;
		public bool disableDrag;

		protected virtual void Awake() {
			rt = GetComponent<RectTransform>();
			AddPointerEvent(EventTriggerType.Drag, OnDrag);
		}

		public void AddPointerEvent(EventTriggerType type, UnityAction<BaseEventData> pointerEvent) {
			PointerTrigger.AddEvent(gameObject, type, pointerEvent);
		}

		public virtual void OnDrag(BaseEventData basedata) {
			if (disableDrag) return;
			PointerEventData data = basedata as PointerEventData;
			rt.localPosition += (Vector3)data.delta;
			if(rt.parent != null) {
				RectTransform parentRt = rt.parent.GetComponent<RectTransform>();
				KeepInBounds(parentRt.rect);
			}
		}

		public void KeepInBounds(Rect p) {
			Rect me = rt.rect;
			Vector2 pos = rt.localPosition;
			bool change = false;
			if (me.yMin < p.yMin) { pos.y = 0; change = true; }
			if (me.xMin < p.xMin) { pos.x = 0; change = true; }
			if (me.yMax > p.yMax) { pos.y = 0; change = true; }
			if (me.xMax > p.xMax) { pos.x = 0; change = true; }
			if (change) {
				rt.localPosition = pos;
			}
		}
	}
}