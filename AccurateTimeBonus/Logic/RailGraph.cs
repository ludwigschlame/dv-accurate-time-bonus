using DV.OriginShift;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AccurateTimeBonus.Logic;

public enum RailGraphState
{
	Uninitialized,
	InProgress,
	Built,
	Faulty,
};

public static class RailGraph
{
	private static readonly List<Node> nodes = [];

	// Cache nearest node to a YardId
	private static readonly Dictionary<String, int> NearestNodeToYardIdCache = new();
	public static IReadOnlyList<Node> Nodes => nodes;
	public static RailGraphState State;

	// The new distance between station calculation can only yield larger distances than before.
	// A multiplier is introduced (config option) that tries to keep the current balancing.
	public static float DistanceScalingFactor = 1.0f;

	public struct Node
	{
		public int Id;
		public Vector3 Position;
		public List<Edge> OutgoingEdges;
	}

	public struct Edge
	{
		public int ToId;
		public float Length;
	}

	private record QuantizedPosition
	{
		private int x;
		private int z;

		public static QuantizedPosition Quantize(Vector3 position)
		{
			return new QuantizedPosition
			{
				x = (int)position.x,
				z = (int)position.z
			};
		}

		public QuantizedPosition Shift(int dx, int dz)
		{
			return new QuantizedPosition
			{
				x = x + dx,
				z = z + dz
			};
		}
	}

	public static void Clear()
	{
		nodes.Clear();
		State = RailGraphState.Uninitialized;
	}

	public static void TryBuildRailGraph()
	{
		if (State != RailGraphState.Uninitialized)
		{
			return;
		}

		RailTrackRegistry? trackRegistry = UnityEngine.Object.FindObjectOfType<RailTrackRegistry>();
		if (trackRegistry == null || trackRegistry._logicToRailTrack == null || !trackRegistry._logicToRailTrack.Any())
		{
			return;
		}

		if (StationController.allStations == null || !StationController.allStations.Any())
		{
			return;
		}

		State = RailGraphState.InProgress;
		Task.Run(() =>
		{
			try
			{
				BuildGraph(trackRegistry);
			}
			catch (Exception e)
			{
				Main.Error("BuildGraph failed", e);
				State = RailGraphState.Faulty;
			}
		});
	}

	private static void BuildGraph(RailTrackRegistry registry)
	{
		State = RailGraphState.InProgress;
		Build(registry.OrderedRailtracks);
		State = RailGraphState.Built;

		if (!PrecomputeDistances())
		{
			throw new Exception("Precompute failed");
		}

		if (!GetDistanceScaling(out DistanceScalingFactor))
		{
			throw new Exception("Scaling failed");
		}

		Main.Log("Finished initializing graph");
	}

	private static bool PrecomputeDistances()
	{
		Stopwatch now = Stopwatch.StartNew();
		List<StationController> stationList = StationController.allStations;
		int stationCount = stationList.Count;

		// Get nearest nearest node for all stations
		int[] stationNode = new int[stationCount];
		for (int i = 0; i < stationCount; i++)
		{
			if (FindNearestNodeToStation(stationList[i], out int nodeId))
			{
				stationNode[i] = nodeId;
			}
		}

		for (int i = 0; i < stationCount - 1; i++)
		{
			int startNode = stationNode[i];
			HashSet<int> targetNodes = stationNode.Skip(i + 1).ToHashSet();
			PathFinding.FindShortestDistances(startNode, targetNodes);
		}

		Main.Debug($"Finished precomputing distances: {stationCount:N0} stations ({now.ElapsedMilliseconds:n0} ms)");
		return true;
	}

	// Calculate the distances between all pairs of stations after graph building,
	// so that the job generation can solely work with distance lookups from the cache.
	private static bool GetDistanceScaling(out float distanceScalingFactor)
	{
		Stopwatch now = Stopwatch.StartNew();
		distanceScalingFactor = 1.0f;
		float totalDistanceGraph = 0.0f;
		float totalDistanceEuclid = 0.0f;

		for (int i = 0; i < StationController.allStations.Count; i++)
		{
			StationController startStation = StationController.allStations[i];
			for (int j = i + 1; j < StationController.allStations.Count; j++)
			{
				StationController destinationStation = StationController.allStations[j];
				Vector3 startPosition = startStation.transform.position - OriginShift.currentMove;
				Vector3 destinationPosition = destinationStation.transform.position - OriginShift.currentMove;
				if (!FindNearestNodeToStation(startStation, out int nodeA) ||
				    !FindNearestNodeToStation(destinationStation, out int nodeB))
				{
					return false;
				}

				if (PathFinding.FindShortestDistance(nodeA, nodeB, out float distance))
				{
					totalDistanceGraph += distance;
					totalDistanceEuclid += Vector3.Distance(startPosition, destinationPosition);
				}
				else
				{
					return false;
				}
			}
		}

		distanceScalingFactor = totalDistanceEuclid / totalDistanceGraph;
		Main.Debug($"Finished distance scaling: {distanceScalingFactor:P0} ({now.ElapsedMilliseconds:n0} ms)");
		return true;
	}

	private static void Build(RailTrack[] railTracks)
	{
		Stopwatch now = Stopwatch.StartNew();

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

		Main.Debug($"Finished building graph: {nodes.Count:N0} nodes ({now.ElapsedMilliseconds:n0} ms)");
	}

	private static int GetOrAddNode(Dictionary<QuantizedPosition, int> nodeIndex, Vector3 position)
	{
		QuantizedPosition quantizedPosition = QuantizedPosition.Quantize(position);
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

	private static bool TryGetNode(Dictionary<QuantizedPosition, int> nodeIndex, Vector3 position,
		QuantizedPosition quantizedPosition, out int id)
	{
		const float mergeEpsilon = 0.1f;
		const int quantizedEpsilon = 1;
		int[] range = Enumerable.Range(-quantizedEpsilon, 2 * quantizedEpsilon + 1).ToArray();

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

				QuantizedPosition shifted = quantizedPosition.Shift(dx, dz);
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
		Edge edge = new Edge
		{
			ToId = toId,
			Length = length,
		};
		Node node = nodes[fromId];
		node.OutgoingEdges.Add(edge);
	}

	public static bool FindNearestNodeToStation(StationController stationController, out int nodeId)
	{
		if (NearestNodeToYardIdCache.TryGetValue(stationController.stationInfo.YardID, out nodeId))
		{
			return true;
		}

		Vector3 position = stationController.transform.position - OriginShift.currentMove;
		if (FindNearestNode(position, out nodeId))
		{
			NearestNodeToYardIdCache[stationController.stationInfo.YardID] = nodeId;
			return true;
		}

		return false;
	}

	private static bool FindNearestNode(Vector3 position, out int nodeId)
	{
		nodeId = -1;
		float bestDist = float.MaxValue;

		for (int i = 0; i < nodes.Count; i++)
		{
			float dist = Vector3.Distance(nodes[i].Position, position);
			if (dist < bestDist)
			{
				bestDist = dist;
				nodeId = nodes[i].Id;
			}
		}

		return nodeId != -1;
	}

	private static float ApproximateBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
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

	private static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
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
