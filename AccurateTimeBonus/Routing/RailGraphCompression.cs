using System;
using System.Collections.Generic;
using System.Linq;

namespace AccurateTimeBonus.Routing;

public static class RailGraphCompression
{
	public sealed class Result
	{
		public RailNetwork Network { get; }
		public IReadOnlyList<int> OriginalToCompressed { get; }

		internal Result(RailNetwork network, int[] mapping)
		{
			Network = network;
			OriginalToCompressed = Array.AsReadOnly(mapping);
		}
	}

	public static Result Compress(RailNetwork original, IEnumerable<int> stationAnchors)
	{
		bool[] keep = new bool[original.NodeCount];
		for (int i = 0; i < keep.Length; i++)
		{
			keep[i] = original.EdgesFrom(i).Count != 2;
		}
		foreach (int anchor in stationAnchors)
		{
			keep[anchor] = true;
		}

		// A component with no terminals is a closed ring: retain it unchanged.
		bool[] seen = new bool[original.NodeCount];
		for (int i = 0; i < seen.Length; i++)
		{
			if (seen[i]) continue;
			List<int> component = new();
			Stack<int> pending = new();
			pending.Push(i);
			seen[i] = true;
			while (pending.Count > 0)
			{
				int node = pending.Pop();
				component.Add(node);
				foreach (CorridorTraversal edge in original.EdgesFrom(node))
				{
					if (seen[edge.ToId]) continue;
					seen[edge.ToId] = true;
					pending.Push(edge.ToId);
				}
			}
			if (!component.Any(node => keep[node]))
			{
				foreach (int node in component) keep[node] = true;
			}
		}

		int[] mapping = Enumerable.Repeat(-1, original.NodeCount).ToArray();
		int nodeCount = 0;
		for (int i = 0; i < keep.Length; i++)
		{
			if (keep[i]) mapping[i] = nodeCount++;
		}

		HashSet<RailCorridor> visited = new();
		List<RailCorridor> corridors = new();
		for (int start = 0; start < keep.Length; start++)
		{
			if (!keep[start]) continue;
			foreach (CorridorTraversal first in original.EdgesFrom(start))
			{
				if (visited.Contains(first.Corridor)) continue;
				CorridorTraversal current = first;
				List<TrackTraversal> segments = new();
				float length = 0f;
				while (true)
				{
					visited.Add(current.Corridor);
					length += current.Corridor.Length;
					segments.AddRange(current.GetSegments());
					int end = current.ToId;
					if (keep[end])
					{
						corridors.Add(new RailCorridor(mapping[start], mapping[end], length, segments));
						break;
					}
					IReadOnlyList<CorridorTraversal> next = original.EdgesFrom(end);
					current = ReferenceEquals(next[0].Corridor, current.Corridor) ? next[1] : next[0];
				}
			}
		}
		return new Result(new RailNetwork(nodeCount, corridors), mapping);
	}
}
