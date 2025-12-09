using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityModManagerNet;

namespace DistanceCalculation.Settings
{
	public class ATBModSettings : UnityModManager.ModSettings, IDrawable
	{

		[Draw("Adjust new distance calculation to preserve time bonus and payout balancing")]
		public bool UseDistanceBalancing = true;

		[XmlIgnore]
		public Action<ATBModSettings>? OnSettingsSaved;

		public override void Save(UnityModManager.ModEntry modEntry)
		{
			Save(this, modEntry);
			OnSettingsSaved?.Invoke(this);
		}

		public void OnChange()
		{
			;
		}
	}
}
