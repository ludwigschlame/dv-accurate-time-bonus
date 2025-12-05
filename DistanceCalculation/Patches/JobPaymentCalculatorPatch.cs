using DistanceCalculation.Logic;
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
			if (!RailGraph.IsBuilt)
			{
				Main.Warning("The rail graph has not been built; falling back to default distance calculation.");
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

			float originalDistance = Vector3.Distance(startStation.transform.position, destinationStation.transform.position);

			float? distance = PathFinding.FindShortestDistance(startNode, destinationNode);
			switch (distance)
			{
				case null:
					return true;
				case { } d:
					__result = d;
					Main.Log($"Original distance: {originalDistance}, new distance: {__result}");
					return false;
			}
		}
	}
}
