using System;
using DLS.Graphics;
using DLS.SaveSystem;
using UnityEngine;

namespace DLS.Game
{
	public static class PlaytimeTracker
	{
		const double FlushIntervalSeconds = 30;
		const double MaxFrameDeltaSeconds = 0.5;

		static Project trackedProject;
		static double timeSinceFlush;
		static bool dirty;

		public static void OnProjectOpened(Project project)
		{
			Flush();
			trackedProject = project;
			timeSinceFlush = 0;
			dirty = false;
		}

		public static void OnProjectClosed()
		{
			Flush();
			trackedProject = null;
		}

		public static void Update()
		{
			if (trackedProject == null || Project.ActiveProject != trackedProject) return;
			if (!Application.isFocused) return;
			if (UIDrawer.ActiveMenu == UIDrawer.MenuType.MainMenu) return;

			double dt = Math.Min(Time.unscaledDeltaTime, MaxFrameDeltaSeconds);
			if (dt <= 0) return;

			trackedProject.description.PlaytimeSeconds += dt;
			Main.ActivePlayerStats.TotalPlaytimeSeconds += dt;
			timeSinceFlush += dt;
			dirty = true;

			if (timeSinceFlush >= FlushIntervalSeconds) Flush();
		}

		public static void Flush()
		{
			if (!dirty && trackedProject == null) return;

			if (dirty)
			{
				if (trackedProject != null)
				{
					Saver.SaveProjectPlaytime(trackedProject.description);
				}

				Saver.SavePlayerStats(Main.ActivePlayerStats);
				dirty = false;
			}

			timeSinceFlush = 0;
		}

		public static void Reset()
		{
			Flush();
			trackedProject = null;
			timeSinceFlush = 0;
			dirty = false;
		}
	}
}
