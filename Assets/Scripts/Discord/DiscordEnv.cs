using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DLS.Discord
{
	public static class DiscordEnv
	{
		const string ApplicationIdKey = "DISCORD_APP_ID";
		const string LargeImageKeyKey = "DISCORD_LARGE_IMAGE_KEY";

		public static string ApplicationId { get; private set; } = string.Empty;
		public static string LargeImageKey { get; private set; } = string.Empty;
		public static bool IsConfigured => IsValidApplicationId(ApplicationId);

		static bool loaded;

		public static void Load()
		{
			if (loaded) return;
			loaded = true;

			Dictionary<string, string> values = ReadEnvFile();
			ApplicationId = ReadValue(ApplicationIdKey, values);
			LargeImageKey = ReadValue(LargeImageKeyKey, values);
		}

		public static void Reset()
		{
			loaded = false;
			ApplicationId = string.Empty;
			LargeImageKey = string.Empty;
		}

		static string ReadValue(string key, Dictionary<string, string> values)
		{
			string environmentValue = Environment.GetEnvironmentVariable(key);
			if (!string.IsNullOrWhiteSpace(environmentValue)) return environmentValue.Trim();

			return values.TryGetValue(key, out string value) ? value : string.Empty;
		}

		static Dictionary<string, string> ReadEnvFile()
		{
			Dictionary<string, string> values = new(StringComparer.Ordinal);
			string path = GetEnvPath();

			try
			{
				if (string.IsNullOrEmpty(path) || !File.Exists(path)) return values;

				foreach (string rawLine in File.ReadAllLines(path))
				{
					string line = rawLine.Trim().TrimStart('\uFEFF');
					if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
					if (line.StartsWith("export ", StringComparison.Ordinal)) line = line.Substring(7).Trim();

					int equalsIndex = line.IndexOf('=');
					if (equalsIndex <= 0) continue;

					string key = line.Substring(0, equalsIndex).Trim();
					string value = line.Substring(equalsIndex + 1).Trim();
					if (key.Length == 0) continue;

					if (value.Length >= 2)
					{
						char first = value[0];
						char last = value[value.Length - 1];
						if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
						{
							value = value.Substring(1, value.Length - 2);
						}
					}

					values[key] = value;
				}
			}
			catch (Exception)
			{
			}

			return values;
		}

		static string GetEnvPath()
		{
#if UNITY_EDITOR
			return Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".env");
#else
			string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, ".env");
			if (File.Exists(streamingAssetsPath)) return streamingAssetsPath;

			return Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".env");
#endif
		}

		static bool IsValidApplicationId(string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return false;
			if (id.Length < 17 || id.Length > 20) return false;

			foreach (char c in id)
			{
				if (!char.IsDigit(c)) return false;
			}

			return true;
		}
	}
}
