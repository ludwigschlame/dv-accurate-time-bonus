using System.Collections;
using AccurateTimeBonus.Logic;
using HarmonyLib;

namespace AccurateTimeBonus.Patches;

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
