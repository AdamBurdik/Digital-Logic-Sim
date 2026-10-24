using System;
using DLS.Game;
using DLS.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DLS.Discord
{
	public static class DiscordPresence
	{
		const int MaxDetailsLength = 128;
		const int MaxStateLength = 128;

		static DiscordIpcClient client;
		static bool initialized;
		static bool lastEnabled;
		static string lastDetails;
		static string lastState;
		static long launchUnixTime;

		public static bool IsConfigured
		{
			get
			{
				DiscordEnv.Load();
				return DiscordEnv.IsConfigured;
			}
		}

		public static void Initialize()
		{
			if (initialized) return;
			initialized = true;

			DiscordEnv.Load();
			launchUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)Time.realtimeSinceStartup;
			lastEnabled = false;
			lastDetails = null;
			lastState = null;

			if (!DiscordEnv.IsConfigured) return;

			client = new DiscordIpcClient(DiscordEnv.ApplicationId);
			client.Start();
		}

		public static void Update()
		{
			if (!initialized) return;

			bool enabled = DiscordEnv.IsConfigured && !Main.ActiveAppSettings.DiscordRichPresenceDisabled;

			if (!enabled)
			{
				if (lastEnabled)
				{
					client?.ClearActivity();
					lastEnabled = false;
					lastDetails = null;
					lastState = null;
				}

				return;
			}

			if (client == null) return;

			bool inProject = Main.ActiveProject != null && UIDrawer.ActiveMenu != UIDrawer.MenuType.MainMenu;

			string details = inProject
				? "Project: " + Main.ActiveProject.description.ProjectName
				: "Main menu";

			string state = inProject ? "Editing circuits" : "Browsing projects";

			details = Truncate(details, MaxDetailsLength);
			state = Truncate(state, MaxStateLength);

			if (lastEnabled && details == lastDetails && state == lastState) return;

			JObject activity = new()
			{
				["details"] = details,
				["state"] = state,
				["timestamps"] = new JObject
				{
					["start"] = launchUnixTime
				},
				["instance"] = true
			};

			if (!string.IsNullOrEmpty(DiscordEnv.LargeImageKey))
			{
				activity["assets"] = new JObject
				{
					["large_image"] = DiscordEnv.LargeImageKey,
					["large_text"] = "Digital Logic Sim"
				};
			}

			client.SetActivity(activity.ToString(Formatting.None));
			lastEnabled = true;
			lastDetails = details;
			lastState = state;
		}

		public static void Shutdown()
		{
			if (!initialized) return;

			client?.Dispose();
			client = null;
			initialized = false;
			lastEnabled = false;
			lastDetails = null;
			lastState = null;
		}

		public static void Reset()
		{
			Shutdown();
			DiscordEnv.Reset();
		}

		static string Truncate(string value, int maxLength)
		{
			if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
			return value.Substring(0, maxLength);
		}
	}
}
