using AccurateTimeBonus.Routing;

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
				Check(result.Network.Corridors.Select(edge => edge.Length).Order().SequenceEqual(new[] { 2f, 10f }));
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
			Run("Multi-target searches do not mutate caller input", () =>
			{
				var cache = new PathFindingCache(Network(3, (0, 1, 1), (1, 2, 2)));
				HashSet<int> targets = new() { 0, 1, 2 };
				cache.FindShortestDistances(0, targets);
				Check(targets.SetEquals(new[] { 0, 1, 2 }));
				Check(cache.FindShortestDistance(0, 2, out float distance) && distance == 3);
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
		Check(segments.Order().SequenceEqual(Enumerable.Range(0, network.Corridors.Count)), "Tracks lost or duplicated");
		return compressed;
	}

	private static bool Equal(double expected, double actual)
	{
		return expected == actual || Math.Abs(expected - actual) <= Math.Max(0.001, Math.Abs(expected) * 0.00001);
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
			Random random = new(seed);
			int count = random.Next(1, 25);
			List<(int from, int to, float length)> edges = new();
			for (int i = 0; i < count * 2; i++)
			{
				edges.Add((random.Next(count), random.Next(count), random.Next(0, 100)));
			}
			CheckCompression(Network(count, edges.ToArray()), random.Next(count));
		}
	}
}
