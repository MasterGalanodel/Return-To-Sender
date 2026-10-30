using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cave;
using Shared.HUD;
using TD;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Run in Cave Play mode with: unity command script run --file AgentScripts/VerticalCaveChecks.cs --entry VerticalCaveChecks.Start
public static class VerticalCaveChecks
{
    [Serializable] private sealed class Report
    {
        public bool passed;
        public List<string> checks = new List<string>();
        public string error;
    }
    private static Report report;
    private static GameSession session;
    private static Gamepad pad;
    private static int stage, targetStep;
    private static float stageAt, peakY, jumpStart;
    private static float oldScale;
    private static double deadline;
    private static Vector2 lastStick;
    private static bool lastButton;
    private static float dropReleasedAt;
    private static int carriedHealth, carriedTokens;
    private static int oldSceneHandle;
    private static float climbStartedAt;

    public static void Start()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start Play in Cave first.");
        report = new Report();
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 240;
        oldScale = Time.timeScale;
        Time.timeScale = 1;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/VerticalCaveChecks.json", "{\"running\":true}");
        File.WriteAllText("Logs/VerticalCaveProgress.txt", "Starting fresh Cave");
        pad = InputSystem.AddDevice<Gamepad>("Cave verification controller");
        oldSceneHandle = SceneManager.GetActiveScene().handle;
        GameSession.Instance.ResetForNewGame();
        SceneManager.LoadSceneAsync(CaveExploration.ScenePath);
        EditorApplication.update += Tick;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.checks.Add(message);
    }
    private static void Next(int value)
    {
        stage = value;
        stageAt = Time.time;
    }
    private static void Drive(float x, bool drop = false, bool jumping = false)
    {
        Vector2 stick = new Vector2(x, drop ? -1 : 0);
        bool button = drop || jumping;
        if ((stick - lastStick).sqrMagnitude < .0001f && lastButton == button) return;
        var state = new GamepadState { leftStick = stick }.WithButton(GamepadButton.South, button);
        InputSystem.QueueStateEvent(pad, state);
        lastStick = stick;
        lastButton = button;
    }
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Vertical Cave checks stopped or timed out at stage " + stage);
            var cave = UnityEngine.Object.FindFirstObjectByType<CaveExploration>();
            var td = UnityEngine.Object.FindFirstObjectByType<TDBootstrap>();
            if (cave && cave.Initialized)
            {
                cave.enabled = false; // Keep the clock separate from physics traversal, but exercise the real camera.
                cave.UpdateCamera();
            }
            switch (stage)
            {
                case 0:
                    if (!cave || cave.gameObject.scene.handle == oldSceneHandle || !cave.Initialized || !cave.player.IsGrounded) return;
                    session = GameSession.Instance;
                    session.ResetForNewGame();
                    session.BeginPhase(GameSession.PlayPhase.Cave);
                    Check(cave.platforms.Length == 40 && cave.GetComponentsInChildren<CavePlatform>().Length == 40,
                        "Exactly 40 platforms exist");
                    Check(cave.GetComponentsInChildren<CavePickup>().Length == 12, "Twelve pickups are distributed over the descent");
                    Check(cave.platforms.All(p => p.GetComponent<BoxCollider2D>().usedByEffector
                        && p.GetComponent<PlatformEffector2D>().useOneWay), "All 40 surfaces are one-way platforms");
                    Check(cave.player.GetComponent<SpriteRenderer>().sprite.name == "mc_0", "The Cave uses the shared player artwork");
                    Check(Mathf.Approximately(cave.player.Body.gravityScale, .28f), "Cave gravity is one tenth of the previous 2.8 scale");
                    Check(Mathf.Abs(cave.exitGate.position.y - cave.platforms[0].SurfaceY) < .01f
                        && !cave.exitGate.GetComponent<Collider2D>().bounds.Intersects(cave.player.GetComponent<Collider2D>().bounds),
                        "The exit is on the starting platform without overlapping the spawn");
                    Check(cave.GetComponentsInChildren<CavePickup>().All(p => p.Amount == 1), "All health and build pickups award one unit");
                    Check(cave.transform.Find("Cave Environment/Shaft/Shaft Cap").GetComponent<BoxCollider2D>(),
                        "The existing cave ceiling blocks upward escape");
                    jumpStart = cave.player.Body.position.y;
                    peakY = jumpStart;
                    Drive(0, jumping: true);
                    Next(1);
                    break;
                case 1:
                    peakY = Mathf.Max(peakY, cave.player.Body.position.y);
                    if (Time.time - stageAt > .1f) Drive(0);
                    if (Time.time - stageAt < .45f) return;
                    Check(!cave.player.IsGrounded && peakY > jumpStart + .8f, "The existing Jump action gains height in low gravity");
                    Drive(0, jumping: true);
                    Next(12);
                    break;
                case 2:
                    if (Time.time - stageAt < .22f) return;
                    Check(cave.player.Body.position.x < jumpStart - .4f, "The existing Move action accepts controller movement");
                    Drive(0);
                    cave.player.TeleportTo(cave.platforms[0].SafePosition);
                    targetStep = 1;
                    dropReleasedAt = -1;
                    Next(3);
                    break;
                case 3:
                    if (cave.player.IsGrounded && cave.player.LastSafeStep == targetStep
                        && Mathf.Abs(cave.player.Body.position.x - cave.platforms[targetStep].transform.position.x) < .25f
                        && Mathf.Abs(cave.player.Velocity.x) < .8f)
                    {
                        Check(true, "Player physically lands on step " + (targetStep + 1));
                        File.WriteAllText("Logs/VerticalCaveProgress.txt", "Landed on step " + (targetStep + 1));
                        if (targetStep == 39)
                        {
                            Drive(0);
                            Check(session.Health == 58 && session.TowerTokens == 6, "Eight health pickups award eight HP and four build pickups award four tokens");
                            Check(cave.GetComponentsInChildren<CavePickup>().Length == 0, "Collected pickups disappear");
                            Check(cave.caveCamera.transform.position.y < -85, "The camera follows the complete vertical descent");
                            Check(!session.TryCollectCavePickup("step-3", GameSession.PickupType.Health, 1) && session.Health == 58,
                                "A pickup cannot award its reward twice in one day");
                            climbStartedAt = Time.time;
                            Next(11);
                            return;
                        }
                        targetStep++;
                        stageAt = Time.time;
                        dropReleasedAt = -1;
                    }
                    if (Time.time - stageAt > 5)
                        throw new InvalidOperationException("Could not reach step " + (targetStep + 1) + "; position " + cave.player.Body.position);
                    CheckNotSkipped(cave);
                    float delta = cave.platforms[targetStep].transform.position.x - cave.player.Body.position.x;
                    float input = Mathf.Abs(delta) < .08f ? 0 : Mathf.Clamp(delta * 2.5f, -1, 1);
                    bool sameColumn = targetStep == 1 || Mathf.Abs(cave.platforms[targetStep].transform.position.x
                        - cave.platforms[targetStep - 1].transform.position.x) < .1f;
                    if (sameColumn && cave.player.IsGrounded && cave.player.LastSafeStep == targetStep - 1 && dropReleasedAt < 0)
                        dropReleasedAt = Time.time + .1f;
                    // Drop beneath the entrance trigger before moving toward the first right-hand ledge.
                    if (targetStep == 1 && cave.player.Body.position.y > -.45f) input = 0;
                    Drive(input, dropReleasedAt > Time.time);
                    break;
                case 4:
                    if (!td || !td.Initialized) return;
                    Drive(0);
                    td.enabled = false;
                    Check(GameSession.Instance == session && session.Phase == GameSession.PlayPhase.TowerDefense,
                        "Walking into the starting-platform gate loads TD with the same session");
                    Check(session.Health == 58 && session.TowerTokens == 6, "Collected resources persist into TD");
                    Check(td.Placement.TryPlaceCurrent() && session.TowerTokens == 5, "Collected build tokens can construct a castle");
                    carriedHealth = session.Health;
                    carriedTokens = session.TowerTokens;
                    session.StepPhase(60);
                    td.enabled = true;
                    Next(5);
                    break;
                case 5:
                    if (!cave || !cave.Initialized || session.Day != 2) return;
                    Check(cave.platforms.Length == 40 && cave.GetComponentsInChildren<CavePickup>().Length == 12,
                        "Day 2 returns to the same 40-step Cave with fresh supplies");
                    Check(session.Health == carriedHealth && session.TowerTokens == carriedTokens,
                        "Returning to Cave preserves health and remaining build tokens");
                    cave.player.TeleportTo(cave.platforms[3].SafePosition + Vector2.up * .2f);
                    Next(6);
                    break;
                case 6:
                    if (!cave.player.IsGrounded || cave.player.LastSafeStep != 3) return;
                    Check(session.Health == carriedHealth + 1, "A +1 health pickup is available again on a new day");
                    SceneManager.LoadSceneAsync(CaveExploration.ScenePath);
                    Next(7);
                    break;
                case 7:
                    if (!cave || !cave.Initialized) return;
                    if (Time.time - stageAt < .2f) return;
                    Check(cave.GetComponentsInChildren<CavePickup>().Length == 11
                        && session.HasCollectedCavePickup("step-3"), "Reloading the same Cave day does not respawn a collected pickup");
                    cave.player.TeleportTo(cave.platforms[20].SafePosition + Vector2.up * .2f);
                    Next(8);
                    break;
                case 8:
                    if (!cave.player.IsGrounded || cave.player.LastSafeStep != 20) return;
                    cave.player.TeleportTo(new Vector2(0, CaveExploration.BottomY - 5));
                    Next(9);
                    break;
                case 9:
                    if (Time.time - stageAt < .3f) return;
                    Check(cave.player.IsGrounded && cave.player.LastSafeStep == 20
                        && cave.player.Body.position.y > cave.platforms[20].SurfaceY,
                        "Falling below the shaft recovers to the last landed step");
                    Check(session.Health == carriedHealth + 1 && session.TowerTokens == carriedTokens,
                        "Fall recovery does not change resources or recreate pickups");
                    cave.player.TeleportTo(new Vector2(-1.2f, cave.platforms[20].SurfaceY - .75f));
                    cave.player.QueueJump();
                    peakY = cave.player.Body.position.y;
                    Next(10);
                    break;
                case 10:
                    peakY = Mathf.Max(peakY, cave.player.Body.position.y);
                    if (Time.time - stageAt < .3f) return;
                    Check(peakY > cave.platforms[20].SurfaceY + .2f,
                        "An airborne jump passes through a one-way platform from below");
                    Finish(null);
                    break;
                case 11:
                    if (Time.time - climbStartedAt > 60) throw new InvalidOperationException("Return climb timed out");
                    float climbX = Mathf.Clamp((-1.2f - cave.player.Body.position.x) * 2.5f, -1, 1);
                    if (cave.player.Body.position.y > cave.platforms[0].SafePosition.y + .3f)
                    {
                        Drive(0);
                        Check(true, "Repeated jumps climb from step 40 back above the starting platform");
                        Next(13);
                        return;
                    }
                    Drive(climbX, jumping: (Time.time - stageAt) % .5f < .12f);
                    break;
                case 12:
                    if (Time.time - stageAt < .08f) return;
                    Check(!cave.player.IsGrounded && cave.player.Velocity.y > 8,
                        "A second Jump input restores upward speed while airborne");
                    Drive(-1);
                    cave.player.TeleportTo(cave.platforms[0].SafePosition);
                    jumpStart = cave.player.Body.position.x;
                    Next(2);
                    break;
                case 13:
                    if (!cave.player.IsGrounded || cave.player.LastSafeStep != 0) return;
                    Check(cave.caveCamera.transform.position.y > -3, "The camera follows the return to the entrance");
                    Drive(1);
                    Next(4);
                    break;
            }
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }
    private static void CheckNotSkipped(CaveExploration cave)
    {
        if (cave.player.Body.position.y < cave.platforms[targetStep].SurfaceY - .6f)
            throw new InvalidOperationException("Skipped target step " + (targetStep + 1) + " at " + cave.player.Body.position);
    }
    private static void Finish(string error)
    {
        EditorApplication.update -= Tick;
        if (pad != null) InputSystem.RemoveDevice(pad);
        Time.timeScale = oldScale;
        report.passed = error == null;
        report.error = error;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/VerticalCaveChecks.json", JsonUtility.ToJson(report, true));
        if (error == null) Debug.Log("Vertical Cave checks passed: " + report.checks.Count);
        else Debug.LogError("Vertical Cave checks failed: " + error);
    }
}
