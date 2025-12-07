using System.Collections;
using DistanceCalculation.Logic;
using HarmonyLib;

namespace DistanceCalculation.Patches;

[HarmonyPatch(typeof(WorldStreamingInit))]
public static class WorldStreamingInitPatch
{
	[HarmonyPatch(nameof(WorldStreamingInit.LoadingRoutine))]
	[HarmonyPostfix]
	static IEnumerator LoadingRoutinePostfix(IEnumerator __result)
	{
		while (__result.MoveNext())
		{
			yield return __result.Current;
			RailGraph.TryBuildRailGraph();
		}
	}
}
