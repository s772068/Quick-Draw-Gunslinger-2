using UnityEngine;

public class RoundDefinition : MonoBehaviour
{
    public string enemyId = "bungler";
    public string enemyNameEn = "Bungler";
    public string enemyNameRu = "Недотёпа";
    public Sprite enemySprite;
    public Sprite enemyShotSprite;
    public float enemyReactionTime = 2.2f;
    [Range(0f, 1f)] public float enemyMissChance = 0.25f;

    public float EnemyReactionTime => Mathf.Max(0.2f, enemyReactionTime);

    public int GetDifficult(Vector2 reshotDelayRange)
    {
        float missChance = Mathf.Clamp01(enemyMissChance);
        if (missChance >= 1f)
            return 0;

        // A geometric sequence of shots has p / (1 - p) misses before the first hit.
        float meanReshotDelay = (reshotDelayRange.x + reshotDelayRange.y) * 0.5f;
        float meanTimeToHit = EnemyReactionTime + meanReshotDelay * missChance / (1f - missChance);
        return Mathf.RoundToInt(100f / meanTimeToHit);
    }

    public string LocalizedEnemyName =>
        LocalizationTables.Get("enemy_" + enemyId, LocalizationTables.IsRussian ? enemyNameRu : enemyNameEn);
}
