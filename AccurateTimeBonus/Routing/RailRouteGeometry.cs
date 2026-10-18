using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccurateTimeBonus.Routing;

public readonly struct CubicSpan
{
	public Vector3 P0 { get; }
	public Vector3 P1 { get; }
	public Vector3 P2 { get; }
	public Vector3 P3 { get; }

	public CubicSpan(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{
		P0 = p0;
		P1 = p1;
		P2 = p2;
		P3 = p3;
	}
}

public static class RailRouteGeometry
{
	public const int SamplesPerSpan = 16;

	public static bool TryBuild(RailRoute route, Vector3 fromStation, Vector3 fromAnchor,
		Vector3 toAnchor, Vector3 toStation, Func<int, IReadOnlyList<CubicSpan>?> getCurve, out Vector3[] points)
	{
		points = Array.Empty<Vector3>();
		List<Vector3> sampled = new();
		Add(sampled, fromStation);
		Add(sampled, fromAnchor);
		foreach (TrackTraversal segment in route.GetSegments())
		{
			IReadOnlyList<CubicSpan>? curve = getCurve(segment.SegmentId);
			if (curve == null || curve.Count == 0) return false;
			for (int spanIndex = 0; spanIndex < curve.Count; spanIndex++)
			{
				CubicSpan span = curve[segment.Reversed ? curve.Count - spanIndex - 1 : spanIndex];
				for (int i = 0; i <= SamplesPerSpan; i++)
				{
					float t = (segment.Reversed ? SamplesPerSpan - i : i) / (float)SamplesPerSpan;
					Add(sampled, EvaluateCubic(span.P0, span.P1, span.P2, span.P3, t));
				}
			}
		}
		Add(sampled, toAnchor);
		Add(sampled, toStation);
		points = sampled.ToArray();
		return true;
	}

	private static void Add(List<Vector3> points, Vector3 point)
	{
		if (points.Count == 0 || (point - points[points.Count - 1]).sqrMagnitude > 0.00000001f) points.Add(point);
	}

	public static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float u = 1f - t;
		float uu = u * u;
		float uuu = uu * u;
		float tt = t * t;
		float ttt = tt * t;
		return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
	}
}
