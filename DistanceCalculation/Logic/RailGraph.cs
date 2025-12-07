using DV.OriginShift;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DistanceCalculation.Logic;

public enum RailGraphState
{
	Uninitialized,
	InProgress,
	Built,
	Faulty,
};

public static class RailGraph
{

	public struct Node
	{
		public int Id;
		public Vector3 Position;
		public List<Edge> OutgoingEdges;
	}

	public struct Edge
	{
		public int FromId;
		public int ToId;
		public float Length;
	}

	private record QuantizedPosition
	{
		public int X;
		public int Z;
	}

	private static readonly List<Node> nodes = [];
	public static IReadOnlyList<Node> Nodes => nodes;
	public static RailGraphState State;

	// The new distance between station calculation can only yield larger distances than before.
	// A multiplier is introduced (config option) that tries to keep the current balancing.
	public static float DistanceScalingFactor = 1.0f;


	private static QuantizedPosition Quantize(Vector3 position)
	{
		return new QuantizedPosition
		{
			X = (int)position.x,
			Z = (int)position.z
		};
	}

	private static QuantizedPosition Shift(QuantizedPosition position, int dx, int dz)
	{
		return new QuantizedPosition
		{
			X = position.X + dx,
			Z = position.Z + dz
		};
	}

	public static void TryBuildRailGraph()
	{
		if (State != RailGraphState.Uninitialized)
		{
			return;
		}

		RailTrackRegistry? registry = UnityEngine.Object.FindObjectOfType<RailTrackRegistry>();
		if (registry == null || registry._logicToRailTrack == null || !registry._logicToRailTrack.Any())
		{
			return;
		}

		State = RailGraphState.InProgress;
		Task.Run(() => BuildGraph(registry));
	}

	private static void BuildGraph(RailTrackRegistry registry)
	{
		State = RailGraphState.InProgress;

		try
		{
			Build(registry.OrderedRailtracks);
		}
		catch (Exception ex)
		{
			Main.Error("This is bad", ex);
			State = RailGraphState.Faulty;
			return;
		}

		State = RailGraphState.Built;

		if (GetDistanceScaling(out float factor))
		{
			DistanceScalingFactor = factor;
		}
		else
		{
			State = RailGraphState.Faulty;
		}
	}

	// Calculate the distances between all pairs of stations after graph building,
	// so that the job generation can solely work with distance lookups from the cache.
	private static bool GetDistanceScaling(out float distanceScalingFactor)
	{
		distanceScalingFactor = 1.0f;
		float totalDistances = 0.0f;
		float legacyTotalDistances = 0.0f;
		foreach (StationController startController in StationController.allStations)
		{
			// Due to the memoization, it does not matter that we calculate the distance
			// between each pair of stations twice.
			foreach (StationController destinationController in StationController.allStations)
			{
				if (startController.stationInfo.YardID == destinationController.stationInfo.YardID)
				{
					continue;
				}
				Vector3 startStationPos = startController.transform.position - OriginShift.currentMove;
				Vector3 destinationPos = destinationController.transform.position - OriginShift.currentMove;
				int startNode = FindNearestNode(startStationPos);
				int destinationNode = FindNearestNode(destinationPos);
				float? distance = PathFinding.FindShortestDistance(startNode, destinationNode);
				switch (distance)
				{
					case null:
					{
						Main.Error($"Could not calculate graph distance between {startController.stationInfo.YardID} and {destinationController.stationInfo.YardID}");
						return false;
					}
					case { } d:
					{
						totalDistances += d;
						legacyTotalDistances += Vector3.Distance(startController.transform.position, destinationController.transform.position);
						break;
					}
				}
			}
		}
		if (totalDistances == 0.0f)
		{
			Main.Error("Cumulated distances of all stations resulted in 0.0f");
			return false;
		}
		distanceScalingFactor = legacyTotalDistances / totalDistances;
		Main.Log($"The distance reduction factor is {distanceScalingFactor}");
		return true;
	}

	public static void Clear()
	{
		nodes.Clear();
		State = RailGraphState.Uninitialized;
	}

	private static void Build(RailTrack[] railTracks)
	{
		nodes.Clear();
		nodes.Capacity = railTracks.Length * 2; // Set capacity to upper limit

		// Map from position to node id to aggregate
		// similar endpoints of edges into the same node.
		Dictionary<QuantizedPosition, int> nodeIndex = new();

		foreach (RailTrack railTrack in railTracks)
		{
			BezierCurve curve = railTrack.curve;
			int pointCount = curve.pointCount;

			if (pointCount < 2)
			{
				Main.Warning($"Invalid railtrack: curve should consist of at least two points, but has {pointCount}");
				continue;
			}

			float length = 0f;
			for (int i = 0; i < pointCount - 1; i++)
			{
				BezierPoint pA = curve[i];
				BezierPoint pB = curve[i + 1];

				Vector3 p0 = pA.position - OriginShift.currentMove;
				Vector3 p1 = pA.globalHandle2 - OriginShift.currentMove;
				Vector3 p2 = pB.globalHandle1 - OriginShift.currentMove;
				Vector3 p3 = pB.position - OriginShift.currentMove;

				length += ApproximateBezierLength(p0, p1, p2, p3);
			}
			int startId = GetOrAddNode(nodeIndex, curve[0].position - OriginShift.currentMove);
			int endId = GetOrAddNode(nodeIndex, curve.Last().position - OriginShift.currentMove);

			// Add both forward and backward edge for
			// simpler path finding implementation.
			AddEdge(startId, endId, length);
			AddEdge(endId, startId, length);
		}
	}

	private static int GetOrAddNode(Dictionary<QuantizedPosition, int> nodeIndex, Vector3 position)
	{
		QuantizedPosition quantizedPosition = Quantize(position);
		if (TryGetNode(nodeIndex, position, quantizedPosition, out int id))
		{
			return id;
		}

		id = nodes.Count;
		nodes.Add(new Node
		{
			Id = id,
			Position = position,
			OutgoingEdges = new(3)
		});
		nodeIndex[quantizedPosition] = id;
		return id;
	}

	private static bool TryGetNode(Dictionary<QuantizedPosition, int> nodeIndex, Vector3 position, QuantizedPosition quantizedPosition, out int id)
	{
		const float mergeEpsilon = 0.1f;
		const int quantizedEpsilon = 1;
		IEnumerable<int> range = Enumerable.Range(-quantizedEpsilon, 2 * quantizedEpsilon + 1);

		// Check exact cell
		if (nodeIndex.TryGetValue(quantizedPosition, out id)
		    && Math.Abs(nodes[id].Position.x - position.x) <= mergeEpsilon
		    && Math.Abs(nodes[id].Position.z - position.z) <= mergeEpsilon)
		{
			return true;
		}

		// Check neighbors
		foreach (int dx in range)
		{
			foreach (int dz in range)
			{
				// Already checked
				if (dx == 0 && dz == 0) continue;

				QuantizedPosition shifted = Shift(quantizedPosition, dx, dz);
				if (nodeIndex.TryGetValue(shifted, out id)
				    && Math.Abs(nodes[id].Position.x - position.x) <= mergeEpsilon
				    && Math.Abs(nodes[id].Position.z - position.z) <= mergeEpsilon)
				{
					return true;
				}
			}
		}

		return false;
	}

	static void AddEdge(int fromId, int toId, float length)
	{
		var edge = new Edge
		{
			FromId = fromId,
			ToId = toId,
			Length = length,
		};
		var node = nodes[fromId];
		node.OutgoingEdges.Add(edge);
	}

	public static int FindNearestNode(Vector3 position)
	{
		int bestId = -1;
		float bestDist = float.MaxValue;

		for (int i = 0; i < nodes.Count; i++)
		{
			float dist = Vector3.Distance(nodes[i].Position, position);
			if (dist < bestDist)
			{
				bestDist = dist;
				bestId = nodes[i].Id;
			}
		}

		return bestId;
	}

	static float ApproximateBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{
		const int samples = 16;

		float length = 0f;
		Vector3 prev = EvaluateCubic(p0, p1, p2, p3, 0f);

		for (int i = 1; i <= samples; i++)
		{
			float t = (float)i / samples;
			Vector3 curr = EvaluateCubic(p0, p1, p2, p3, t);
			length += Vector3.Distance(prev, curr);
			prev = curr;
		}

		return length;
	}

	static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float u = 1f - t;
		float uu = u * u;
		float uuu = uu * u;
		float tt = t * t;
		float ttt = tt * t;

		// B(t) = (1 - t)^3 p0 + 3(1 - t)^2 t p1 + 3(1 - t) t^2 p2 + t^3 p3
		return uuu * p0 +
		       3f * uu * t * p1 +
		       3f * u * tt * p2 +
		       ttt * p3;
	}

}
