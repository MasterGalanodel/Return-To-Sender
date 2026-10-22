using System;
using UnityEngine;

namespace Shared.HUD
{
    // Persists player stats across scenes and fires events whenever health or token balances change.
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDSession")]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        [Header("New game defaults")]
        [SerializeField, Min(0)] private int startingHealth = 50;
        [SerializeField, Min(0)] private int startingTowerTokens = 2;
        [SerializeField, Min(0)] private int startingUpdateTokens = 10;
        private int health, towerTokens, updateTokens;
        public event Action Changed;
        public int Health { get => health; set => SetValues(value, towerTokens, updateTokens); }
        public int TowerTokens { get => towerTokens; set => SetValues(health, value, updateTokens); }
        public int UpdateTokens { get => updateTokens; set => SetValues(health, towerTokens, value); }

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
            newHealth = Mathf.Max(0, newHealth);
            newTowerTokens = Mathf.Max(0, newTowerTokens);
            newUpdateTokens = Mathf.Max(0, newUpdateTokens);
            if (health == newHealth && towerTokens == newTowerTokens && updateTokens == newUpdateTokens) return;
            health = newHealth;
            towerTokens = newTowerTokens;
            updateTokens = newUpdateTokens;
            Changed?.Invoke();
        }

        // Resets health and token balances to their default starting values for a new session.
        public void ResetForNewGame() { SetValues(startingHealth, startingTowerTokens, startingUpdateTokens); }

        // Clears the singleton reference when this instance is destroyed.
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
