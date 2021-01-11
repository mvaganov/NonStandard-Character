using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NonStandard.Ui {
	public static class PointerTrigger {
		/// <param name="pointerEvent">tip: try to typecast the <see cref="BaseEventData"/> as <see cref="PointerEventData"/></param>
		public static void AddEvent(GameObject go, EventTriggerType type, UnityAction<BaseEventData> pointerEvent) {
			EventTrigger et = go.GetComponent<EventTrigger>();
			if (et == null) { et = go.AddComponent<EventTrigger>(); }
			AddEvent(et, type, pointerEvent);
		}
		/// <param name="pointerEvent">tip: try to typecast the <see cref="BaseEventData"/> as <see cref="PointerEventData"/></param>
		public static void AddEvent(EventTrigger et, EventTriggerType type, UnityAction<BaseEventData> pointerEvent) {
			EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
			entry.callback.AddListener(pointerEvent);
			et.triggers.Add(entry);
		}
		public static void RemoveEvent(GameObject go, EventTriggerType type, UnityAction<BaseEventData> pointerEvent) {
			EventTrigger et = go.GetComponent<EventTrigger>();
			if (et != null) { RemoveEvent(et, type, pointerEvent); }
		}
		public static void RemoveEvent(EventTrigger et, EventTriggerType type, UnityAction<BaseEventData> pointerEvent) {
			EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
			entry.callback.RemoveListener(pointerEvent);
			et.triggers.Add(entry);
		}
	}
}