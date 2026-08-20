using System.Collections.Generic;
using SimpleJSON;
using VaMMCP.Api;

namespace VaMMCP.Mcp {
	/// <summary>Builds the complete MCP tool surface.</summary>
	public static class ToolRegistry {
		public static List<Tool> CreateAll(VaMApi api, bool allowEval) {
			List<Tool> tools = new List<Tool>();

			// ---------- status / session ----------
			tools.Add(new Tool("status", "VaM runtime status: plugin version, VaM version, atom/person counts, endpoint info.")
				.Fn(a => api.Status()));

			// ---------- scene ----------
			tools.Add(new Tool("list_scenes", "List scene files under Saves/scene. Returns path + name for each scene.")
				.P("search", "string", "Optional substring filter", false)
				.Fn(api.ListScenes));
			tools.Add(new Tool("load_scene", "Load a scene. With merge=true the scene is merged into the current one. The load happens over the next few seconds.")
				.P("path", "string", "Scene path from list_scenes", true)
				.P("merge", "boolean", "Merge instead of replace (default false)", false)
				.Fn(api.LoadScene));
			tools.Add(new Tool("new_scene", "Start a new empty scene. Warning: discards the current scene.")
				.Fn(a => api.NewScene()));
			tools.Add(new Tool("save_scene", "Save the current scene to Saves/scene.")
				.P("path", "string", "Optional save path (default Saves/scene/VaMMCP_<timestamp>.json)", false)
				.Fn(api.SaveScene));

			// ---------- atoms ----------
			tools.Add(new Tool("list_atom_types", "List all atom types VaM can create, grouped by category.")
				.Fn(a => api.ListAtomTypes()));
			tools.Add(new Tool("list_atoms", "List atoms in the current scene with uid, type, on/off, position, rotation.")
				.P("type", "string", "Optional atom type filter, e.g. Person", false)
				.Fn(api.ListAtoms));
			tools.Add(new Tool("add_atom", "Create an atom of the given type (Person, Light, AnimationPattern, ...). Waits until it exists.")
				.P("type", "string", "Atom type (see list_atom_types)", true)
				.P("uid", "string", "Optional uid; auto-generated when omitted", false)
				.Timeout(60000).Poller()
				.Fn(api.AddAtom));
			tools.Add(new Tool("remove_atom", "Remove an atom from the scene.")
				.P("uid", "string", "Atom uid", true)
				.Fn(api.RemoveAtom));
			tools.Add(new Tool("set_atom_on", "Show (on=true) or hide (on=false) an atom.")
				.P("uid", "string", "Atom uid", true)
				.P("on", "boolean", "true to show, false to hide", true)
				.Fn(api.SetAtomOn));
			tools.Add(new Tool("get_atom_transform", "Get an atom's world position, rotation and scale.")
				.P("uid", "string", "Atom uid", true)
				.Fn(api.GetAtomTransform));
			tools.Add(new Tool("set_atom_transform", "Move/rotate/scale an atom. With relative=true the values are added to the current transform.")
				.P("uid", "string", "Atom uid", true)
				.P("position", "array", "[x, y, z] world position", false)
				.P("rotation", "array", "[x, y, z] euler angles", false)
				.P("scale", "array", "[x, y, z] local scale", false)
				.P("relative", "boolean", "Apply relative to current transform (default false)", false)
				.Fn(api.SetAtomTransform));

			// ---------- persons ----------
			tools.Add(new Tool("add_person", "Add a Person atom to the scene. Waits until it exists.")
				.P("uid", "string", "Optional uid (default auto)", false)
				.Timeout(90000).Poller()
				.Fn(api.AddPerson));
			tools.Add(new Tool("list_persons", "List Person atoms: uid, character, gender, on/off, position.")
				.Fn(a => api.ListPersons()));
			tools.Add(new Tool("list_looks", "List appearance presets (looks) on disk.")
				.P("search", "string", "Optional substring filter", false)
				.Fn(api.ListLooks));
			tools.Add(new Tool("load_look", "Apply an appearance preset (look) to a person. Replaces their character, morphs and skin.")
				.P("path", "string", "Look path from list_looks", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.LoadLook));
			tools.Add(new Tool("save_look", "Save the current appearance of a person as a look preset.")
				.P("path", "string", "Optional save path (default Saves/Person/Appearance/<uid>_<timestamp>.json)", false)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.SaveLook));
			tools.Add(new Tool("set_character", "Switch the base character mesh of a person (e.g. 'Female 3'). List available characters with get_param uid=<person> storable=geometry param=characterSelection. Loads asynchronously.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("name", "string", "Character name", true)
				.Fn(api.SetCharacter));

			// ---------- morphs (捏人) ----------
			tools.Add(new Tool("list_morphs", "Search body/face morphs of a person. Returns name, uid, region, group and current value. The morphs ARE the character sliders.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("search", "string", "Substring filter on morph name or uid", false)
				.P("region", "string", "Exact region filter (e.g. Face, Body)", false)
				.P("group", "string", "Exact group filter", false)
				.P("limit", "number", "Max results (default 500)", false)
				.Fn(api.ListMorphs));
			tools.Add(new Tool("set_morph", "Set a morph value on a person (e.g. breast size, face shape). Typical range -1..1.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("name", "string", "Morph display name or uid from list_morphs", true)
				.P("value", "number", "Morph value, typically -1..1", true)
				.Fn(api.SetMorph));
			tools.Add(new Tool("get_morph", "Read a morph value on a person.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("name", "string", "Morph display name or uid", true)
				.Fn(api.GetMorph));
			tools.Add(new Tool("reset_morphs", "Reset morphs of a person to 0 (optionally only one region).")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("region", "string", "Optional region to reset only (e.g. Face)", false)
				.Fn(api.ResetMorphs));

			// ---------- clothing / hair ----------
			tools.Add(new Tool("list_packages", "List installed .var packages (their asset paths are usable in load_clothing_preset / load_hair_preset / add_clothing_item / add_hair_item).")
				.Fn(a => api.ListPackages()));
			tools.Add(new Tool("list_clothing_presets", "List clothing presets/items on disk AND inside all .var packages (path may be a package path like 'Package.1:/Custom/Clothing/...').")
				.P("search", "string", "Optional substring filter", false)
				.Fn(api.ListClothingPresets));
			tools.Add(new Tool("load_clothing_preset", "Apply a clothing preset to a person.")
				.P("path", "string", "Preset path from list_clothing_presets", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.LoadClothingPreset));
			tools.Add(new Tool("list_clothing_items", "List clothing items currently on a person.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.ListClothingItems));
			tools.Add(new Tool("add_clothing_item", "Add a clothing item to a person from its .vam/.json path (package paths work, e.g. from list_clothing_presets). Waits for the character to be ready; loads asynchronously. Use set_clothing_item_on to activate it.")
				.P("path", "string", "Clothing item .vam/.json path", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Timeout(60000).Poller()
				.Fn(api.AddClothingItem));
			tools.Add(new Tool("remove_clothing_item", "Remove a clothing item from a person.")
				.P("id", "string", "Clothing item uid from list_clothing_items", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.RemoveClothingItem));
			tools.Add(new Tool("set_clothing_item_on", "Show or hide a clothing item.")
				.P("id", "string", "Clothing item uid", true)
				.P("on", "boolean", "true to wear, false to hide", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.SetClothingItemOn));
			tools.Add(new Tool("list_hair_presets", "List hair presets/items on disk AND inside all .var packages (path may be a package path).")
				.P("search", "string", "Optional substring filter", false)
				.Fn(api.ListHairPresets));
			tools.Add(new Tool("load_hair_preset", "Apply a hair preset to a person.")
				.P("path", "string", "Preset path from list_hair_presets", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.LoadHairPreset));
			tools.Add(new Tool("list_hair_items", "List hair items currently on a person.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.ListHairItems));
			tools.Add(new Tool("add_hair_item", "Add a hair item to a person from its .vam/.json path (package paths work, e.g. from list_hair_presets). Waits for the character to be ready; loads asynchronously.")
				.P("path", "string", "Hair item .vam/.json path", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Timeout(60000).Poller()
				.Fn(api.AddHairItem));
			tools.Add(new Tool("remove_hair_item", "Remove a hair item from a person.")
				.P("id", "string", "Hair item uid from list_hair_items", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.RemoveHairItem));
			tools.Add(new Tool("set_hair_item_on", "Show or hide a hair item.")
				.P("id", "string", "Hair item uid", true)
				.P("on", "boolean", "true to show, false to hide", true)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.SetHairItemOn));

			// ---------- poses / expressions ----------
			tools.Add(new Tool("list_poses", "List pose presets on disk.")
				.P("search", "string", "Optional substring filter", false)
				.Fn(api.ListPoses));
			tools.Add(new Tool("load_pose", "Apply a pose preset. With all=true applies to every Person in the scene.")
				.P("path", "string", "Pose path from list_poses", true)
				.P("person", "string", "Person uid (default: first Person; ignored when all=true)", false)
				.P("all", "boolean", "Apply to all persons (default false)", false)
				.Fn(api.LoadPose));
			tools.Add(new Tool("save_pose", "Save a person's current pose as a pose preset.")
				.P("path", "string", "Optional save path (default Saves/Person/Pose/<uid>_<timestamp>.json)", false)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.SavePose));
			tools.Add(new Tool("save_full_preset", "Export a person as a full preset (appearance + pose + physics) — equivalent to VaM's 'Save Full'.")
				.P("path", "string", "Optional save path (default Saves/Person/full/<uid>_<timestamp>.json)", false)
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.SaveFullPreset));
			tools.Add(new Tool("list_expressions", "List expression-like face morphs of a person with current values.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.ListExpressions));
			tools.Add(new Tool("set_expression", "Set a facial expression: optionally resets all expression morphs first, then sets the named one (default value 1).")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("name", "string", "Expression morph name from list_expressions", true)
				.P("value", "number", "Morph value (default 1)", false)
				.P("reset", "boolean", "Reset other expression morphs first (default true)", false)
				.Fn(api.SetExpression));

			// ---------- controls ----------
			tools.Add(new Tool("list_controls", "List FreeControllerV3 controls of a person (headControl, chestControl, lHandControl, ...).")
				.P("person", "string", "Person uid (default: first Person)", false)
				.Fn(api.ListControls));
			tools.Add(new Tool("get_control", "Read a person control's world position and rotation.")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("control", "string", "Control id from list_controls", true)
				.Fn(api.GetControl));
			tools.Add(new Tool("set_control", "Move/rotate a person control (e.g. position a hand, tilt the head).")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("control", "string", "Control id from list_controls", true)
				.P("position", "array", "[x, y, z] world position", false)
				.P("rotation", "array", "[x, y, z] euler angles", false)
				.Fn(api.SetControl));
			tools.Add(new Tool("set_gaze", "Adjust a person's gaze via the lookAt storable (best effort; inspect with list_storable_params storable=lookAt for the full param list).")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("amount", "number", "gazeAmount 0..1 (optional)", false)
				.P("targetControl", "string", "Target control/atom for the eyes (e.g. None, Camera)", false)
				.Fn(api.SetGaze));

			// ---------- generic param access (long tail) ----------
			tools.Add(new Tool("list_atom_storables", "List every storable (sub-component with params) of an atom.")
				.P("uid", "string", "Atom uid", true)
				.Fn(api.ListAtomStorables));
			tools.Add(new Tool("list_storable_params", "Introspect a storable: all float/bool/string/chooser/color params (with ranges, choices) and actions. This is how an agent can control ANY VaM slider.")
				.P("uid", "string", "Atom uid", true)
				.P("storable", "string", "Storable id from list_atom_storables", true)
				.Fn(api.ListStorableParams));
			tools.Add(new Tool("get_param", "Read a single param value (float|bool|string|chooser|color).")
				.P("uid", "string", "Atom uid", true)
				.P("storable", "string", "Storable id", true)
				.P("param", "string", "Param name", true)
				.Fn(api.GetParam));
			tools.Add(new Tool("set_param", "Set a single param value. Type is auto-detected; pass type to force (float|bool|string|chooser|color|property). 'property' writes a public property/field of the storable (covers custom params like gravityX). Colors accept #RRGGBB or r,g,b,a.")
				.P("uid", "string", "Atom uid", true)
				.P("storable", "string", "Storable id", true)
				.P("param", "string", "Param name", true)
				.P("value", "any", "Value to set", true)
				.P("type", "string", "Optional type override", false)
				.Fn(api.SetParam));
			tools.Add(new Tool("call_action", "Invoke an action button of a storable (e.g. Reset, Stop, Play).")
				.P("uid", "string", "Atom uid", true)
				.P("storable", "string", "Storable id", true)
				.P("action", "string", "Action name from list_storable_params", true)
				.Fn(api.CallAction));

			// ---------- camera ----------
			tools.Add(new Tool("get_camera", "Read the monitor camera position, rotation and FOV.")
				.Fn(a => api.GetCamera()));
			tools.Add(new Tool("set_camera", "Move the monitor camera: position, rotation (euler) and/or FOV.")
				.P("position", "array", "[x, y, z] world position", false)
				.P("rotation", "array", "[x, y, z] euler angles", false)
				.P("fov", "number", "Field of view in degrees", false)
				.Fn(api.SetCamera));
			tools.Add(new Tool("capture_view", "Render the monitor camera to a PNG file (default Saves/PluginData/vam-mcp/preview.png) and return its path. Set return_image=true to also get the picture back inline, for clients that cannot read the VaM folder themselves.")
				.P("width", "number", "Image width (default 1280, max 4096)", false)
				.P("height", "number", "Image height (default 720, max 4096)", false)
				.P("path", "string", "Optional output path", false)
				.P("return_image", "boolean", "Return the PNG inline as an MCP image (default false; keep the resolution modest, e.g. 640x360, and note that images above 4 MB stay on disk only)", false)
				.Fn(api.CaptureView));

			// ---------- simulation ----------
			tools.Add(new Tool("set_simulation", "Pause/resume the simulation or set the global time scale.")
				.P("paused", "boolean", "true pauses, false resumes", false)
				.P("timeScale", "number", "Global time scale (1 = normal)", false)
				.Fn(api.SetSimulation));
			tools.Add(new Tool("reset_simulation", "Reset the physics simulation (collapses soft-body / cloth jitter).")
				.Fn(a => api.ResetSimulation()));

			// ---------- hub / community resources ----------
			tools.Add(new Tool("hub_browse", "Browse the VaM Hub (community content: looks, clothing, scenes, plugins...). Queries the game's own Hub API.")
				.P("search", "string", "Search text", false)
				.P("page", "number", "Page number (default 1)", false)
				.P("perpage", "number", "Results per page (default 20)", false)
				.P("sort", "string", "Sort order (e.g. resource_update_date, downloads, rating)", false)
				.P("type", "string", "Content type filter", false)
				.Timeout(90000).Poller()
				.Fn(api.HubBrowse));
			tools.Add(new Tool("hub_detail", "Get full detail of one Hub resource: package name, author, version and all downloadable .var files + dependencies. Use the package name from here with hub_download.")
				.P("package", "string", "Package name (e.g. AcidBubbles.Timeline)", false)
				.P("resource_id", "string", "Hub resource id from hub_browse (alternative to package)", false)
				.Timeout(90000).Poller()
				.Fn(api.HubDetail));
			tools.Add(new Tool("hub_download", "Download a package from the VaM Hub using the game's own downloader (auth + install into AddonPackages). Package name from hub_browse/hub_detail results.")
				.P("package", "string", "Package name, e.g. AcidBubbles.Timeline", true)
				.Timeout(300000).Poller()
				.Fn(api.HubDownload));

			// ---------- skin ----------
			tools.Add(new Tool("set_skin_sss", "Set the skin subsurface-scattering (SSS) tint of a person via the skin material's 'Subsurface Color' (_SubdermisColor).")
				.P("person", "string", "Person uid (default: first Person)", false)
				.P("color", "string", "#RRGGBB color (e.g. #C06060 reddish subsurface tint)", true)
				.Fn(api.SetSkinSss));

			// ---------- plugins (VaM native plugin system) ----------
			tools.Add(new Tool("list_plugins", "List plugins on an atom's PluginManager. Empty uid = CoreControl (session plugins); pass uid=<person> for a person's plugins. A loaded plugin also registers as a storable of that atom — configure it via list_storable_params/set_param with storable=<plugin_uid>.")
				.P("uid", "string", "Atom uid (default CoreControl = session plugins)", false)
				.Fn(api.ListPlugins));
			tools.Add(new Tool("add_plugin", "Add a VaM plugin (.cs/.cslist/.dll) to an atom's PluginManager and wait until it compiles/loads. Default target = CoreControl (session plugins); pass uid=<person> to attach it to a person.")
				.P("path", "string", "Plugin path, e.g. Custom/Scripts/MyPlugin.cs or a package path", true)
				.P("uid", "string", "Atom uid (default CoreControl = session plugins)", false)
				.Timeout(90000).Poller()
				.Fn(api.AddPlugin));
			tools.Add(new Tool("remove_plugin", "Remove a plugin from an atom's PluginManager.")
				.P("plugin_uid", "string", "Plugin uid from list_plugins", true)
				.P("uid", "string", "Atom uid (default CoreControl = session plugins)", false)
				.Fn(api.RemovePlugin));

			// ---------- eval escape hatch ----------
			if (allowEval) {
				int evalTimeoutMs = Plugin.cfgEvalTimeoutSec.Value > 0 ? Plugin.cfgEvalTimeoutSec.Value * 1000 : 30000;
				tools.Add(new Tool("eval_cs", "Execute arbitrary C# inside the VaM process. 'sc' is bound to SuperController.singleton. Without ';' the code is treated as an expression and its value is returned; with ';' it runs as statements (use an explicit return if you want a value). VaM's runtime sandbox prohibits System.IO, System.Reflection, System.AppDomain, UnityEditor and Mono.Cecil; avoid GetType().Name (use ToString()). USE AT YOUR OWN RISK.")
					.P("code", "string", "C# expression or statements to evaluate", true)
					.Timeout(evalTimeoutMs)
					.Fn(api.EvalCs));
			}

			return tools;
		}
	}
}
