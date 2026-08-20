using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VaMMCP.Mcp {
	public class McpHttpResult {
		public int Code;
		public string Body;
		public bool Empty;
		public static McpHttpResult Accepted() { return new McpHttpResult { Code = 202, Empty = true }; }
		public static McpHttpResult Json(int code, string body) { return new McpHttpResult { Code = code, Body = body }; }
	}

	public class HttpRequest {
		public string Method = "";
		public string Path = "";
		public Dictionary<string, string> Headers = new Dictionary<string, string>();
		public string Body = "";
		public bool KeepAlive = true;
	}

	/// <summary>
	/// Minimal HTTP/1.1 server for the MCP Streamable HTTP transport.
	/// POST /mcp -> JSON-RPC (single JSON object response, per spec 2025-06-18).
	/// GET  /mcp -> 405 (SSE stream not offered, also allowed by the spec).
	/// Binds to 127.0.0.1 only and validates the Origin header (DNS-rebinding protection).
	/// </summary>
	public class HttpServer {
		private readonly McpServer mcp;
		private readonly int port;
		private TcpListener listener;
		private Thread acceptThread;
		private volatile bool running;

		public HttpServer(McpServer mcp, int port) {
			this.mcp = mcp;
			this.port = port;
		}

		public void Start() {
			running = true;
			listener = new TcpListener(IPAddress.Loopback, port);
			listener.Start();
			acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "VaMMCP-accept" };
			acceptThread.Start();
		}

		public void Stop() {
			running = false;
			try {
				if (listener != null) listener.Stop();
			} catch { }
		}

		private void AcceptLoop() {
			while (running) {
				TcpClient client = null;
				try {
					client = listener.AcceptTcpClient();
					TcpClient c = client;
					ThreadPool.QueueUserWorkItem(delegate { HandleClient(c); });
				} catch (Exception e) {
					if (running) Log.Error("accept error: " + e.Message);
					try { if (client != null) client.Close(); } catch { }
					if (running) Thread.Sleep(100);
				}
			}
		}

		private void HandleClient(TcpClient client) {
			try {
				client.NoDelay = true;
				NetworkStream stream = client.GetStream();
				stream.ReadTimeout = 60000;
				stream.WriteTimeout = 60000;
				byte[] pending = new byte[0];
				bool keepAlive = true;
				while (keepAlive && running) {
					HttpRequest req;
					try {
						req = ReadRequest(stream, ref pending);
					} catch (IOException) { break; }
					catch (SocketException) { break; }
					if (req == null) break;

					if (!OriginAllowed(req.Headers)) {
						WriteResponse(stream, 403, "Forbidden", "application/json", "{\"error\":\"origin not allowed\"}", false);
						break;
					}
					if (req.Method == "OPTIONS") {
						WriteResponse(stream, 204, "No Content", null, null, true);
						continue;
					}
					if (req.Method == "GET") {
						WriteResponse(stream, 405, "Method Not Allowed", "text/plain", "MCP endpoint: POST /mcp (SSE GET not offered)", false);
						break;
					}
					if (req.Method != "POST") {
						WriteResponse(stream, 405, "Method Not Allowed", "text/plain", "method not supported", false);
						break;
					}
					if (req.Path != "/mcp" && !req.Path.EndsWith("/mcp")) {
						WriteResponse(stream, 404, "Not Found", "text/plain", "not found (use POST /mcp)", false);
						break;
					}

					McpHttpResult res;
					try {
						res = mcp.HandleHttp(req.Body);
					} catch (Exception e) {
						Log.Error("mcp handler error: " + e);
						res = McpHttpResult.Json(500, "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32603,\"message\":\"internal error\"}}");
					}
					if (res.Empty) {
						WriteResponse(stream, 202, "Accepted", null, null, req.KeepAlive);
					} else {
						WriteResponse(stream, res.Code, "OK", "application/json", res.Body, req.KeepAlive);
					}
					keepAlive = req.KeepAlive;
				}
			} catch (Exception e) {
				Log.Debug("client handler error: " + e.Message);
			} finally {
				try { client.Close(); } catch { }
			}
		}

		private static bool OriginAllowed(Dictionary<string, string> headers) {
			string origin;
			if (!headers.TryGetValue("origin", out origin) || string.IsNullOrEmpty(origin)) return true;
			try {
				Uri u = new Uri(origin);
				string h = u.Host.ToLowerInvariant();
				return h == "localhost" || h == "127.0.0.1" || h == "[::1]";
			} catch {
				return false;
			}
		}

		private static HttpRequest ReadRequest(NetworkStream stream, ref byte[] pending) {
			List<byte> buf = new List<byte>(pending.Length + 1024);
			buf.AddRange(pending);
			pending = new byte[0];
			int headerEnd = IndexOf(buf, "\r\n\r\n");
			while (headerEnd < 0) {
				byte[] chunk = new byte[8192];
				int n = stream.Read(chunk, 0, chunk.Length);
				if (n <= 0) return null;
				for (int i = 0; i < n; i++) buf.Add(chunk[i]);
				if (buf.Count > 65536) throw new IOException("headers too large");
				headerEnd = IndexOf(buf, "\r\n\r\n");
			}

			string headerText = Encoding.UTF8.GetString(buf.ToArray(), 0, headerEnd);
			string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
			if (lines.Length == 0) return null;
			string[] reqLine = lines[0].Split(' ');
			if (reqLine.Length < 3) return null;

			HttpRequest req = new HttpRequest();
			req.Method = reqLine[0].ToUpperInvariant();
			string path = reqLine[1];
			int q = path.IndexOf('?');
			req.Path = q >= 0 ? path.Substring(0, q) : path;
			req.KeepAlive = !reqLine[2].StartsWith("HTTP/1.0");

			for (int i = 1; i < lines.Length; i++) {
				string line = lines[i];
				int c = line.IndexOf(':');
				if (c <= 0) continue;
				string k = line.Substring(0, c).Trim().ToLowerInvariant();
				string v = line.Substring(c + 1).Trim();
				if (req.Headers.ContainsKey(k)) req.Headers[k] = v; else req.Headers.Add(k, v);
				if (k == "connection" && v.ToLowerInvariant().Contains("close")) req.KeepAlive = false;
			}

			int contentLength = 0;
			if (req.Headers.ContainsKey("content-length")) {
				if (!int.TryParse(req.Headers["content-length"], out contentLength)) return null;
			}
			if (contentLength < 0 || contentLength > 8 * 1024 * 1024) throw new IOException("bad content length");

			int bodyStart = headerEnd + 4;
			if (bodyStart < buf.Count) {
				int have = buf.Count - bodyStart;
				if (have >= contentLength) {
					if (contentLength > 0) {
						req.Body = Encoding.UTF8.GetString(buf.ToArray(), bodyStart, contentLength);
						byte[] rest = new byte[have - contentLength];
						Array.Copy(buf.ToArray(), bodyStart + contentLength, rest, 0, have - contentLength);
						pending = rest;
					}
					return req;
				}
				byte[] body = new byte[contentLength];
				Array.Copy(buf.ToArray(), bodyStart, body, 0, have);
				ReadExact(stream, body, have);
				req.Body = Encoding.UTF8.GetString(body);
			} else {
				byte[] body = new byte[contentLength];
				ReadExact(stream, body, 0);
				req.Body = Encoding.UTF8.GetString(body);
			}
			return req;
		}

		private static void ReadExact(NetworkStream stream, byte[] body, int start) {
			int off = start;
			while (off < body.Length) {
				int n = stream.Read(body, off, body.Length - off);
				if (n <= 0) throw new IOException("connection closed");
				off += n;
			}
		}

		private static int IndexOf(List<byte> buf, string marker) {
			byte[] m = Encoding.ASCII.GetBytes(marker);
			int last = buf.Count - m.Length;
			for (int i = 0; i <= last; i++) {
				bool match = true;
				for (int j = 0; j < m.Length; j++) {
					if (buf[i + j] != m[j]) { match = false; break; }
				}
				if (match) return i;
			}
			return -1;
		}

		private static void WriteResponse(NetworkStream s, int code, string reason, string contentType, string body, bool keepAlive) {
			StringBuilder sb = new StringBuilder(256);
			sb.Append("HTTP/1.1 ").Append(code).Append(' ').Append(reason).Append("\r\n");
			if (contentType != null) {
				sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
			}
			int len = body != null ? Encoding.UTF8.GetByteCount(body) : 0;
			sb.Append("Content-Length: ").Append(len).Append("\r\n");
			sb.Append("Connection: ").Append(keepAlive ? "keep-alive" : "close").Append("\r\n");
			sb.Append("Access-Control-Allow-Origin: *\r\n");
			sb.Append("Access-Control-Allow-Headers: Content-Type, Accept, Mcp-Session-Id\r\n");
			sb.Append("Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n");
			sb.Append("\r\n");
			byte[] head = Encoding.UTF8.GetBytes(sb.ToString());
			s.Write(head, 0, head.Length);
			if (body != null) {
				byte[] b = Encoding.UTF8.GetBytes(body);
				s.Write(b, 0, b.Length);
			}
			s.Flush();
		}
	}
}
