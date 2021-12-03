﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EnvDTE;
using Newtonsoft.Json;

namespace Codist.AutoBuildVersion
{
	/// <summary>
	/// Contains settings to automate assembly versioning after successful build.
	/// </summary>
	public sealed class BuildSetting : Dictionary<string, BuildConfigSetting>
	{
		public BuildSetting() : base(StringComparer.OrdinalIgnoreCase) {
		}

		public static string GetConfigPath(Project project) {
			return Path.Combine(Path.GetDirectoryName(project.FullName), "obj", project.Name + ".autoversion.json");
		}
		public static BuildSetting Load(Project project) {
			return Load(GetConfigPath(project));
		}
		public static BuildSetting Load(string configPath) {
			try {
				return File.Exists(configPath)
					? JsonConvert.DeserializeObject<BuildSetting>(File.ReadAllText(configPath), new JsonSerializerSettings {
						DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
						NullValueHandling = NullValueHandling.Ignore,
						Error = (sender, args) => {
							args.ErrorContext.Handled = true; // ignore json error
						}
					})
					: null;
			}
			catch (Exception ex) {
				Debug.Write("Error loading " + nameof(BuildSetting) + " from " + configPath);
				Debug.WriteLine(ex.ToString());
				return null;
			}
		}
	}
}
