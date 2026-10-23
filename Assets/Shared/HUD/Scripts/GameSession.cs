using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shared.HUD
{
    // Persists player stats across scenes and fires events whenever health or token balances change.
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDSession")]
    public sealed class GameSession : MonoBehaviour
    {
        public enum PlayPhase { None, Cave, TowerDefense }
        public enum RunOutcome { Playing, Victory, Defeat }
        public enum PickupType { Health, Build }
        public const int TotalDays = 6;
        public const int PhaseSeconds = 60;
        public const int CaveOvertimeSeconds = 5;
        public const int VictoryHealth = 80;

        public static GameSession Instance { get; private set; }
        [Header("New game defaults")]
        [SerializeField, Min(0)] private int startingHealth = 50;
        [SerializeField, Min(0)] private int startingTowerTokens = 2;
        [SerializeField, Min(0)] private int startingUpdateTokens = 10;
        private int health, towerTokens, updateTokens;
        private double elapsedSeconds;
        private int overtimePenalties;
        private float timeScaleBeforeEnd = 1;
        private readonly List<Vector2Int> towerCells = new List<Vector2Int>();
        private readonly HashSet<string> collectedCavePickups = new HashSet<string>();
        public event Action Changed;
        public event Action BuildDenied;
        public int Health { get => health; set => SetValues(value, towerTokens, updateTokens); }
        public int TowerTokens { get => towerTokens; set => SetValues(health, value, updateTokens); }
        public int UpdateTokens { get => updateTokens; set => SetValues(health, towerTokens, value); }
        public int Day { get; private set; } = 1;
        public PlayPhase Phase { get; private set; }
        public RunOutcome Outcome { get; private set; }
        public bool HasEnded => Outcome != RunOutcome.Playing;
        public bool PhaseComplete { get; private set; }
        public float ElapsedSeconds => (float)elapsedSeconds;
        public bool TowerDefenseExpired => Phase == PlayPhase.TowerDefense && elapsedSeconds >= PhaseSeconds;
        public bool IsOvertime => Phase == PlayPhase.Cave && elapsedSeconds > PhaseSeconds;
        public string ResultMessage { get; private set; }
        public IReadOnlyList<Vector2Int> TowerCells => towerCells;

        // Clears the static singleton reference on domain reload to prevent stale references in the editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() { Instance = null; }

        // Enforces the persistent singleton instance across scene loads and initializes starting stats.
        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (Instance && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResetForNewGame();
        }

        // Clamps and assigns player stats, raising the Changed event if any values differ.
        public void SetValues(int newHealth, int newTowerTokens, int newUpdateTokens)
        {
            if (HasEnded) return;
            newHealth = Mathf.Max(0, newHealth);
            newTowerTokens = Mathf.Max(0, newTowerTokens);
            newUpdateTokens = Mathf.Max(0, newUpdateTokens);
            if (health == newHealth && towerTokens == newTowerTokens && updateTokens == newUpdateTokens) return;
            health = newHealth;
            towerTokens = newTowerTokens;
            updateTokens = newUpdateTokens;
            if (health == 0)
            {
                EndRun(RunOutcome.Defeat, "Health reached zero.");
                return;
            }
            Changed?.Invoke();
        }

        public void BeginPhase(PlayPhase phase)
        {
            if (HasEnded) return;
            Phase = phase;
            PhaseComplete = false;
            elapsedSeconds = 0;
            overtimePenalties = 0;
            Changed?.Invoke();
        }

        public bool IsPhaseActive(PlayPhase phase) => !HasEnded && !PhaseComplete && Phase == phase
            && (phase != PlayPhase.TowerDefense || elapsedSeconds < PhaseSeconds);

        // Scene controllers tick before movement/combat, so the TD deadline wins over same-frame escapes.
        public void StepPhase(float deltaTime)
        {
            if (HasEnded || PhaseComplete || Phase == PlayPhase.None || deltaTime <= 0
                || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            int previousSecond = (int)elapsedSeconds;
            bool wasOvertime = IsOvertime;
            elapsedSeconds += deltaTime;
            if (Phase == PlayPhase.TowerDefense) elapsedSeconds = Math.Min(elapsedSeconds, PhaseSeconds);
            if (Phase == PlayPhase.Cave)
            {
                int due = (int)Math.Max(0, Math.Floor((elapsedSeconds - PhaseSeconds) / CaveOvertimeSeconds));
                int damage = due - overtimePenalties;
                overtimePenalties = due;
                if (damage > 0) Damage(damage);
            }
            if (!HasEnded && (previousSecond != (int)elapsedSeconds || wasOvertime != IsOvertime)) Changed?.Invoke();
        }

        public void FinishCave()
        {
            if (!IsPhaseActive(PlayPhase.Cave)) return;
            PhaseComplete = true;
            Changed?.Invoke();
        }

        // Returns true only when another visit to the same Cave scene should begin.
        public bool FinishTowerDefense()
        {
            if (HasEnded || PhaseComplete || Phase != PlayPhase.TowerDefense || elapsedSeconds < PhaseSeconds) return false;
            PhaseComplete = true;
            if (Day == TotalDays)
            {
                EndRun(health > VictoryHealth ? RunOutcome.Victory : RunOutcome.Defeat,
                    health > VictoryHealth ? "Six days completed with more than 80 HP." : "Six days completed. Victory requires more than 80 HP.");
                return false;
            }
            Day++;
            Changed?.Invoke();
            return true;
        }

        public void Damage(int amount)
        {
            if (!HasEnded && amount > 0) Health = Mathf.Max(0, health - amount);
        }

        public bool TrySpendBuildToken()
        {
            if (HasEnded) return false;
            if (towerTokens == 0) { BuildDenied?.Invoke(); return false; }
            TowerTokens--;
            return true;
        }

        public void RememberTower(Vector2Int cell)
        {
            if (!HasEnded && !towerCells.Contains(cell)) towerCells.Add(cell);
        }

        public bool HasCollectedCavePickup(string pickupId) => collectedCavePickups.Contains(Day + ":" + pickupId);

        public bool TryCollectCavePickup(string pickupId, PickupType kind, int amount)
        {
            if (!IsPhaseActive(PlayPhase.Cave) || string.IsNullOrEmpty(pickupId) || amount <= 0
                || (kind != PickupType.Health && kind != PickupType.Build)) return false;
            if (!collectedCavePickups.Add(Day + ":" + pickupId)) return false;
            if (kind == PickupType.Health) Health += amount;
            else TowerTokens += amount;
            return true;
        }

        private void EndRun(RunOutcome outcome, string message)
        {
            if (HasEnded) return;
            Outcome = outcome;
            ResultMessage = message;
            timeScaleBeforeEnd = Time.timeScale > 0 ? Time.timeScale : 1;
            Time.timeScale = 0;
            Changed?.Invoke();
        }

        // Future pickups can change Health/TowerTokens; this reset also clears all persisted towers.
        public void ResetForNewGame()
        {
            if (HasEnded) Time.timeScale = timeScaleBeforeEnd;
            Outcome = RunOutcome.Playing;
            ResultMessage = null;
            Day = 1;
            Phase = PlayPhase.None;
            PhaseComplete = false;
            elapsedSeconds = 0;
            overtimePenalties = 0;
            towerCells.Clear();
            collectedCavePickups.Clear();
            health = Mathf.Max(0, startingHealth);
            towerTokens = Mathf.Max(0, startingTowerTokens);
            updateTokens = Mathf.Max(0, startingUpdateTokens);
            if (health == 0) EndRun(RunOutcome.Defeat, "Health reached zero.");
            else Changed?.Invoke();
        }

        // Clears the singleton reference when this instance is destroyed.
        private void OnDestroy()
        {
            if (Instance != this) return;
            if (HasEnded) Time.timeScale = timeScaleBeforeEnd;
            Instance = null;
        }
    }
}
