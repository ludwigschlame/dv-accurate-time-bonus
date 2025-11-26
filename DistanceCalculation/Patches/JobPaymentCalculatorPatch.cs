using HarmonyLib;

namespace DistanceCalculation.Patches
{
	[HarmonyPatch(typeof(JobPaymentCalculator))]
	internal class JobPaymentCalculatorPatch
	{
		[HarmonyPatch(nameof(JobPaymentCalculator.GetDistanceBetweenStations))]
		[HarmonyPrefix]
		public static bool GetDistanceBetweenStationsPrefix(
			StationController startStation,
			StationController destinationStation,
			ref float __result
		)
		{
			var startStationId = startStation.stationInfo.YardID;
			var destinationStationId1 = destinationStation.stationInfo.YardID;

			Main.Log($"GetDistanceBetweenStationsPrefix called between {startStationId} and {destinationStationId1}");

			__result = 1_000f;

			return false;
		}
	}
}
