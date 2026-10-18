using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AccurateTimeBonus.Routing;

public sealed class RailRoute
{
	private sealed class TraversalView : IReadOnlyList<CorridorTraversal>
	{
		private readonly IReadOnlyList<CorridorTraversal> steps;
		private readonly bool reversed;
		public int Count => steps.Count;
		public CorridorTraversal this[int index] => reversed ? steps[Count - index - 1].Reverse() : steps[index];

		public TraversalView(IReadOnlyList<CorridorTraversal> steps, bool reversed)
		{
			this.steps = steps;
			this.reversed = reversed;
		}

		public IEnumerator<CorridorTraversal> GetEnumerator()
		{
			for (int i = 0; i < Count; i++) yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private readonly IReadOnlyList<CorridorTraversal> steps;
	private readonly bool reversed;
	public int FromId { get; }
	public int ToId { get; }
	public float Distance { get; }
	public IReadOnlyList<CorridorTraversal> Traversals { get; }

	internal RailRoute(int fromId, int toId, float distance, IEnumerable<CorridorTraversal> steps)
	{
		FromId = fromId;
		ToId = toId;
		Distance = distance;
		this.steps = Array.AsReadOnly(steps.ToArray());
		Traversals = new TraversalView(this.steps, reversed: false);
	}

	private RailRoute(RailRoute original)
	{
		FromId = original.ToId;
		ToId = original.FromId;
		Distance = original.Distance;
		steps = original.steps;
		reversed = !original.reversed;
		Traversals = new TraversalView(steps, reversed);
	}

	public RailRoute Reverse() => new(this);

	public IEnumerable<TrackTraversal> GetSegments()
	{
		foreach (CorridorTraversal traversal in Traversals)
		{
			foreach (TrackTraversal segment in traversal.GetSegments()) yield return segment;
		}
	}
}
