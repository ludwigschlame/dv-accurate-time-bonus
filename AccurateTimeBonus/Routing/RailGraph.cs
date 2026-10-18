using DV.OriginShift;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AccurateTimeBonus.Routing;

public enum RailGraphState
{
	Uninitialized,
	InProgress,
	Built,
	Faulty,
}

public static class RailGraph
{
	private static BuildResult? graph;
	private static Task<BuildResult>? pendingBuild;
	public static IReadOnlyList<Node> Nodes => graph?.Nodes ?? Array.Empty<Node>();
	public static RailGraphState State { get; private set; }
	public static int Generation { get; private set; }
	public static float DistanceScalingFactor { get; private set; } = 1f;

	public readonly struct Node
	{
		public int Id { get; }
		public Vector3 Position { get; }

		public Node(int id, Vector3 position)
		{
			Id = id;
			Position = position;
		}
	}

	private sealed class TrackInput
	{
		public int SegmentId;
		public Vector3[] Controls = Array.Empty<Vector3>();
	}

	private sealed class BuildResult
	{
		public Node[] Nodes = Array.Empty<Node>();
		public RailTrack[] Tracks = Array.Empty<RailTrack>();
		public Dictionary<string, (int nodeId, float distance)> StationNodes = new();
		public PathFindingCache Paths = null!;
		public float Scaling;
		public string[] Diagnostics = Array.Empty<string>();
	}

	public static void Clear()
	{
		// Detached workers own snapshots only and cannot publish into a new world.
		pendingBuild = null;
		graph = null;
		Generation++;
		DistanceScalingFactor = 1f;
		State = RailGraphState.Uninitialized;
	}

	public static void Update()
	{
		if (pendingBuild == null || !pendingBuild.IsCompleted) return;
		Task<BuildResult> completed = pendingBuild;
		pendingBuild = null;
		try
		{
			BuildResult result = completed.GetAwaiter().GetResult();
			graph = result;
			PathFinding.Initialize(result.Paths);
			DistanceScalingFactor = result.Scaling;
			State = RailGraphState.Built;
			foreach (string diagnostic in result.Diagnostics) Main.Debug(diagnostic);
			Main.Log("Finished initializing graph");
		}
		catch (Exception e)
		{
			State = RailGraphState.Faulty;
			Main.Error("BuildGraph failed", e);
		}
	}

	public static void TryBuildRailGraph()
	{
		Update();
		if (State != RailGraphState.Uninitialized) return;
		RailTrackRegistry? registry = UnityEngine.Object.FindObjectOfType<RailTrackRegistry>();
		if (registry == null || registry._logicToRailTrack == null || !registry._logicToRailTrack.Any()) return;
		if (StationController.allStations == null) return;
		List<StationController> stations = StationController.allStations.Where(station => station != null).ToList();
		if (stations.Count == 0) return;

		State = RailGraphState.InProgress;
		try
		{
			Stopwatch initializationTimer = Stopwatch.StartNew();
			// Capture Unity objects on the main thread; workers see value data only.
			RailTrack[] tracks = registry.OrderedRailtracks;
			Vector3 origin = OriginShift.currentMove;
			List<TrackInput> input = new();
			for (int trackId = 0; trackId < tracks.Length; trackId++)
			{
				BezierCurve curve = tracks[trackId].curve;
				if (curve.pointCount < 2)
				{
					Main.Warning($"Invalid railtrack: curve has {curve.pointCount} points");
					continue;
				}
				Vector3[] controls = new Vector3[(curve.pointCount - 1) * 4];
				for (int i = 0; i < curve.pointCount - 1; i++)
				{
					controls[i * 4] = curve[i].position - origin;
					controls[i * 4 + 1] = curve[i].globalHandle2 - origin;
					controls[i * 4 + 2] = curve[i + 1].globalHandle1 - origin;
					controls[i * 4 + 3] = curve[i + 1].position - origin;
				}
				input.Add(new TrackInput { SegmentId = trackId, Controls = controls });
			}
			var stationPositions = stations.ToDictionary(station => station.stationInfo.YardID,
				station => station.transform.position - origin);
			pendingBuild = Task.Run(() => Build(input, stationPositions, tracks, initializationTimer));
		}
		catch (Exception e)
		{
			State = RailGraphState.Faulty;
			Main.Error("Could not capture rail graph", e);
		}
	}

	private static BuildResult Build(List<TrackInput> tracks, Dictionary<string, Vector3> stations,
		RailTrack[] trackObjects, Stopwatch initializationTimer)
	{
		Stopwatch stageTimer = Stopwatch.StartNew();
		List<Node> nodes = new();
		List<RailCorridor> edges = new();
		Dictionary<(int, int), int> index = new();
		foreach (TrackInput track in tracks)
		{
			float length = 0f;
			for (int i = 0; i < track.Controls.Length; i += 4)
			{
				length += ApproximateBezierLength(track.Controls[i], track.Controls[i + 1],
					track.Controls[i + 2], track.Controls[i + 3]);
			}
			int start = GetOrAddNode(nodes, index, track.Controls[0]);
			int end = GetOrAddNode(nodes, index, track.Controls[track.Controls.Length - 1]);
			edges.Add(new RailCorridor(start, end, length, new[] { new TrackTraversal(track.SegmentId) }));
		}
		RailNetwork original = new(nodes.Count, edges);
		long buildTime = stageTimer.ElapsedMilliseconds;
		stageTimer.Restart();
		Dictionary<string, (int nodeId, float distance)> anchors = new();
		foreach (var station in stations)
		{
			int nearest = -1;
			float distance = float.MaxValue;
			foreach (Node node in nodes)
			{
				float candidate = Vector3.Distance(station.Value, node.Position);
				if (candidate < distance)
				{
					nearest = node.Id;
					distance = candidate;
				}
			}
			if (nearest < 0) throw new InvalidOperationException("No rail nodes available for stations");
			anchors.Add(station.Key, (nearest, distance));
		}
		RailGraphCompression.Result compressed = RailGraphCompression.Compress(original, anchors.Values.Select(anchor => anchor.nodeId));
		Node[] compressedNodes = nodes.Where(node => compressed.OriginalToCompressed[node.Id] >= 0)
			.Select(node => new Node(compressed.OriginalToCompressed[node.Id], node.Position)).ToArray();
		var stationNodes = anchors.ToDictionary(anchor => anchor.Key,
			anchor => (nodeId: compressed.OriginalToCompressed[anchor.Value.nodeId], distance: anchor.Value.distance));
		long compressionTime = stageTimer.ElapsedMilliseconds;
		stageTimer.Restart();
		PathFindingCache paths = new(compressed.Network);
		int[] targets = stationNodes.Values.Select(anchor => anchor.nodeId).Distinct().ToArray();
		for (int i = 0; i < targets.Length; i++)
		{
			paths.FindShortestDistances(targets[i], targets.Skip(i + 1).ToHashSet());
		}
		long searchTime = stageTimer.ElapsedMilliseconds;
		stageTimer.Restart();

		float graphDistance = 0f;
		float euclideanDistance = 0f;
		string[] stationIds = stations.Keys.ToArray();
		for (int i = 0; i < stationIds.Length; i++)
		{
			for (int j = i + 1; j < stationIds.Length; j++)
			{
				string from = stationIds[i];
				string to = stationIds[j];
				if (paths.FindShortestDistance(stationNodes[from].nodeId, stationNodes[to].nodeId, out float distance))
				{
					graphDistance += distance;
					euclideanDistance += Vector3.Distance(stations[from], stations[to]);
				}
			}
		}
		float scaling = graphDistance > 0f ? euclideanDistance / graphDistance : 1f;
		long scalingTime = stageTimer.ElapsedMilliseconds;
		return new BuildResult
		{
			Nodes = compressedNodes,
			Tracks = trackObjects,
			StationNodes = stationNodes,
			Paths = paths,
			Scaling = scaling,
			Diagnostics =
			[
				$"Finished building graph: {original.NodeCount:N0} nodes ({buildTime:N0} ms)",
				$"Finished compressing graph: {original.NodeCount:N0} -> {compressed.Network.NodeCount:N0} nodes, " +
					$"{original.Corridors.Count:N0} -> {compressed.Network.Corridors.Count:N0} corridors " +
					$"({compressionTime:N0} ms)",
				$"Finished precomputing distances: {stations.Count:N0} stations ({searchTime:N0} ms)",
				$"Finished distance scaling: {scaling:P0} ({scalingTime:N0} ms)",
				$"Graph initialization total: {initializationTimer.ElapsedMilliseconds:N0} ms"
			]
		};
	}

	private static int GetOrAddNode(List<Node> nodes, Dictionary<(int, int), int> index, Vector3 position)
	{
		(int x, int z) cell = ((int)position.x, (int)position.z);
		if (index.TryGetValue(cell, out int exact) && Matches(nodes[exact].Position, position)) return exact;
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dz = -1; dz <= 1; dz++)
			{
				if (dx == 0 && dz == 0) continue;
				if (index.TryGetValue((cell.x + dx, cell.z + dz), out int existing) &&
					Matches(nodes[existing].Position, position))
				{
					return existing;
				}
			}
		}
		int id = nodes.Count;
		nodes.Add(new Node(id, position));
		index[cell] = id;
		return id;
	}

	private static bool Matches(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x) <= 0.1f && Math.Abs(a.z - b.z) <= 0.1f;

	public static bool FindNearestNodeToStation(StationController station, out int nodeId, out float distance)
	{
		nodeId = -1;
		distance = 0f;
		if (graph == null || !graph.StationNodes.TryGetValue(station.stationInfo.YardID, out var anchor)) return false;
		(nodeId, distance) = anchor;
		return true;
	}

	private static float ApproximateBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{
		float length = 0f;
		Vector3 previous = p0;
		for (int i = 1; i <= 16; i++)
		{
			Vector3 current = EvaluateCubic(p0, p1, p2, p3, i / 16f);
			length += Vector3.Distance(previous, current);
			previous = current;
		}
		return length;
	}

	internal static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float u = 1f - t;
		float uu = u * u;
		float uuu = uu * u;
		float tt = t * t;
		float ttt = tt * t;
		return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
	}
}
