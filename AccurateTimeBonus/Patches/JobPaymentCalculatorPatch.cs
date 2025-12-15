using AccurateTimeBonus.Logic;
using DV.OriginShift;
using HarmonyLib;
using UnityEngine;

namespace AccurateTimeBonus.Patches;

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
		// If there was an error during graph generation,
		// fallback to the default distance calculation.
		if (RailGraph.State != RailGraphState.Built)
		{
			Main.Warning("The rail graph has not been built; falling back to default distance calculation.");
			return true;
		}

		string startYardID = startStation.stationInfo.YardID;
		string destinationYardID = destinationStation.stationInfo.YardID;

		if (!RailGraph.FindNearestNodeToStation(startStation, out int startNode))
		{
			Main.Warning($"Could not map station to graph node: {startYardID}");
			return true;
		}
		if (!RailGraph.FindNearestNodeToStation(destinationStation, out int destinationNode))
		{
			Main.Warning($"Could not map station to graph node: {destinationYardID}.");
			return true;
		}

		float originalDistance =
			Vector3.Distance(startStation.transform.position, destinationStation.transform.position);

		if (!PathFinding.FindShortestDistance(startNode, destinationNode, out float d))
		{
			return true;
		}

		__result = d * (Main.Settings.UseDistanceBalancing ? RailGraph.DistanceScalingFactor : 1.0f);
		Main.Debug($"{startYardID}-{destinationYardID}: {__result:0} m (was: {originalDistance:0} m)");
		return false;
	}
}
