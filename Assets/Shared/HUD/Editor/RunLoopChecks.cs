using System;
using System.Collections.Generic;
using System.IO;
using Cave;
using TD;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Shared.HUD.Editor
{
    // Run from the Cave scene in Play mode. Scene changes use the real runtime controllers.
    public static class RunLoopChecks
    {
        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public List<string> checks = new List<string>();
            public string error;
        }

        private static Report report;
        private static GameSession session;
        private static int step;
        private static double deadline;
        private static double frozenAt;
        private static int carriedHealth;
        private static Vector2Int firstCell, secondCell;

        [MenuItem("Tools/Shared/HUD/Verify Run Loop (Play Mode)")]
        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start Play in the Cave scene first.");
            if (report != null) throw new InvalidOperationException("Run loop checks are already running.");
            report = new Report();
            step = 0;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.checks.Add(message);
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Run loop verification stopped or timed out at step " + step);
                var cave = UnityEngine.Object.FindFirstObjectByType<CaveExploration>();
                var td = UnityEngine.Object.FindFirstObjectByType<TDBootstrap>();
                switch (step)
                {
                    case 0:
                        if (!cave || !cave.Initialized) return;
                        cave.enabled = false;
                        session = GameSession.Instance;
                        VerifyRules();
                        session.ResetForNewGame();
                        session.BeginPhase(GameSession.PlayPhase.Cave);
                        session.Health = 47;
                        Check(session.GetComponent<SharedHud>().transform.Find("SafeArea/TopCenterStats/Health/Icon")
                            .GetComponent<UnityEngine.UI.Image>().sprite, "Health icon is wired");
                        Check(session.transform.Find("SafeArea/TopCenterStats/TowerTokens/Icon")
                            .GetComponent<UnityEngine.UI.Image>().sprite, "Build icon is wired");
                        cave.ReachExit();
                        step++;
                        break;
                    case 1:
                        if (!td || !td.Initialized) return;
                        td.enabled = false;
                        Check(GameSession.Instance == session && session.Day == 1 && session.Health == 47,
                            "Cave to TD preserves session, day and HP");
                        firstCell = td.Player.CurrentCell;
                        var baseCells = td.Map.BaseCells();
                        baseCells.Remove(firstCell);
                        secondCell = baseCells[0];
                        Check(td.Placement.TryPlace(firstCell, out _) && session.TowerTokens == 1, "First tower costs one token");
                        Check(!td.Placement.TryPlace(firstCell, out _) && session.TowerTokens == 1,
                            "Occupied cells do not spend tokens");
                        Check(!td.Placement.TryPlace(new Vector2Int(-1, -1), out _) && session.TowerTokens == 1,
                            "Invalid cells do not spend tokens");
                        Check(td.Placement.TryPlace(secondCell, out _) && session.TowerTokens == 0, "Second tower costs last token");
                        bool denied = false;
                        Action onDenied = () => denied = true;
                        session.BuildDenied += onDenied;
                        Check(!td.Placement.TryPlace(baseCells[1], out _) && denied,
                            "Zero tokens block construction and signal the HUD warning");
                        session.BuildDenied -= onDenied;
                        Check(td.Placement.TowerCount == 2, "Rejected placement creates no tower");
                        var escaped = td.World.Enemies[0];
                        int health = session.Health;
                        escaped.MoveStep(10000);
                        escaped.MoveStep(10000);
                        Check(session.Health == health - 1 && td.World.ExitedCount == 1, "One escape applies one HP damage exactly once");
                        var killed = td.World.SpawnEnemy();
                        health = session.Health;
                        killed.ApplyDamage(10000);
                        Check(session.Health == health && td.World.KilledCount == 1, "Killed enemies do not damage shared HP");
                        td.World.SpawnEnemy();
                        carriedHealth = session.Health;
                        session.StepPhase(60);
                        td.enabled = true;
                        step++;
                        break;
                    case 2:
                        if (!cave || !cave.Initialized) return;
                        cave.enabled = false;
                        Check(GameSession.Instance == session && session.Day == 2 && session.Health == carriedHealth,
                            "TD deadline returns to the same Cave on day 2 without retreat damage");
                        Check(session.TowerTokens == 0 && session.TowerCells.Count == 2 && session.ElapsedSeconds < 1,
                            "Balances and tower positions persist while the Cave timer resets");
                        Check(cave.gameObject.scene.path == CaveExploration.ScenePath && cave.player.transform.position.x < 2,
                            "The same Cave scene is reused with its entrance available");
                        cave.ReachExit();
                        step++;
                        break;
                    case 3:
                        if (!td || !td.Initialized) return;
                        td.enabled = false;
                        Check(td.Placement.TowerCount == 2 && session.TowerTokens == 0,
                            "Returning to TD restores both towers without charging again");
                        Check(!td.Placement.TryPlace(firstCell, out _) && !td.Placement.TryPlace(secondCell, out _),
                            "Restored towers still occupy their original cells");
                        Check(td.World.ExitedCount == 0 && session.Health == carriedHealth && session.Day == 2,
                            "New TD phase begins without old enemies or extra HP loss");
                        session.Damage(session.Health);
                        Vector3 position = td.Player.transform.position;
                        td.Player.MoveStep(Vector2.right, 10);
                        Check(session.HasEnded && Time.timeScale == 0 && td.Player.transform.position == position,
                            "Zero HP freezes gameplay and blocks player movement");
                        Check(session.transform.Find("SafeArea/RunResult").gameObject.activeSelf,
                            "Game over result is visible over the frozen scene");
                        frozenAt = EditorApplication.timeSinceStartup;
                        step++;
                        break;
                    case 4:
                        if (EditorApplication.timeSinceStartup - frozenAt < .2) return;
                        Check(Time.timeScale == 0 && session.Health == 0 && session.Day == 2,
                            "Game over remains frozen across subsequent frames");
                        Finish(null);
                        break;
                }
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        private static void VerifyRules()
        {
            session.ResetForNewGame();
            Check(session.Health == 50 && session.TowerTokens == 2 && session.Day == 1, "New game starts at 50 HP, 2 tokens and day 1");
            session.BeginPhase(GameSession.PlayPhase.Cave);
            var timer = session.transform.Find("SafeArea/TopCenterStats/DayAndTime/Time").GetComponent<TextMeshProUGUI>();
            Check(GameSession.CaveSeconds == 20 && GameSession.TowerDefenseSeconds == 60,
                "Cave and TD have independent 20-second and 60-second durations");
            Check(session.PhaseDurationSeconds == 20 && timer.text == "CAVE 0/20s", "Cave HUD displays its 20-second duration");
            session.StepPhase(20);
            Check(session.Health == 50 && !session.IsOvertime, "No Cave damage or red timer at exactly 20 seconds");
            session.StepPhase(.25f);
            Check(session.IsOvertime && timer.color.r > timer.color.g && session.Health == 50,
                "Cave timer turns red immediately after 20 without early damage");
            session.StepPhase(4.75f);
            Check(session.Health == 49, "First Cave overtime damage occurs at 25 seconds");
            session.StepPhase(10);
            Check(session.Health == 47 && session.ElapsedSeconds == 35, "Cave damage repeats at 30 and 35 seconds");
            session.FinishCave();
            session.StepPhase(100);
            Check(session.Health == 47 && session.ElapsedSeconds == 35, "Finished Cave stops its clock and damage");
            session.BeginPhase(GameSession.PlayPhase.TowerDefense);
            Check(session.PhaseDurationSeconds == 60 && timer.text == "TD 0/60s", "TD HUD displays its separate 60-second duration");
            session.StepPhase(20);
            Check(!session.TowerDefenseExpired && !session.FinishTowerDefense() && session.Day == 1,
                "TD continues beyond the Cave's 20-second duration");
            session.StepPhase(10);
            session.StepPhase(29.999998f);
            Check(!session.TowerDefenseExpired && session.IsPhaseActive(GameSession.PlayPhase.TowerDefense),
                "Display rounding near 60 seconds cannot end TD early");
            session.StepPhase(.001f);
            Check(session.TowerDefenseExpired && !session.IsPhaseActive(GameSession.PlayPhase.TowerDefense),
                "Crossing the TD deadline uses the full-precision clock");
            foreach (int finalHealth in new[] { 80, 81, 50 })
            {
                session.ResetForNewGame();
                session.Health = finalHealth;
                for (int day = 1; day <= GameSession.TotalDays; day++)
                {
                    session.BeginPhase(GameSession.PlayPhase.TowerDefense);
                    session.StepPhase(59);
                    Check(!session.FinishTowerDefense() && session.Day == day, "TD cannot finish before 60 seconds on day " + day);
                    session.StepPhase(1);
                    bool again = session.FinishTowerDefense();
                    Check(again == (day < 6) && session.Day == Math.Min(day + 1, 6), "TD advances exactly once after day " + day);
                    Check(!session.FinishTowerDefense(), "Repeated phase completion cannot skip a day");
                }
                Check(session.Outcome == (finalHealth > 80 ? GameSession.RunOutcome.Victory : GameSession.RunOutcome.Defeat)
                    && Time.timeScale == 0, "Day 6 outcome is correct and frozen at HP " + finalHealth);
            }
            session.ResetForNewGame();
            Check(Time.timeScale > 0 && session.TowerCells.Count == 0, "New run restores time and clears persisted towers");
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            report.passed = error == null;
            report.error = error;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/RunLoopChecks.json", JsonUtility.ToJson(report, true));
            if (error != null) Debug.LogError("Run loop checks failed: " + error);
            else Debug.Log("Run loop checks passed: " + report.checks.Count);
            report = null;
        }
    }
}
