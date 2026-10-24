using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DLS.Simulation
{
	public static class SimProfiler
	{
		public static bool Enabled;

		public static long ChipsVisited;
		public static long ChipsProcessed;
		public static long ChipsSkipped;
		public static long BuiltinsProcessed;
		public static long PinPropagations;
		public static long InputsReceived;
		public static long DirtyChips;

		public static double LastTickMs;
		public static double LastOrderPassMs;
		public static double LastNormalPassMs;

		static readonly Stopwatch tickWatch = new();
		static readonly Stopwatch passWatch = new();
		static readonly Stopwatch logWatch = Stopwatch.StartNew();

		static double windowTickMs;
		static double windowOrderMs;
		static double windowNormalMs;
		static long windowTicks;

		public static void BeginTick()
		{
			if (!Enabled) return;

			ChipsVisited = 0;
			ChipsProcessed = 0;
			ChipsSkipped = 0;
			BuiltinsProcessed = 0;
			PinPropagations = 0;
			InputsReceived = 0;
			DirtyChips = 0;
			LastOrderPassMs = 0;
			LastNormalPassMs = 0;
			tickWatch.Restart();
		}

		public static void BeginOrderPass()
		{
			if (!Enabled) return;
			passWatch.Restart();
		}

		public static void EndOrderPass()
		{
			if (!Enabled) return;
			LastOrderPassMs = passWatch.Elapsed.TotalMilliseconds;
			windowOrderMs += LastOrderPassMs;
		}

		public static void BeginNormalPass()
		{
			if (!Enabled) return;
			passWatch.Restart();
		}

		public static void EndNormalPass()
		{
			if (!Enabled) return;
			LastNormalPassMs = passWatch.Elapsed.TotalMilliseconds;
			windowNormalMs += LastNormalPassMs;
		}

		public static void EndTick()
		{
			if (!Enabled) return;

			LastTickMs = tickWatch.Elapsed.TotalMilliseconds;
			windowTickMs += LastTickMs;
			windowTicks++;

			if (logWatch.Elapsed.TotalSeconds < 1) return;

			double seconds = logWatch.Elapsed.TotalSeconds;
			double avgTick = windowTickMs / windowTicks;
			double avgOrder = windowOrderMs / windowTicks;
			double avgNormal = windowNormalMs / windowTicks;
			double stepsPerSec = windowTicks / seconds;

			Debug.Log(
				$"[SimProfiler] steps/s {stepsPerSec:0.0} | tick {avgTick:0.00}ms (last {LastTickMs:0.00}) " +
				$"order {avgOrder:0.00}ms normal {avgNormal:0.00}ms | " +
				$"visited {ChipsVisited} processed {ChipsProcessed} skipped {ChipsSkipped} " +
				$"builtins {BuiltinsProcessed} pins {PinPropagations} inputs {InputsReceived} dirty {DirtyChips}");

			logWatch.Restart();
			windowTickMs = 0;
			windowOrderMs = 0;
			windowNormalMs = 0;
			windowTicks = 0;
		}
	}
}
