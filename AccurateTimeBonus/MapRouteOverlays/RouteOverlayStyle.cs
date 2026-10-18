using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

internal readonly struct RouteOverlayStyle
{
	public const float BaseWidthToMapSizeRatio = 0.005f;

	private static readonly Color Red = new(154f / 255f, 46f / 255f, 46f / 255f, 1.0f);
	private static readonly Color Yellow = new(208f / 255f, 156f / 255f, 65f / 255f, 1.0f);

	public static readonly RouteOverlayStyle Active = new(Red, 1.0f, 0f);
	public static readonly RouteOverlayStyle Preview = new(Yellow, 0.6f, 64f);

	public Color Tint { get; }
	public float WidthScale { get; }
	public float DashLengthMapSizeDivisor { get; }
	public bool IsDashed => DashLengthMapSizeDivisor > 0f;

	private RouteOverlayStyle(Color tint, float widthScale, float dashLengthMapSizeDivisor)
	{
		Tint = tint;
		WidthScale = widthScale;
		DashLengthMapSizeDivisor = dashLengthMapSizeDivisor;
	}
}
