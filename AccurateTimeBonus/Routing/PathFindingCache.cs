using System.Collections.Generic;
using System.Linq;

namespace AccurateTimeBonus.Routing;

public sealed class PathFindingCache
{
	private readonly RailNetwork network;
	private readonly Dictionary<(int, int), float> distances = new();

	public PathFindingCache(RailNetwork network)
	{
		this.network = network;
	}

	public bool FindShortestDistance(int startId, int targetId, out float distance)
	{
		if (startId < 0 || startId >= network.NodeCount || targetId < 0 || targetId >= network.NodeCount)
		{
			distance = 0f;
			return false;
		}
		if (startId == targetId)
		{
			distance = 0f;
			return true;
		}
		if (!distances.TryGetValue((startId, targetId), out distance))
		{
			FindShortestDistances(startId, new HashSet<int> { targetId });
		}
		return distances.TryGetValue((startId, targetId), out distance);
	}

	public void FindShortestDistances(int startId, HashSet<int> targetIds)
	{
		HashSet<int> targets = new(targetIds.Where(target => target != startId && !distances.ContainsKey((startId, target))));
		if (targets.Count == 0) return;

		int nodeCount = network.NodeCount;
		float[] best = Enumerable.Repeat(float.PositiveInfinity, nodeCount).ToArray();
		bool[] visited = new bool[nodeCount];
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
				if (candidate < best[edge.ToId]) best[edge.ToId] = candidate;
			}
		}
		foreach (int target in targets)
		{
			if (float.IsPositiveInfinity(best[target])) continue;
			distances[(startId, target)] = best[target];
			distances[(target, startId)] = best[target];
		}
	}
}
