using System;
using System.Collections.Generic;
using System.Linq;

namespace AccurateTimeBonus.Routing;

public sealed class PathFindingCache
{
	private readonly RailNetwork network;
	private readonly Dictionary<(int, int), RailRoute?> routes = new();
	public int CachedPairCount => routes.Count;

	public PathFindingCache(RailNetwork network)
	{
		this.network = network;
	}

	public bool FindShortestDistance(int startId, int targetId, out float distance)
	{
		if (!ValidNode(startId) || !ValidNode(targetId))
		{
			distance = 0f;
			return false;
		}
		if (startId == targetId)
		{
			distance = 0f;
			return true;
		}
		var key = Pair(startId, targetId);
		if (!routes.TryGetValue(key, out RailRoute? route))
		{
			FindShortestDistances(startId, new HashSet<int> { targetId });
			routes.TryGetValue(key, out route);
		}
		distance = route?.Distance ?? 0f;
		return route != null;
	}

	public bool TryGetShortestRoute(int startId, int targetId, out RailRoute route)
	{
		route = null!;
		if (!FindShortestDistance(startId, targetId, out float distance)) return false;
		if (startId == targetId)
		{
			route = new RailRoute(startId, targetId, distance, Array.Empty<CorridorTraversal>());
			return true;
		}
		RailRoute cached = routes[Pair(startId, targetId)]!;
		route = cached.FromId == startId ? cached : cached.Reverse();
		return true;
	}

	public void FindShortestDistances(int startId, HashSet<int> targetIds)
	{
		if (!ValidNode(startId)) return;
		HashSet<int> targets = new(targetIds.Where(target =>
			ValidNode(target) && target != startId && !routes.ContainsKey(Pair(startId, target))));
		if (targets.Count == 0) return;

		int nodeCount = network.NodeCount;
		float[] best = Enumerable.Repeat(float.PositiveInfinity, nodeCount).ToArray();
		bool[] visited = new bool[nodeCount];
		CorridorTraversal?[] previous = new CorridorTraversal?[nodeCount];
		best[startId] = 0f;
		int remaining = targets.Count;
		for (int step = 0; step < nodeCount; step++)
		{
			int current = -1;
			float distance = float.PositiveInfinity;
			for (int i = 0; i < nodeCount; i++)
			{
				if (!visited[i] && best[i] < distance)
				{
					distance = best[i];
					current = i;
				}
			}
			if (current == -1) break;
			visited[current] = true;
			if (targets.Contains(current) && --remaining == 0) break;
			foreach (CorridorTraversal edge in network.EdgesFrom(current))
			{
				if (visited[edge.ToId]) continue;
				float candidate = distance + edge.Corridor.Length;
				if (candidate < best[edge.ToId])
				{
					best[edge.ToId] = candidate;
					previous[edge.ToId] = edge;
				}
			}
		}
		foreach (int target in targets)
		{
			var key = Pair(startId, target);
			if (float.IsPositiveInfinity(best[target]))
			{
				routes[key] = null;
				continue;
			}
			List<CorridorTraversal> steps = new();
			for (int node = target; node != startId;)
			{
				CorridorTraversal edge = previous[node]!.Value;
				steps.Add(edge);
				node = edge.FromId;
			}
			steps.Reverse();
			RailRoute route = new(startId, target, best[target], steps);
			routes[key] = startId < target ? route : route.Reverse();
		}
	}

	private bool ValidNode(int nodeId) => nodeId >= 0 && nodeId < network.NodeCount;
	private static (int, int) Pair(int a, int b) => a < b ? (a, b) : (b, a);
}
