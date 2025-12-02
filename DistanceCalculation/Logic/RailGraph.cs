using DV.OriginShift;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace DistanceCalculation.Logic
{
	public static class RailGraph
	{
		public struct Node
		{
			public int Id;
			public Vector3 Position;
			public Edge[] OutgoingEdges;
			public int OutgoingEdgesLen;
		}

		public struct Edge
		{
			public int FromId;
			public int ToId;
			public float Length;
		}

		private static List<Node> nodes = new List<Node>();
		public static bool built;

		public static IReadOnlyList<Node> Nodes => nodes;

		public static void BuildGraph()
		{
			if (built) return;

			Stopwatch sw = Stopwatch.StartNew();

			RailTrackRegistry? registry = UnityEngine.Object.FindObjectOfType<RailTrackRegistry>();
			if (registry == null)
			{
				Main.Error("Could not find RailTrackRegistry");
				return;
			}

			Build(registry.OrderedRailtracks);
			Main.Log($"RailGraph.BuildGraph took {sw.ElapsedMilliseconds} ms");
			built = true;
		}

		public static void Clear()
		{
			nodes.Clear();
			built = false;
		}

		private static void Build(IEnumerable<RailTrack> railTracks)
		{
			nodes.Clear();
			nodes.Capacity = railTracks.Count();
			//nodes = new List<Node>(railTracks.Count());

			// Map from position to node id to aggregate
			// similar endpoints of edges into the same node.
			var nodeIndex = new Dictionary<(int, int), int>();

			Main.Log($"Track Count {railTracks.Count().ToString()}");
			int robert = 0;
			foreach (RailTrack railTrack in railTracks)
			{
				var curve = railTrack.curve;
				int pointCount = curve.pointCount;

				if (pointCount < 2)
				{
					Main.Warning($"Invalid railtrack: curve should consist of at least two points, but was {pointCount}");
					continue;
				}


				robert += pointCount;
				float length = 0f;
				for (int i = 0; i < pointCount - 1; i++)
				{
					BezierPoint pA = curve[i];
					BezierPoint pB = curve[i + 1];

					Vector3 p0 = pA.position - OriginShift.currentMove;
					Vector3 p1 = pA.globalHandle2 - OriginShift.currentMove;
					Vector3 p2 = pB.globalHandle1 - OriginShift.currentMove;
					Vector3 p3 = pB.position - OriginShift.currentMove;

					length += ApproximateBezierLength(p0, p1, p2, p3);
				}
				int startId = GetOrAddNode(nodeIndex, curve[0].position - OriginShift.currentMove);
				int endId = GetOrAddNode(nodeIndex, curve.Last().position - OriginShift.currentMove);


				// Add both forward and backward edge for
				// simpler path finding implementation.
				AddEdge(startId, endId, length);
				AddEdge(endId, startId, length);

			}
			Main.Log($"Point Count {robert.ToString()}");
		}

		static int GetOrAddNode(Dictionary<(int, int), int> nodeIndex, Vector3 position)
		{
			if (nodeIndex.TryGetValue(((int)position.x, (int)position.z), out int id))
			{
				return id;
			}

			//const float mergeEpsilon = 0.1f;

			//for (int i = 0; i < nodes.Count; i++)
			//{
			//	if (Math.Abs(nodes[i].Position.x - position.x) <= mergeEpsilon && Math.Abs(nodes[i].Position.z - position.z) <= mergeEpsilon)
			//	{
			//		Main.Log("´Fallback hit");
			//		id = nodes[i].Id;
			//		nodeIndex[((int)position.x, (int)position.z)] = id;
			//		return id;
			//	}
			//}

			id = nodes.Count;
			nodes.Add(new Node
			{
				Id = id,
				Position = position,
				OutgoingEdges = new Edge[3]
			});
			nodeIndex[((int)position.x, (int)position.z)] = id;
			return id;
		}

		static void AddEdge(int fromId, int toId, float length)
		{
			var edge = new Edge
			{
				FromId = fromId,
				ToId = toId,
				Length = length
			};
			nodes[fromId].OutgoingEdges[nodes[fromId].OutgoingEdgesLen] = edge;
			nodes[fromId].OutgoingEdgesLen = nodes[fromId].OutgoingEdgesLen + 1;
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
				float dist = Vector3.Distance(nodes[i].Position, position);
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


