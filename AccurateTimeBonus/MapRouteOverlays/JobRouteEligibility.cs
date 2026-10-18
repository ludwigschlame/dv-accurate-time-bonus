using DV.ThingTypes;

namespace AccurateTimeBonus.MapRouteOverlays;

public static class JobRouteEligibility
{
	public static bool CanPreview(JobType type)
	{
		return type == JobType.Transport || type == JobType.EmptyHaul;
	}

	public static bool CanShowActiveRoute(JobType type, JobState state)
	{
		return CanPreview(type) && state == JobState.InProgress;
	}
}
