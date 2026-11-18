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

    public string LocalizedEnemyName =>
        LocalizationTables.Language == "ru" ? enemyNameRu : enemyNameEn;
}
