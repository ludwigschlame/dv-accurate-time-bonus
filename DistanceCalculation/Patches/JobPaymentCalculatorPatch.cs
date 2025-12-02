using DistanceCalculation.Logic;
using DistanceCalculation.LegacyLogic;
using DV.OriginShift;
using HarmonyLib;
using System;
using System.Diagnostics;
using UnityEngine;

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
			// If there was an error during graph generation,
			// fallback to the default distance calculation.
			if (!RailGraph.built || !LegacyRailGraph.built)
			{
				return true;
			}

			Vector3 startStationPos = startStation.transform.position - OriginShift.currentMove;
			Vector3 destinationPos = destinationStation.transform.position - OriginShift.currentMove;

			int startNode = RailGraph.FindNearestNode(startStationPos);
			int destinationNode = RailGraph.FindNearestNode(destinationPos);

			if (startNode < 0 || destinationNode < 0)
			{
				Main.Warning($"Could not map stations to graph nodes (start:{startNode}, end:{destinationNode}).");
				return true;
			}

			var sw = Stopwatch.StartNew();
			float newDistance = PathFinding.FindShortestDistance(startNode, destinationNode);
			long newMs = sw.ElapsedMilliseconds;

			int legacyStart = LegacyRailGraph.FindNearestNode(startStationPos);
			int legacyDestination = LegacyRailGraph.FindNearestNode(destinationPos);

			if (legacyStart < 0 || legacyDestination < 0)
			{
				Main.Warning($"[Legacy] Could not map stations to graph nodes (start:{legacyStart}, end:{legacyDestination}).");
				return true;
			}

			sw.Restart();
			float legacyDistance = LegacyPathFinding.FindShortestDistance(legacyStart, legacyDestination);
			long legacyMs = sw.ElapsedMilliseconds;

			float originalDistance = Vector3.Distance(startStation.transform.position, destinationStation.transform.position);
			__result = newDistance;

			Main.Log($"Distance comparison | original:{originalDistance} legacy:{legacyDistance} ({legacyMs}ms) new:{newDistance} ({newMs}ms) delta:{newDistance - legacyDistance}");

			return false;
		}
	}
}
