using System;
using System.Collections.Generic;
using System.Linq;

namespace AccurateTimeBonus.Routing;

public readonly struct TrackTraversal
{
	public int SegmentId { get; }
	public bool Reversed { get; }

	public TrackTraversal(int segmentId, bool reversed = false)
	{
		SegmentId = segmentId;
		Reversed = reversed;
	}

	public TrackTraversal Reverse() => new(SegmentId, !Reversed);
}

public sealed class RailCorridor
{
	public int FromId { get; }
	public int ToId { get; }
	public float Length { get; }
	public IReadOnlyList<TrackTraversal> Segments { get; }

	public RailCorridor(int fromId, int toId, float length, IEnumerable<TrackTraversal> segments)
	{
		if (length < 0f || float.IsNaN(length) || float.IsInfinity(length))
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}
		FromId = fromId;
		ToId = toId;
		Length = length;
		Segments = Array.AsReadOnly(segments.ToArray());
	}
}

public readonly struct CorridorTraversal
{
	public RailCorridor Corridor { get; }
	public bool Reversed { get; }
	public int FromId => Reversed ? Corridor.ToId : Corridor.FromId;
	public int ToId => Reversed ? Corridor.FromId : Corridor.ToId;

	public CorridorTraversal(RailCorridor corridor, bool reversed = false)
	{
		Corridor = corridor;
		Reversed = reversed;
	}

	public CorridorTraversal Reverse() => new(Corridor, !Reversed);

	public IEnumerable<TrackTraversal> GetSegments()
	{
		if (Reversed)
		{
			for (int i = Corridor.Segments.Count - 1; i >= 0; i--)
			{
				yield return Corridor.Segments[i].Reverse();
			}
		}
		else
		{
			foreach (TrackTraversal segment in Corridor.Segments)
			{
				yield return segment;
			}
		}
	}
}

public sealed class RailNetwork
{
	private readonly IReadOnlyList<CorridorTraversal>[] outgoingEdges;
	public int NodeCount => outgoingEdges.Length;
	public IReadOnlyList<RailCorridor> Corridors { get; }

	public RailNetwork(int nodeCount, IEnumerable<RailCorridor> corridors)
	{
		List<CorridorTraversal>[] edges = new List<CorridorTraversal>[nodeCount];
		for (int i = 0; i < nodeCount; i++)
		{
			edges[i] = new List<CorridorTraversal>();
		}
		RailCorridor[] connections = corridors.ToArray();
		foreach (RailCorridor corridor in connections)
		{
			if (corridor.FromId < 0 || corridor.FromId >= nodeCount ||
			    corridor.ToId < 0 || corridor.ToId >= nodeCount)
			{
				throw new ArgumentOutOfRangeException(nameof(corridors));
			}
			edges[corridor.FromId].Add(new CorridorTraversal(corridor));
			edges[corridor.ToId].Add(new CorridorTraversal(corridor, reversed: true));
		}
		Corridors = Array.AsReadOnly(connections);
		outgoingEdges = edges.Select(edgeList => (IReadOnlyList<CorridorTraversal>)edgeList.AsReadOnly()).ToArray();
	}

	public IReadOnlyList<CorridorTraversal> EdgesFrom(int nodeId) => outgoingEdges[nodeId];
}
