using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccurateTimeBonus.MapRouteOverlays;

public sealed class HandheldMapRouteOverlay : MonoBehaviour
{
	private static readonly HashSet<HandheldMapRouteOverlay> instances = new();
	private static readonly HashSet<int> attachedMapIds = new();
	private MapMarkersController map = null!;
	private RouteLineLayer? activeLayer;
	private RouteLineLayer? previewLayer;
	private JobRouteDisplaySet? displayed;
	private JobRouteDisplaySet? failed;

	public static void Attach(MapMarkersController controller)
	{
		if (controller == null || !controller.gameObject.scene.IsValid() || !controller.gameObject.scene.isLoaded) return;
		if (!attachedMapIds.Add(controller.GetInstanceID())) return;
		HandheldMapRouteOverlay? overlay = controller.GetComponent<HandheldMapRouteOverlay>();
		if (overlay == null) overlay = controller.gameObject.AddComponent<HandheldMapRouteOverlay>();
		instances.Add(overlay);
	}

	public static void AttachExisting()
	{
		RemovePreviousOverlayComponents();
		foreach (MapMarkersController controller in Resources.FindObjectsOfTypeAll<MapMarkersController>())
		{
			if (controller != null && controller.gameObject.scene.IsValid() && controller.gameObject.scene.isLoaded)
			{
				Attach(controller);
			}
		}
	}

	private static void RemovePreviousOverlayComponents()
	{
		foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
		{
			if (component == null || !component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded) continue;
			Type type = component.GetType();
			if (type.FullName == typeof(HandheldMapRouteOverlay).FullName && type != typeof(HandheldMapRouteOverlay))
			{
				Destroy(component);
			}
		}
	}

	public static void DetachAll()
	{
		foreach (HandheldMapRouteOverlay overlay in new List<HandheldMapRouteOverlay>(instances))
		{
			if (overlay == null) continue;
			overlay.enabled = false;
			Destroy(overlay);
		}
		instances.Clear();
		attachedMapIds.Clear();
	}

	private void Awake()
	{
		map = GetComponent<MapMarkersController>();
		instances.Add(this);
		attachedMapIds.Add(gameObject.GetInstanceID());
	}

	private void LateUpdate()
	{
		if (!Main.Enabled || map == null || !map.isActiveAndEnabled)
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
		if (ReferenceEquals(routeSet, displayed)) return;
		if (ReferenceEquals(routeSet, failed)) return;
		if (!EnsureLayers())
		{
			failed = routeSet;
			return;
		}

		try
		{
			List<Vector3[]> activePaths = new(routeSet.ActiveRoutes.Count);
			foreach (StationRoute route in routeSet.ActiveRoutes)
			{
				activePaths.Add(ProjectRoute(route));
			}

			List<Vector3[]> previewPaths = new(1);
			if (routeSet.PreviewRoute != null)
			{
				previewPaths.Add(ProjectRoute(routeSet.PreviewRoute));
			}

			activeLayer!.SetPaths(activePaths);
			previewLayer!.SetPaths(previewPaths);
			displayed = routeSet;
			failed = null;
		}
		catch (Exception exception)
		{
			Main.Error("Could not project job routes onto the handheld map", exception);
			Fail(routeSet);
		}
	}

	private bool EnsureLayers()
	{
		if (activeLayer != null && previewLayer != null) return true;
		float mapSize = Mathf.Min(map.mapBounds.x, map.mapBounds.y) * 2f;
		activeLayer = RouteLineLayer.Create(transform, "AccurateTimeBonus active routes", gameObject.layer,
			mapSize, RouteOverlayStyle.Active, sortingOrder: 0);
		previewLayer = RouteLineLayer.Create(transform, "AccurateTimeBonus preview route", gameObject.layer,
			mapSize, RouteOverlayStyle.Preview, sortingOrder: 1);
		if (activeLayer != null && previewLayer != null)
		{
			Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
			Vector3 position = Vector3.up * (map.staticMarkerYOffset * 0.5f);
			activeLayer.SetLocalPose(position, rotation);
			previewLayer.SetLocalPose(position, rotation);
			return true;
		}
		activeLayer?.Dispose();
		previewLayer?.Dispose();
		activeLayer = null;
		previewLayer = null;
		return false;
	}

	private Vector3[] ProjectRoute(StationRoute route)
	{
		Vector3[] positions = new Vector3[route.Points.Count];
		for (int i = 0; i < positions.Length; i++)
		{
			Vector3 projected = map.GetMapPosition(route.Points[i], false);
			positions[i] = new Vector3(projected.x, projected.z, 0f);
		}
		return positions;
	}

	private void Fail(JobRouteDisplaySet routeSet)
	{
		activeLayer?.SetPaths(Array.Empty<Vector3[]>());
		previewLayer?.SetPaths(Array.Empty<Vector3[]>());
		displayed = null;
		failed = routeSet;
	}

	private void Hide()
	{
		activeLayer?.SetPaths(Array.Empty<Vector3[]>());
		previewLayer?.SetPaths(Array.Empty<Vector3[]>());
		displayed = null;
		failed = null;
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
