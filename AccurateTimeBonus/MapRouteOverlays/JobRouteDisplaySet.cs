using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

public sealed class StationRoute
{
	public float Distance { get; }
	public IReadOnlyList<Vector3> Points { get; }

	internal StationRoute(float distance, Vector3[] points)
	{
		Distance = distance;
		Points = Array.AsReadOnly(points);
	}
}

public sealed class JobRouteDisplaySet
{
	public static readonly JobRouteDisplaySet Empty = new(Array.Empty<StationRoute>(), null);

	public IReadOnlyList<StationRoute> ActiveRoutes { get; }
	public StationRoute? PreviewRoute { get; }
	public bool HasRoutes => ActiveRoutes.Count > 0 || PreviewRoute != null;

	internal JobRouteDisplaySet(IReadOnlyList<StationRoute> activeRoutes, StationRoute? previewRoute)
	{
		ActiveRoutes = activeRoutes;
		PreviewRoute = previewRoute;
	}
}
