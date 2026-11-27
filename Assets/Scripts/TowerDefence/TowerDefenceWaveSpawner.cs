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
        new TowerDefenceWaveDefinition { whiteGhosts = 15, blueGhosts = 10, redGhosts = 8, blackGhosts = 2 },
        new TowerDefenceWaveDefinition { whiteGhosts = 18, blueGhosts = 12, redGhosts = 9, blackGhosts = 4 },
        new TowerDefenceWaveDefinition { whiteGhosts = 20, blueGhosts = 13, redGhosts = 10, blackGhosts = 4 },
        new TowerDefenceWaveDefinition { whiteGhosts = 22, blueGhosts = 15, redGhosts = 12, blackGhosts = 5 },
        new TowerDefenceWaveDefinition { whiteGhosts = 24, blueGhosts = 16, redGhosts = 13, blackGhosts = 6 },
        new TowerDefenceWaveDefinition { whiteGhosts = 26, blueGhosts = 18, redGhosts = 15, blackGhosts = 3 }
    };

    public int TotalWaves => waves != null ? waves.Length : 0;

    public int GetEnemyCountForWave(int waveNumber)
    {
        if (waves == null || waveNumber < 1 || waveNumber > waves.Length)
            return 0;

        TowerDefenceWaveDefinition wave = waves[waveNumber - 1];
        if (wave == null)
            return 0;

        return Mathf.Max(0, wave.whiteGhosts) +
               Mathf.Max(0, wave.blueGhosts) +
               Mathf.Max(0, wave.redGhosts) +
               Mathf.Max(0, wave.blackGhosts);
    }

    public IEnumerator SpawnWave(int waveNumber, TowerDefenceManager manager)
    {
        if (manager == null)
        {
            Debug.LogError("Tower Defence: менеджер отсутствует, волну запустить нельзя.");
            yield break;
        }

        if (waves == null || waveNumber < 1 || waveNumber > waves.Length)
        {
            Debug.LogError("Tower Defence: запрошена несуществующая волна " + waveNumber + ".");
            yield break;
        }

        TowerDefenceWaveDefinition wave = waves[waveNumber - 1];
        if (wave == null || GetEnemyCountForWave(waveNumber) <= 0)
        {
            Debug.LogError("Tower Defence: волна " + waveNumber + " не содержит врагов.");
            yield break;
        }

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
