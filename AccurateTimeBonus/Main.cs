using AccurateTimeBonus.Logic;
using HarmonyLib;
using System;
using System.Reflection;
using UnityModManagerNet;

namespace AccurateTimeBonus
{
	[EnableReloading]
	public static class Main
	{
		public static UnityModManager.ModEntry ModEntry { get; private set; } = null!;
		public static Settings.ATBModSettings Settings { get; private set; } = null!;
		public static bool Enabled => ModEntry.Active;

		public static void Log(string msg) => ModEntry.Logger.Log(msg);
		public static void Warning(string msg) => ModEntry.Logger.Warning(msg);
		public static void Error(string msg) => ModEntry.Logger.Error(msg);

		public static void Error(string msg, Exception ex)
		{
			ModEntry.Logger.Error(msg);
			ModEntry.Logger.LogException(ex);
		}

		private static Harmony? _harmony;

		private static bool Load(UnityModManager.ModEntry modEntry)
		{
			ModEntry = modEntry;
			modEntry.OnUnload = Unload;

			Settings = UnityModManager.ModSettings.Load<Settings.ATBModSettings>(ModEntry);

			ModEntry.OnGUI = DrawGUI;
			ModEntry.OnSaveGUI = SaveGUI;

			RailGraph.TryBuildRailGraph();

			try
			{
				_harmony = new Harmony(modEntry.Info.Id);
				_harmony.PatchAll(Assembly.GetExecutingAssembly());
			}
			catch (Exception ex)
			{
				Error($"Failed to load {modEntry.Info.DisplayName}:", ex);
				_harmony?.UnpatchAll(modEntry.Info.Id);
				return false;
			}

			Log("AccurateTimeBonus mod loaded.");
			return true;
		}

		static bool Unload(UnityModManager.ModEntry modEntry)
		{
			RailGraph.Clear();
			PathFinding.Clear();
			_harmony?.UnpatchAll(modEntry.Info.Id);
			return true;
		}

		static void DrawGUI(UnityModManager.ModEntry entry)
		{
			Settings.Draw(entry);
		}

		static void SaveGUI(UnityModManager.ModEntry entry)
		{
			Settings.Save(entry);
		}
	}
}
