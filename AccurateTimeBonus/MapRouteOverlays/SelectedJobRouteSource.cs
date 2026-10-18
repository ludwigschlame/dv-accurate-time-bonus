using DV.InventorySystem;
using DV.Logic.Job;
using DV.UI.Inventory;
using DV.Utils;
using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

public static class SelectedJobRouteSource
{
	public static Job? GetSelectedJob()
	{
		HotbarController? hotbar = SingletonBehaviour<HotbarController>.Instance;
		Inventory? inventory = SingletonBehaviour<Inventory>.Instance;
		if (hotbar == null || inventory == null || !hotbar.LoadingFinished ||
			!InventoryUtils.IsValidHotbarIndex(hotbar.SelectedSlot)) return null;

		// The open hotbar's SelectedSlot follows its pointer, before the item is equipped.
		if (hotbar.IsOpen)
		{
			GameObject? hoveredItem = inventory.PeekItemAtSlot(hotbar.SelectedSlot);
			if (hoveredItem != null) return GetEligibleJob(hoveredItem);
		}

		// Equipped items are marked dropped/reserved too; verify the actual hand item belongs to this slot.
		GameObject? item = inventory.GetEquippedItemAtSlot(0);
		if (item == null || inventory.IndexOf(item) != hotbar.SelectedSlot) return null;
		return GetEligibleJob(item);
	}

	private static Job? GetEligibleJob(GameObject item)
	{
		JobOverview? overview = item.GetComponent<JobOverview>();
		if (overview != null)
		{
			Job? job = overview.job;
			return job != null && JobRouteEligibility.CanPreview(job.jobType) ? job : null;
		}
		JobBooklet? booklet = item.GetComponent<JobBooklet>();
		Job? acceptedJob = booklet == null ? null : booklet.job;
		return acceptedJob != null && JobRouteEligibility.CanPreview(acceptedJob.jobType)
			? acceptedJob : null;
	}
}
