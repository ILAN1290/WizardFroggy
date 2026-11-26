using System.Collections;
using UnityEngine;

[System.Serializable]
public class TowerDefenceWaveDefinition
{
    [Min(0)] public int whiteGhosts;
    [Min(0)] public int blueGhosts;
    [Min(0)] public int redGhosts;
    [Min(0)] public int blackGhosts;
}

[DisallowMultipleComponent]
public class TowerDefenceWaveSpawner : MonoBehaviour
{
    [Header("Расписание волн")]
    [SerializeField, Min(0.05f)] private float spawnInterval = 0.8f;
    [SerializeField] private TowerDefenceWaveDefinition[] waves =
    {
        new TowerDefenceWaveDefinition { whiteGhosts = 5 },
        new TowerDefenceWaveDefinition { whiteGhosts = 6, blueGhosts = 2 },
        new TowerDefenceWaveDefinition { whiteGhosts = 7, blueGhosts = 3, redGhosts = 1 },
        new TowerDefenceWaveDefinition { whiteGhosts = 8, blueGhosts = 4, redGhosts = 2 },
        new TowerDefenceWaveDefinition { whiteGhosts = 8, blueGhosts = 4, redGhosts = 2, blackGhosts = 1 },
        new TowerDefenceWaveDefinition { whiteGhosts = 10, blueGhosts = 5, redGhosts = 3 },
        new TowerDefenceWaveDefinition { whiteGhosts = 11, blueGhosts = 6, redGhosts = 4 },
        new TowerDefenceWaveDefinition { whiteGhosts = 12, blueGhosts = 7, redGhosts = 5 },
        new TowerDefenceWaveDefinition { whiteGhosts = 13, blueGhosts = 8, redGhosts = 6 },
        new TowerDefenceWaveDefinition { whiteGhosts = 15, blueGhosts = 10, redGhosts = 8, blackGhosts = 3 }
    };

    public int TotalWaves => waves != null ? waves.Length : 0;

    public IEnumerator SpawnWave(int waveNumber, TowerDefenceManager manager)
    {
        if (manager == null || waves == null ||
            waveNumber < 1 || waveNumber > waves.Length)
            yield break;

        TowerDefenceWaveDefinition wave = waves[waveNumber - 1];
        if (wave == null)
            yield break;

        // Сначала появляются обычные, затем быстрые и крупные,
        // а чёрные тяжёлые призраки завершают волну.
        for (int i = 0; i < wave.whiteGhosts; i++)
        {
            manager.SpawnGhost(TowerDefenceGhostType.White);
            yield return new WaitForSeconds(spawnInterval);
        }

        for (int i = 0; i < wave.blueGhosts; i++)
        {
            manager.SpawnGhost(TowerDefenceGhostType.Blue);
            yield return new WaitForSeconds(spawnInterval);
        }

        for (int i = 0; i < wave.redGhosts; i++)
        {
            manager.SpawnGhost(TowerDefenceGhostType.Red);
            yield return new WaitForSeconds(spawnInterval);
        }

        for (int i = 0; i < wave.blackGhosts; i++)
        {
            manager.SpawnGhost(TowerDefenceGhostType.Black);
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}
