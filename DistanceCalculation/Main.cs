using HarmonyLib;
using System;
using System.Reflection;
using UnityModManagerNet;

namespace DistanceCalculation
{
	public static class Main
	{
		public static UnityModManager.ModEntry ModEntry { get; private set; } = null!;
		public static bool Enabled => ModEntry.Active;

		public static void Log(string msg) => ModEntry.Logger.Log(msg);
		public static void Warning(string msg) => ModEntry.Logger.Warning(msg);
		public static void Error(string msg) => ModEntry.Logger.Error(msg);
		public static void Error(string msg, Exception ex)
		{
			ModEntry.Logger.LogException(ex);
			ModEntry.Logger.Error(msg);
		}

		private static bool Load(UnityModManager.ModEntry modEntry)
		{
			ModEntry = modEntry;

			Log("DistanceCalculation mod loaded.");

			Harmony? harmony = null;
			try
			{
				harmony = new Harmony(modEntry.Info.Id);
				harmony.PatchAll(Assembly.GetExecutingAssembly());
			}
			catch (Exception ex)
			{
				Error($"Failed to load {modEntry.Info.DisplayName}:", ex);
				harmony?.UnpatchAll(modEntry.Info.Id);
				return false;
			}

			return true;
		}
	}
}
