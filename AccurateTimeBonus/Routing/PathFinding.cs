using System.Collections.Generic;

namespace AccurateTimeBonus.Routing;

public static class PathFinding
{
	private static PathFindingCache? cache;

	internal static void Initialize(PathFindingCache paths) => cache = paths;
	public static void Clear() => cache = null;

	public static bool FindShortestDistance(int startId, int targetId, out float distance)
	{
		distance = 0f;
		return cache != null && cache.FindShortestDistance(startId, targetId, out distance);
	}

	public static void FindShortestDistances(int startId, HashSet<int> targetIds)
	{
		cache?.FindShortestDistances(startId, targetIds);
	}

	public static bool TryGetShortestRoute(int startId, int targetId, out RailRoute route)
	{
		route = null!;
		return cache != null && cache.TryGetShortestRoute(startId, targetId, out route);
	}
}
