using System;
using BepInEx.Logging;

namespace VaMMCP {
	/// <summary>Central logger (BepInEx LogOutput.log).</summary>
	public static class Log {
		private static ManualLogSource src;
		public static void Init(ManualLogSource s) { src = s; }
		public static void Info(string m) { if (src != null) src.LogInfo(m); }
		public static void Warn(string m) { if (src != null) src.LogWarning(m); }
		public static void Error(string m) { if (src != null) src.LogError(m); }
		public static void Debug(string m) { if (src != null) src.LogDebug(m); }
	}

	/// <summary>User-facing tool error (message goes back to the MCP client).</summary>
	public class ApiError : Exception {
		public ApiError(string message) : base(message) { }
	}
}
