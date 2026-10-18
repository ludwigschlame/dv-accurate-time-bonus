using AccurateTimeBonus.Routing;
using AccurateTimeBonus.MapRouteOverlays;
using DV.ThingTypes;
using UnityEngine;

namespace AccurateTimeBonus.Tests;

internal static class Program
{
	private static int passed;

	private static int Main()
	{
		try
		{
			Run("Linear chains collapse", () =>
			{
				var network = Network(4, (0, 1, 10), (2, 1, 20), (2, 3, 30));
				var result = CheckCompression(network);
				Check(result.Network.NodeCount == 2 && result.Network.Corridors.Count == 1);
				Check(result.Network.Corridors[0].Length == 60);
				Check(result.Network.Corridors[0].Segments[1].Reversed);
				Check(ReferenceEquals(result.Network.EdgesFrom(0)[0].Corridor, result.Network.EdgesFrom(1)[0].Corridor));
			});
			Run("Station anchors split corridors", () =>
			{
				var result = CheckCompression(Network(5, (0, 1, 2), (1, 2, 3), (2, 3, 4), (3, 4, 5)), 2);
				Check(result.Network.NodeCount == 3 && result.OriginalToCompressed[2] >= 0);
			});
			Run("Branches and dead ends survive", () =>
			{
				var result = CheckCompression(Network(6, (0, 1, 1), (1, 2, 2), (2, 3, 3), (2, 4, 4), (4, 5, 5)));
				Check(result.Network.NodeCount == 4 && result.Network.Corridors.Count == 3);
			});
			Run("Parallel corridors remain distinct", () =>
			{
				var result = CheckCompression(Network(4, (0, 1, 1), (1, 3, 1), (0, 2, 5), (2, 3, 5)), 0, 3);
				Check(result.Network.Corridors.Count == 2 && result.Network.NodeCount == 2);
				Check(result.Network.Corridors.Select(edge => edge.Length).OrderBy(length => length).SequenceEqual(new[] { 2f, 10f }));
			});
			Run("Closed components remain intact", () =>
			{
				var result = CheckCompression(Network(3, (0, 1, 1), (1, 2, 2), (2, 0, 3)));
				Check(result.Network.NodeCount == 3 && result.Network.Corridors.Count == 3);
			});
			Run("Anchored rings preserve their tracks", () =>
			{
				CheckCompression(Network(4, (0, 1, 1), (1, 2, 2), (2, 0, 3), (0, 3, 4)), 0);
				CheckCompression(Network(1, (0, 0, 3)), 0);
			});
			Run("Disconnected and isolated nodes survive", () =>
			{
				CheckCompression(Network(6, (0, 1, 1), (1, 2, 2), (3, 4, 3)));
				CheckCompression(Network(0));
				CheckCompression(Network(3));
			});
			Run("Zero lengths are supported", () => CheckCompression(Network(4, (0, 1, 0), (1, 2, 0), (2, 3, 5)), 1));
			Run("Invalid edge lengths are rejected", () =>
			{
				foreach (float length in new[] { -1f, float.NaN, float.PositiveInfinity })
				{
					try
					{
						Network(2, (0, 1, length));
						throw new Exception("Expected invalid length to be rejected");
					}
					catch (ArgumentOutOfRangeException) { }
				}
			});
			Run("Random networks match an independent distance oracle", RandomNetworks);
			Run("Long segmented lines reduce to protected stations", () =>
			{
				const int segmentCount = 10000;
				var network = Network(segmentCount + 1,
					Enumerable.Range(0, segmentCount).Select(i => (i, i + 1, 1f)).ToArray());
				var anchors = Enumerable.Range(0, 21).Select(i => i * 500).ToArray();
				var result = RailGraphCompression.Compress(network, anchors);
				Check(result.Network.NodeCount == 21 && result.Network.Corridors.Count == 20);
				Check(result.Network.Corridors.Sum(corridor => corridor.Segments.Count) == segmentCount);
				var cache = new PathFindingCache(result.Network);
				Check(cache.TryGetShortestRoute(0, 20, out var route));
				Check(route.Distance == segmentCount && route.Traversals.Count == 20);
			});
			Run("Multi-target searches do not mutate caller input", () =>
			{
				var cache = new PathFindingCache(Network(3, (0, 1, 1), (1, 2, 2)));
				HashSet<int> targets = new() { 0, 1, 2 };
				cache.FindShortestDistances(0, targets);
				Check(targets.SetEquals(new[] { 0, 1, 2 }));
				Check(cache.FindShortestDistance(0, 2, out float distance) && distance == 3);
			});
			Run("Opposite routes share corridors and one cache entry", () =>
			{
				var network = Network(3, (0, 1, 2), (1, 2, 3));
				var cache = new PathFindingCache(network);
				Check(cache.TryGetShortestRoute(2, 0, out RailRoute backward));
				Check(cache.TryGetShortestRoute(0, 2, out RailRoute forward));
				Check(cache.CachedPairCount == 1);
				Check(forward.Distance == 5 && backward.Distance == 5);
				Check(forward.GetSegments().Select(segment => segment.SegmentId).SequenceEqual(new[] { 0, 1 }));
				Check(backward.GetSegments().All(segment => segment.Reversed));
				Check(backward.GetSegments().Select(segment => segment.SegmentId).SequenceEqual(new[] { 1, 0 }));
				Check(ReferenceEquals(forward.Traversals[0].Corridor, backward.Traversals[1].Corridor));
			});
			Run("Predecessor edges select the correct parallel corridor", () =>
			{
				var network = Network(2, (0, 1, 30), (0, 1, 3));
				var cache = new PathFindingCache(network);
				Check(cache.TryGetShortestRoute(0, 1, out RailRoute route));
				Check(route.Distance == 3 && ReferenceEquals(route.Traversals[0].Corridor, network.Corridors[1]));
			});
			Run("Invalid, identical, and unreachable route endpoints", () =>
			{
				var cache = new PathFindingCache(Network(3, (0, 1, 4)));
				Check(!cache.TryGetShortestRoute(-1, 0, out _));
				Check(!cache.TryGetShortestRoute(0, 3, out _));
				Check(cache.TryGetShortestRoute(1, 1, out RailRoute same) && same.Distance == 0 && same.Traversals.Count == 0);
				Check(!cache.TryGetShortestRoute(0, 2, out _));
				Check(!cache.TryGetShortestRoute(2, 0, out _));
				Check(cache.CachedPairCount == 1);
			});
			Run("Haul documents can be previewed in any state but active routes require in-progress jobs", () =>
			{
				foreach (JobType type in Enum.GetValues(typeof(JobType)))
				{
					bool haul = type == JobType.Transport || type == JobType.EmptyHaul;
					Check(JobRouteEligibility.CanPreview(type) == haul);
					foreach (JobState state in Enum.GetValues(typeof(JobState)))
					{
						Check(JobRouteEligibility.CanShowActiveRoute(type, state) == (haul && state == JobState.InProgress));
					}
				}
			});
			Run("Station route keys ignore direction and deduplicate identical pairs", () =>
			{
				Check(StationRouteKey.TryCreate("A", "B", out StationRouteKey forward));
				Check(StationRouteKey.TryCreate("B", "A", out StationRouteKey reverse));
				Check(forward.Equals(reverse));
				Check(forward.GetHashCode() == reverse.GetHashCode());
				Check(StationRouteKey.TryCreate("A", "C", out StationRouteKey other));
				Check(!forward.Equals(other));
				Check(StationRouteKey.TryCreate("A", "A", out StationRouteKey same));
				Check(same.FromStationId == "A" && same.ToStationId == "A");
				Check(!StationRouteKey.TryCreate("", "B", out _));
			});
			Run("Active and preview layers remain distinct for the same station route", () =>
			{
				var route = new StationRoute(100, new[] { Vector3.zero, Vector3.one });
				var routeSet = new JobRouteDisplaySet(new[] { route }, route);
				Check(routeSet.ActiveRoutes.Count == 1);
				Check(ReferenceEquals(routeSet.ActiveRoutes[0], routeSet.PreviewRoute));
				Check(routeSet.HasRoutes);
				Check(!JobRouteDisplaySet.Empty.HasRoutes);
			});
			Run("Preview route style controls width and dashing independently", () =>
			{
				Check(Near(RouteOverlayStyle.Active.WidthScale, 1f));
				Check(!RouteOverlayStyle.Active.IsDashed);
				Check(RouteOverlayStyle.Preview.WidthScale > 0f &&
					RouteOverlayStyle.Preview.WidthScale < RouteOverlayStyle.Active.WidthScale);
				Check(RouteOverlayStyle.Preview.IsDashed);
				Check(Near(RouteOverlayStyle.Preview.DashLengthMapSizeDivisor, 64f));
			});
			Run("Dashed preview mesh contains separated dash ribbons", () =>
			{
				List<Vector3> vertices = new();
				List<int> triangles = new();
				RouteRibbonMeshBuilder.BuildDashed(new[] { new[] { Vector3.zero, new Vector3(10, 0, 0) } },
					0.2f, 2f, 2f, vertices, triangles);
				Check(vertices.Count == 12 && triangles.Count == 18);
				Check(Near(vertices[0].x, 0) && Near(vertices[2].x, 2) && Near(vertices[0].y, 0.1f));
				Check(Near(vertices[4].x, 4) && Near(vertices[6].x, 6));
				Check(Near(vertices[8].x, 8) && Near(vertices[10].x, 10));
			});
			Run("Solid and dashed ribbons use the same stroke width", () =>
			{
				List<Vector3> solidVertices = new();
				List<int> solidTriangles = new();
				List<Vector3> dashedVertices = new();
				List<int> dashedTriangles = new();
				Vector3[] path = { Vector3.zero, new Vector3(10, 0, 0) };
				RouteRibbonMeshBuilder.BuildSolid(new[] { path }, 0.2f, solidVertices, solidTriangles);
				RouteRibbonMeshBuilder.BuildDashed(new[] { path }, 0.2f, 2f, 2f,
					dashedVertices, dashedTriangles);
				Check(Near(solidVertices[0].y, dashedVertices[0].y));
				Check(Near(solidVertices[1].y, dashedVertices[1].y));
				Check(Near(dashedVertices[4].x, 4));
				Check(dashedVertices.Count == 12 && dashedTriangles.Count == 18);
			});
			Run("Cubic sampling retains the curve and its endpoints", () =>
			{
				CubicSpan curve = new(new Vector3(0, 0, 0), new Vector3(0, 0, 16),
					new Vector3(16, 0, 16), new Vector3(16, 0, 0));
				var route = new RailRoute(0, 1, 32, Network(2, (0, 1, 32)).EdgesFrom(0));
				Check(RailRouteGeometry.TryBuild(route, curve.P0, curve.P0, curve.P3, curve.P3, _ => new[] { curve }, out var points));
				Check(points.Length == 17);
				Check(Near(points[0], curve.P0) && Near(points[16], curve.P3));
				Check(Near(points[8], new Vector3(8, 0, 12)));
			});
			Run("Multi-span geometry reverses continuously and retains station connectors", () =>
			{
				var spans = new Dictionary<int, IReadOnlyList<CubicSpan>>
				{
					[0] = new[] { Line(0, 5), Line(5, 10) },
					[1] = new[] { Line(20, 10) }
				};
				var corridor = new RailCorridor(0, 1, 20, new[] { new TrackTraversal(0), new TrackTraversal(1, true) });
				var route = new RailRoute(0, 1, 20, new[] { new CorridorTraversal(corridor) });
				Vector3 from = new(-2, 0, 0);
				Vector3 to = new(22, 0, 0);
				Vector3 anchorA = new(0, 0, 0);
				Vector3 anchorB = new(20, 0, 0);
				Check(RailRouteGeometry.TryBuild(route, from, anchorA, anchorB, to, id => spans[id], out var forward));
				Check(RailRouteGeometry.TryBuild(route.Reverse(), to, anchorB, anchorA, from, id => spans[id], out var backward));
				Check(Near(forward[0], from) && Near(forward[forward.Length - 1], to));
				Check(forward.Length == backward.Length);
				for (int i = 0; i < forward.Length; i++)
				{
					Check(Near(forward[i], backward[backward.Length - i - 1]));
					if (i > 0) Check(forward[i].x > forward[i - 1].x, "Duplicate or out-of-order points");
				}
				float sampledLength = 0;
				for (int i = 1; i < forward.Length; i++) sampledLength += Vector3.Distance(forward[i - 1], forward[i]);
				Check(Equal(sampledLength, route.Distance + 2 + 2));
			});
			Run("Missing curves never produce a partial preview", () =>
			{
				var route = new RailRoute(0, 1, 1, Network(2, (0, 1, 1)).EdgesFrom(0));
				Check(!RailRouteGeometry.TryBuild(route, default, default, default, default, _ => null, out var points));
				Check(points.Length == 0);
				Check(!RailRouteGeometry.TryBuild(route, default, default, default, default, _ => Array.Empty<CubicSpan>(), out _));
			});
			Run("Identical anchors retain both station connectors", () =>
			{
				var route = new RailRoute(0, 0, 0, Array.Empty<CorridorTraversal>());
				Check(RailRouteGeometry.TryBuild(route, new Vector3(-1, 0, 0), default, default,
					new Vector3(1, 0, 0), _ => throw new Exception("No tracks should be read"), out var points));
				Check(points.Length == 3 && Near(points[1], default));
			});
			Run("World XZ coordinates normalize with map-world offset", () =>
			{
				Vector2 uv = OfficeMapProjection.GetWorldTextureCoordinate(
					new Vector3(150, 0, 250), new Vector3(100, 0, 200), new Vector3(100, 0, 50));
				Check(Near(uv.x, 0.5f) && Near(uv.y, 1f));
				Vector2 outside = OfficeMapProjection.GetWorldTextureCoordinate(
					new Vector3(0, 0, 500), new Vector3(100, 0, 200), new Vector3(100, 0, 50));
				Check(Near(outside.x, 0f) && Near(outside.y, 1f));
			});
			Run("Office routes project onto the back face from either camera side", () =>
			{
				Bounds bounds = new(Vector3.zero, new Vector3(2, 0.2f, 2));
				Check(OfficeMapProjection.TryProjectToBackFace(new Vector3(25, 0, 75),
					new Vector3(100, 0, 100), Vector3.zero, bounds, positiveNormalSide: true,
					out Vector3 positiveBackPoint, out Vector3 positiveBackNormal));
				Check(Near(positiveBackPoint, new Vector3(-0.5f, -0.1f, 0.5f)));
				Check(Near(positiveBackNormal, Vector3.down));
				Check(OfficeMapProjection.TryProjectToBackFace(new Vector3(25, 0, 75),
					new Vector3(100, 0, 100), Vector3.zero, bounds, positiveNormalSide: false,
					out Vector3 negativeBackPoint, out Vector3 negativeBackNormal));
				Check(Near(negativeBackPoint, new Vector3(-0.5f, 0.1f, 0.5f)));
				Check(Near(negativeBackNormal, Vector3.up));
			});
			Run("Office mesh bounds cover world edges and reject degenerate surfaces", () =>
			{
				Bounds bounds = new(Vector3.zero, new Vector3(2, 0.2f, 2));
				Check(OfficeMapProjection.TryProjectToBackFace(new Vector3(0.5f, 0, 0.5f), Vector3.one, Vector3.zero,
					bounds, positiveNormalSide: true, out Vector3 center, out _));
				Check(Near(center, new Vector3(0, -0.1f, 0)));
				Bounds degenerate = new(Vector3.zero, new Vector3(0, 1, 0));
				Check(!OfficeMapProjection.TryProjectToBackFace(Vector3.zero, Vector3.one, Vector3.zero,
					degenerate, positiveNormalSide: true, out _, out _));
			});
			Console.WriteLine($"Passed {passed} tests.");
			return 0;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception);
			return 1;
		}
	}

	private static void Run(string name, Action test)
	{
		test();
		passed++;
		Console.WriteLine($"PASS {name}");
	}

	private static void Check(bool condition, string message = "Assertion failed")
	{
		if (!condition) throw new Exception(message);
	}

	private static RailNetwork Network(int nodeCount, params (int from, int to, float length)[] edges)
	{
		return new RailNetwork(nodeCount, edges.Select((edge, id) =>
			new RailCorridor(edge.from, edge.to, edge.length, new[] { new TrackTraversal(id) })));
	}

	private static RailGraphCompression.Result CheckCompression(RailNetwork network, params int[] anchors)
	{
		var compressed = RailGraphCompression.Compress(network, anchors);
		double[,] before = Distances(network);
		double[,] after = Distances(compressed.Network);
		var cache = new PathFindingCache(compressed.Network);
		for (int i = 0; i < network.NodeCount; i++)
		{
			int a = compressed.OriginalToCompressed[i];
			if (a < 0) continue;
			for (int j = 0; j < network.NodeCount; j++)
			{
				int b = compressed.OriginalToCompressed[j];
				if (b < 0) continue;
				Check(Equal(before[i, j], after[a, b]), $"Distance changed {i}-{j}");
				bool reachable = cache.FindShortestDistance(a, b, out float distance);
				Check(reachable == !double.IsPositiveInfinity(before[i, j]));
				if (reachable) Check(Equal(before[i, j], distance), "Dijkstra disagrees with oracle");
				Check(cache.TryGetShortestRoute(a, b, out RailRoute route) == reachable);
				if (reachable)
				{
					Check(route.FromId == a && route.ToId == b);
					int current = a;
					float cost = 0f;
					foreach (CorridorTraversal step in route.Traversals)
					{
						Check(current == step.FromId, "Route is not continuous");
						current = step.ToId;
						cost += step.Corridor.Length;
					}
					Check(current == b && Equal(cost, distance), "Route cost disagrees with distance cache");
				}
			}
		}
		foreach (int anchor in anchors) Check(compressed.OriginalToCompressed[anchor] >= 0);
		int[] originalNodes = Enumerable.Range(0, network.NodeCount)
			.Where(node => compressed.OriginalToCompressed[node] >= 0).ToArray();
		List<int> segments = new();
		foreach (RailCorridor corridor in compressed.Network.Corridors)
		{
			int current = originalNodes[corridor.FromId];
			float length = 0f;
			foreach (TrackTraversal segment in corridor.Segments)
			{
				RailCorridor original = network.Corridors[segment.SegmentId];
				Check(current == (segment.Reversed ? original.ToId : original.FromId), "Disconnected segment chain");
				current = segment.Reversed ? original.FromId : original.ToId;
				length += original.Length;
				segments.Add(segment.SegmentId);
			}
			Check(current == originalNodes[corridor.ToId]);
			Check(Equal(length, corridor.Length));
		}
		Check(segments.OrderBy(segment => segment).SequenceEqual(Enumerable.Range(0, network.Corridors.Count)), "Tracks lost or duplicated");
		return compressed;
	}

	private static bool Equal(double expected, double actual)
	{
		return expected == actual || Math.Abs(expected - actual) <= Math.Max(0.001, Math.Abs(expected) * 0.00001);
	}

	private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.000001f;
	private static bool Near(float a, float b) => Math.Abs(a - b) < 0.000001f;

	private static CubicSpan Line(float from, float to)
	{
		float delta = to - from;
		return new CubicSpan(new Vector3(from, 0, 0), new Vector3(from + delta / 3, 0, 0),
			new Vector3(from + 2 * delta / 3, 0, 0), new Vector3(to, 0, 0));
	}

	private static double[,] Distances(RailNetwork network)
	{
		double[,] distances = new double[network.NodeCount, network.NodeCount];
		for (int i = 0; i < network.NodeCount; i++)
		{
			for (int j = 0; j < network.NodeCount; j++) distances[i, j] = i == j ? 0 : double.PositiveInfinity;
		}
		foreach (RailCorridor edge in network.Corridors)
		{
			distances[edge.FromId, edge.ToId] = distances[edge.ToId, edge.FromId] =
				Math.Min(distances[edge.FromId, edge.ToId], edge.Length);
		}
		for (int k = 0; k < network.NodeCount; k++)
		{
			for (int i = 0; i < network.NodeCount; i++)
			{
				for (int j = 0; j < network.NodeCount; j++)
					distances[i, j] = Math.Min(distances[i, j], distances[i, k] + distances[k, j]);
			}
		}
		return distances;
	}

	private static void RandomNetworks()
	{
		for (int seed = 0; seed < 100; seed++)
		{
			System.Random random = new(seed);
			int count = random.Next(1, 25);
			List<(int from, int to, float length)> edges = new();
			for (int i = 0; i < count * 2; i++)
			{
				edges.Add((random.Next(count), random.Next(count),
					seed % 2 == 0 ? random.Next(0, 100) : (float)(random.NextDouble() * 10000)));
			}
			CheckCompression(Network(count, edges.ToArray()), random.Next(count));
		}
	}
}
