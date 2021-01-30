using System;
using System.Collections.Generic;
using UnityEngine;

namespace NonStandard.Ui {
	public class MouseCursor : MonoBehaviour {
		public CursorSet[] cursors = new CursorSet[] { };

		public CursorType currentCursor = CursorType.Cursor;

		[ContextMenuItem("Add Cursor Assets", "AddCursors")]
		public int currentSet;
		private static MouseCursor _instance;

		public enum CursorType {
			Cursor = 0, CursorDrag, Pointer, No, Horizontal, Vertical, Diagonal, Diagonal2, Move, Move2, 
			Add, Subtract, ZoomIn, ZoomOut, Text, Shortcut, Circle
		}

		public static MouseCursor Instance {
			get {
				if (_instance) return _instance;
				if (Inputs.AppInput.IsQuitting) return null;
				_instance = FindObjectOfType<MouseCursor>();
				if (!_instance) {
					Debug.LogWarning("A MouseCursor object must be created to use mouse cursor changes");
				}
				return _instance;
			}
		}

		[Serializable]
		public class Cursor {
			public string name;
			public Texture2D texture;
			public Vector2 hotspot;
		}

		[Serializable]
		public class CursorSet {
			public string name;
			public Cursor[] cursors;

			public static CursorSet Empty() {
				CursorSet set = new CursorSet { name = "cursors", cursors = new Cursor[Enum.GetValues(typeof(CursorType)).Length] };
				for (int i = 0; i < set.cursors.Length; ++i) {
					set.cursors[i] = new Cursor { name = ((CursorType)i).ToString(), hotspot = new Vector2(11, 11) };
				}
				set.cursors[((int)CursorType.Add)].hotspot = Vector2.zero;
				set.cursors[((int)CursorType.Subtract)].hotspot = Vector2.zero;
				set.cursors[((int)CursorType.Shortcut)].hotspot = Vector2.zero;
				return set;
			}
		}

		[Serializable]
		public class CursorRotation {
			public bool Rotating;
			public float Angle, Speed;
			public void Update() {
				Angle += Time.deltaTime * Speed;
				while (Angle >= 360) Angle -= 360;
			}
		}

		[SerializeField] private CursorRotation rotation = new CursorRotation { Rotating = false, Speed = 90 };
		public class CursorUiRequest {
			public object source;
			public CursorType cursor;
			public void Apply(MouseCursor mc) {
				mc.SetCursor(cursor);
			}
			public void Set(MouseCursor mc, CursorType type, bool alsoApply = true) {
				cursor = type;
				if (alsoApply) { Apply(mc); }
			}
		}

		public List<CursorUiRequest> cursorUiRequest = new List<CursorUiRequest>();

		public int CursorUiStackDepth(object source) {
			for (int i = cursorUiRequest.Count - 1; i >= 0; --i) {
				if (cursorUiRequest[i].source == source) { return i; }
			}
			return -1;
		}

		public CursorUiRequest MostRelevantCursor { get { return cursorUiRequest != null && cursorUiRequest.Count > 0 
					? cursorUiRequest[cursorUiRequest.Count - 1] : null; } }

		public CursorUiRequest GetCursorRequest(object source, bool createIfNotFound = false) {
			int index = CursorUiStackDepth(source);
			if (index >= 0) return cursorUiRequest[index];
			if (createIfNotFound) {
				CursorUiRequest newOne = new CursorUiRequest { source = source, cursor = CursorType.Cursor };
				cursorUiRequest.Add(newOne);
				return newOne;
			}
			return null;
		}

		public Cursor GetCurrentGetCursorData() { return cursors[currentSet].cursors[(int)currentCursor]; }

		public void SetCursorSet(int cursorSetIndex) { currentSet = cursorSetIndex; }

		public void SetCursor(CursorType type) {
			currentCursor = type;
			Cursor cursor = GetCurrentGetCursorData();
			UnityEngine.Cursor.SetCursor(cursor.texture, cursor.hotspot, CursorMode.Auto);
		}

		public void Start() {
			#if !RENDER_SOFTWARE_CURSOR
			Debug.LogWarning("software mouse cursor may not work correctly...");
			#endif
			SetCursor(currentCursor);
			if(rotation.Rotating || rotation.Angle != 0) { AddRotator(); }
		}

		public void SetCursor(object source, Direction uiDirection) {
			SetCursor(source, TranslateCursor(uiDirection));
		}

		public void SetCursor(object source, CursorType cursorType) {
			CursorUiRequest cursorStackEntry = GetCursorRequest(source, true);
			bool isTop = MostRelevantCursor.source == source;
			cursorStackEntry.Set(this, cursorType, isTop);
		}

		public void UnsetCursor(object source) {
			CursorUiRequest top = MostRelevantCursor;
			if (top == null) return;
			if (top.source == source) {
				cursorUiRequest.RemoveAt(cursorUiRequest.Count - 1);
			}
			if (cursorUiRequest.Count > 0) {
				top = MostRelevantCursor;
				top.Apply(this);
			} else {
				SetCursor(CursorType.Cursor);
			}
		}

		public void SetCursor(Direction uiDirection) { SetCursor(TranslateCursor(uiDirection)); }

		public CursorType TranslateCursor(Direction uiDirection) {
			switch (uiDirection) {
			case Direction.All: return CursorType.Move;
			case Direction.Bottom: return CursorType.Vertical;
			case Direction.BottomLeft: return CursorType.Diagonal2;
			case Direction.BottomRight: return CursorType.Diagonal;
			case Direction.Horizontal: return CursorType.Horizontal;
			case Direction.HorizontalBottom: return CursorType.Vertical;
			case Direction.HorizontalTop: return CursorType.Vertical;
			case Direction.Left: return CursorType.Horizontal;
			case Direction.None: return CursorType.No;
			case Direction.Right: return CursorType.Horizontal;
			case Direction.Top: return CursorType.Vertical;
			case Direction.TopLeft: return CursorType.Diagonal;
			case Direction.TopRight: return CursorType.Diagonal2;
			case Direction.Vertical: return CursorType.Vertical;
			case Direction.VerticalLeft: return CursorType.Horizontal;
			case Direction.VerticalRight: return CursorType.Horizontal;
			}
			return CursorType.Cursor;
		}

		private MouseCursorRotator rotator;
		private bool IsUsingRotatedCursor() { return rotator != null && (rotation.Angle != 0 || rotation.Rotating); }
		private void AddRotator() {
			if (rotator != null) return;
			rotator = gameObject.AddComponent<MouseCursorRotator>();
			rotator.SetRotationData(rotation);
			UnityEngine.Cursor.visible = false;
		}
		public bool IsCursorRotating {
			get { return rotation.Rotating; }
			set {
				if (rotator != null) rotation.Rotating = value;
				else if (value) { AddRotator(); }
				if (value) {
					UnityEngine.Cursor.visible = false;
					rotator.enabled = true;
				} else if(CursorRotationAngle == 0) {
					if(rotator != null) rotator.enabled = false;
					UnityEngine.Cursor.visible = true;
				}
			}
		}
		public float CursorRotationSpeed {
			get { return rotation.Speed; }
			set {
				if (value != 0 && IsCursorRotating && rotator == null) { AddRotator(); }
				rotation.Speed = value;
			}
		}
		public float CursorRotationAngle {
			get {
				if (rotator != null) return rotation.Angle;
				return 0;
			}
			set {
				if (value != 0 && rotator == null) {
					AddRotator();
					IsCursorRotating = false;
				}
				rotation.Angle = value;
				if(value == 0 && !IsCursorRotating) {
					if(rotator != null) rotator.enabled = false;
					UnityEngine.Cursor.visible = true;
				} else {
					if (rotator != null) rotator.enabled = true;
					UnityEngine.Cursor.visible = false;
				}
			}
		}
		private bool _visible = true;
		public bool Visible {
			get { return _visible; }
			set {
				_visible = value;
				if (_visible) {
					if(IsUsingRotatedCursor()) {
						UnityEngine.Cursor.visible = false;
						rotator.enabled = true;
					} else {
						UnityEngine.Cursor.visible = true;
						rotator.enabled = false;
					}
				} else {
					UnityEngine.Cursor.visible = false;
					rotator.enabled = false;
				}
			}
		}
		public class MouseCursorRotator : MonoBehaviour {
			private CursorRotation rot;
			public void SetRotationData(CursorRotation r) { rot = r; }
			void Update() {
				if (!rot.Rotating) return;
				rot.Update();
			}
			private Texture2D tex;
			private Cursor cursor;
			private Rect rect;
			private Vector3 pivot;
			void OnGUI() {
				cursor = Instance.GetCurrentGetCursorData();
				tex = cursor.texture;
				float x = Event.current.mousePosition.x - cursor.hotspot.x;
				float y = Event.current.mousePosition.y - cursor.hotspot.y;
				pivot = new Vector2(x + cursor.hotspot.x, y + cursor.hotspot.y);
				rect = new Rect(x, y, tex.width, tex.height);
				Matrix4x4 matx = GUI.matrix;
				GUIUtility.RotateAroundPivot(rot.Angle, pivot);
				GUI.DrawTexture(rect, tex);
				GUI.matrix = matx;
			}
		}
#if UNITY_EDITOR
		private void CheckForMultipleMouseCursorObjects() {
			MouseCursor[] mouseCursors = FindObjectsOfType<MouseCursor>();
			if (mouseCursors.Length > 1) {
				Debug.LogError("Found " + mouseCursors.Length + " mouse cursors (should only have one):\n" +
					string.Join("\n", Array.ConvertAll(mouseCursors, mc => mc.transform.HierarchyPath())));
			}
		}
		void Reset() {
			if(cursors == null || cursors.Length == 0) { AddCursors(); }
			CheckForMultipleMouseCursorObjects();
		}
		void OnValidate() {
			CheckForMultipleMouseCursorObjects();
			if (!Application.isPlaying) { return; }
			CursorRotationAngle = rotation.Angle;
			CursorRotationSpeed = rotation.Speed;
			IsCursorRotating = rotation.Rotating;
		}
		private const string CursorFolderName = "cursor/";
		private string PathFilter(string path, int parentFolderCount) {
			if (parentFolderCount < 0) return path;
			int index = path.LastIndexOf('/');
			while (parentFolderCount > 0) {
				if (index < 0) return "";
				index = path.LastIndexOf('/', index - 1);
				--parentFolderCount;
			}
			return path.Substring(index + 1);
		}
		private void FindTextures(out string[] paths, out string[] guids, int pathDepth = -1) {
			guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D");
			paths = Array.ConvertAll(guids, g => PathFilter(UnityEditor.AssetDatabase.GUIDToAssetPath(g), pathDepth));
		}
		private CursorSet AddSet(string name) {
			CursorSet set = CursorSet.Empty();
			set.name = name;
			string[] paths, guids;
			FindTextures(out paths, out guids, 2);
			string path = CursorFolderName.ToLower() + name.ToLower() + "/";
			for(int i = 0; i < set.cursors.Length; ++i) {
				Cursor c = set.cursors[i];
				string textureName = path + c.name.ToLower();
				int index = Array.FindIndex(paths, s=>s.ToLower().StartsWith(textureName));
				if(index >= 0) {
					Texture2D t = (Texture2D)UnityEditor.AssetDatabase.LoadAssetAtPath(
						UnityEditor.AssetDatabase.GUIDToAssetPath(guids[index]), typeof(Texture2D));
					c.texture = t;
				} else {
					Debug.Log(textureName+" not found");
				}
			}
			Array.Resize(ref cursors, cursors.Length + 1);
			cursors[cursors.Length - 1] = set;
			return set;
		}
		private void AddCursors() {
			string[] paths, guids;
			FindTextures(out paths, out guids, 2);
			List<string> options = new List<string>();
			string cfn = CursorFolderName.ToLower();
			for(int i = 0; i < paths.Length; ++i) {
				if(paths.Length > 0 && paths[i].ToLower().StartsWith(cfn)) {
					int index = paths[i].IndexOf('/', cfn.Length);
					options.Add(paths[i].Substring(cfn.Length, index- cfn.Length));
				}
			}
			for(int i = 0; i < options.Count; ++i) {
				if (Array.FindIndex(cursors, c => c.name.ToLower().Contains(options[i].ToLower())) >= 0) continue;
				AddSet(options[i]);
			}
		}
#endif
	}
}