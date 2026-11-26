using UnityEngine;

/// <summary>
/// GDD 1.0 level database editable in the inspector.
/// Hierarchy: LevelSystem → Level (city) → Round (1..5).
/// </summary>
[DefaultExecutionOrder(-100)]
public class LevelSystem : MonoBehaviour
{
    public static LevelSystem Instance { get; private set; }

    [SerializeField] private LevelDefinition[] levels;

    private void Awake()
    {
        Instance = this;
        if (levels == null || levels.Length == 0)
            levels = GetComponentsInChildren<LevelDefinition>(true);
        if ((levels == null || levels.Length == 0) && Application.isPlaying)
            BootstrapDefaults();
    }

    private void BootstrapDefaults()
    {
        // Minimal runtime fallback until editor setup creates the full hierarchy with art.
        var list = new System.Collections.Generic.List<LevelDefinition>();
        list.Add(BootstrapLevel("DryCreek", "drycreek", "Dry Creek", "Сухой Ручей"));
        list.Add(BootstrapLevel("DeadWell", "deadwell", "Dead Well", "Мёртвый Колодец"));
        list.Add(BootstrapLevel("RavenRock", "ravenrock", "Raven Rock", "Воронья Скала"));
        levels = list.ToArray();
        if (ScoreSystem.Instance == null && GetComponent<ScoreSystem>() == null)
            gameObject.AddComponent<ScoreSystem>();
    }

    private LevelDefinition BootstrapLevel(string goName, string id, string en, string ru)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        var level = go.AddComponent<LevelDefinition>();
        level.cityId = id;
        level.displayNameEn = en;
        level.displayNameRu = ru;
        level.bellDelayRange = new Vector2(0.8f, 1.4f);

        string[] ids = { "bungler", "cowboy", "deputy", "sheriff", "gunslinger" };
        string[] enN = { "Bungler", "Cowboy", "Deputy", "Sheriff", "Gunslinger" };
        string[] ruN = { "Недотёпа", "Ковбой", "Помощник шерифа", "Шериф", "Стрелок" };
        float[] ert = { 2.6f, 2.3f, 2.0f, 1.7f, 1.4f };
        var rounds = new RoundDefinition[5];
        for (int i = 0; i < 5; i++)
        {
            var rgo = new GameObject("Round" + (i + 1));
            rgo.transform.SetParent(go.transform, false);
            var round = rgo.AddComponent<RoundDefinition>();
            round.enemyId = ids[i];
            round.enemyNameEn = enN[i];
            round.enemyNameRu = ruN[i];
            round.enemyReactionTime = ert[i];
            round.enemyMissChance = 0.35f - i * 0.05f;
            rounds[i] = round;
        }
        level.rounds = rounds;
        return level;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int LevelCount => levels != null ? levels.Length : 0;

    public LevelDefinition GetLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length)
            return null;
        return levels[index];
    }

    public static string CityBoardId(int levelIndex)
    {
        switch (levelIndex)
        {
            case 1: return "deadwell";
            case 2: return "ravenrock";
            default: return "drycreek";
        }
    }
}

public class LevelDefinition : MonoBehaviour
{
    public string cityId = "drycreek";
    public string displayNameEn = "Dry Creek";
    public string displayNameRu = "Сухой Ручей";
    public Sprite backgroundHorizontal;
    public Sprite backgroundVertical;
    [Tooltip("Random delay before the bell rings (seconds). X = min, Y = max.")]
    public Vector2 bellDelayRange = new Vector2(0.8f, 1.4f);
    [Header("Stars (GDD 1.2)")]
    [Tooltip("Score thresholds for 1/2/3 stars. Player never sees these numbers.")]
    public int star1Threshold = 100;
    public int star2Threshold = 200;
    public int star3Threshold = 300;
    public RoundDefinition[] rounds;

    private void OnValidate()
    {
        if (rounds == null || rounds.Length == 0)
            rounds = GetComponentsInChildren<RoundDefinition>(true);
    }

    public RoundDefinition GetRound(int roundIndex1Based)
    {
        int i = roundIndex1Based - 1;
        if (rounds == null || i < 0 || i >= rounds.Length)
            return null;
        return rounds[i];
    }

    public string LocalizedName =>
        LocalizationTables.Get("city_" + cityId, LocalizationTables.IsRussian ? displayNameRu : displayNameEn);

    public int StarsForTotal(int totalDollars)
    {
        return StarThresholds.Count(totalDollars, star1Threshold, star2Threshold, star3Threshold);
    }
}

/// <summary>
/// GDD 1.2: Net Worth star thresholds on MainMenu (inspector object).
/// </summary>
public class StarsNetWorth : MonoBehaviour
{
    public static StarsNetWorth Instance { get; private set; }

    [Tooltip("Score thresholds for 1/2/3 stars. Player never sees these numbers.")]
    public int star1Threshold = 100;
    public int star2Threshold = 200;
    public int star3Threshold = 300;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int StarsForTotal(int totalDollars)
    {
        return StarThresholds.Count(totalDollars, star1Threshold, star2Threshold, star3Threshold);
    }
}

public static class StarThresholds
{
    public static int Count(int totalDollars, int star1, int star2, int star3)
    {
        if (totalDollars >= star3)
            return 3;
        if (totalDollars >= star2)
            return 2;
        if (totalDollars >= star1)
            return 1;
        return 0;
    }
}

public class ScoreSystem : MonoBehaviour
{
    public static ScoreSystem Instance { get; private set; }

    [SerializeField] private int star1Threshold = 100;
    [SerializeField] private int star2Threshold = 200;
    [SerializeField] private int star3Threshold = 300;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static int RoundDollars(int roundNumber, float factTimeSeconds)
    {
        float t = Mathf.Max(0.01f, factTimeSeconds);
        return Mathf.RoundToInt((roundNumber / t) * 100f);
    }

    public int StarsForTotal(int totalDollars)
    {
        return StarThresholds.Count(totalDollars, star1Threshold, star2Threshold, star3Threshold);
    }
}
