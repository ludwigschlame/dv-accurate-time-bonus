using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using DV.Utils;
using DV.UI;
using DistanceCalculation.Logic;

namespace DistanceCalculation.Patches
{
	[HarmonyPatch(typeof(WorldStreamingInit))]
	public static class Patch_WorldStreamingInit_LoadingRoutine
	{
		[HarmonyPatch(nameof(WorldStreamingInit.LoadingRoutine))]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> TranspileLoadingRoutine(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			foreach (var instruction in instructions)
			{
				yield return instruction;
				if (!RailGraph.built)
				{
					RailTrackRegistry? registry = UnityEngine.Object.FindObjectOfType<RailTrackRegistry>();
					if (registry != null)
					{
						RailGraph.BuildGraph();
					}
				}
			}
			
		}
	}
}
