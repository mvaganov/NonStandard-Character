using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonStandard.Ui {
	public class DragWithMouseOrResize : DragWithMouse {
		private bool pointerDown;
		public bool disableReize = true;
		public bool dynamicMouseCursor = true;
		public float resizeEdgeRadius = 10;
		public Vector2 minimumSize = new Vector2(-1, -1);
		[Tooltip("if negative values, calculate a minimum size based on child elements")]
		private Vector2 startSize;
		private Direction mouseCursorState = Direction.None;
		private Direction heldDir = Direction.None;

		public static void ResizeRect(RectTransform rectTransform, Vector2 move, Direction dir) {
			Vector2 min = rectTransform.offsetMin, max = rectTransform.offsetMax;
			if ((dir & Direction.Bottom) != 0) { min.y += move.y; }
			if ((dir & Direction.Left) != 0) { min.x += move.x; }
			if ((dir & Direction.Top) != 0) { max.y += move.y; }
			if ((dir & Direction.Right) != 0) { max.x += move.x; }
			rectTransform.offsetMin = min; rectTransform.offsetMax = max;
		}

		public void ResizeRect(Vector2 move, Direction dir) {
			ResizeRect(rt, move, dir);
			Rect size = rt.rect;
			if (size.width < minimumSize.x || size.height < minimumSize.y) {
				ResizeRect(rt, -move, dir);
			}
		}

		public static Direction CalculateEdgeDirection(Vector2 p, Vector2 min, Vector2 max, float edgeRadius) {
			float h = max.y - min.y, w = max.x - min.x;
			float top_ = Mathf.Abs(p.y - min.y);
			float left = Mathf.Abs(p.x - min.x);
			float bott = Mathf.Abs(p.y - max.y);
			float righ = Mathf.Abs(p.x - max.x);
			if (h < edgeRadius && w < edgeRadius) { // if the rectangle is too small, make sure at least one edge is always unmoved 
				float m = Mathf.Max(left, bott, righ, top_);
				if (top_ == m) { top_ = edgeRadius + 1; }
				if (left == m) { left = edgeRadius + 1; }
				if (bott == m) { bott = edgeRadius + 1; }
				if (righ == m) { righ = edgeRadius + 1; }
			}
			int result = 0;
			if (top_ < edgeRadius) result |= (int)Direction.Top;
			if (left < edgeRadius) result |= (int)Direction.Left;
			if (bott < edgeRadius) result |= (int)Direction.Bottom;
			if (righ < edgeRadius) result |= (int)Direction.Right;
			return (Direction)result;
		}

		protected override void Awake() {
			rt = GetComponent<RectTransform>();
			AddPointerEvent(EventTriggerType.PointerDown, PointerDown);
			AddPointerEvent(EventTriggerType.BeginDrag, BeginDrag);
			AddPointerEvent(EventTriggerType.Drag, OnDrag);
			AddPointerEvent(EventTriggerType.EndDrag, EndDrag);
			AddPointerEvent(EventTriggerType.PointerUp, PointerUp);
			//AddPointerEvent(EventTriggerType.Move, CursorChangeOnMove);
			AddPointerEvent(EventTriggerType.PointerEnter, PointerEnter);
			AddPointerEvent(EventTriggerType.PointerExit, PointerExit);
			enabled = false;
		}

		void Start() {
			startSize = rt.rect.size;
			if (minimumSize.x < 0 && minimumSize.y < 0) {
				minimumSize = CalculateMinimumSize();
			}
		}

		public Vector2 CalculateMinimumSize() {
			Vector2 s = Vector2.zero, prefSize;
			for (int i = 0; i < transform.childCount; ++i) {
				RectTransform child = transform.GetChild(i) as RectTransform;
				prefSize = new Vector2(LayoutUtility.GetPreferredSize(child, 0), LayoutUtility.GetPreferredSize(child, 1));
				s = new Vector2(Mathf.Max(prefSize.x, s.x), Mathf.Max(prefSize.y, s.y));
				//Debug.Log(child.name + " " + prefSize);
			}
			prefSize = new Vector2(LayoutUtility.GetPreferredSize(rt, 0), LayoutUtility.GetPreferredSize(rt, 1));
			s = new Vector2(Mathf.Max(prefSize.x, s.x), Mathf.Max(prefSize.y, s.y));
			s = new Vector2(Mathf.Min(startSize.x, s.x), Mathf.Min(startSize.y, s.y));
			//Debug.Log(rt.name + " " + prefSize);
			return s;
		}


		public virtual void PointerDown(BaseEventData data) {
			pointerDown = true;
			if (heldDir == Direction.None) {
				heldDir = mouseCursorState;
			}
		}

		public virtual void BeginDrag(BaseEventData data) {
			OnDrag(data);
			if (dynamicMouseCursor && heldDir == Direction.None) {
				MouseCursor mc = MouseCursor.Instance;
				if (mc != null) { mc.SetCursor(this, MouseCursor.CursorType.Move); }
			}
		}

		public override void OnDrag(BaseEventData basedata) {
			if (disableDrag) return;
			//isDragging = true;
			PointerEventData data = basedata as PointerEventData;
			if (heldDir == Direction.None) {
				rt.localPosition += (Vector3)data.delta;
			} else {
				ResizeRect(data.delta, heldDir);
			}
		}

		public virtual void EndDrag(BaseEventData data) { }

		public virtual void PointerUp(BaseEventData data) {
			pointerDown = false;
			heldDir = Direction.None;
			//if (!disableReize) {
			CursorChangeOnMove(null);
			//}
		}

		public void FixedUpdate() {
			if (!disableReize && !pointerDown && heldDir == Direction.None && resizeEdgeRadius != 0 && 
				Inputs.AppInput.MousePositionDelta != Vector3.zero) {
				CursorChangeOnMove(null);
			}
		}

		public virtual void PointerEnter(BaseEventData data) {
			//Debug.Log("enter "+this);
			enabled = resizeEdgeRadius != 0;
		}
		public virtual void PointerExit(BaseEventData data) {
			//Debug.Log("exit "+this);
			if (dynamicMouseCursor) {
				MouseCursor mc = MouseCursor.Instance;
				if (mc != null) { mc.UnsetCursor(this); }
			}
			if (heldDir == Direction.None) {
				mouseCursorState = Direction.None;
				enabled = false;
			}
		}

		public virtual void CursorChangeOnMove(BaseEventData data) {
			MouseCursor mc = MouseCursor.Instance;
			Vector3[] corners = new Vector3[4];
			rt.GetWorldCorners(corners);
			Vector2 min = corners[1], max = corners[3];
			Vector2 mousePosition = Input.mousePosition; // data.position;
			mouseCursorState = CalculateEdgeDirection(mousePosition, min, max, resizeEdgeRadius);
			if (!dynamicMouseCursor || mc == null) return;
			if (mouseCursorState != Direction.None) {
				mc.SetCursor(this, mouseCursorState);
			} else {
				mc.SetCursor(this, MouseCursor.CursorType.Cursor);
			}
		}
	}
}