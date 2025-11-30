using System;
using DistanceCalculation.Logic;
using DV.OriginShift;
using HarmonyLib;
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
			if (!RailGraph.built)
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

			__result = PathFinding.FindShortestDistance(startNode, destinationNode);

			float originalDistance = Vector3.Distance(startStation.transform.position, destinationStation.transform.position);
			Main.Log($"Before {originalDistance.ToString()}");
			Main.Log($"New Calc {__result.ToString()}");

			return false;
		}
	}
}
