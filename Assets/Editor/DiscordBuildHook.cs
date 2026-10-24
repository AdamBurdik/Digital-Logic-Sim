using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DLS.Editor
{
	public sealed class DiscordBuildHook : IPostprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPostprocessBuild(BuildReport report)
		{
			string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
			if (projectRoot == null) return;

			string sourcePath = Path.Combine(projectRoot, ".env");
			if (!File.Exists(sourcePath)) return;

			string outputPath = report.summary.outputPath;
			string dataDirectory = FindDataDirectory(outputPath);
			if (dataDirectory == null) return;

			string destinationDirectory = Path.Combine(dataDirectory, "StreamingAssets");
			Directory.CreateDirectory(destinationDirectory);
			File.Copy(sourcePath, Path.Combine(destinationDirectory, ".env"), true);
		}

		static string FindDataDirectory(string outputPath)
		{
			if (Directory.Exists(outputPath))
			{
				string dataDirectory = Directory.GetDirectories(outputPath, "*_Data").FirstOrDefault();
				if (dataDirectory != null) return dataDirectory;
			}

			string parentDirectory = Path.GetDirectoryName(outputPath);
			if (parentDirectory == null || !Directory.Exists(parentDirectory)) return null;

			return Directory.GetDirectories(parentDirectory, "*_Data").FirstOrDefault();
		}
	}
}
