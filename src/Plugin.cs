using System;
using BepInEx;
using BepInEx.Configuration;
using VaMMCP.Mcp;

namespace VaMMCP {
	[BepInPlugin("com.vammcp.core", "VaMMCP", "1.0.1")]
	public class Plugin : BaseUnityPlugin {
		public static Plugin instance;
		public static ConfigEntry<bool> cfgEnabled;
		public static ConfigEntry<int> cfgPort;
		public static ConfigEntry<bool> cfgAllowEval;
		public static ConfigEntry<int> cfgEvalTimeoutSec;

		private MainThreadDispatcher dispatcher;
		private HttpServer httpServer;

		private void Awake() {
			instance = this;
			Log.Init(Logger);

			cfgEnabled = Config.Bind("Server", "Enabled", true, "Start the MCP HTTP server when VaM launches");
			cfgPort = Config.Bind("Server", "Port", 9837, "Port for the MCP Streamable HTTP endpoint (bound to 127.0.0.1 only)");
			cfgAllowEval = Config.Bind("Security", "AllowEval", false, "Enable the eval_cs tool (runs arbitrary C# inside the VaM process). Only enable on trusted machines.");
			cfgEvalTimeoutSec = Config.Bind("Security", "EvalTimeoutSec", 30, "Timeout for eval_cs executions");

			dispatcher = new MainThreadDispatcher();

			if (cfgEnabled.Value) {
				try {
					httpServer = new HttpServer(new McpServer(dispatcher, cfgAllowEval.Value), cfgPort.Value);
					httpServer.Start();
					Log.Info("VaMMCP ready. MCP endpoint: http://127.0.0.1:" + cfgPort.Value + "/mcp");
				} catch (Exception e) {
					Log.Error("VaMMCP failed to start the MCP server: " + e);
				}
			} else {
				Log.Info("VaMMCP loaded but the MCP server is disabled (Server.Enabled=false).");
			}
		}

		private void Update() {
			if (dispatcher != null) {
				dispatcher.Update();
			}
		}

		private void OnDestroy() {
			if (httpServer != null) {
				try {
					httpServer.Stop();
				} catch (Exception e) {
					Log.Debug("stop: " + e.Message);
				}
			}
		}
	}
}
