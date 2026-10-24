using System;

namespace DLS.Game
{
	public static class PlaytimeFormat
	{
		public static string Compact(double seconds)
		{
			if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;

			long totalSeconds = (long)Math.Floor(seconds);

			if (totalSeconds < 60) return totalSeconds + "s";

			long totalMinutes = totalSeconds / 60;
			if (totalMinutes < 60) return totalMinutes + "m";

			long totalHours = totalMinutes / 60;
			if (totalHours < 24) return $"{totalHours}h {totalMinutes % 60:00}m";

			long totalDays = totalHours / 24;
			return $"{totalDays}d {totalHours % 24:00}h";
		}
	}
}
