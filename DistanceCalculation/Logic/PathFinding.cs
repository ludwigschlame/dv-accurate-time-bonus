using System.Collections.Generic;

namespace DistanceCalculation.Logic;

public static class PathFinding
{
	// Memoization
	private static readonly Dictionary<(int, int), float> Cache = new();

	public static void Clear()
	{
		Cache.Clear();
	}

	// Simple implementation of Dijkstra's Algorithm
	// that returns the minimal distance between two nodes.
	public static bool FindShortestDistance(int startId, int targetId, out float distance)
	{
		if (Cache.TryGetValue((startId, targetId), out distance))
		{
			return true;
		}

		FindShortestDistances(startId, [targetId]);
		if (Cache.TryGetValue((startId, targetId), out distance))
		{
			return true;
		}

		Main.Error($"Could not find path between {startId} and {targetId}");
		return false;
	}

	public static void FindShortestDistances(int startId, HashSet<int> targetIds)
	{
		targetIds.Remove(startId);
		targetIds.RemoveWhere(targetId => Cache.ContainsKey((startId, targetId)));
		if (targetIds.Count == 0)
		{
			return;
		}

		int nodeCount = RailGraph.Nodes.Count;
		float[] distances = new float[nodeCount];
		bool[] visited = new bool[nodeCount];

		for (int i = 0; i < nodeCount; i++)
		{
			distances[i] = float.PositiveInfinity;
		}

		distances[startId] = 0.0f;

		int remainingTargets = targetIds.Count;
		for (int step = 0; step < nodeCount; step++)
		{
			// Find unvisited node with the smallest distance
			int currentNode = -1;
			float bestDistance = float.PositiveInfinity;

			for (int i = 0; i < nodeCount; i++)
			{
				if (!visited[i] && distances[i] < bestDistance)
				{
					bestDistance = distances[i];
					currentNode = i;
				}
			}

			// No reachable node left
			if (currentNode == -1 || float.IsPositiveInfinity(bestDistance))
			{
				break;
			}

			visited[currentNode] = true;

			// Check if a target node has been settled
			if (targetIds.Contains(currentNode))
			{
				remainingTargets--;
				// Exit early if all target nodes have been settled
				if (remainingTargets == 0)
				{
					break;
				}
			}

			// Relax outgoing edges
			foreach (RailGraph.Edge edge in RailGraph.Nodes[currentNode].OutgoingEdges)
			{
				int neighborNode = edge.ToId;
				if (visited[neighborNode])
				{
					continue;
				}

				float altDistance = distances[currentNode] + edge.Length;
				if (altDistance < distances[neighborNode])
				{
					distances[neighborNode] = altDistance;
				}
			}
		}

		// Cache all requested target distances
		foreach (int targetId in targetIds)
		{
			float distance = distances[targetId];
			if (float.IsPositiveInfinity(distance))
			{
				Main.Error($"Could not find path between {startId} and {targetId}");
				continue;
			}

			Cache[(startId, targetId)] = distance;
			Cache[(targetId, startId)] = distance;
		}
	}
}
