using System.Globalization;
using System.Text;
using SimpleJSON;

namespace VaMMCP {
	/// <summary>
	/// VaM's SimpleJSON quotes every value it serialises, so a bool or a number written through
	/// it goes out as a JSON string. MCP clients built on the official SDK validate responses
	/// against the spec schema and match them by JSON-RPC id, and reject "false" where false is
	/// required or fail to pair "0" with request id 0. Protocol-level values therefore go through
	/// this node, which writes the token exactly as given.
	/// </summary>
	public class JSONRaw : JSONNode {
		private readonly string token;

		public JSONRaw(string token) {
			this.token = token;
		}

		public static JSONRaw Bool(bool value) {
			return new JSONRaw(value ? "true" : "false");
		}

		public static JSONRaw Num(int value) {
			return new JSONRaw(value.ToString(CultureInfo.InvariantCulture));
		}

		public static JSONRaw Num(long value) {
			return new JSONRaw(value.ToString(CultureInfo.InvariantCulture));
		}

		/// <summary>JSON has no NaN or Infinity, so those degrade to null.</summary>
		public static JSONRaw Num(float value) {
			if (float.IsNaN(value) || float.IsInfinity(value)) return Null();
			return new JSONRaw(value.ToString("0.######", CultureInfo.InvariantCulture));
		}

		public static JSONRaw Null() {
			return new JSONRaw("null");
		}

		/// <summary>
		/// Echo a JSON-RPC id back with its original JSON type. SimpleJSON parses 1 and "1" into
		/// the same node, so an id that looks like an integer is written as a number — which is
		/// what every client in practice sends.
		/// </summary>
		public static JSONNode Id(JSONNode id) {
			string s = id != null ? id.Value : null;
			if (string.IsNullOrEmpty(s)) return null;
			long n;
			if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) {
				return new JSONRaw(n.ToString(CultureInfo.InvariantCulture));
			}
			return new JSONData(s);
		}

		public override string Value {
			get { return token; }
			set { }
		}

		public override string ToString() {
			return token;
		}

		public override string ToString(string aPrefix) {
			return token;
		}

		public override void ToString(string aPrefix, StringBuilder sb) {
			sb.Append(token);
		}
	}
}
