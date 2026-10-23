using Shared.Graphics;
using Shared.HUD;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TD
{
    // Orchestrates the staged initialization of all Tower Defense subsystems including the map, pathfinding, player, and combat.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
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
        public bool Transitioning { get; private set; }

        private void Update()
        {
            if (!Initialized || Transitioning) return;
            var session = GameSession.Instance;
            if (!session || session.HasEnded || session.PhaseComplete) return;
            session.StepPhase(Time.deltaTime);
            if (!session.TowerDefenseExpired || session.HasEnded) return;
            Transitioning = true;
            // Retreating enemies never count as escapes. Towers are reconstructed from the session next visit.
            World?.Retreat();
            if (session.FinishTowerDefense())
                SceneManager.LoadSceneAsync(Cave.CaveExploration.ScenePath, LoadSceneMode.Single);
        }

        // Starts prototype initialization and adds the build smoke test runner if requested via command-line flags.
        private void Start()
        {
            bool initialized=InitializePrototype();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--hex-td-smoke") >= 0)
            {
                if (initialized) gameObject.AddComponent<TDBuildSmoke>();
                else Application.Quit(1);
            }
        }

        // Executes sequential startup stages to instantiate the map, route, player, camera follower, tower placement, and spawner.
        public bool InitializePrototype()
        {
            if(Initialized) return true;
            try
            {
                if (!settings || !shapes || !prototypeCamera)
                    throw new InvalidOperationException("TD: setup references are missing; run Tools/TD/Setup Prototype.");
                settings.Validate();
                SharedHud.EnsureExists();
                if (!GameSession.Instance) throw new InvalidOperationException("TD: shared HUD session is missing.");
                if (!Application.CanStreamedLevelBeLoaded(Cave.CaveExploration.ScenePath))
                    throw new InvalidOperationException("TD: Cave scene must be enabled in Build Settings.");
                GameSession.Instance.BeginPhase(GameSession.PlayPhase.TowerDefense);
                if (stage >= 2) InitializeMap();
                if (stage >= 3) InitializeRoute();
                prototypeCamera.orthographic = true;
                prototypeCamera.orthographicSize = settings.cameraZoom;
                if (map) prototypeCamera.transform.position = new Vector3(map.WorldBounds.center.x, map.WorldBounds.center.y, -10);
                if (stage >= 4)
                {
                    var renderer = shapes.CreateActor("Player", shapes.player,
                        map.Center(new Vector2Int(TDMap.Columns/2, TDMap.Rows/2)), .42f, 30, transform);
                    Player = renderer.gameObject.AddComponent<TDPlayer>();
                    Player.Initialize(map, settings, existingInputActions);
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
                    Placement.RestoreTowers();
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

        // Loads terrain data, creates the hex map grid, and initializes the TDMap component.
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

        // Disposes active simulation world entities when the bootstrap object is destroyed.
        private void OnDestroy() { World?.Dispose(); }

        // Computes and caches the shortest road route across the map using pathfinding.
        public void InitializeRoute()
        {
            route = TDRoadPathfinder.FindRoute(map.TerrainAt);
            Debug.Log($"TD: cached shortest road route ({route.Count} cells).", this);
        }

        // Draws yellow route path lines between cell centers in the Scene view.
        private void OnDrawGizmos()
        {
            if (!drawRouteGizmo || !map || !map.Terrain || route == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < route.Count; i++) Gizmos.DrawLine(map.Center(route[i-1]), map.Center(route[i]));
        }
    }
}
