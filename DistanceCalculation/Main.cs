using DistanceCalculation.Logic;
using DistanceCalculation.LegacyLogic;
using HarmonyLib;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace DistanceCalculation
{
	[EnableReloading]
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
			modEntry.OnUnload = Unload;
			Log("DistanceCalculation mod loaded.");
			// TODO: this is too early, the RailTrackRegistry does not exist yet,
			// we need to wait until the game is loaded I think
			RailGraph.BuildGraph();
			LegacyRailGraph.BuildGraph();

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

		static bool Unload(UnityModManager.ModEntry modEntry)
		{
			RailGraph.Clear();
			LegacyRailGraph.Clear();
			return true;
		}
	}
}
