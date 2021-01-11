using System;

namespace NonStandard.Ui {
	[Flags]
	public enum Direction {
		None = 0, Bottom = 1, Left = 2, Top = 4, Right = 8,
		TopLeft = Top | Left, TopRight = Top | Right,
		BottomLeft = Bottom | Left, BottomRight = Bottom | Right,
		Horizontal = Left | Right, Vertical = Bottom | Top,
		HorizontalBottom = ~Top & All, HorizontalTop = ~Bottom & All,
		VerticalLeft = ~Right & All, VerticalRight = ~Left & All,
		All = Bottom | Top | Left | Right
	};

	public static class DirectionExtension {
		public static Direction Opposite(this Direction orig) { return (Direction)(((int)orig >> 2) | ((int)orig << 2)) & Direction.All; }
		public static bool HasFlag(this Direction cs, Direction flag) { return ((int)cs & (int)flag) != 0; }
	}
}
