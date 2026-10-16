using System;
using UnityEngine;

namespace Shared.HUD
{
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() { Instance = null; }

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

        public void ResetForNewGame() { SetValues(startingHealth, startingTowerTokens, startingUpdateTokens); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
