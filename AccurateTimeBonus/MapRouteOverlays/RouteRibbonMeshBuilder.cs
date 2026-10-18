using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

internal static class RouteRibbonMeshBuilder
{
	private const float Epsilon = 0.000001f;

	public static void BuildSolid(IReadOnlyList<Vector3[]> paths, float width, List<Vector3> vertices,
		List<int> triangles)
	{
		vertices.Clear();
		triangles.Clear();
		if (width <= 0f) return;

		List<Vector3> cleanPath = new();
		foreach (Vector3[] path in paths)
		{
			if (path == null || path.Length < 2) continue;
			cleanPath.Clear();
			foreach (Vector3 point in path) AddDistinct(cleanPath, point);
			if (cleanPath.Count >= 2) AppendRibbon(cleanPath, width, vertices, triangles);
		}
	}

	public static void BuildDashed(IReadOnlyList<Vector3[]> paths, float width, float dashLength, float gapLength,
		List<Vector3> vertices, List<int> triangles)
	{
		vertices.Clear();
		triangles.Clear();
		if (width <= 0f || dashLength <= 0f || gapLength < 0f) return;

		float period = dashLength + gapLength;
		foreach (Vector3[] path in paths)
		{
			if (path == null || path.Length < 2) continue;
			float[] cumulativeLengths = GetCumulativeLengths(path);
			float totalLength = cumulativeLengths[cumulativeLengths.Length - 1];
			if (totalLength <= Epsilon) continue;

			List<Vector3> dashPoints = new();
			for (float dashStart = 0f; dashStart < totalLength; dashStart += period)
			{
				float dashEnd = Mathf.Min(dashStart + dashLength, totalLength);
				dashPoints.Clear();
				AddDistinct(dashPoints, PointAtDistance(path, cumulativeLengths, dashStart));
				for (int i = 1; i < path.Length - 1; i++)
				{
					if (cumulativeLengths[i] > dashStart && cumulativeLengths[i] < dashEnd)
					{
						AddDistinct(dashPoints, path[i]);
					}
				}
				AddDistinct(dashPoints, PointAtDistance(path, cumulativeLengths, dashEnd));
				if (dashPoints.Count >= 2) AppendRibbon(dashPoints, width, vertices, triangles);
			}
		}
	}

	private static void AddDistinct(List<Vector3> points, Vector3 point)
	{
		if (points.Count == 0 || (point - points[points.Count - 1]).sqrMagnitude > Epsilon * Epsilon)
		{
			points.Add(point);
		}
	}

	private static float[] GetCumulativeLengths(IReadOnlyList<Vector3> points)
	{
		float[] lengths = new float[points.Count];
		for (int i = 1; i < points.Count; i++)
		{
			lengths[i] = lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
		}
		return lengths;
	}

	private static Vector3 PointAtDistance(IReadOnlyList<Vector3> points, float[] lengths, float distance)
	{
		int index = Array.BinarySearch(lengths, distance);
		if (index >= 0) return points[index];
		int next = ~index;
		if (next <= 0) return points[0];
		if (next >= points.Count) return points[points.Count - 1];
		float segmentLength = lengths[next] - lengths[next - 1];
		float t = segmentLength <= Epsilon ? 0f : (distance - lengths[next - 1]) / segmentLength;
		return Vector3.LerpUnclamped(points[next - 1], points[next], t);
	}

	private static void AppendRibbon(IReadOnlyList<Vector3> points, float width,
		List<Vector3> vertices, List<int> triangles)
	{
		float halfWidth = width * 0.5f;
		int firstVertex = vertices.Count;
		for (int i = 0; i < points.Count; i++)
		{
			Vector2 incoming = i == 0 ? default : GetDirection(points[i - 1], points[i]);
			Vector2 outgoing = i == points.Count - 1 ? default : GetDirection(points[i], points[i + 1]);
			if (incoming.sqrMagnitude <= Epsilon * Epsilon) incoming = outgoing;
			if (outgoing.sqrMagnitude <= Epsilon * Epsilon) outgoing = incoming;
			if (incoming.sqrMagnitude <= Epsilon * Epsilon) continue;

			Vector2 incomingNormal = new(-incoming.y, incoming.x);
			Vector2 outgoingNormal = new(-outgoing.y, outgoing.x);
			Vector2 miter = i == 0 || i == points.Count - 1
				? (i == 0 ? outgoingNormal : incomingNormal)
				: incomingNormal + outgoingNormal;
			if (miter.sqrMagnitude <= Epsilon * Epsilon) miter = outgoingNormal;
			miter.Normalize();
			float denominator = Mathf.Abs(Vector2.Dot(miter, outgoingNormal));
			float miterLength = halfWidth / Mathf.Max(denominator, 0.5f);
			miterLength = Mathf.Min(miterLength, halfWidth * 2f);

			Vector3 side = new(miter.x * miterLength, miter.y * miterLength, 0f);
			vertices.Add(points[i] + side);
			vertices.Add(points[i] - side);
		}

		int pointCount = vertices.Count - firstVertex;
		for (int i = 0; i + 3 < pointCount; i += 2)
		{
			int leftStart = firstVertex + i;
			int rightStart = leftStart + 1;
			int rightEnd = leftStart + 3;
			int leftEnd = leftStart + 2;
			triangles.Add(leftStart);
			triangles.Add(rightStart);
			triangles.Add(rightEnd);
			triangles.Add(leftStart);
			triangles.Add(rightEnd);
			triangles.Add(leftEnd);
		}
	}

	private static Vector2 GetDirection(Vector3 from, Vector3 to)
	{
		Vector2 direction = new(to.x - from.x, to.y - from.y);
		if (direction.sqrMagnitude > Epsilon * Epsilon) direction.Normalize();
		return direction;
	}
}
