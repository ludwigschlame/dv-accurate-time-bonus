using AccurateTimeBonus.Routing;
using DV.Logic.Job;
using DV.OriginShift;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

public static class JobRouteService
{
	private static readonly Dictionary<StationRouteKey, StationRoute?> routeCache = new();
	private static List<StationRouteKey> lastActiveKeys = new();
	private static StationRouteKey? lastPreviewKey;
	private static int routeCacheGeneration = -1;
	private static int displayGeneration = -1;
	private static int lastEvaluatedFrame = -1;
	private static JobRouteDisplaySet displaySet = JobRouteDisplaySet.Empty;

	public static void Clear()
	{
		routeCache.Clear();
		lastActiveKeys.Clear();
		lastPreviewKey = null;
		routeCacheGeneration = -1;
		displayGeneration = -1;
		lastEvaluatedFrame = -1;
		displaySet = JobRouteDisplaySet.Empty;
	}

	public static JobRouteDisplaySet GetDisplaySet()
	{
		if (!Main.Enabled || RailGraph.State != RailGraphState.Built)
		{
			Clear();
			return JobRouteDisplaySet.Empty;
		}

		if (lastEvaluatedFrame == Time.frameCount) return displaySet;
		lastEvaluatedFrame = Time.frameCount;
		if (routeCacheGeneration != RailGraph.Generation)
		{
			routeCache.Clear();
			lastActiveKeys.Clear();
			lastPreviewKey = null;
			routeCacheGeneration = RailGraph.Generation;
			displayGeneration = -1;
		}

		List<StationRouteKey> activeKeys = new();
		HashSet<StationRouteKey> uniqueActiveKeys = new();
		foreach (Job job in ActiveJobRouteSource.GetActiveJobs())
		{
			if (TryGetKey(job, out StationRouteKey key) && uniqueActiveKeys.Add(key)) activeKeys.Add(key);
		}

		activeKeys.Sort();
		StationRouteKey? previewKey = null;
		Job? previewJob = SelectedJobRouteSource.GetSelectedJob();
		if (previewJob != null && TryGetKey(previewJob, out StationRouteKey selectedKey)) previewKey = selectedKey;

		bool samePreviewKey = Nullable.Equals(lastPreviewKey, previewKey);
		if (displayGeneration == RailGraph.Generation &&
		    lastActiveKeys.SequenceEqual(activeKeys) && samePreviewKey) return displaySet;

		List<StationRoute> activeRoutes = new(activeKeys.Count);
		foreach (StationRouteKey key in activeKeys)
		{
			StationRoute? route = GetOrBuildRoute(key);
			if (route != null) activeRoutes.Add(route);
		}
		StationRoute? previewRoute = previewKey.HasValue ? GetOrBuildRoute(previewKey.Value) : null;

		lastActiveKeys = activeKeys;
		lastPreviewKey = previewKey;
		displayGeneration = RailGraph.Generation;
		displaySet = new JobRouteDisplaySet(activeRoutes.AsReadOnly(), previewRoute);
		return displaySet;
	}

	private static bool TryGetKey(Job job, out StationRouteKey key)
	{
		key = default;
		return job.chainData != null &&
			StationRouteKey.TryCreate(job.chainData.chainOriginYardId, job.chainData.chainDestinationYardId, out key);
	}

	private static StationRoute? GetOrBuildRoute(StationRouteKey key)
	{
		if (routeCache.TryGetValue(key, out StationRoute? cachedRoute)) return cachedRoute;
		StationRoute? routePreview = null;
		try
		{
			StationController? from = StationController.GetStationByYardID(key.FromStationId);
			StationController? to = StationController.GetStationByYardID(key.ToStationId);
			if (from == null || to == null ||
			    !RailGraph.FindNearestNodeToStation(from, out int start, out float startDistance) ||
			    !RailGraph.FindNearestNodeToStation(to, out int end, out float endDistance) ||
			    !PathFinding.TryGetShortestRoute(start, end, out RailRoute route))
			{
				routeCache.Add(key, null);
				return null;
			}

			Vector3 origin = OriginShift.currentMove;
			if (RailRouteGeometry.TryBuild(route, from.transform.position - origin, RailGraph.Nodes[start].Position,
				    RailGraph.Nodes[end].Position, to.transform.position - origin, CaptureCurve, out Vector3[] points))
			{
				routePreview = new StationRoute(route.Distance + (startDistance + endDistance), points);
			}
		}
		catch (Exception exception)
		{
			Main.Error($"Could not prepare route between {key.FromStationId} and {key.ToStationId}", exception);
		}
		routeCache.Add(key, routePreview);
		return routePreview;
	}

	private static IReadOnlyList<CubicSpan>? CaptureCurve(int segmentId)
	{
		if (!RailGraph.TryGetTrack(segmentId, out RailTrack track)) return null;
		BezierCurve curve = track.curve;
		if (curve.pointCount < 2) return null;
		Vector3 origin = OriginShift.currentMove;
		CubicSpan[] spans = new CubicSpan[curve.pointCount - 1];
		for (int i = 0; i < spans.Length; i++)
		{
			spans[i] = new CubicSpan(curve[i].position - origin, curve[i].globalHandle2 - origin,
				curve[i + 1].globalHandle1 - origin, curve[i + 1].position - origin);
		}
		return spans;
	}
}
