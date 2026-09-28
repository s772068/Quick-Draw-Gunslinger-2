using UnityEngine;

/// <summary>
/// GDD 0.4: keep Direction bottom-right, Direction top-left, and Crossfire center colinear
/// by rotating PlayerGun. Does not touch shake/recoil parameters.
/// </summary>
[ExecuteAlways]
public class PlayerGunAim : MonoBehaviour
{
    [SerializeField] private RectTransform playerGun;
    [SerializeField] private RectTransform direction;
    [SerializeField] private RectTransform crossfire;
    [SerializeField] private bool aimWhenCrossfireVisible = true;
    [Tooltip("How many correction passes per frame (BR moves when gun rotates around its pivot).")]
    [SerializeField] private int solveIterations = 3;

    private Quaternion _restLocalRotation;
    private bool _hasRest;
    private bool _aiming;

    private void Awake()
    {
        AutoFindIfNeeded();
        CaptureRest();
    }

    private void OnEnable()
    {
        AutoFindIfNeeded();
        CaptureRest();
    }

    private void LateUpdate()
    {
        if (playerGun == null || direction == null || crossfire == null)
            return;

        bool shouldAim = _aiming;
        if (aimWhenCrossfireVisible)
            shouldAim = shouldAim || (crossfire.gameObject.activeInHierarchy && playerGun.gameObject.activeInHierarchy);

        if (!shouldAim)
        {
            if (_hasRest)
                playerGun.localRotation = _restLocalRotation;
            return;
        }

        AlignToCrossfire();
    }

    public void SetAiming(bool aiming)
    {
        _aiming = aiming;
        if (!aiming && _hasRest && playerGun != null)
            playerGun.localRotation = _restLocalRotation;
    }

    public void CaptureRestPose()
    {
        CaptureRest();
    }

    public void Configure(RectTransform gun, RectTransform dir, RectTransform cf)
    {
        if (gun != null)
            playerGun = gun;
        if (dir != null)
            direction = dir;
        if (cf != null)
            crossfire = cf;
        CaptureRest();
    }

    private void CaptureRest()
    {
        if (playerGun == null)
            return;
        _restLocalRotation = playerGun.localRotation;
        _hasRest = true;
    }

    private void AutoFindIfNeeded()
    {
        if (playerGun == null)
            playerGun = GetComponent<RectTransform>();

        if (direction == null && playerGun != null)
        {
            var t = playerGun.Find("Direction");
            if (t != null)
                direction = t as RectTransform;
        }
    }

    private void AlignToCrossfire()
    {
        int iterations = Mathf.Clamp(solveIterations, 1, 8);
        for (int i = 0; i < iterations; i++)
        {
            Vector3[] corners = new Vector3[4];
            direction.GetWorldCorners(corners);
            // 0=BL, 1=TL, 2=TR, 3=BR
            Vector3 br = corners[3];
            Vector3 tl = corners[1];
            Vector3 cf = crossfire.position;

            Vector2 axis = new Vector2(tl.x - br.x, tl.y - br.y);
            Vector2 toTarget = new Vector2(cf.x - br.x, cf.y - br.y);
            if (axis.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
                return;

            float delta = Vector2.SignedAngle(axis, toTarget);
            if (Mathf.Abs(delta) < 0.01f)
                return;

            playerGun.Rotate(0f, 0f, delta, Space.World);
        }
    }
}
