using System.Collections.Generic;

namespace NonStandard.Data {
	public struct ParseError {
		public int row, col;
		public string message;
		public ParseError(int r, int c, string m) { row = r; col = c; message = m; }
		public ParseError(Token token, IList<int> rows, string m) {
			CodeParse.FilePositionOf(token, rows, out row, out col);
			message = m;
		}
		public override string ToString() { return "@" + row + "," + col + ": " + message; }
		public static ParseError None = default(ParseError);
		public void OffsetBy(Token token, IList<int> rows) {
			int r, c; CodeParse.FilePositionOf(token, rows, out r, out c); row += r; col += c;
		}
	}
}
