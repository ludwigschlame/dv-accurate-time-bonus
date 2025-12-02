using System.Collections.Generic;

namespace DistanceCalculation.LegacyLogic
{
	public static class LegacyPathFinding
	{
		// Simple implementation of Dijkstra's Algorithm
		// that returns the minimal distance between two nodes.
		public static float FindShortestDistance(int startId, int targetId)
		{
			int n = LegacyRailGraph.Nodes.Count;
			var dist = new float[n];
			var prev = new int[n];
			var visited = new bool[n];

			for (int i = 0; i < n; i++)
			{
				dist[i] = float.PositiveInfinity;
				prev[i] = -1;
			}

			dist[startId] = 0f;

			for (int step = 0; step < n; step++)
			{
				int u = -1;
				float bestDist = float.PositiveInfinity;

				// pick nearest unvisited node
				for (int i = 0; i < n; i++)
				{
					if (visited[i]) continue;
					if (dist[i] < bestDist)
					{
						bestDist = dist[i];
						u = i;
					}
				}

				if (u == -1 || bestDist == float.PositiveInfinity) break; // no more reachable nodes

				visited[u] = true;
				if (u == targetId) break; // we reached the target

				// relax all edges starting at u
				foreach (var edge in LegacyRailGraph.Edges)
				{
					if (edge.FromId != u) continue;

					int v = edge.ToId;
					if (visited[v]) continue;

					float alt = dist[u] + edge.Length;
					if (alt < dist[v])
					{
						dist[v] = alt;
						prev[v] = u;
					}
				}
			}

			if (prev[targetId] == -1 && startId != targetId)
			{
				Main.Error($"Could not find path between {startId} and {targetId}");
				return 0.0f;
			}

			return dist[targetId];
		}
	}
}
