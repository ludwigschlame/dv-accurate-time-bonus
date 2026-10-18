using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AccurateTimeBonus.MapRouteOverlays;

internal sealed class RouteLineLayer
{
	private readonly GameObject root;
	private readonly Material material;
	private readonly Mesh routeMesh;
	private readonly RouteOverlayStyle style;
	private readonly float mapSize;
	private readonly float width;
	private readonly List<Vector3> vertices = new();
	private readonly List<int> triangles = new();

	public Transform Transform => root.transform;

	private RouteLineLayer(Transform parent, string objectName, int layer, float width, float mapSize,
		RouteOverlayStyle style, int sortingOrder, Shader shader)
	{
		this.width = width;
		this.mapSize = mapSize;
		this.style = style;
		root = new GameObject(objectName)
		{
			layer = layer,
			hideFlags = HideFlags.DontSave
		};
		root.transform.SetParent(parent, worldPositionStays: false);

		material = new Material(shader)
		{
			name = objectName + " material",
			color = style.Tint,
			renderQueue = (int)RenderQueue.Transparent - 1,
			hideFlags = HideFlags.DontSave
		};
		routeMesh = new Mesh
		{
			name = objectName + " mesh",
			indexFormat = IndexFormat.UInt32,
			hideFlags = HideFlags.DontSave
		};
		routeMesh.MarkDynamic();
		MeshFilter meshFilter = root.AddComponent<MeshFilter>();
		meshFilter.sharedMesh = routeMesh;
		MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = material;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		meshRenderer.sortingOrder = sortingOrder;
	}

	public static RouteLineLayer? Create(Transform parent, string objectName, int layer, float mapSize,
		RouteOverlayStyle style, int sortingOrder)
	{
		Shader? shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
		if (shader == null)
		{
			Main.Warning($"No unlit shader available for {objectName}");
			return null;
		}
		float width = mapSize * RouteOverlayStyle.BaseWidthToMapSizeRatio * style.WidthScale;
		return new RouteLineLayer(parent, objectName, layer, width, mapSize, style, sortingOrder, shader);
	}

	public void SetLocalPose(Vector3 position, Quaternion rotation)
	{
		root.transform.localPosition = position;
		root.transform.localRotation = rotation;
	}

	public void SetPaths(IReadOnlyList<Vector3[]> paths)
	{
		if (style.IsDashed)
		{
			float dashLength = mapSize / style.DashLengthMapSizeDivisor;
			RouteRibbonMeshBuilder.BuildDashed(paths, width, dashLength, dashLength,
				vertices, triangles);
		}
		else
		{
			RouteRibbonMeshBuilder.BuildSolid(paths, width, vertices, triangles);
		}

		routeMesh.Clear();
		routeMesh.SetVertices(vertices);
		routeMesh.SetTriangles(triangles, 0);
		routeMesh.RecalculateBounds();
	}

	public void Dispose()
	{
		Object.Destroy(root);
		Object.Destroy(material);
		Object.Destroy(routeMesh);
	}
}
