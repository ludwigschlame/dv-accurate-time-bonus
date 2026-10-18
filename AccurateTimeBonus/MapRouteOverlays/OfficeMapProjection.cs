using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

public static class OfficeMapProjection
{
	public static bool TryProjectToBackFace(Vector3 absolutePosition, Vector3 worldSize, Vector3 worldOffset,
		Bounds meshBounds, bool positiveNormalSide, out Vector3 position, out Vector3 normal)
	{
		Vector2 normalizedPosition = GetWorldTextureCoordinate(absolutePosition, worldSize, worldOffset);
		Vector3 size = meshBounds.size;
		if (size.x <= 0f || size.z <= 0f || size.y < 0f)
		{
			position = normal = default;
			return false;
		}
		float faceSign = positiveNormalSide ? -1f : 1f;
		position = new Vector3(
			Mathf.Lerp(meshBounds.min.x, meshBounds.max.x, normalizedPosition.x),
			meshBounds.center.y + faceSign * Mathf.Abs(size.y) * 0.5f,
			Mathf.Lerp(meshBounds.min.z, meshBounds.max.z, normalizedPosition.y));
		normal = faceSign * Vector3.up;
		return true;
	}

	public static Vector2 GetWorldTextureCoordinate(Vector3 absolutePosition, Vector3 worldSize, Vector3 worldOffset)
	{
		return new Vector2(
			Mathf.InverseLerp(0f, worldSize.x, absolutePosition.x - worldOffset.x),
			Mathf.InverseLerp(0f, worldSize.z, absolutePosition.z - worldOffset.z));
	}
}
