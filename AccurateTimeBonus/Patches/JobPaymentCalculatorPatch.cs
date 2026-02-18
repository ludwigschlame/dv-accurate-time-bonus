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

		if (!RailGraph.FindNearestNodeToStation(startStation, out int startNode, out float startNodeDistance))
		{
			Main.Warning($"Could not map station to graph node: {startYardID}");
			return true;
		}
		if (!RailGraph.FindNearestNodeToStation(destinationStation, out int destinationNode, out float destinationNodeDistance))
		{
			Main.Warning($"Could not map station to graph node: {destinationYardID}.");
			return true;
		}

		if (!PathFinding.FindShortestDistance(startNode, destinationNode, out float accurateDistance))
		{
			Main.Warning($"Could not find path between {startYardID} and {destinationYardID}");
			return true;
		}

		// Add distances from stations to their nearest node.
		accurateDistance += startNodeDistance + destinationNodeDistance;

		float originalDistance =
			Vector3.Distance(startStation.transform.position, destinationStation.transform.position);

		float balancedDistance = accurateDistance * RailGraph.DistanceScalingFactor;
		__result = Main.Settings.UseDistanceBalancing ? balancedDistance : accurateDistance;
		Main.Debug($"{startYardID}-{destinationYardID}: {accurateDistance:0} m (was: {originalDistance:0} m)");
		return false;
	}
}
