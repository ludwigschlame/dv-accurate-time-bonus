using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

public sealed class OfficeMapRouteOverlay : MonoBehaviour
{
	private const float BackFaceOffsetWorld = 0.007f;
	private static readonly HashSet<OfficeMapRouteOverlay> instances = new();
	private static readonly HashSet<int> attachedMapIds = new();
	private MeshFilter? surface;
	private Bounds meshBounds;
	private RouteLineLayer? activeLayer;
	private RouteLineLayer? previewLayer;
	private JobRouteDisplaySet? displayed;
	private JobRouteDisplaySet? failed;
	private bool? displayedCameraSide;
	private float nextProjectionAttempt;

	public static void AttachExisting()
	{
		RemovePreviousOverlayComponents();
		Transform[] sceneTransforms = Resources.FindObjectsOfTypeAll<Transform>();
		foreach (Transform candidate in sceneTransforms)
		{
			if (candidate == null || !candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded ||
				!IsOfficeMapSurface(candidate)) continue;
			if (!attachedMapIds.Add(candidate.GetInstanceID())) continue;
			OfficeMapRouteOverlay? overlay = candidate.GetComponent<OfficeMapRouteOverlay>();
			if (overlay == null)
			{
				overlay = candidate.gameObject.AddComponent<OfficeMapRouteOverlay>();
			}
			instances.Add(overlay);
		}
	}

	private static void RemovePreviousOverlayComponents()
	{
		foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
		{
			if (component == null || !component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded) continue;
			Type type = component.GetType();
			bool previousOfficeOverlay = type.FullName == typeof(OfficeMapRouteOverlay).FullName && type != typeof(OfficeMapRouteOverlay);
			if (!previousOfficeOverlay) continue;
			Destroy(component);
		}
	}

	public static void DetachAll()
	{
		foreach (OfficeMapRouteOverlay overlay in new List<OfficeMapRouteOverlay>(instances))
		{
			if (overlay == null) continue;
			overlay.enabled = false;
			Destroy(overlay);
		}
		instances.Clear();
		attachedMapIds.Clear();
	}

	public static bool IsOfficeMapSurface(Transform candidate)
	{
		if (!string.Equals(candidate.name, "Map_LOD0", StringComparison.OrdinalIgnoreCase)) return false;
		bool mapPaper = false;
		bool officeProps = false;
		bool worldOffices = false;
		for (Transform? current = candidate.parent; current != null; current = current.parent)
		{
			mapPaper |= string.Equals(current.name, "MapPaperOffice", StringComparison.OrdinalIgnoreCase);
			officeProps |= current.name.StartsWith("PropsOffice_", StringComparison.OrdinalIgnoreCase);
			worldOffices |= string.Equals(current.name, "Offices_w3", StringComparison.OrdinalIgnoreCase);
		}
		return mapPaper && officeProps && worldOffices;
	}

	private void Awake() => instances.Add(this);

	private void LateUpdate()
	{
		if (!Main.Enabled)
		{
			Hide();
			return;
		}
		JobRouteDisplaySet routeSet = JobRouteService.GetDisplaySet();
		if (!routeSet.HasRoutes)
		{
			Hide();
			return;
		}
		if (surface == null)
		{
			if (Time.unscaledTime < nextProjectionAttempt) return;
			nextProjectionAttempt = Time.unscaledTime + 2f;
			if (!TryInitializeProjection()) return;
		}
		bool positiveNormalSide = IsCameraOnPositiveSurfaceSide();
		if (ReferenceEquals(routeSet, displayed) && displayedCameraSide == positiveNormalSide) return;
		if (ReferenceEquals(routeSet, failed) && displayedCameraSide == positiveNormalSide) return;

		StationRoute? firstRoute = routeSet.ActiveRoutes.Count > 0
			? routeSet.ActiveRoutes[0]
			: routeSet.PreviewRoute;
		if (firstRoute == null || firstRoute.Points.Count < 2) return;
		Vector3 referenceNormal = GetFacingNormal(firstRoute.Points[0], positiveNormalSide);
		if (!EnsureLayers())
		{
			Main.Warning($"Could not create route layers for office map {gameObject.name}");
			failed = routeSet;
			displayedCameraSide = positiveNormalSide;
			return;
		}
		Quaternion lineRotation = Quaternion.FromToRotation(Vector3.forward, referenceNormal);
		activeLayer!.SetLocalPose(Vector3.zero, lineRotation);
		previewLayer!.SetLocalPose(Vector3.zero, lineRotation);

		List<Vector3[]> activePaths = new(routeSet.ActiveRoutes.Count);
		foreach (StationRoute route in routeSet.ActiveRoutes)
		{
			if (!TryProjectRoute(route, positiveNormalSide, referenceNormal, activeLayer.Transform, out Vector3[] points))
			{
				Fail(routeSet, positiveNormalSide);
				return;
			}
			activePaths.Add(points);
		}

		List<Vector3[]> previewPaths = new(1);
		if (routeSet.PreviewRoute != null)
		{
			if (!TryProjectRoute(routeSet.PreviewRoute, positiveNormalSide, referenceNormal, previewLayer.Transform,
					out Vector3[] points))
			{
				Fail(routeSet, positiveNormalSide);
				return;
			}
			previewPaths.Add(points);
		}

		activeLayer.SetPaths(activePaths);
		previewLayer.SetPaths(previewPaths);
		displayed = routeSet;
		failed = null;
		displayedCameraSide = positiveNormalSide;
	}

	private bool EnsureLayers()
	{
		if (activeLayer != null && previewLayer != null) return true;
		float mapSize = GetMapSizeLocal();
		activeLayer = RouteLineLayer.Create(surface!.transform, "AccurateTimeBonus active routes",
			gameObject.layer, mapSize, RouteOverlayStyle.Active, sortingOrder: 0);
		previewLayer = RouteLineLayer.Create(surface.transform, "AccurateTimeBonus preview route",
			gameObject.layer, mapSize, RouteOverlayStyle.Preview, sortingOrder: 1);
		if (activeLayer != null && previewLayer != null) return true;
		activeLayer?.Dispose();
		previewLayer?.Dispose();
		activeLayer = null;
		previewLayer = null;
		return false;
	}

	private bool TryProjectRoute(StationRoute route, bool positiveNormalSide, Vector3 referenceNormal,
		Transform lineTransform, out Vector3[] positions)
	{
		positions = new Vector3[route.Points.Count];
		float offset = GetSurfaceOffset(referenceNormal);
		for (int i = 0; i < positions.Length; i++)
		{
			if (!OfficeMapProjection.TryProjectToBackFace(route.Points[i], LevelInfo.WorldSize, LevelInfo.WorldOffset,
					meshBounds, positiveNormalSide, out Vector3 position, out Vector3 normal))
			{
				Main.Warning("Could not project a route onto the office map");
				return false;
			}
			if (Vector3.Dot(normal, referenceNormal) < 0f) normal = -normal;
			Vector3 meshPosition = position + normal * offset;
			positions[i] = lineTransform.InverseTransformPoint(surface!.transform.TransformPoint(meshPosition));
		}
		return true;
	}

	private void Fail(JobRouteDisplaySet routeSet, bool positiveNormalSide)
	{
		activeLayer?.SetPaths(Array.Empty<Vector3[]>());
		previewLayer?.SetPaths(Array.Empty<Vector3[]>());
		failed = routeSet;
		displayed = null;
		displayedCameraSide = positiveNormalSide;
	}

	private bool TryInitializeProjection()
	{
		MeshFilter? filter = GetComponent<MeshFilter>();
		Mesh? mesh = filter == null ? null : filter.sharedMesh;
		if (mesh == null || mesh.vertexCount == 0 || mesh.bounds.size.x <= 0f || mesh.bounds.size.z <= 0f) return false;
		surface = filter;
		meshBounds = mesh.bounds;
		return true;
	}

	private bool IsCameraOnPositiveSurfaceSide()
	{
		Camera? camera = Camera.main;
		if (camera == null) return true;
		Vector3 cameraLocal = surface!.transform.InverseTransformPoint(camera.transform.position);
		return cameraLocal.y >= meshBounds.center.y;
	}

	private Vector3 GetFacingNormal(Vector3 meshLocalPoint, Vector3 meshLocalNormal)
	{
		Camera? camera = Camera.main;
		if (camera == null) return meshLocalNormal;
		Vector3 surfaceWorld = surface!.transform.TransformPoint(meshLocalPoint);
		Vector3 normalWorld = surface.transform.worldToLocalMatrix.transpose.MultiplyVector(meshLocalNormal).normalized;
		if (Vector3.Dot(normalWorld, camera.transform.position - surfaceWorld) < 0f) return -meshLocalNormal;
		return meshLocalNormal;
	}

	private Vector3 GetFacingNormal(Vector3 absolutePoint, bool positiveNormalSide)
	{
		if (OfficeMapProjection.TryProjectToBackFace(absolutePoint, LevelInfo.WorldSize, LevelInfo.WorldOffset,
				meshBounds, positiveNormalSide, out Vector3 position, out Vector3 normal))
		{
			return GetFacingNormal(position, normal);
		}
		return Vector3.forward;
	}

	private float GetSurfaceOffset(Vector3 localNormal)
	{
		float worldNormalScale = surface!.transform.TransformVector(localNormal).magnitude;
		return BackFaceOffsetWorld / Mathf.Max(worldNormalScale, 0.0001f);
	}

	private float GetMapSizeLocal()
	{
		Vector3 size = meshBounds.size;
		return Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.z));
	}

	private void Hide()
	{
		activeLayer?.SetPaths(Array.Empty<Vector3[]>());
		previewLayer?.SetPaths(Array.Empty<Vector3[]>());
		displayed = null;
		failed = null;
		displayedCameraSide = null;
	}

	private void OnDisable() => Hide();

	private void OnDestroy()
	{
		instances.Remove(this);
		attachedMapIds.Remove(gameObject.GetInstanceID());
		activeLayer?.Dispose();
		previewLayer?.Dispose();
	}
}
