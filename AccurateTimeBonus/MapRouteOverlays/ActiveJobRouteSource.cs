using DV.Logic.Job;
using DV.Utils;
using System.Collections.Generic;

namespace AccurateTimeBonus.MapRouteOverlays;

public static class ActiveJobRouteSource
{
	public static IReadOnlyList<Job> GetActiveJobs()
	{
		List<Job> activeJobs = new();
		JobsManager? manager = SingletonBehaviour<JobsManager>.Instance;
		if (manager == null) return activeJobs;

		foreach (Job job in manager.currentJobs)
		{
			if (job != null && job.chainData != null &&
			    JobRouteEligibility.CanShowActiveRoute(job.jobType, job.State))
			{
				activeJobs.Add(job);
			}
		}
		return activeJobs;
	}
}
