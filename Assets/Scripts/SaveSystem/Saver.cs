using System;
using System.IO;
using DLS.Description;
using DLS.Game;

namespace DLS.SaveSystem
{
	public static class Saver
	{
		public static void SaveAppSettings(AppSettings settings)
		{
			string data = Serializer.SerializeAppSettings(settings);
			WriteToFile(data, SavePaths.AppSettingsPath);
		}

		public static void SavePlayerStats(PlayerStats playerStats)
		{
			string data = Serializer.SerializePlayerStats(playerStats);
			WriteToFile(data, SavePaths.PlayerStatsPath);
		}

		public static void SaveProjectDescription(ProjectDescription projectDescription)
		{
			projectDescription.LastSaveTime = DateTime.Now;
			projectDescription.DLSVersion_LastSaved = Main.DLSVersion.ToString();
			projectDescription.DLSVersion_EarliestCompatible = Main.DLSVersion_EarliestCompatible.ToString();

			string data = Serializer.SerializeProjectDescription(projectDescription);
			WriteToFile(data, SavePaths.GetProjectDescriptionPath(projectDescription.ProjectName));
		}

		public static void SaveProjectPlaytime(ProjectDescription projectDescription)
		{
			string path = SavePaths.GetProjectDescriptionPath(projectDescription.ProjectName);
			ProjectDescription descriptionToSave = projectDescription;

			if (File.Exists(path))
			{
				try
				{
					// Read the last saved description so that unsaved changes (e.g. open preferences menu) are not written out.
					descriptionToSave = Loader.LoadProjectDescription(projectDescription.ProjectName);
				}
				catch (Exception)
				{
					descriptionToSave = projectDescription;
				}
			}

			descriptionToSave.PlaytimeSeconds = projectDescription.PlaytimeSeconds;
			string data = Serializer.SerializeProjectDescription(descriptionToSave);
			WriteToFile(data, path);
		}

		public static void RenameProject(string nameOld, string nameNew)
		{
			ProjectDescription desc = Loader.LoadProjectDescription(nameOld);
			desc.ProjectName = nameNew;
			Directory.Move(SavePaths.GetProjectPath(nameOld), SavePaths.GetProjectPath(nameNew));
			SaveProjectDescription(desc);
		}

		public static void DuplicateProject(string nameOriginal, string nameDuplicate)
		{
			SaveUtils.CopyDirectory(SavePaths.GetProjectPath(nameOriginal), SavePaths.GetProjectPath(nameDuplicate), true);
			ProjectDescription descNew = Loader.LoadProjectDescription(nameDuplicate);
			descNew.ProjectName = nameDuplicate;
			descNew.PlaytimeSeconds = 0;
			SaveProjectDescription(descNew);
		}

		public static void SaveChip(ChipDescription chipDescription, string projectName)
		{
			string serializedDescription = CreateSerializedChipDescription(chipDescription);
			WriteToFile(serializedDescription, GetChipFilePath(chipDescription.Name, projectName));
		}


		public static ChipDescription CloneChipDescription(ChipDescription desc)
		{
			if (desc == null) return null;
			return Serializer.DeserializeChipDescription(Serializer.SerializeChipDescription(desc));
		}


		public static string CreateSerializedChipDescription(ChipDescription chipDescription) => Serializer.SerializeChipDescription(chipDescription);


		// Delete chip save file, with option to keep backup in a DeletedChips folder.
		public static void DeleteChip(string chipName, string projectName, bool backupInDeletedFolder = true)
		{
			string filePath = GetChipFilePath(chipName, projectName);
			if (backupInDeletedFolder)
			{
				string deletedChipDirectoryPath = SavePaths.GetDeletedChipsPath(projectName);
				string deletedFilePath = SaveUtils.EnsureUniqueFileName(Path.Combine(deletedChipDirectoryPath, chipName + ".json"));
				SavePaths.EnsureDirectoryExists(Path.GetDirectoryName(deletedFilePath));
				File.Move(filePath, deletedFilePath);
			}
			else
			{
				File.Delete(filePath);
			}
		}

		public static void DeleteProject(string projectName, bool backupInDeletedFolder = true)
		{
			string projectPath = SavePaths.GetProjectPath(projectName);

			if (backupInDeletedFolder)
			{
				SavePaths.EnsureDirectoryExists(SavePaths.DeletedProjectsPath);
				string deletedPath = Path.Combine(SavePaths.DeletedProjectsPath, projectName);
				deletedPath = SaveUtils.EnsureUniqueDirectoryName(deletedPath);
				Directory.Move(projectPath, deletedPath);
			}
			//Directory.Move
		}

		public static bool HasUnsavedChanges(ChipDescription lastSaved, ChipDescription current)
		{
			string jsonA = CreateSerializedChipDescription(lastSaved);
			string jsonB = CreateSerializedChipDescription(current);
			return !UnsavedChangeDetector.IsEquivalentJson(jsonA, jsonB);
		}

		static void WriteToFile(string data, string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			using StreamWriter writer = new(path);
			writer.Write(data);
		}

		static string GetChipFilePath(string chipName, string projectName)
		{
			string saveDirectoryPath = SavePaths.GetChipsPath(projectName);
			return Path.Combine(saveDirectoryPath, chipName + ".json");
		}
	}
}