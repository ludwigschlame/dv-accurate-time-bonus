using System.Collections;
using System.IO;
using AccurateTimeBonus.Logic;
using HarmonyLib;

namespace AccurateTimeBonus.Patches;

[HarmonyPatch(typeof(WorldStreamingInit))]
public static class WorldStreamingInitPatch
{
	static bool loadingRoutineDone = true;

	[HarmonyPatch(nameof(WorldStreamingInit.LoadingRoutine))]
	[HarmonyPostfix]
	static IEnumerator LoadingRoutinePostfix(IEnumerator __result)
	{
		while (__result.MoveNext())
		{
			yield return __result.Current;
			if (loadingRoutineDone)
			{
				loadingRoutineDone = false;
				Main.Clear();
			}
			RailGraph.TryBuildRailGraph();
		}

		loadingRoutineDone = true;
	}
}
