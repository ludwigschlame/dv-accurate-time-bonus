using DV.OriginShift;
using System.Collections.Generic;
using UnityEngine;

namespace DistanceCalculation.Logic
{
	public static class RailGraph
	{
		public class Node
		{
			public int Id;
			public Vector3 Position;
		}

		public class Edge
		{
			public int FromId;
			public int ToId;
			public float Length;
			public string TrackName;
		}

		private static readonly List<Node> nodes = new List<Node>();
		private static readonly List<Edge> edges = new List<Edge>();
		public static bool built;

		public static IReadOnlyList<Node> Nodes => nodes;
		public static IReadOnlyList<Edge> Edges => edges;

		public static void BuildGraph()
		{
			if (built) return;

			RailTrackRegistry? registry = UnityEngine.Object.FindObjectOfType<RailTrackRegistry>();
			if (registry == null)
			{
				Main.Error("Could not find RailTrackRegistry");
				return;
			}

			Build(registry.OrderedRailtracks);
			built = true;
		}

		public static void Clear()
		{
			nodes.Clear();
			edges.Clear();
			built = false;
		}

		private static void Build(IEnumerable<RailTrack> railTracks)
		{
			nodes.Clear();
			edges.Clear();

			// Map from position to node id to aggregate
			// similar endpoints of edges into the same node.
			var nodeIndex = new Dictionary<Vector3, int>();

			foreach (RailTrack railTrack in railTracks)
			{
				var curve = railTrack.curve;
				int pointCount = curve.pointCount;

				if (pointCount < 2) {
					Main.Warning($"Invalid railtrack: curve should consist of at least two points, but was {pointCount}");
					continue;
				}
				

				for (int i = 0; i < pointCount - 1; i++)
				{
					BezierPoint pA = curve[i];
					BezierPoint pB = curve[i + 1];

					Vector3 p0 = pA.position - OriginShift.currentMove;
					Vector3 p1 = pA.globalHandle2 - OriginShift.currentMove;
					Vector3 p2 = pB.globalHandle1 - OriginShift.currentMove;
					Vector3 p3 = pB.position - OriginShift.currentMove;

					float length = ApproximateBezierLength(p0, p1, p2, p3);

					int startId = GetOrAddNode(nodeIndex, p0);
					int endId = GetOrAddNode(nodeIndex, p3);

					// Add both forward and backward edge for
					// simpler path finding implementation.
					edges.Add(new Edge
					{
						FromId = startId,
						ToId = endId,
						Length = length,
						TrackName = railTrack.name
					});
					edges.Add(new Edge
					{
						FromId = endId,
						ToId = startId,
						Length = length,
						TrackName = railTrack.name
					});
				}
			}
		}

		static int GetOrAddNode(Dictionary<Vector3, int> nodeIndex, Vector3 position)
		{
			// TODO: currently a lot of paths are not found, I suspect that the
			// nodeIndex is not working as inteded. As a first step go through
			// entries after generation and check if there are nodes that
			// are really close by
			if (nodeIndex.TryGetValue(position, out int id))
			{
				return id;
			}

			id = nodes.Count;
			nodes.Add(new Node
			{
				Id = id,
				Position = position
			});
			nodeIndex[position] = id;
			return id;
		}

		public static int FindNearestNode(Vector3 position)
		{
			int bestId = -1;
			float bestDist = float.MaxValue;

			for (int i = 0; i < nodes.Count; i++)
			{
				// Since the vector distances are only used for comparison with
				// other distances, SqrMagnitude can be used instad of
				// Vector3.Distance which avoids the expensive sqrt operation.
				float dist = Vector3.SqrMagnitude(nodes[i].Position - position);
				if (dist < bestDist)
				{
					bestDist = dist;
					bestId = nodes[i].Id;
				}
			}

			return bestId;
		}

		static float ApproximateBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
		{
			const int samples = 16;

			float length = 0f;
			Vector3 prev = EvaluateCubic(p0, p1, p2, p3, 0f);

			for (int i = 1; i <= samples; i++)
			{
				float t = (float)i / samples;
				Vector3 curr = EvaluateCubic(p0, p1, p2, p3, t);
				length += Vector3.Distance(prev, curr);
				prev = curr;
			}

			return length;
		}

		static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
		{
			float u = 1f - t;
			float uu = u * u;
			float uuu = uu * u;
			float tt = t * t;
			float ttt = tt * t;

			// B(t) = (1 - t)^3 p0 + 3(1 - t)^2 t p1 + 3(1 - t) t^2 p2 + t^3 p3
			return uuu * p0 +
				   3f * uu * t * p1 +
				   3f * u * tt * p2 +
				   ttt * p3;
		}
	}
}


