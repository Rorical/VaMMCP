using System;
using System.Collections.Generic;
using SimpleJSON;
using VaMMCP.Api;

namespace VaMMCP.Mcp {
	/// <summary>
	/// MCP (Model Context Protocol) server: JSON-RPC 2.0 over Streamable HTTP.
	/// Implements initialize, ping, tools/list, tools/call, resources/list, resources/read.
	/// </summary>
	public class McpServer {
		private const string ProtocolVersion = "2025-06-18";
		public const string ServerName = "VaMMCP";
		public const string ServerVersion = "1.0.0";

		private readonly MainThreadDispatcher mt;
		private readonly List<Tool> tools;
		private readonly VaMApi api;

		public McpServer(MainThreadDispatcher mt, bool allowEval) {
			this.mt = mt;
			this.api = new VaMApi(mt);
			this.tools = ToolRegistry.CreateAll(api, allowEval);
		}

		public McpHttpResult HandleHttp(string body) {
			JSONNode node;
			try {
				node = JSON.Parse(body);
			} catch {
				return Err(null, -32700, "Parse error");
			}
			JSONClass obj = node != null ? node.AsObject : null;
			if (obj == null) return Err(null, -32700, "Parse error: expected a JSON-RPC object");

			string method = obj["method"] != null ? obj["method"].Value : null;
			JSONNode idNode = obj["id"];

			if (method == null) {
				return Err(idNode, -32600, "Invalid Request: missing method");
			}
			if (method.StartsWith("notifications/")) {
				// notifications are accepted with 202 and no body
				return McpHttpResult.Accepted();
			}

			JSONClass result;
			try {
				switch (method) {
					case "initialize":
						result = DoInitialize(obj);
						break;
					case "ping":
						result = new JSONClass();
						break;
					case "tools/list":
						result = DoToolsList();
						break;
					case "tools/call":
						result = DoToolsCall(obj["params"]);
						break;
					case "resources/list":
						result = DoResourcesList();
						break;
					case "resources/read":
						result = DoResourcesRead(obj["params"]);
						break;
					case "logging/setLevel":
						result = new JSONClass();
						break;
					case "completion/complete":
						result = new JSONClass();
						break;
					default:
						return Err(idNode, -32601, "Method not found: " + method);
				}
			} catch (ApiError ae) {
				Log.Warn("mcp method " + method + " failed: " + ae.Message);
				return Err(idNode, -32603, "Internal error: " + ae.Message);
			} catch (Exception e) {
				Log.Error("mcp method " + method + " failed: " + e);
				return Err(idNode, -32603, "Internal error: " + e.Message);
			}

			JSONClass resp = new JSONClass();
			resp["jsonrpc"] = "2.0";
			if (idNode != null) resp["id"] = idNode;
			resp["result"] = result;
			return McpHttpResult.Json(200, resp.ToString());
		}

		private static McpHttpResult Err(JSONNode id, int code, string message) {
			JSONClass resp = new JSONClass();
			resp["jsonrpc"] = "2.0";
			if (id != null) resp["id"] = id;
			JSONClass err = new JSONClass();
			err["code"] = new JSONData(code);
			err["message"] = message;
			resp["error"] = err;
			return McpHttpResult.Json(200, resp.ToString());
		}

		private static JSONClass DoInitialize(JSONClass obj) {
			JSONClass capabilities = new JSONClass();
			JSONClass toolsCap = new JSONClass();
			toolsCap["listChanged"] = "false";
			capabilities["tools"] = toolsCap;
			JSONClass resCap = new JSONClass();
			resCap["subscribe"] = "false";
			capabilities["resources"] = resCap;

			JSONClass info = new JSONClass();
			info["name"] = ServerName;
			info["version"] = ServerVersion;

			JSONClass r = new JSONClass();
			r["protocolVersion"] = ProtocolVersion;
			if (obj["params"] != null && obj["params"]["protocolVersion"] != null) {
				string pv = obj["params"]["protocolVersion"].Value;
				if (pv == "2024-11-05" || pv == "2025-03-26") r["protocolVersion"] = pv;
			}
			r["capabilities"] = capabilities;
			r["serverInfo"] = info;
			return r;
		}

		private JSONClass DoToolsList() {
			JSONArray arr = new JSONArray();
			foreach (Tool t in tools) arr.Add(t.ToJson());
			JSONClass r = new JSONClass();
			r["tools"] = arr;
			return r;
		}

		private JSONClass DoToolsCall(JSONNode p) {
			string name = p != null && p["name"] != null ? p["name"].Value : "";
			Tool tool = null;
			foreach (Tool t in tools) {
				if (t.Name == name) { tool = t; break; }
			}
			if (tool == null) throw new ApiError("unknown tool: " + name);

			JSONNode args = (p != null && p["arguments"] != null) ? p["arguments"] : new JSONClass();
			Log.Info("tool call: " + name + " " + args.ToString());

			JSONNode outNode;
			try {
				// Tools that poll for async results must run on the caller thread (they use Mt.Run internally).
				outNode = tool.NeedsPoller
					? tool.Handler(args)
					: mt.Run(() => tool.Handler(args), tool.TimeoutMs);
			} catch (ApiError ae) {
				return ToolError("Error: " + ae.Message);
			} catch (Exception e) {
				Log.Error("tool " + name + " failed: " + e);
				return ToolError("Error: " + e.Message);
			}

			JSONArray content = new JSONArray();
			JSONClass textItem = new JSONClass();
			textItem["type"] = "text";
			textItem["text"] = outNode != null ? outNode.ToString() : "{}";
			content.Add(textItem);

			JSONClass r = new JSONClass();
			r["content"] = content;
			r["isError"] = "false";
			return r;
		}

		private static JSONClass ToolError(string message) {
			JSONArray content = new JSONArray();
			JSONClass textItem = new JSONClass();
			textItem["type"] = "text";
			textItem["text"] = message;
			content.Add(textItem);
			JSONClass r = new JSONClass();
			r["content"] = content;
			r["isError"] = "true";
			return r;
		}

		private static JSONClass DoResourcesList() {
			JSONArray arr = new JSONArray();
			arr.Add(Resource("vam://status", "VaM runtime status", "application/json"));
			arr.Add(Resource("vam://atoms", "Atoms in the current scene", "application/json"));
			JSONClass r = new JSONClass();
			r["resources"] = arr;
			return r;
		}

		private static JSONNode Resource(string uri, string name, string mime) {
			JSONClass r = new JSONClass();
			r["uri"] = uri;
			r["name"] = name;
			r["mimeType"] = mime;
			return r;
		}

		private JSONClass DoResourcesRead(JSONNode p) {
			string uri = p != null && p["uri"] != null ? p["uri"].Value : "";
			string text;
			if (uri == "vam://status") {
				text = mt.Run(() => api.Status()).ToString();
			} else if (uri == "vam://atoms") {
				text = mt.Run(() => api.ListAtoms(new JSONClass())).ToString();
			} else {
				throw new ApiError("unknown resource: " + uri);
			}
			JSONArray contents = new JSONArray();
			JSONClass c = new JSONClass();
			c["uri"] = uri;
			c["mimeType"] = "application/json";
			c["text"] = text;
			contents.Add(c);
			JSONClass r = new JSONClass();
			r["contents"] = contents;
			return r;
		}
	}
}
