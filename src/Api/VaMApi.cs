using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using SimpleJSON;
using MVR.FileManagementSecure;

namespace VaMMCP.Api {
	/// <summary>
	/// The VaM control layer. Every method must be called on the Unity main thread
	/// (wrap with MainThreadDispatcher.Run), except AddAtom-style pollers.
	/// </summary>
	public class VaMApi {
		public readonly MainThreadDispatcher Mt;

		public VaMApi(MainThreadDispatcher mt) {
			Mt = mt;
		}

		// ===================== helpers =====================

		private static SuperController SC { get { return SuperController.singleton; } }

		public static string F(float v) { return v.ToString("0.######", CultureInfo.InvariantCulture); }

		public static JSONArray Vec(Vector3 v) {
			JSONArray a = new JSONArray();
			a.Add(F(v.x));
			a.Add(F(v.y));
			a.Add(F(v.z));
			return a;
		}

		public static Vector3 ParseVec3(JSONNode n) {
			JSONArray a = n.AsArray;
			if (a == null || a.Count < 3) throw new ApiError("expected [x, y, z] array");
			return new Vector3(a[0].AsFloat, a[1].AsFloat, a[2].AsFloat);
		}

		public static bool Has(JSONNode o, string k) { return o != null && o[k] != null; }
		public static string S(JSONNode o, string k) { return Has(o, k) ? o[k].Value : ""; }
		public static bool Bool(JSONNode o, string k, bool def) { return Has(o, k) ? o[k].AsBool : def; }
		public static float Num(JSONNode o, string k, float def) { return Has(o, k) ? o[k].AsFloat : def; }

		public static Atom FindAtom(string uid) {
			if (string.IsNullOrEmpty(uid)) throw new ApiError("atom uid required");
			Atom a = SC.GetAtomByUid(uid);
			if (a == null) throw new ApiError("atom not found: " + uid);
			return a;
		}

		public static Atom RequirePerson(string uid) {
			if (!string.IsNullOrEmpty(uid)) {
				Atom a = SC.GetAtomByUid(uid);
				if (a == null) throw new ApiError("person not found: " + uid);
				if (a.type != "Person") throw new ApiError("atom is not a Person: " + uid);
				return a;
			}
			foreach (Atom a in SC.GetAtoms()) {
				if (a != null && a.type == "Person") return a;
			}
			throw new ApiError("no Person atom in the current scene");
		}

		public static DAZCharacterSelector Selector(Atom p) {
			JSONStorable g = p.GetStorableByID("geometry");
			if (g == null) throw new ApiError("no geometry storable on " + p.uid);
			DAZCharacterSelector sel = g as DAZCharacterSelector;
			if (sel == null) throw new ApiError("geometry is not a DAZCharacterSelector on " + p.uid);
			return sel;
		}

		public static JSONClass AtomInfo(Atom a) {
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["type"] = a.type;
			r["name"] = a.name != null ? a.name : "";
			r["on"] = a.on ? "true" : "false";
			r["position"] = Vec(a.transform.position);
			r["rotation"] = Vec(a.transform.eulerAngles);
			return r;
		}

		public static List<string> ListJsonFiles(string dir) {
			List<string> list = new List<string>();
			try {
				string[] files = FileManagerSecure.GetFiles(dir, "*.json");
				if (files != null) {
					foreach (string f in files) {
						if (f != null && f.ToLowerInvariant().EndsWith(".json")) list.Add(f);
					}
				}
			} catch (Exception e) {
				Log.Debug("ListJsonFiles(" + dir + "): " + e.Message);
			}
			list.Sort();
			return list;
		}

		public static string FileName(string path) {
			string p = path.Replace('\\', '/');
			int i = p.LastIndexOf('/');
			string name = i >= 0 ? p.Substring(i + 1) : p;
			if (name.EndsWith(".json")) name = name.Substring(0, name.Length - 5);
			return name;
		}

		public static string Timestamp() {
			return DateTime.Now.ToString("yyyyMMdd_HHmmss");
		}

		/// <summary>Apply a VaM preset file (look / clothing / hair / pose / full) by restoring its storables.</summary>
		public static void ApplyPresetStorables(Atom atom, string path) {
			JSONNode node = SC.LoadJSON(path);
			if (node == null) throw new ApiError("could not load JSON: " + path);
			JSONClass jc = node.AsObject;
			if (jc == null) throw new ApiError("preset is not a JSON object: " + path);
			JSONArray storables = jc["storables"] != null ? jc["storables"].AsArray : null;
			if (storables == null && jc["atoms"] != null) {
				// VaM 1.22+ presets wrap storables under atoms[0].storables
				JSONArray atoms = jc["atoms"].AsArray;
				if (atoms != null && atoms.Count > 0) {
					JSONClass atom0 = atoms[0].AsObject;
					if (atom0 != null) storables = atom0["storables"] != null ? atom0["storables"].AsArray : null;
				}
			}
			if (storables == null) throw new ApiError("preset has no storables array: " + path);
			bool setUnlisted = true;
			if (jc["setUnlistedParamsToDefault"] != null && jc["setUnlistedParamsToDefault"].Value == "false") setUnlisted = false;
			int restored = 0;
			for (int i = 0; i < storables.Count; i++) {
				JSONClass sj = storables[i].AsObject;
				if (sj == null) continue;
				string sid = sj["id"] != null ? sj["id"].Value : "";
				if (sid == "") continue;
				JSONStorable st = atom.GetStorableByID(sid);
				if (st == null) continue;
				st.RestoreFromJSON(sj, true, true, null, setUnlisted);
				restored++;
			}
			if (restored == 0) throw new ApiError("no matching storables on " + atom.uid + " for " + path);
		}

		private static IEnumerable<DAZMorph> AllMorphs(DAZCharacterSelector sel) {
			HashSet<string> seen = new HashSet<string>();
			DAZMorphBank[] banks = { sel.morphBank1, sel.morphBank2, sel.morphBank3 };
			foreach (DAZMorphBank bank in banks) {
				if (bank == null || bank.morphs == null) continue;
				foreach (DAZMorph m in bank.morphs) {
					if (m == null) continue;
					if (seen.Add(m.uid)) yield return m;
				}
			}
		}

		private static DAZMorph FindMorph(DAZCharacterSelector sel, string name) {
			foreach (DAZMorph m in AllMorphs(sel)) {
				if (m.displayName != null && string.Equals(m.displayName, name, StringComparison.OrdinalIgnoreCase)) return m;
				if (m.uid != null && string.Equals(m.uid, name, StringComparison.OrdinalIgnoreCase)) return m;
			}
			return null;
		}

		// ---------- reflection helpers (protected/internal VaM internals) ----------

		private static FieldInfo floatParamsField;
		private static Dictionary<string, JSONStorableFloat> FloatParams(JSONStorable st) {
			try {
				if (floatParamsField == null) {
					floatParamsField = typeof(JSONStorable).GetField("floatParams", BindingFlags.NonPublic | BindingFlags.Instance);
				}
				return floatParamsField != null ? (Dictionary<string, JSONStorableFloat>)floatParamsField.GetValue(st) : null;
			} catch {
				return null;
			}
		}

		private static FieldInfo chooserParamsField;
		private static Dictionary<string, JSONStorableStringChooser> ChooserParams(JSONStorable st) {
			try {
				if (chooserParamsField == null) {
					chooserParamsField = typeof(JSONStorable).GetField("stringChooserParams", BindingFlags.NonPublic | BindingFlags.Instance);
				}
				return chooserParamsField != null ? (Dictionary<string, JSONStorableStringChooser>)chooserParamsField.GetValue(st) : null;
			} catch {
				return null;
			}
		}

		public static object Invoke(object target, string method, params object[] args) {
			MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (m == null) throw new ApiError("method not found: " + method + " on " + target.GetType().Name);
			return m.Invoke(target, args);
		}

		/// <summary>Find a component by type name without a compile-time reference (tries plain name, MeshVR.* and full scan fallback).</summary>
		private static Type runtimeCreatorType;
		public static Component FindComponent(Component root, string typeName) {
			if (root == null) return null;
			Type t = Type.GetType(typeName + ", Assembly-CSharp");
			if (t == null) t = Type.GetType("MeshVR." + typeName + ", Assembly-CSharp");
			if (t == null) {
				// fallback: scan the game assembly for the type by simple name (cached)
				try {
					if (runtimeCreatorType == null) {
						Type[] all = typeof(SuperController).Assembly.GetTypes();
						foreach (Type tt in all) {
							if (tt.Name == typeName) { runtimeCreatorType = tt; break; }
						}
					}
					t = runtimeCreatorType;
				} catch { }
			}
			if (t == null) return null;
			return root.GetComponentInChildren(t, true);
		}

		// ===================== scene =====================

		public JSONNode Status() {
			JSONClass r = new JSONClass();
			r["plugin"] = "VaMMCP";
			r["version"] = "1.0.0";
			try { r["vaMVersion"] = Application.version; } catch { r["vaMVersion"] = "unknown"; }
			try { r["vaMRoot"] = Application.dataPath + "/.."; } catch { }
			int atoms = 0;
			int persons = 0;
			foreach (Atom a in SC.GetAtoms()) {
				if (a == null) continue;
				atoms++;
				if (a.type == "Person") persons++;
			}
			r["atoms"] = atoms.ToString();
			r["persons"] = persons.ToString();
			r["endpoint"] = "http://127.0.0.1:" + Plugin.cfgPort.Value + "/mcp";
			r["allowEval"] = Plugin.cfgAllowEval.Value ? "true" : "false";
			r["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			return r;
		}

		public JSONNode ListScenes(JSONNode args) {
			string search = S(args, "search").ToLowerInvariant();
			List<string> files = ListJsonFiles("Saves/scene");
			JSONArray arr = new JSONArray();
			foreach (string f in files) {
				string name = FileName(f);
				if (search != "" && name.ToLowerInvariant().IndexOf(search) < 0 && f.ToLowerInvariant().IndexOf(search) < 0) continue;
				JSONClass row = new JSONClass();
				row["path"] = f;
				row["name"] = name;
				arr.Add(row);
			}
			JSONClass r = new JSONClass();
			r["count"] = arr.Count.ToString();
			r["scenes"] = arr;
			return r;
		}

		public JSONNode LoadScene(JSONNode args) {
			string path = S(args, "path");
			if (path == "") throw new ApiError("path required (see list_scenes)");
			bool merge = Bool(args, "merge", false);
			if (!merge) {
				if (!FileManagerSecure.FileExists(path)) throw new ApiError("scene file not found: " + path);
				SC.Load(path);
			} else {
				SC.LoadMerge(path);
			}
			JSONClass r = new JSONClass();
			r["path"] = path;
			r["merge"] = merge ? "true" : "false";
			r["loading"] = "true";
			return r;
		}

		public JSONNode NewScene() {
			SC.NewScene();
			JSONClass r = new JSONClass();
			r["ok"] = "true";
			return r;
		}

		/// <summary>Normalize a save path to VaM style (backslashes), ensure its directory exists, and remove an existing file
		/// (VaM asks for user confirmation when overwriting an existing file from a plugin context — we delete via System.IO first so Save writes fresh).</summary>
		public static string PrepareSavePath(string path) {
			path = path.Replace('/', '\\');
			int idx = path.LastIndexOf('\\');
			if (idx > 0) {
				string dir = path.Substring(0, idx);
				try { FileManagerSecure.CreateDirectory(dir); } catch (Exception e) { Log.Debug("PrepareSavePath mkdir: " + e.Message); }
			}
			try {
				if (FileManagerSecure.FileExists(path)) {
					string abs = Application.dataPath + "/.." + (path.StartsWith("\\") ? "" : "\\") + path;
					if (System.IO.File.Exists(abs)) {
						System.IO.File.Delete(abs);
						Log.Info("PrepareSavePath: deleted existing " + path);
					}
				}
			} catch (Exception e) {
				Log.Info("PrepareSavePath delete failed: " + e.Message);
			}
			return path;
		}

		public JSONNode SaveScene(JSONNode args) {
			string path = S(args, "path");
			if (path == "") path = "Saves/scene/VaMMCP_" + Timestamp() + ".json";
			if (!path.EndsWith(".json")) path += ".json";
			SC.Save(PrepareSavePath(path));
			JSONClass r = new JSONClass();
			r["saved"] = path;
			return r;
		}

		// ===================== atoms =====================

		public JSONNode ListAtomTypes() {
			JSONClass r = new JSONClass();
			JSONArray cats = new JSONArray();
			try {
				FieldInfo f = typeof(SuperController).GetField("atomCategoryToAtomTypes", BindingFlags.NonPublic | BindingFlags.Instance);
				if (f != null) {
					object val = f.GetValue(SC);
					Dictionary<string, List<string>> d = val as Dictionary<string, List<string>>;
					if (d != null) {
						foreach (KeyValuePair<string, List<string>> kv in d) {
							JSONClass c = new JSONClass();
							c["category"] = kv.Key;
							JSONArray types = new JSONArray();
							foreach (string t in kv.Value) types.Add(t);
							c["types"] = types;
							cats.Add(c);
						}
					}
				}
			} catch (Exception e) {
				Log.Debug("atom types reflection failed: " + e.Message);
			}
			if (cats.Count == 0) {
				string[] common = {
					"Person", "Clothing", "Hair", "Light", "Spotlight", "AudioSource", "ImagePanel", "Canvas",
					"UIButton", "UIText", "AnimationPattern", "AnimationStep", "CollisionTrigger", "ForceProducer",
					"LiquidCollider", "ParticleSystem", "Screen", "Microphone", "WindowCamera", "TV", "PostProcessing"
				};
				foreach (string t in common) {
					JSONClass c = new JSONClass();
					c["category"] = "Common";
					JSONArray types = new JSONArray();
					types.Add(t);
					c["types"] = types;
					cats.Add(c);
				}
			}
			r["categories"] = cats;
			return r;
		}

		public JSONNode ListAtoms(JSONNode args) {
			string type = S(args, "type");
			JSONArray arr = new JSONArray();
			foreach (Atom a in SC.GetAtoms()) {
				if (a == null) continue;
				if (type != "" && !string.Equals(a.type, type, StringComparison.OrdinalIgnoreCase)) continue;
				arr.Add(AtomInfo(a));
			}
			JSONClass r = new JSONClass();
			r["count"] = arr.Count.ToString();
			r["atoms"] = arr;
			return r;
		}

		/// <summary>Poller tool: starts AddAtomByType on the main thread, then polls for the new atom.</summary>
		public JSONNode AddAtom(JSONNode args) {
			string type = S(args, "type");
			if (type == "") throw new ApiError("type required (see list_atom_types)");
			string uid = S(args, "uid");

			List<string> before = Mt.Run(delegate {
				if (uid != "" && SC.GetAtomByUid(uid) != null) throw new ApiError("atom already exists: " + uid);
				List<string> b = new List<string>();
				foreach (Atom a in SC.GetAtoms()) if (a != null) b.Add(a.uid);
				string useUid = uid == "" ? null : uid;
				SC.StartCoroutine(SC.AddAtomByType(type, useUid, true));
				return b;
			}, 15000);

			string created = null;
			DateTime deadline = DateTime.UtcNow.AddSeconds(45);
			while (DateTime.UtcNow < deadline) {
				Thread.Sleep(150);
				created = Mt.Run(delegate {
					foreach (Atom a in SC.GetAtoms()) {
						if (a == null) continue;
						if (a.type == type && !before.Contains(a.uid)) return a.uid;
					}
					return null;
				}, 10000);
				if (created != null) break;
			}
			if (created == null) throw new ApiError("atom creation timed out for type " + type + " (check list_atom_types for valid types)");

			JSONClass r = new JSONClass();
			r["uid"] = created;
			r["type"] = type;
			return r;
		}

		public JSONNode RemoveAtom(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			string uid = a.uid;
			SC.RemoveAtom(a);
			JSONClass r = new JSONClass();
			r["removed"] = uid;
			return r;
		}

		public JSONNode SetAtomOn(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			bool on = Bool(args, "on", true);
			if (a.on != on) a.ToggleOn();
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["on"] = a.on ? "true" : "false";
			return r;
		}

		public JSONNode GetAtomTransform(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["position"] = Vec(a.transform.position);
			r["rotation"] = Vec(a.transform.eulerAngles);
			r["scale"] = Vec(a.transform.localScale);
			return r;
		}

		public JSONNode SetAtomTransform(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			bool relative = Bool(args, "relative", false);
			if (Has(args, "position")) {
				Vector3 p = ParseVec3(args["position"]);
				a.transform.position = relative ? a.transform.position + p : p;
			}
			if (Has(args, "rotation")) {
				Quaternion q = Quaternion.Euler(ParseVec3(args["rotation"]));
				a.transform.rotation = relative ? a.transform.rotation * q : q;
			}
			if (Has(args, "scale")) {
				Vector3 s = ParseVec3(args["scale"]);
				a.transform.localScale = relative ? a.transform.localScale + s : s;
			}
			return GetAtomTransform(args);
		}

		// ===================== persons =====================

		public JSONNode AddPerson(JSONNode args) {
			// add_person = add_atom with type forced to Person
			if (!Has(args, "type")) args["type"] = "Person";
			return AddAtom(args);
		}

		public JSONNode ListPersons() {
			JSONArray arr = new JSONArray();
			foreach (Atom a in SC.GetAtoms()) {
				if (a == null || a.type != "Person") continue;
				JSONClass row = AtomInfo(a);
				try {
					DAZCharacterSelector sel = Selector(a);
					string character = "";
					try { character = sel.GetStringChooserParamValue("characterSelection"); } catch { }
					if (character == "") {
						try { character = sel.GetStringParamValue("character"); } catch { }
					}
					row["character"] = character;
					row["gender"] = sel.gender.ToString();
				} catch { }
				arr.Add(row);
			}
			JSONClass r = new JSONClass();
			r["count"] = arr.Count.ToString();
			r["persons"] = arr;
			return r;
		}

		public JSONNode SetCharacter(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string name = S(args, "name");
			if (name == "") throw new ApiError("name required (characters are listed via get_param uid=<person> storable=geometry param=characterSelection)");
			sel.SelectCharacterByName(name, false);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["name"] = name;
			r["loading"] = "true";
			return r;
		}

		public JSONNode ListLooks(JSONNode args) {
			return ListPresets("Saves/Person/Appearance", S(args, "search"), "looks");
		}

		public JSONNode ListClothingPresets(JSONNode args) {
			JSONClass r = new JSONClass();
			JSONArray arr = new JSONArray();
			AddPresetFiles(arr, "Saves/Person/Clothing", S(args, "search"));
			AddPresetFiles(arr, "Custom/Clothing", S(args, "search"));
			AddPackageItemFiles(arr, "Custom/Clothing", S(args, "search"), 300);
			r["count"] = arr.Count.ToString();
			r["presets"] = arr;
			return r;
		}

		public JSONNode ListHairPresets(JSONNode args) {
			JSONClass r = new JSONClass();
			JSONArray arr = new JSONArray();
			AddPresetFiles(arr, "Saves/Person/Hair", S(args, "search"));
			AddPresetFiles(arr, "Custom/Hair", S(args, "search"));
			AddPackageItemFiles(arr, "Custom/Hair", S(args, "search"), 300);
			r["count"] = arr.Count.ToString();
			r["presets"] = arr;
			return r;
		}

		// ---------- .var package assets ----------

		public static List<string> ListPackageUids() {
			List<string> list = new List<string>();
			string[] files = null;
			try {
				files = FileManagerSecure.GetFiles("AddonPackages", "*.var");
			} catch { }
			if (files != null) {
				foreach (string f in files) {
					if (f == null) continue;
					string p = f.Replace('\\', '/');
					int i = p.LastIndexOf('/');
					string name = i >= 0 ? p.Substring(i + 1) : p;
					if (name.EndsWith(".var")) name = name.Substring(0, name.Length - 4);
					if (name != "") list.Add(name);
				}
			}
			list.Sort();
			return list;
		}

		public JSONNode ListPackages() {
			List<string> pkgs = ListPackageUids();
			JSONArray arr = new JSONArray();
			foreach (string p in pkgs) {
				JSONClass row = new JSONClass();
				row["uid"] = p;
				arr.Add(row);
			}
			JSONClass r = new JSONClass();
			r["count"] = arr.Count.ToString();
			r["packages"] = arr;
			return r;
		}

		/// <summary>Scan every .var package for item jsons under the given relative dir (e.g. Custom/Clothing), recursively.</summary>
		private static void AddPackageItemFiles(JSONArray arr, string relativeDir, string search, int cap) {
			string s = search.ToLowerInvariant();
			foreach (string pkg in ListPackageUids()) {
				if (arr.Count >= cap) break;
				string root = pkg + ":/" + relativeDir;
				ScanPackageDir(arr, root, s, cap, pkg, 0);
			}
		}

		private static void ScanPackageDir(JSONArray arr, string dir, string search, int cap, string pkg, int depth) {
			if (depth > 5 || arr.Count >= cap) return;
			AddItemFilesInDir(arr, dir, search, cap, pkg);
			string[] dirs = null;
			try { dirs = FileManagerSecure.GetDirectories(dir); } catch { }
			if (dirs == null) return;
			foreach (string d in dirs) {
				if (arr.Count >= cap) break;
				ScanPackageDir(arr, d, search, cap, pkg, depth + 1);
			}
		}

		private static void AddItemFilesInDir(JSONArray arr, string dir, string search, int cap, string pkg) {
			string[] files = null;
			try { files = FileManagerSecure.GetFiles(dir); } catch { }
			if (files == null) return;
			foreach (string f in files) {
				if (arr.Count >= cap) break;
				if (f == null) continue;
				string low = f.ToLowerInvariant();
				// item files are .vam (assets); presets are .json. Skip texture/variant/meta files.
				if (!low.EndsWith(".json") && !low.EndsWith(".vam")) continue;
				if (low.EndsWith("meta.json") || low.EndsWith("package.json")) continue;
				if (low.EndsWith(".vam") && (low.EndsWith("_main.vam") || low.EndsWith("_sim.vam"))) continue;
				string name = FileName(f);
				if (search != "" && name.ToLowerInvariant().IndexOf(search) < 0 && f.ToLowerInvariant().IndexOf(search) < 0) continue;
				JSONClass row = new JSONClass();
				row["path"] = f;
				row["name"] = name;
				row["package"] = pkg;
				arr.Add(row);
			}
		}

		public JSONNode ListPoses(JSONNode args) {
			return ListPresets("Saves/Person/Pose", S(args, "search"), "poses");
		}

		private JSONNode ListPresets(string dir, string search, string key) {
			string s = search.ToLowerInvariant();
			List<string> files = ListJsonFiles(dir);
			JSONArray arr = new JSONArray();
			foreach (string f in files) {
				string name = FileName(f);
				if (s != "" && name.ToLowerInvariant().IndexOf(s) < 0 && f.ToLowerInvariant().IndexOf(s) < 0) continue;
				JSONClass row = new JSONClass();
				row["path"] = f;
				row["name"] = name;
				arr.Add(row);
			}
			JSONClass r = new JSONClass();
			r["count"] = arr.Count.ToString();
			r[key] = arr;
			return r;
		}

		private static void AddPresetFiles(JSONArray arr, string dir, string search) {
			string s = search.ToLowerInvariant();
			foreach (string f in ListJsonFiles(dir)) {
				string name = FileName(f);
				if (s != "" && name.ToLowerInvariant().IndexOf(s) < 0 && f.ToLowerInvariant().IndexOf(s) < 0) continue;
				JSONClass row = new JSONClass();
				row["path"] = f;
				row["name"] = name;
				arr.Add(row);
			}
		}

		public JSONNode LoadLook(JSONNode args) {
			return ApplyPreset(args, "look");
		}

		public JSONNode LoadClothingPreset(JSONNode args) {
			return ApplyPreset(args, "clothing");
		}

		public JSONNode LoadHairPreset(JSONNode args) {
			return ApplyPreset(args, "hair");
		}

		public JSONNode LoadPose(JSONNode args) {
			Atom p = null;
			bool all = Bool(args, "all", false);
			string path = S(args, "path");
			if (path == "") throw new ApiError("path required (see list_poses)");
			JSONArray applied = new JSONArray();
			if (all) {
				foreach (Atom a in SC.GetAtoms()) {
					if (a == null || a.type != "Person") continue;
					ApplyPresetStorables(a, path);
					applied.Add(a.uid);
				}
				if (applied.Count == 0) throw new ApiError("no Person atom in the current scene");
			} else {
				p = RequirePerson(S(args, "person"));
				ApplyPresetStorables(p, path);
				applied.Add(p.uid);
			}
			JSONClass r = new JSONClass();
			r["path"] = path;
			r["kind"] = "pose";
			r["applied"] = applied;
			return r;
		}

		private JSONNode ApplyPreset(JSONNode args, string kind) {
			Atom p = RequirePerson(S(args, "person"));
			string path = S(args, "path");
			if (path == "") throw new ApiError("path required");
			ApplyPresetStorables(p, path);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["path"] = path;
			r["kind"] = kind;
			return r;
		}

		public JSONNode SaveLook(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			string path = S(args, "path");
			if (path == "") path = "Saves/Person/Appearance/" + p.uid + "_" + Timestamp() + ".json";
			if (!path.EndsWith(".json")) path += ".json";
			SC.SaveFromAtom(PrepareSavePath(path), p, false, true);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["saved"] = path;
			r["kind"] = "look";
			return r;
		}

		public JSONNode SavePose(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			string path = S(args, "path");
			if (path == "") path = "Saves/Person/Pose/" + p.uid + "_" + Timestamp() + ".json";
			if (!path.EndsWith(".json")) path += ".json";
			SC.SaveFromAtom(PrepareSavePath(path), p, true, false);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["saved"] = path;
			r["kind"] = "pose";
			return r;
		}

		public JSONNode SaveFullPreset(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			string path = S(args, "path");
			if (path == "") path = "Saves/Person/full/" + p.uid + "_" + Timestamp() + ".json";
			if (!path.EndsWith(".json")) path += ".json";
			SC.SaveFromAtom(PrepareSavePath(path), p, true, true);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["saved"] = path;
			r["kind"] = "full";
			return r;
		}

		// ===================== morphs (捏人) =====================

		public JSONNode ListMorphs(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string search = S(args, "search").ToLowerInvariant();
			string region = S(args, "region").ToLowerInvariant();
			string group = S(args, "group").ToLowerInvariant();
			int limit = (int)Num(args, "limit", 500);
			if (limit <= 0) limit = 500;
			JSONArray arr = new JSONArray();
			int count = 0;
			foreach (DAZMorph m in AllMorphs(sel)) {
				string dn = m.displayName != null ? m.displayName : "";
				string uid = m.uid != null ? m.uid : "";
				if (search != "" && dn.ToLowerInvariant().IndexOf(search) < 0 && uid.ToLowerInvariant().IndexOf(search) < 0) continue;
				if (region != "" && (m.region == null || m.region.ToLowerInvariant() != region)) continue;
				if (group != "" && (m.group == null || m.group.ToLowerInvariant() != group)) continue;
				JSONClass row = new JSONClass();
				row["name"] = dn;
				row["uid"] = uid;
				row["region"] = m.region != null ? m.region : "";
				row["group"] = m.group != null ? m.group : "";
				row["value"] = F(m.morphValue);
				arr.Add(row);
				count++;
				if (count >= limit) break;
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["count"] = count.ToString();
			r["morphs"] = arr;
			return r;
		}

		public JSONNode SetMorph(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string name = S(args, "name");
			if (name == "") throw new ApiError("name required (see list_morphs)");
			float value = Num(args, "value", 0f);
			DAZMorph m = FindMorph(sel, name);
			if (m == null) throw new ApiError("morph not found: " + name + " (see list_morphs)");
			m.morphValue = value;
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["name"] = m.displayName != null ? m.displayName : name;
			r["value"] = F(m.morphValue);
			return r;
		}

		public JSONNode GetMorph(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string name = S(args, "name");
			DAZMorph m = FindMorph(sel, name);
			if (m == null) throw new ApiError("morph not found: " + name + " (see list_morphs)");
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["name"] = m.displayName != null ? m.displayName : name;
			r["region"] = m.region != null ? m.region : "";
			r["group"] = m.group != null ? m.group : "";
			r["value"] = F(m.morphValue);
			return r;
		}

		public JSONNode ResetMorphs(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string region = S(args, "region").ToLowerInvariant();
			int reset = 0;
			foreach (DAZMorph m in AllMorphs(sel)) {
				if (region != "" && (m.region == null || m.region.ToLowerInvariant() != region)) continue;
				if (m.morphValue != 0f) {
					m.morphValue = 0f;
					reset++;
				}
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["reset"] = reset.ToString();
			if (region != "") r["region"] = region;
			return r;
		}

		// ===================== clothing / hair =====================

		public JSONNode ListClothingItems(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			JSONArray arr = new JSONArray();
			DAZClothingItem[] items = sel.clothingItems;
			if (items != null) {
				foreach (DAZClothingItem it in items) {
					if (it == null) continue;
					JSONClass row = new JSONClass();
					row["uid"] = it.uid;
					row["name"] = it.displayName != null ? it.displayName : "";
					row["active"] = it.active ? "true" : "false";
					arr.Add(row);
				}
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["count"] = arr.Count.ToString();
			r["items"] = arr;
			return r;
		}

		public JSONNode ListHairItems(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			JSONArray arr = new JSONArray();
			DAZHairGroup[] items = sel.hairItems;
			if (items != null) {
				foreach (DAZHairGroup it in items) {
					if (it == null) continue;
					JSONClass row = new JSONClass();
					row["uid"] = it.uid;
					row["name"] = it.displayName != null ? it.displayName : "";
					row["active"] = it.active ? "true" : "false";
					arr.Add(row);
				}
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["count"] = arr.Count.ToString();
			r["items"] = arr;
			return r;
		}

		/// <summary>Poller tool. Adds a clothing item to a person from its .vam/.json path.
		/// Waits for the character to finish loading so the creator item has its DAZRuntimeCreator component.</summary>
		public JSONNode AddClothingItem(JSONNode args) {
			Atom p = Mt.Run(() => RequirePerson(S(args, "person")), 15000);
			string path = S(args, "path");
			if (path == "") throw new ApiError("path required (clothing item .vam/.json file, e.g. from list_clothing_presets)");
			DAZClothingItem creator = Mt.Run(delegate {
				DAZCharacterSelector sel = Selector(p);
				return sel.gender == DAZCharacterSelector.Gender.Male ? sel.maleClothingCreatorItem : sel.femaleClothingCreatorItem;
			}, 15000);
			if (creator == null) throw new ApiError("no clothing creator item available");
			Mt.Run(() => { try { Selector(p).SetActiveClothingItem(creator, true); } catch { } }, 15000);
			Component drc = PollComponent(creator, 40);
			if (drc == null) throw new ApiError("no DAZRuntimeCreator component on the creator item (character may still be loading; retry in a few seconds)");
			Mt.Run(() => Invoke(drc, "LoadFromPath", path), 15000);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["path"] = path;
			r["loading"] = "true";
			return r;
		}

		/// <summary>Poller tool. Adds a hair item to a person from its .vam/.json path.</summary>
		public JSONNode AddHairItem(JSONNode args) {
			Atom p = Mt.Run(() => RequirePerson(S(args, "person")), 15000);
			string path = S(args, "path");
			if (path == "") throw new ApiError("path required (hair item .vam/.json file, e.g. from list_hair_presets)");
			DAZHairGroup creator = Mt.Run(delegate {
				DAZCharacterSelector sel = Selector(p);
				return sel.gender == DAZCharacterSelector.Gender.Male ? sel.maleHairCreatorItem : sel.femaleHairCreatorItem;
			}, 15000);
			if (creator == null) throw new ApiError("no hair creator item available");
			Mt.Run(() => { try { Invoke(Selector(p), "SetActiveHairItem", creator, true); } catch { } }, 15000);
			Component drc = PollComponent(creator, 40);
			if (drc == null) throw new ApiError("no DAZRuntimeCreator component on the creator item (character may still be loading; retry in a few seconds)");
			Mt.Run(() => Invoke(drc, "LoadFromPath", path), 15000);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["path"] = path;
			r["loading"] = "true";
			return r;
		}

		/// <summary>Poll (on the HTTP thread, via the dispatcher) until the component appears or the deadline passes.</summary>
		private Component PollComponent(Component root, int seconds) {
			DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
			while (DateTime.UtcNow < deadline) {
				Component c = Mt.Run(() => FindComponent(root, "DAZRuntimeCreator"), 10000);
				if (c != null) return c;
				Thread.Sleep(500);
			}
			return null;
		}

		public JSONNode RemoveClothingItem(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string id = S(args, "id");
			if (id == "") throw new ApiError("id required (see list_clothing_items)");
			DAZClothingItem item = sel.GetClothingItem(id);
			if (item == null) throw new ApiError("clothing item not found: " + id);
			sel.SetActiveClothingItem(item, false);
			DAZClothingItemControl ctrl = item.GetComponentInChildren<DAZClothingItemControl>();
			if (ctrl != null) {
				try { ctrl.Delete(); } catch (Exception e) { Log.Debug("clothing delete: " + e.Message); }
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["removed"] = id;
			return r;
		}

		public JSONNode RemoveHairItem(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string id = S(args, "id");
			if (id == "") throw new ApiError("id required (see list_hair_items)");
			DAZHairGroup item = null;
			DAZHairGroup[] items = sel.hairItems;
			if (items != null) {
				foreach (DAZHairGroup it in items) {
					if (it != null && it.uid == id) { item = it; break; }
				}
			}
			if (item == null) throw new ApiError("hair item not found: " + id);
			Invoke(sel, "SetActiveHairItem", item, false);
			DAZHairGroupControl ctrl = item.GetComponentInChildren<DAZHairGroupControl>();
			if (ctrl != null) {
				try { ctrl.Delete(); } catch (Exception e) { Log.Debug("hair delete: " + e.Message); }
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["removed"] = id;
			return r;
		}

		public JSONNode SetClothingItemOn(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string id = S(args, "id");
			bool on = Bool(args, "on", true);
			DAZClothingItem item = sel.GetClothingItem(id);
			if (item == null) throw new ApiError("clothing item not found: " + id);
			sel.SetActiveClothingItem(item, on);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["id"] = id;
			r["on"] = on ? "true" : "false";
			return r;
		}

		public JSONNode SetHairItemOn(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string id = S(args, "id");
			bool on = Bool(args, "on", true);
			DAZHairGroup item = null;
			DAZHairGroup[] items = sel.hairItems;
			if (items != null) {
				foreach (DAZHairGroup it in items) {
					if (it != null && it.uid == id) { item = it; break; }
				}
			}
			if (item == null) throw new ApiError("hair item not found: " + id);
			Invoke(sel, "SetActiveHairItem", item, on);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["id"] = id;
			r["on"] = on ? "true" : "false";
			return r;
		}

		// ===================== expressions =====================

		private static bool LooksLikeExpression(DAZMorph m) {
			string n = (m.displayName != null ? m.displayName : "").ToLowerInvariant();
			string g = (m.group != null ? m.group : "").ToLowerInvariant();
			string reg = (m.region != null ? m.region : "").ToLowerInvariant();
			if (g.IndexOf("expression") >= 0) return true;
			if (reg.IndexOf("face") < 0 && reg.IndexOf("head") < 0) return false;
			string[] kws = {
				"smile", "frown", "surpris", "neutral", "angr", "sad", "happy", "pleasure", "pain", "pout",
				"grin", "laugh", "cry", "blush", "kiss", "tongue", "bite", "eye roll", "extreme pleasure",
				"enjoying", "taking it", "mouth resting", "lip bite", "shy", "squint", "wink", "grit",
				"moan", "lust", "ecstasy", "agony", "scream", "whimper", "sulk", "smirk", "scowl", "snarl",
				"glare", "flirt", "seduct", "sultry", "open mouth", "relaxed", "lips"
			};
			foreach (string k in kws) {
				if (n.IndexOf(k) >= 0) return true;
			}
			return false;
		}

		public JSONNode ListExpressions(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			JSONArray arr = new JSONArray();
			foreach (DAZMorph m in AllMorphs(sel)) {
				if (!LooksLikeExpression(m)) continue;
				JSONClass row = new JSONClass();
				row["name"] = m.displayName != null ? m.displayName : "";
				row["uid"] = m.uid != null ? m.uid : "";
				row["region"] = m.region != null ? m.region : "";
				row["value"] = F(m.morphValue);
				arr.Add(row);
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["count"] = arr.Count.ToString();
			r["expressions"] = arr;
			return r;
		}

		public JSONNode SetExpression(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			DAZCharacterSelector sel = Selector(p);
			string name = S(args, "name");
			if (name == "") throw new ApiError("name required (see list_expressions)");
			float value = Num(args, "value", 1f);
			bool reset = Bool(args, "reset", true);
			int cleared = 0;
			if (reset) {
				foreach (DAZMorph m in AllMorphs(sel)) {
					if (LooksLikeExpression(m) && m.morphValue != 0f) {
						m.morphValue = 0f;
						cleared++;
					}
				}
			}
			DAZMorph target = FindMorph(sel, name);
			if (target == null) throw new ApiError("expression morph not found: " + name + " (see list_expressions)");
			target.morphValue = value;
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["name"] = target.displayName != null ? target.displayName : name;
			r["value"] = F(target.morphValue);
			r["cleared"] = cleared.ToString();
			return r;
		}

		// ===================== controls =====================

		public JSONNode ListControls(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			List<string> names = SC.GetFreeControllerNamesInAtom(p.uid);
			JSONArray arr = new JSONArray();
			if (names != null) {
				foreach (string n in names) arr.Add(n);
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["count"] = arr.Count.ToString();
			r["controls"] = arr;
			return r;
		}

		private static FreeControllerV3 GetControl(Atom p, string controlId) {
			JSONStorable st = p.GetStorableByID(controlId);
			FreeControllerV3 fc = st as FreeControllerV3;
			if (fc == null) {
				string hint = "";
				try {
					List<string> names = SC.GetFreeControllerNamesInAtom(p.uid);
					if (names != null && names.Count > 0) hint = "available: " + string.Join(", ", names.ToArray());
				} catch { }
				throw new ApiError("control not found: " + controlId + (hint != "" ? " (" + hint + ")" : ""));
			}
			return fc;
		}

		public JSONNode GetControl(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			string controlId = S(args, "control");
			if (controlId == "") throw new ApiError("control required (see list_controls)");
			FreeControllerV3 fc = GetControl(p, controlId);
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["control"] = controlId;
			r["position"] = Vec(fc.control.position);
			r["rotation"] = Vec(fc.control.eulerAngles);
			return r;
		}

		public JSONNode SetControl(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			string controlId = S(args, "control");
			if (controlId == "") throw new ApiError("control required (see list_controls)");
			FreeControllerV3 fc = GetControl(p, controlId);
			if (Has(args, "position")) fc.SetPositionNoForce(ParseVec3(args["position"]));
			if (Has(args, "rotation")) fc.SetRotationNoForce(ParseVec3(args["rotation"]));
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["control"] = controlId;
			r["position"] = Vec(fc.control.position);
			r["rotation"] = Vec(fc.control.eulerAngles);
			return r;
		}

		public JSONNode SetGaze(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			JSONStorable lookAt = p.GetStorableByID("lookAt");
			if (lookAt == null) throw new ApiError("no lookAt storable on " + p.uid);
			JSONClass applied = new JSONClass();
			float amount = Num(args, "amount", -1f);
			string targetControl = S(args, "targetControl");
			if (amount >= 0f) {
				if (lookAt.GetFloatParamNames().Contains("gazeAmount")) {
					lookAt.SetFloatParamValue("gazeAmount", amount);
					applied["gazeAmount"] = F(amount);
				}
			}
			if (targetControl != "") {
				if (lookAt.GetStringChooserParamNames().Contains("target")) {
					lookAt.SetStringChooserParamValue("target", targetControl);
					applied["target"] = targetControl;
				} else if (lookAt.GetStringParamNames().Contains("target")) {
					lookAt.SetStringParamValue("target", targetControl);
					applied["target"] = targetControl;
				}
			}
			if (applied.Count == 0) {
				throw new ApiError("lookAt storable has no controllable gaze params; use list_storable_params with storable=lookAt to see what it offers");
			}
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["applied"] = applied;
			return r;
		}

		// ===================== generic param access =====================

		public JSONNode ListAtomStorables(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			List<string> ids = a.GetStorableIDs();
			JSONArray arr = new JSONArray();
			if (ids != null) {
				foreach (string id in ids) {
					JSONStorable st = a.GetStorableByID(id);
					if (st == null) continue;
					JSONClass row = new JSONClass();
					row["id"] = id;
					row["type"] = st.GetType().Name;
					arr.Add(row);
				}
			}
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["count"] = arr.Count.ToString();
			r["storables"] = arr;
			return r;
		}

		public JSONNode ListStorableParams(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			string sid = S(args, "storable");
			JSONStorable st = a.GetStorableByID(sid);
			if (st == null) throw new ApiError("storable not found: " + sid + " on " + a.uid);
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["storable"] = sid;
			r["type"] = st.GetType().Name;

			JSONArray floats = new JSONArray();
			foreach (string n in SortedNames(st.GetFloatParamNames())) {
				try {
					JSONClass p = new JSONClass();
					p["name"] = n;
					p["value"] = F(st.GetFloatParamValue(n));
					JSONStorableFloat f = GetFloatParamObj(st, n);
					if (f != null) {
						p["min"] = F(f.min);
						p["max"] = F(f.max);
						p["default"] = F(f.defaultVal);
					}
					floats.Add(p);
				} catch (Exception e) { Log.Debug("float param " + n + ": " + e.Message); }
			}
			r["floats"] = floats;

			JSONArray bools = new JSONArray();
			foreach (string n in SortedNames(st.GetBoolParamNames())) {
				try {
					JSONClass p = new JSONClass();
					p["name"] = n;
					p["value"] = st.GetBoolParamValue(n) ? "true" : "false";
					bools.Add(p);
				} catch (Exception e) { Log.Debug("bool param " + n + ": " + e.Message); }
			}
			r["bools"] = bools;

			JSONArray strings = new JSONArray();
			foreach (string n in SortedNames(st.GetStringParamNames())) {
				try {
					JSONClass p = new JSONClass();
					p["name"] = n;
					p["value"] = st.GetStringParamValue(n);
					strings.Add(p);
				} catch (Exception e) { Log.Debug("string param " + n + ": " + e.Message); }
			}
			r["strings"] = strings;

			JSONArray choosers = new JSONArray();
			foreach (string n in SortedNames(st.GetStringChooserParamNames())) {
				try {
					JSONClass p = new JSONClass();
					p["name"] = n;
					p["value"] = st.GetStringChooserParamValue(n);
					JSONStorableStringChooser ch = GetChooserObj(st, n);
					if (ch != null && ch.choices != null) {
						JSONArray choices = new JSONArray();
						foreach (string c in ch.choices) choices.Add(c);
						p["choices"] = choices;
					}
					choosers.Add(p);
				} catch (Exception e) { Log.Debug("chooser param " + n + ": " + e.Message); }
			}
			r["choosers"] = choosers;

			JSONArray colors = new JSONArray();
			foreach (string n in SortedNames(st.GetColorParamNames())) {
				try {
					JSONClass p = new JSONClass();
					p["name"] = n;
					p["value"] = ColorToHex(st.GetColorParamValue(n));
					colors.Add(p);
				} catch (Exception e) { Log.Debug("color param " + n + ": " + e.Message); }
			}
			r["colors"] = colors;

			JSONArray actions = new JSONArray();
			foreach (string n in SortedNames(st.GetActionNames())) {
				try {
					JSONClass p = new JSONClass();
					p["name"] = n;
					actions.Add(p);
				} catch (Exception e) { Log.Debug("action " + n + ": " + e.Message); }
			}
			r["actions"] = actions;

			JSONArray customParams = new JSONArray();
			try {
				string[] custom = st.GetCustomParamNames();
				if (custom != null) {
					foreach (string n in custom) {
						JSONClass p = new JSONClass();
						p["name"] = n;
						customParams.Add(p);
					}
				}
			} catch (Exception e) { Log.Debug("custom params: " + e.Message); }
			r["customParams"] = customParams;

			JSONArray properties = new JSONArray();
			AddSimpleMembers(properties, st, 200);
			r["properties"] = properties;
			return r;
		}

		private static List<string> SortedNames(List<string> names) {
			List<string> list = names != null ? new List<string>(names) : new List<string>();
			list.Sort();
			return list;
		}

		private static JSONStorableFloat GetFloatParamObj(JSONStorable st, string name) {
			Dictionary<string, JSONStorableFloat> d = FloatParams(st);
			if (d == null) return null;
			JSONStorableFloat f;
			return d.TryGetValue(name, out f) ? f : null;
		}

		private static JSONStorableStringChooser GetChooserObj(JSONStorable st, string name) {
			Dictionary<string, JSONStorableStringChooser> d = ChooserParams(st);
			if (d == null) return null;
			JSONStorableStringChooser c;
			return d.TryGetValue(name, out c) ? c : null;
		}

		// ---------- generic property access (covers customParamNames like gravityX, DAZBone.position, ...) ----------

		private static bool IsSimpleType(Type t) {
			return t == typeof(float) || t == typeof(double) || t == typeof(int) || t == typeof(long) || t == typeof(bool)
				|| t == typeof(string) || t == typeof(Vector3) || t == typeof(Color) || t.IsEnum;
		}

		private static MemberInfo FindPropOrField(Type t, string name) {
			BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
			PropertyInfo p = t.GetProperty(name, flags);
			if (p != null) {
				if (p.GetIndexParameters().Length > 0) return null;
				if (!IsSimpleType(p.PropertyType)) return null;
				if (p.GetGetMethod() == null && p.GetSetMethod() == null) return null;
				return p;
			}
			foreach (PropertyInfo pp in t.GetProperties(flags)) {
				if (pp.GetIndexParameters().Length > 0) continue;
				if (string.Equals(pp.Name, name, StringComparison.OrdinalIgnoreCase) && IsSimpleType(pp.PropertyType)) return pp;
			}
			FieldInfo f = t.GetField(name, flags);
			if (f != null && IsSimpleType(f.FieldType)) return f;
			foreach (FieldInfo ff in t.GetFields(flags)) {
				if (string.Equals(ff.Name, name, StringComparison.OrdinalIgnoreCase) && IsSimpleType(ff.FieldType)) return ff;
			}
			return null;
		}

		private static Type MemberType(MemberInfo m) {
			if (m is PropertyInfo) return ((PropertyInfo)m).PropertyType;
			return ((FieldInfo)m).FieldType;
		}

		private static object GetMemberValue(MemberInfo m, object target) {
			if (m is PropertyInfo) return ((PropertyInfo)m).GetValue(target, null);
			return ((FieldInfo)m).GetValue(target);
		}

		private static void SetMemberValue(MemberInfo m, object target, object value) {
			if (m is PropertyInfo) {
				((PropertyInfo)m).SetValue(target, value, null);
			} else {
				((FieldInfo)m).SetValue(target, value);
			}
		}

		public static JSONNode SerializePropValue(object v) {
			if (v == null) return new JSONData("null");
			if (v is float) return new JSONData(F((float)v));
			if (v is double) return new JSONData(F((float)(double)v));
			if (v is int) return new JSONData((int)v);
			if (v is long) return new JSONData((double)(long)v);
			if (v is bool) return new JSONData((bool)v ? "true" : "false");
			if (v is string) return new JSONData((string)v);
			if (v is Vector3) {
				Vector3 vec = (Vector3)v;
				JSONClass r = new JSONClass();
				r["x"] = F(vec.x);
				r["y"] = F(vec.y);
				r["z"] = F(vec.z);
				return r;
			}
			if (v is Color) {
				Color c = (Color)v;
				return new JSONData("#" + ColorUtility.ToHtmlStringRGBA(c));
			}
			if (v.GetType().IsEnum) return new JSONData(v.ToString());
			return new JSONData(v.ToString());
		}

		public static object ParsePropValue(Type pt, JSONNode value) {
			if (pt == typeof(float)) return value.AsFloat;
			if (pt == typeof(double)) return (double)value.AsFloat;
			if (pt == typeof(int)) return value.AsInt;
			if (pt == typeof(long)) return (long)value.AsFloat;
			if (pt == typeof(bool)) return value.AsBool;
			if (pt == typeof(string)) return value.Value;
			if (pt == typeof(Vector3)) return ParseVec3(value);
			if (pt == typeof(Color)) {
				Color c;
				if (value.Value.StartsWith("#") && ColorUtility.TryParseHtmlString(value.Value, out c)) return c;
				JSONArray a = value.AsArray;
				if (a != null && a.Count >= 3) {
					return new Color(a[0].AsFloat, a[1].AsFloat, a[2].AsFloat, a.Count > 3 ? a[3].AsFloat : 1f);
				}
				throw new ApiError("bad color: " + value.Value + " (use #RRGGBB or [r,g,b,a])");
			}
			if (pt.IsEnum) return Enum.Parse(pt, value.Value, true);
			throw new ApiError("unsupported property type: " + pt.Name);
		}

		private static void AddSimpleMembers(JSONArray arr, object target, int cap) {
			Type t = target.GetType();
			HashSet<string> seen = new HashSet<string>();
			int count = 0;
			BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
			List<string> names = new List<string>();
			foreach (PropertyInfo p in t.GetProperties(flags)) {
				if (p.GetIndexParameters().Length > 0) continue;
				if (IsSimpleType(p.PropertyType) && p.GetGetMethod() != null) names.Add(p.Name);
			}
			foreach (FieldInfo f in t.GetFields(flags)) {
				if (IsSimpleType(f.FieldType)) names.Add(f.Name);
			}
			names.Sort(StringComparer.OrdinalIgnoreCase);
			foreach (string n in names) {
				if (!seen.Add(n)) continue;
				try {
					MemberInfo m = FindPropOrField(t, n);
					if (m == null) continue;
					JSONClass row = new JSONClass();
					row["name"] = n;
					row["type"] = MemberType(m).Name;
					row["value"] = SerializePropValue(GetMemberValue(m, target));
					arr.Add(row);
					count++;
					if (count >= cap) break;
				} catch { }
			}
		}

		public static string ColorToHex(HSVColor hsv) {
			Color c = Color.HSVToRGB(hsv.H, hsv.S, hsv.V);
			return "#" + ColorUtility.ToHtmlStringRGB(c);
		}

		public static HSVColor ParseColor(string s) {
			Color c;
			if (s.StartsWith("#")) {
				if (!ColorUtility.TryParseHtmlString(s, out c)) throw new ApiError("bad color: " + s + " (use #RRGGBB or r,g,b[,a])");
			} else {
				string[] parts = s.Split(',');
				if (parts.Length < 3) throw new ApiError("bad color: " + s + " (use #RRGGBB or r,g,b[,a])");
				c = new Color(
					float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
					float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
					float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
					parts.Length > 3 ? float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture) : 1f
				);
			}
			float h, s2, v;
			Color.RGBToHSV(c, out h, out s2, out v);
			HSVColor hsv = new HSVColor();
			hsv.H = h;
			hsv.S = s2;
			hsv.V = v;
			return hsv;
		}

		public JSONNode GetParam(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			JSONStorable st = a.GetStorableByID(S(args, "storable"));
			if (st == null) throw new ApiError("storable not found: " + S(args, "storable") + " on " + a.uid);
			string name = S(args, "param");
			if (name == "") throw new ApiError("param required");
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["storable"] = S(args, "storable");
			r["param"] = name;
			if (st.GetFloatParamNames().Contains(name)) {
				r["type"] = "float";
				r["value"] = F(st.GetFloatParamValue(name));
				JSONStorableFloat f = GetFloatParamObj(st, name);
				if (f != null) {
					r["min"] = F(f.min);
					r["max"] = F(f.max);
				}
			} else if (st.GetBoolParamNames().Contains(name)) {
				r["type"] = "bool";
				r["value"] = st.GetBoolParamValue(name) ? "true" : "false";
			} else if (st.GetStringParamNames().Contains(name)) {
				r["type"] = "string";
				r["value"] = st.GetStringParamValue(name);
			} else if (st.GetStringChooserParamNames().Contains(name)) {
				r["type"] = "chooser";
				r["value"] = st.GetStringChooserParamValue(name);
				JSONStorableStringChooser ch = GetChooserObj(st, name);
				if (ch != null && ch.choices != null) {
					JSONArray choices = new JSONArray();
					foreach (string c in ch.choices) choices.Add(c);
					r["choices"] = choices;
				}
			} else if (st.GetColorParamNames().Contains(name)) {
				r["type"] = "color";
				r["value"] = ColorToHex(st.GetColorParamValue(name));
			} else {
				// fall back to a public property/field (covers customParamNames like gravityX, DAZBone.position)
				MemberInfo m = FindPropOrField(st.GetType(), name);
				if (m == null) throw new ApiError("param not found: " + name + " on " + S(args, "storable") + " (see list_storable_params)");
				r["type"] = "property";
				r["propertyType"] = MemberType(m).Name;
				r["value"] = SerializePropValue(GetMemberValue(m, st));
			}
			return r;
		}

		public JSONNode SetParam(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			string sid = S(args, "storable");
			JSONStorable st = a.GetStorableByID(sid);
			if (st == null) throw new ApiError("storable not found: " + sid + " on " + a.uid);
			string name = S(args, "param");
			if (name == "") throw new ApiError("param required");
			JSONNode value = args["value"];
			if (value == null) throw new ApiError("value required");
			string type = S(args, "type");
			if (type == "" || type == "auto") {
				if (st.GetFloatParamNames().Contains(name)) type = "float";
				else if (st.GetBoolParamNames().Contains(name)) type = "bool";
				else if (st.GetStringParamNames().Contains(name)) type = "string";
				else if (st.GetStringChooserParamNames().Contains(name)) type = "chooser";
				else if (st.GetColorParamNames().Contains(name)) type = "color";
				else if (FindPropOrField(st.GetType(), name) != null) type = "property";
			}
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["storable"] = sid;
			r["param"] = name;
			switch (type) {
				case "float":
					st.SetFloatParamValue(name, value.AsFloat);
					r["type"] = "float";
					r["value"] = F(st.GetFloatParamValue(name));
					break;
				case "bool":
					st.SetBoolParamValue(name, value.AsBool);
					r["type"] = "bool";
					r["value"] = st.GetBoolParamValue(name) ? "true" : "false";
					break;
				case "string":
					st.SetStringParamValue(name, value.Value);
					r["type"] = "string";
					r["value"] = st.GetStringParamValue(name);
					break;
				case "chooser":
					st.SetStringChooserParamValue(name, value.Value);
					r["type"] = "chooser";
					r["value"] = st.GetStringChooserParamValue(name);
					break;
				case "color":
					st.SetColorParamValue(name, ParseColor(value.Value));
					r["type"] = "color";
					r["value"] = ColorToHex(st.GetColorParamValue(name));
					break;
				case "property": {
					MemberInfo m = FindPropOrField(st.GetType(), name);
					if (m == null) throw new ApiError("property not found: " + name + " on " + sid);
					SetMemberValue(m, st, ParsePropValue(MemberType(m), value));
					r["type"] = "property";
					r["propertyType"] = MemberType(m).Name;
					r["value"] = SerializePropValue(GetMemberValue(m, st));
					break;
				}
				default:
					throw new ApiError("cannot determine param type for " + name + "; specify type: float|bool|string|chooser|color|property");
			}
			return r;
		}

		public JSONNode CallAction(JSONNode args) {
			Atom a = FindAtom(S(args, "uid"));
			JSONStorable st = a.GetStorableByID(S(args, "storable"));
			if (st == null) throw new ApiError("storable not found: " + S(args, "storable") + " on " + a.uid);
			string actionName = S(args, "action");
			if (actionName == "") throw new ApiError("action required");
			JSONStorableAction action = st.GetAction(actionName);
			if (action == null) throw new ApiError("action not found: " + actionName + " (see list_storable_params)");
			if (action.actionCallback != null) action.actionCallback();
			JSONClass r = new JSONClass();
			r["uid"] = a.uid;
			r["storable"] = S(args, "storable");
			r["action"] = actionName;
			r["called"] = "true";
			return r;
		}

		// ===================== camera =====================

		private static Camera MonitorCamera() {
			Camera cam = SC.MonitorCenterCamera;
			if (cam == null) throw new ApiError("no monitor camera available");
			return cam;
		}

		public JSONNode GetCamera() {
			Camera cam = MonitorCamera();
			JSONClass r = new JSONClass();
			r["position"] = Vec(cam.transform.position);
			r["rotation"] = Vec(cam.transform.eulerAngles);
			r["fov"] = F(cam.fieldOfView);
			return r;
		}

		public JSONNode SetCamera(JSONNode args) {
			Camera cam = MonitorCamera();
			if (Has(args, "position")) cam.transform.position = ParseVec3(args["position"]);
			if (Has(args, "rotation")) cam.transform.rotation = Quaternion.Euler(ParseVec3(args["rotation"]));
			if (Has(args, "fov")) cam.fieldOfView = Num(args, "fov", 60f);
			return GetCamera();
		}

		public JSONNode CaptureView(JSONNode args) {
			Camera cam = MonitorCamera();
			int w = (int)Num(args, "width", 1280);
			int h = (int)Num(args, "height", 720);
			if (w < 64 || h < 64) throw new ApiError("width/height too small");
			string path = S(args, "path");
			if (path == "") path = "Saves/PluginData/vam-mcp/preview.png";
			FileManagerSecure.CreateDirectory("Saves/PluginData/vam-mcp");

			RenderTexture rt = new RenderTexture(w, h, 24);
			RenderTexture oldTarget = cam.targetTexture;
			RenderTexture oldActive = RenderTexture.active;
			Texture2D tex = null;
			try {
				cam.targetTexture = rt;
				cam.Render();
				RenderTexture.active = rt;
				tex = new Texture2D(w, h, TextureFormat.RGB24, false);
				tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
				tex.Apply();
				byte[] bytes = tex.EncodeToPNG();
				FileManagerSecure.WriteAllBytes(path, bytes);
			} finally {
				cam.targetTexture = oldTarget;
				RenderTexture.active = oldActive;
				if (tex != null) UnityEngine.Object.Destroy(tex);
				rt.Release();
			}
			JSONClass r = new JSONClass();
			r["path"] = path;
			r["width"] = w.ToString();
			r["height"] = h.ToString();
			return r;
		}

		// ===================== simulation =====================

		public JSONNode SetSimulation(JSONNode args) {
			JSONClass applied = new JSONClass();
			if (Has(args, "paused")) {
				bool paused = Bool(args, "paused", false);
				Time.timeScale = paused ? 0f : 1f;
				applied["paused"] = paused ? "true" : "false";
			}
			if (Has(args, "timeScale")) {
				Time.timeScale = Num(args, "timeScale", 1f);
			}
			applied["timeScale"] = F(Time.timeScale);
			return applied;
		}

		public JSONNode ResetSimulation() {
			SC.ResetSimulation(10, "VaMMCP", false);
			JSONClass r = new JSONClass();
			r["ok"] = "true";
			return r;
		}

		// ===================== hub (community resources) =====================

		private static string hubApiUrlCache;

		private static string HubApiUrl() {
			if (hubApiUrlCache != null) return hubApiUrlCache;
			try {
				Atom core = SC.GetAtomByUid("CoreControl");
				if (core != null) {
					JSONStorable st = core.GetStorableByID("HubDownloader");
					if (st != null) {
						FieldInfo f = st.GetType().GetField("apiUrl", BindingFlags.NonPublic | BindingFlags.Instance);
						if (f != null) {
							object v = f.GetValue(st);
							if (v != null && v.ToString() != "") {
								hubApiUrlCache = v.ToString();
								return hubApiUrlCache;
							}
						}
					}
				}
			} catch (Exception e) {
				Log.Debug("HubApiUrl: " + e.Message);
			}
			return "https://hub.virtamate.com/api";
		}

		/// <summary>Poller tool. Browse the VaM Hub (community resources) via the game's own Hub API.
		/// Uses UnityWebRequest (Unity's TLS) — plain HttpWebRequest cannot speak TLS1.2 on the .NET 3.5 profile.</summary>
		public JSONNode HubBrowse(JSONNode args) {
			string apiUrl = Mt.Run(() => HubApiUrl(), 10000);
			JSONClass body = new JSONClass();
			body["source"] = "VaM";
			body["action"] = "getResources";
			body["latest_image"] = "Y";
			body["perpage"] = ((int)Num(args, "perpage", 20)).ToString();
			body["page"] = ((int)Num(args, "page", 1)).ToString();
			string search = S(args, "search");
			if (search != "") {
				body["search"] = search;
				body["searchall"] = "true";
			}
			string sort = S(args, "sort");
			if (sort != "") body["sort"] = sort;
			string type = S(args, "type");
			if (type != "") body["type"] = type;
			string postData = body.ToString();

			object gate = new object();
			bool done = false;
			string result = null;
			string error = null;
			Mt.Run(delegate {
				try {
					UnityEngine.Networking.UnityWebRequest req = new UnityEngine.Networking.UnityWebRequest(apiUrl, "POST");
					req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(Encoding.UTF8.GetBytes(postData));
					req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
					req.SetRequestHeader("Content-Type", "application/json");
					req.SetRequestHeader("Accept", "application/json");
					SC.StartCoroutine(DoHubPost(req, gate, delegate(string r) {
						lock (gate) { result = r; done = true; }
					}, delegate(string e) {
						lock (gate) { error = e; done = true; }
					}));
				} catch (Exception e) {
					lock (gate) { error = e.Message; done = true; }
				}
			}, 15000);

			DateTime deadline = DateTime.UtcNow.AddSeconds(90);
			while (DateTime.UtcNow < deadline) {
				Thread.Sleep(200);
				bool f;
				string r, e;
				lock (gate) { f = done; r = result; e = error; }
				if (f) {
					if (e != null) throw new ApiError("hub request failed: " + e);
					JSONNode resp = JSON.Parse(r);
					if (resp == null) throw new ApiError("hub returned invalid JSON");
					JSONClass outNode = new JSONClass();
					outNode["apiUrl"] = apiUrl;
					if (resp["resources"] != null) {
						JSONArray resources = resp["resources"].AsArray;
						if (resources == null) throw new ApiError("hub response has no resources array");
						JSONArray clean = new JSONArray();
						foreach (JSONNode res in resources) {
							JSONClass row = new JSONClass();
							row["id"] = res["resource_id"] != null ? res["resource_id"].Value : "";
							row["package_id"] = res["package_id"] != null ? res["package_id"].Value : "";
							row["title"] = res["title"] != null ? res["title"].Value : "";
							row["author"] = res["username"] != null ? res["username"].Value : "";
							row["version"] = res["version_string"] != null ? res["version_string"].Value : "";
							row["type"] = res["type"] != null ? res["type"].Value : "";
							row["category"] = res["category"] != null ? res["category"].Value : "";
							row["downloads"] = res["download_count"] != null ? res["download_count"].Value : "";
							row["rating"] = res["rating_avg"] != null ? res["rating_avg"].Value : "";
							row["tags"] = res["tags"] != null ? res["tags"].Value : "";
							row["tagline"] = res["tag_line"] != null ? res["tag_line"].Value : "";
							clean.Add(row);
						}
						outNode["count"] = clean.Count.ToString();
						outNode["resources"] = clean;
					} else {
						outNode["raw"] = r.Substring(0, Math.Min(500, r.Length));
					}
					if (resp["numPages"] != null) outNode["numPages"] = resp["numPages"].Value;
					if (resp["numResources"] != null) outNode["numResources"] = resp["numResources"].Value;
					return outNode;
				}
			}
			throw new ApiError("hub request timed out after 90s");
		}

		private IEnumerator DoHubPost(UnityEngine.Networking.UnityWebRequest req, object gate, Action<string> onDone, Action<string> onError) {
			yield return req.SendWebRequest();
			try {
				if (req.isNetworkError || req.isHttpError) {
					onError(req.error + " | " + (req.downloadHandler != null ? req.downloadHandler.text : ""));
				} else {
					onDone(req.downloadHandler.text);
				}
			} finally {
				req.Dispose();
			}
		}

		/// <summary>Poller tool. Get full detail for one Hub resource: package name, all version files, dependencies.</summary>
		public JSONNode HubDetail(JSONNode args) {
			string apiUrl = Mt.Run(() => HubApiUrl(), 10000);
			string package = S(args, "package");
			string resourceId = S(args, "resource_id");
			if (package == "" && resourceId == "") throw new ApiError("package name or resource_id required");
			JSONClass body = new JSONClass();
			body["source"] = "VaM";
			body["action"] = "getResourceDetail";
			body["latest_image"] = "Y";
			if (package != "") body["package_name"] = package;
			if (resourceId != "") body["resource_id"] = resourceId;
			string postData = body.ToString();

			object gate = new object();
			bool done = false;
			string result = null;
			string error = null;
			Mt.Run(delegate {
				try {
					UnityEngine.Networking.UnityWebRequest req = new UnityEngine.Networking.UnityWebRequest(apiUrl, "POST");
					req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(Encoding.UTF8.GetBytes(postData));
					req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
					req.SetRequestHeader("Content-Type", "application/json");
					req.SetRequestHeader("Accept", "application/json");
					SC.StartCoroutine(DoHubPost(req, gate, delegate(string r2) {
						lock (gate) { result = r2; done = true; }
					}, delegate(string e2) {
						lock (gate) { error = e2; done = true; }
					}));
				} catch (Exception e3) {
					lock (gate) { error = e3.Message; done = true; }
				}
			}, 15000);

			DateTime deadline = DateTime.UtcNow.AddSeconds(60);
			while (DateTime.UtcNow < deadline) {
				Thread.Sleep(200);
				bool f;
				string r2, e2;
				lock (gate) { f = done; r2 = result; e2 = error; }
				if (f) {
					if (e2 != null) throw new ApiError("hub request failed: " + e2);
					JSONNode resp = JSON.Parse(r2);
					if (resp == null) throw new ApiError("hub returned invalid JSON");
					JSONClass outNode = new JSONClass();
					if (resp["title"] != null) outNode["title"] = resp["title"].Value;
					if (resp["package_name"] != null) outNode["package"] = resp["package_name"].Value;
					if (resp["username"] != null) outNode["author"] = resp["username"].Value;
					if (resp["version_string"] != null) outNode["version"] = resp["version_string"].Value;
					JSONArray files = resp["hubFiles"] != null ? resp["hubFiles"].AsArray : null;
					if (files != null) {
						JSONArray clean = new JSONArray();
						foreach (JSONNode fnode in files) {
							JSONClass row = new JSONClass();
							row["filename"] = fnode["filename"] != null ? fnode["filename"].Value : "";
							row["version"] = fnode["version_string"] != null ? fnode["version_string"].Value : "";
							row["size"] = fnode["size"] != null ? fnode["size"].Value : "";
							row["download_url"] = fnode["download_url"] != null ? fnode["download_url"].Value : "";
							clean.Add(row);
						}
						outNode["files"] = clean;
					}
					JSONArray deps = resp["dependencies"] != null ? resp["dependencies"].AsArray : null;
					if (deps != null) {
						JSONArray cleanDeps = new JSONArray();
						foreach (JSONNode dn in deps) cleanDeps.Add(dn["package_name"] != null ? dn["package_name"].Value : "");
						outNode["dependencies"] = cleanDeps;
					}
					return outNode;
				}
			}
			throw new ApiError("hub request timed out after 60s");
		}

		/// <summary>Poller tool. Download a package from the VaM Hub using the game's own downloader (handles auth + install into AddonPackages).</summary>
		public JSONNode HubDownload(JSONNode args) {
			string package = S(args, "package");
			if (package == "") throw new ApiError("package required (search with hub_browse)");
			object gate = new object();
			bool finished = false;
			string error = null;
			bool started = Mt.Run(delegate {
				try {
					if (UserPreferences.singleton != null) UserPreferences.singleton.enableHubDownloader = true;
				} catch { }
				Atom core = SC.GetAtomByUid("CoreControl");
				if (core == null) throw new ApiError("no CoreControl atom in the scene");
				JSONStorable st = core.GetStorableByID("HubDownloader");
				MVR.Hub.HubDownloader hd = st as MVR.Hub.HubDownloader;
				if (hd == null) throw new ApiError("no HubDownloader on CoreControl");
				MVR.Hub.HubDownloader.SuccessCallback ok = delegate {
					lock (gate) { finished = true; }
				};
				MVR.Hub.HubDownloader.ErrorCallback err = delegate(string e) {
					lock (gate) { finished = true; error = e; }
				};
				return hd.DownloadPackages(ok, err, package);
			}, 15000);
			if (!started) throw new ApiError("hub download could not start (hub downloader disabled?)");
			DateTime deadline = DateTime.UtcNow.AddSeconds(240);
			while (DateTime.UtcNow < deadline) {
				Thread.Sleep(1000);
				bool f;
				string e;
				lock (gate) { f = finished; e = error; }
				if (f) {
					if (e != null) throw new ApiError("hub download failed: " + e);
					JSONClass r = new JSONClass();
					r["package"] = package;
					r["downloaded"] = "true";
					r["note"] = "the package is installed into AddonPackages; list_packages will show it after the package scan refreshes";
					return r;
				}
			}
			throw new ApiError("hub download timed out after 240s");
		}

		// ===================== skin subsurface scattering =====================

		public JSONNode SetSkinSss(JSONNode args) {
			Atom p = RequirePerson(S(args, "person"));
			JSONStorable skin = p.GetStorableByID("skin");
			if (skin == null) throw new ApiError("no skin storable on " + p.uid);
			string color = S(args, "color");
			if (color == "") throw new ApiError("color required (#RRGGBB; a reddish tint like #C06060 gives a classic subsurface look)");
			if (!skin.GetColorParamNames().Contains("Subsurface Color")) {
				throw new ApiError("skin has no 'Subsurface Color' param on " + p.uid);
			}
			skin.SetColorParamValue("Subsurface Color", ParseColor(color));
			JSONClass r = new JSONClass();
			r["person"] = p.uid;
			r["param"] = "Subsurface Color";
			r["value"] = ColorToHex(skin.GetColorParamValue("Subsurface Color"));
			return r;
		}

		// ===================== plugin management (VaM native plugins) =====================

		private static FieldInfo pluginsField;
		private static List<MVRPlugin> GetPlugins(MVRPluginManager pm) {
			try {
				if (pluginsField == null) pluginsField = typeof(MVRPluginManager).GetField("plugins", BindingFlags.NonPublic | BindingFlags.Instance);
				if (pluginsField != null) return (List<MVRPlugin>)pluginsField.GetValue(pm);
			} catch { }
			return new List<MVRPlugin>();
		}

		private static MVRPluginManager PluginManagerFor(Atom target) {
			JSONStorable st = target.GetStorableByID("PluginManager");
			MVRPluginManager pm = st as MVRPluginManager;
			if (pm == null) throw new ApiError("no PluginManager storable on " + target.uid);
			return pm;
		}

		/// <summary>Resolve the plugin target atom: empty uid = CoreControl (session plugins), else the given atom (e.g. a Person).</summary>
		private static Atom PluginTarget(string uid) {
			if (uid == "" || uid == "CoreControl") {
				Atom core = SC.GetAtomByUid("CoreControl");
				if (core == null) throw new ApiError("no CoreControl atom (session plugins unavailable)");
				return core;
			}
			return FindAtom(uid);
		}

		public JSONNode ListPlugins(JSONNode args) {
			Atom target = PluginTarget(S(args, "uid"));
			MVRPluginManager pm = PluginManagerFor(target);
			JSONArray arr = new JSONArray();
			foreach (MVRPlugin p in GetPlugins(pm)) {
				JSONClass row = new JSONClass();
				row["uid"] = p.uid;
				row["path"] = (p.pluginURLJSON != null && p.pluginURLJSON.val != null) ? p.pluginURLJSON.val : "";
				row["loaded"] = (p.scriptControllers != null && p.scriptControllers.Count > 0) ? "true" : "false";
				arr.Add(row);
			}
			JSONClass r = new JSONClass();
			r["atom"] = target.uid;
			r["count"] = arr.Count.ToString();
			r["plugins"] = arr;
			return r;
		}

		/// <summary>Poller tool. Add a VaM plugin (.cs/.cslist/.dll) to an atom's PluginManager (default: CoreControl = session plugins; pass uid=<person> for a person's plugins).</summary>
		public JSONNode AddPlugin(JSONNode args) {
			string path = S(args, "path");
			if (path == "") throw new ApiError("path required (.cs/.cslist/.dll plugin path, e.g. Custom/Scripts/MyPlugin.cs)");
			Atom target = PluginTarget(S(args, "uid"));
			MVRPluginManager pm = PluginManagerFor(target);
			MVRPlugin created = Mt.Run(delegate {
				try {
					if (UserPreferences.singleton != null) UserPreferences.singleton.enablePlugins = true;
				} catch { }
				MVRPlugin p = pm.CreatePlugin();
				p.pluginURLJSON.val = path; // triggers the async load (compile for .cs)
				return p;
			}, 15000);
			DateTime deadline = DateTime.UtcNow.AddSeconds(60);
			while (DateTime.UtcNow < deadline) {
				Thread.Sleep(500);
				bool loaded = Mt.Run(delegate {
					foreach (MVRPlugin p in GetPlugins(pm)) {
						if (p == created && p.scriptControllers != null && p.scriptControllers.Count > 0) return true;
					}
					return false;
				}, 10000);
				if (loaded) {
					JSONClass r = new JSONClass();
					r["atom"] = target.uid;
					r["plugin_uid"] = created.uid;
					r["path"] = path;
					r["loaded"] = "true";
					r["note"] = "the plugin registers itself as a storable; configure it via list_storable_params / get_param / set_param with uid=<atom> storable=<plugin_uid>";
					return r;
				}
			}
			throw new ApiError("plugin load timed out after 60s (check the VaM console/output_log.txt for compile errors)");
		}

		public JSONNode RemovePlugin(JSONNode args) {
			string pluginUid = S(args, "plugin_uid");
			if (pluginUid == "") throw new ApiError("plugin_uid required (see list_plugins)");
			Atom target = PluginTarget(S(args, "uid"));
			MVRPluginManager pm = PluginManagerFor(target);
			pm.RemovePluginWithUID(pluginUid);
			JSONClass r = new JSONClass();
			r["atom"] = target.uid;
			r["removed"] = pluginUid;
			return r;
		}

		// ===================== eval (escape hatch) =====================

		private static DynamicCSharp.ScriptDomain evalDomain;
		private static readonly Dictionary<string, DynamicCSharp.ScriptProxy> evalCache = new Dictionary<string, DynamicCSharp.ScriptProxy>();

		private static DynamicCSharp.ScriptDomain EvalDomain() {
			if (evalDomain == null) {
				evalDomain = DynamicCSharp.ScriptDomain.CreateDomain("VaMMCP_Eval", true);
			}
			return evalDomain;
		}

		/// <summary>Wrap user code in a full class we control (the library's own eval template is unusable: it inserts the code at class-member level).</summary>
		private static string BuildEvalSource(string code) {
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append("using System;\n");
			sb.Append("using System.Linq;\n");
			sb.Append("using System.Collections.Generic;\n");
			sb.Append("using UnityEngine;\n");
			sb.Append("using SimpleJSON;\n");
			sb.Append("using MVR;\n");
			sb.Append("using MVR.FileManagementSecure;\n");
			sb.Append("public class _EvalProgram_" + Guid.NewGuid().ToString("N").Substring(0, 8) + " {\n");
			sb.Append("  public SuperController sc;\n");
			sb.Append("  public string __error = \"\";\n");
			sb.Append("  public object Run() {\n");
			sb.Append("    try {\n");
			if (code.IndexOf(';') >= 0) {
				// statements mode: execute as-is, return null unless the code returns itself
				sb.Append(code);
				sb.Append("\n      return null;\n");
			} else {
				// expression mode
				sb.Append("      return (" + code + ");\n");
			}
			sb.Append("    } catch (System.Exception __e) {\n");
			// NOTE: __e.ToString() — NOT __e.GetType().Name: Type.Name is declared on System.Reflection.MemberInfo,
			// which VaM's DynamicCSharp sandbox prohibits (NamespaceRestriction).
			sb.Append("      __error = __e.ToString();\n");
			sb.Append("      return null;\n");
			sb.Append("    }\n");
			sb.Append("  }\n");
			sb.Append("}\n");
			return sb.ToString();
		}

		public JSONNode EvalCs(JSONNode args) {
			string code = S(args, "code");
			if (code == "") throw new ApiError("code required");
			DynamicCSharp.ScriptDomain domain = EvalDomain();
			string source = BuildEvalSource(code);
			DynamicCSharp.ScriptProxy proxy;
			if (!evalCache.TryGetValue(source, out proxy)) {
				DynamicCSharp.ScriptType type = domain.CompileAndLoadScriptSource(source);
				if (type == null) throw new ApiError("eval compilation failed (see VaM output_log.txt for compiler errors)");
				proxy = type.CreateInstance();
				if (proxy == null) throw new ApiError("eval failed to instantiate compiled type");
				evalCache[source] = proxy;
			}
			try {
				proxy.Fields["sc"] = SuperController.singleton;
			} catch (Exception e) {
				throw new ApiError("eval bind failed: " + e.Message);
			}
			object result;
			try {
				result = proxy.Call("Run");
			} catch (Exception e) {
				Exception inner = e;
				while (inner.InnerException != null) inner = inner.InnerException;
				throw new ApiError("eval error: " + inner.Message);
			}
			string err = "";
			try {
				object errObj = proxy.Fields["__error"];
				if (errObj != null) err = errObj.ToString();
			} catch { }
			if (err != "") throw new ApiError("eval error: " + err);
			JSONClass r = new JSONClass();
			r["result"] = SerializeValue(result);
			return r;
		}

		public static JSONNode SerializeValue(object o) {
			if (o == null) return new JSONData("null");
			if (o is string) return new JSONData((string)o);
			if (o is bool) return new JSONData((bool)o ? "true" : "false");
			if (o is int) return new JSONData((int)o);
			if (o is long) return new JSONData((double)(long)o);
			if (o is float) return new JSONData(F((float)o));
			if (o is double) return new JSONData(F((float)(double)o));
			if (o is Vector3) {
				Vector3 v = (Vector3)o;
				JSONClass r = new JSONClass();
				r["x"] = F(v.x);
				r["y"] = F(v.y);
				r["z"] = F(v.z);
				return r;
			}
			IEnumerable e = o as IEnumerable;
			if (e != null) {
				JSONArray arr = new JSONArray();
				foreach (object x in e) {
					try { arr.Add(SerializeValue(x)); } catch { }
				}
				return arr;
			}
			return new JSONData(o.ToString());
		}
	}
}
