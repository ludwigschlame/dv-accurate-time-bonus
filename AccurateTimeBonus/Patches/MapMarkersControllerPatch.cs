using AccurateTimeBonus.MapRouteOverlays;
using HarmonyLib;

namespace AccurateTimeBonus.Patches;

[HarmonyPatch(typeof(MapMarkersController), "Awake")]
internal static class MapMarkersControllerPatch
{
	[HarmonyPostfix]
	private static void AwakePostfix(MapMarkersController __instance)
	{
		HandheldMapRouteOverlay.Attach(__instance);
	}
}
