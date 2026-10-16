using Shared.Graphics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TD
{
    [DisallowMultipleComponent]
    public sealed class TDBootstrap : MonoBehaviour
    {
        public TDSettings settings;
        public SharedShapes shapes;
        public Camera prototypeCamera;
        public InputActionAsset existingInputActions;
        [Range(1, 9)] public int stage = 9;
        [SerializeField] private TDMap map;
        public TDMap Map => map;
        [SerializeField] private List<Vector2Int> route = new List<Vector2Int>();
        public IReadOnlyList<Vector2Int> Route => route;
        public bool drawRouteGizmo = true;
        public TDPlayer Player { get; private set; }
        public TDTowerPlacement Placement { get; private set; }
        public TDWorld World { get; private set; }
        public TDSpawner Spawner { get; private set; }
        public bool Initialized { get; private set; }
        public string InitializationError { get; private set; }

        private void Start()
        {
            bool initialized=InitializePrototype();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--hex-td-smoke") >= 0)
            {
                if (initialized) gameObject.AddComponent<TDBuildSmoke>();
                else Application.Quit(1);
            }
        }

        public bool InitializePrototype()
        {
            if(Initialized) return true;
            try
            {
                if (!settings || !shapes || !prototypeCamera)
                    throw new InvalidOperationException("TD: setup references are missing; run Tools/TD/Setup Prototype.");
                settings.Validate();
                if (stage >= 2) InitializeMap();
                if (stage >= 3) InitializeRoute();
                prototypeCamera.orthographic = true;
                prototypeCamera.orthographicSize = settings.cameraZoom;
                if (map) prototypeCamera.transform.position = new Vector3(map.WorldBounds.center.x, map.WorldBounds.center.y, -10);
                if (stage >= 4)
                {
                    var renderer = shapes.Create("Player", shapes.circle, Color.white,
                        map.Center(new Vector2Int(TDMap.Columns/2, TDMap.Rows/2)), .42f, 30, transform);
                    Player = renderer.gameObject.AddComponent<TDPlayer>();
                    Player.Initialize(map, settings, existingInputActions);
                    var outline = shapes.Create("Player Contrast Ring", shapes.circle, new Color(.08f,.08f,.08f),
                        Player.transform.position, .48f/.42f, 29, Player.transform);
                }
                if (stage >= 5)
                {
                    var follow = prototypeCamera.GetComponent<TDCameraFollow>();
                    if (!follow) follow = prototypeCamera.gameObject.AddComponent<TDCameraFollow>();
                    follow.Initialize(Player.transform, map, settings);
                }
                if (stage >= 6)
                {
                    var building = new GameObject("Tower Building", typeof(TDTowerPlacement));
                    building.transform.SetParent(transform, false);
                    Placement = building.GetComponent<TDTowerPlacement>();
                    Placement.Initialize(map, Player, settings, shapes);
                }
                if (stage >= 7)
                {
                    var routeCenters = new Vector3[route.Count];
                    for (int i=0;i<route.Count;i++) routeCenters[i]=map.Center(route[i]);
                    var actors = new GameObject("Enemies and Projectiles");
                    actors.transform.SetParent(transform, false);
                    World = new TDWorld(settings, shapes, actors.transform, routeCenters);
                    if(stage >= 8) Placement.EnableCombat(World);
                    Spawner = actors.AddComponent<TDSpawner>();
                    Spawner.Initialize(World);
                }
                Initialized = true;
                return true;
            }
            catch (Exception exception)
            {
                InitializationError = exception.Message;
                Debug.LogError(exception.Message, this);
                return false;
            }
        }

        public void InitializeMap()
        {
            string[] rows = TDMap.Load(settings, out string source);
            if (!map) map = GetComponentInChildren<TDMap>();
            if (!map)
            {
                var grid = new GameObject("Hex Map", typeof(Grid), typeof(TDMap));
                grid.layer = 30;
                grid.transform.SetParent(transform, false);
                map = grid.GetComponent<TDMap>();
            }
            map.Initialize(rows, source, shapes);
        }

        private void OnDestroy() { World?.Dispose(); }

        public void InitializeRoute()
        {
            route = TDRoadPathfinder.FindRoute(map.TerrainAt);
            Debug.Log($"TD: cached shortest road route ({route.Count} cells).", this);
        }

        private void OnDrawGizmos()
        {
            if (!drawRouteGizmo || !map || !map.Terrain || route == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < route.Count; i++) Gizmos.DrawLine(map.Center(route[i-1]), map.Center(route[i]));
        }
    }
}
