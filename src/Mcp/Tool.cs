using System;
using System.Collections.Generic;
using SimpleJSON;

namespace VaMMCP.Mcp {
	/// <summary>One MCP tool: schema + handler. Handlers run on the VaM main thread.</summary>
	public class Tool {
		public string Name;
		public string Description;
		public int TimeoutMs = 30000;
		/// <summary>True if the handler itself must run on the HTTP thread (it uses MainThreadDispatcher.Run internally).</summary>
		public bool NeedsPoller;
		public Func<JSONNode, JSONNode> Handler;

		private readonly List<ParamDef> props = new List<ParamDef>();
		private readonly List<string> required = new List<string>();

		public Tool(string name, string description) {
			Name = name;
			Description = description;
		}

		/// <summary>Add an input property. Type: string|number|boolean|array|object|any.</summary>
		public Tool P(string propName, string type, string description, bool req = false) {
			props.Add(new ParamDef { Name = propName, Type = type, Description = description, Required = req });
			if (req) required.Add(propName);
			return this;
		}

		public Tool Timeout(int ms) {
			TimeoutMs = ms;
			return this;
		}

		public Tool Poller() {
			NeedsPoller = true;
			return this;
		}

		public Tool Fn(Func<JSONNode, JSONNode> handler) {
			Handler = handler;
			return this;
		}

		public JSONClass ToJson() {
			JSONClass p = new JSONClass();
			foreach (ParamDef d in props) {
				JSONClass s = new JSONClass();
				if (d.Type != "any") s["type"] = d.Type;
				s["description"] = d.Description;
				p[d.Name] = s;
			}
			JSONClass schema = new JSONClass();
			schema["type"] = "object";
			schema["properties"] = p;
			if (required.Count > 0) {
				JSONArray reqArr = new JSONArray();
				foreach (string r in required) reqArr.Add(r);
				schema["required"] = reqArr;
			}
			JSONClass t = new JSONClass();
			t["name"] = Name;
			t["description"] = Description;
			t["inputSchema"] = schema;
			return t;
		}

		private class ParamDef {
			public string Name;
			public string Type;
			public string Description;
			public bool Required;
		}
	}
}
