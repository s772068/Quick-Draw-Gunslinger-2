using UnityEngine;

/// <summary>
/// Legacy helper from the old bottom-leaderboard layout.
/// GDD 1.0: does not move authored menu controls.
/// Scene positions stay as you set them in the editor; nothing is written back from Play Mode.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuLayout : MonoBehaviour
{
    // Intentionally empty. Kept so existing scene components don't break.
    // Do not add [ExecuteAlways] and do not mutate RectTransforms here.
}
