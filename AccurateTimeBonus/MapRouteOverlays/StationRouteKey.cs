using System;

namespace AccurateTimeBonus.MapRouteOverlays;

internal readonly struct StationRouteKey : IEquatable<StationRouteKey>, IComparable<StationRouteKey>
{
	public string FromStationId { get; }
	public string ToStationId { get; }

	private StationRouteKey(string fromStationId, string toStationId)
	{
		FromStationId = fromStationId;
		ToStationId = toStationId;
	}

	public static bool TryCreate(string? firstStationId, string? secondStationId, out StationRouteKey key)
	{
		key = default;
		if (firstStationId == null || firstStationId.Length == 0 ||
		    secondStationId == null || secondStationId.Length == 0) return false;
		string first = firstStationId;
		string second = secondStationId;
		key = string.CompareOrdinal(first, second) <= 0
			? new StationRouteKey(first, second)
			: new StationRouteKey(second, first);
		return true;
	}

	public int CompareTo(StationRouteKey other)
	{
		int firstComparison = string.CompareOrdinal(FromStationId, other.FromStationId);
		return firstComparison != 0 ? firstComparison : string.CompareOrdinal(ToStationId, other.ToStationId);
	}

	public bool Equals(StationRouteKey other)
	{
		return string.Equals(FromStationId, other.FromStationId, StringComparison.Ordinal) &&
			string.Equals(ToStationId, other.ToStationId, StringComparison.Ordinal);
	}

	public override bool Equals(object? obj) => obj is StationRouteKey other && Equals(other);

	public override int GetHashCode()
	{
		unchecked
		{
			return (StringComparer.Ordinal.GetHashCode(FromStationId ?? string.Empty) * 397) ^
				StringComparer.Ordinal.GetHashCode(ToStationId ?? string.Empty);
		}
	}
}
