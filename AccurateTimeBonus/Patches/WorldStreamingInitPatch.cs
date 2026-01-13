using System.Collections;
using AccurateTimeBonus.Logic;
using HarmonyLib;

namespace AccurateTimeBonus.Patches;

[HarmonyPatch(typeof(WorldStreamingInit))]
public static class WorldStreamingInitPatch
{
	/// Initialization guard ensuring internal state is reinitialized each time the loading routine is run
	static bool needsReinitialization = true;

	[HarmonyPatch(nameof(WorldStreamingInit.LoadingRoutine))]
	[HarmonyPostfix]
	static IEnumerator LoadingRoutinePostfix(IEnumerator __result)
	{
		while (__result.MoveNext())
		{
			yield return __result.Current;

			if (needsReinitialization)
			{
				needsReinitialization = false;
				Main.Clear();
			}
			RailGraph.TryBuildRailGraph();
		}

		needsReinitialization = true;
	}
}
