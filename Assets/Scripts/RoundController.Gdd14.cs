using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public partial class RoundController
{
    [SerializeField] private Image gunFlash;

    public event Action CrossfireEnterHitbox;
    public event Action CrossfireLeaveHitbox;

    private Image _crossfireOnTarget;
    private Texture2D _outlineTexture;
    private Sprite _outlineSource;
    private Coroutine _gunFlashRoutine;
    private float? _firstHitboxEnter;
    private float? _firstHitboxLeave;

    private void SetCrossfireOnTarget(bool on)
    {
        _crossfireWasOverHitbox = on;
        if (on && _outlineSource != _hitMaskSource)
            RebuildTargetOutline();
        if (_crossfireOnTarget == null)
            return;
        bool visible = on && _outlineTexture != null;
        if (_crossfireOnTarget.gameObject.activeSelf != visible)
            _crossfireOnTarget.gameObject.SetActive(visible);
    }

    private void RebuildTargetOutline()
    {
        ReleaseTargetOutline();
        if (_hitMask == null)
            return;

        if (_crossfireOnTarget == null)
        {
            var go = new GameObject("CrossfireOnTarget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(enemyBody, false);
            _crossfireOnTarget = go.GetComponent<Image>();
            _crossfireOnTarget.raycastTarget = false;
        }

        // Build a padded alpha mask at a fixed maximum resolution so the glow
        // is inexpensive to generate and can extend outside the sprite rect.
        const int radius = 6;
        const byte opaque = 255;
        float scale = 256f / Mathf.Max(_hitMask.width, _hitMask.height);
        int bodyWidth = Mathf.Max(1, Mathf.RoundToInt(_hitMask.width * scale));
        int bodyHeight = Mathf.Max(1, Mathf.RoundToInt(_hitMask.height * scale));
        int width = bodyWidth + 2 * radius;
        int height = bodyHeight + 2 * radius;
        var alpha = new byte[width * height];
        for (int y = 0; y < bodyHeight; y++)
        for (int x = 0; x < bodyWidth; x++)
        {
            if (_hitMask.GetPixelBilinear((x + 0.5f) / bodyWidth, (y + 0.5f) / bodyHeight).a >= opaqueAlphaThreshold)
                alpha[(y + radius) * width + x + radius] = opaque;
        }

        var glow = new byte[alpha.Length];
        var kernel = new byte[(2 * radius + 1) * (2 * radius + 1)];
        int kernelWidth = 2 * radius + 1;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance <= 0f || distance >= radius)
                continue;
            float falloff = 1f - Mathf.SmoothStep(0f, 1f, (distance - 1f) / (radius - 1f));
            kernel[(dy + radius) * kernelWidth + dx + radius] = (byte)Mathf.RoundToInt(opaque * falloff);
        }

        for (int y = radius; y < height - radius; y++)
        for (int x = radius; x < width - radius; x++)
        {
            int source = y * width + x;
            if (alpha[source] == 0 || (alpha[source - 1] == opaque && alpha[source + 1] == opaque
                && alpha[source - width] == opaque && alpha[source + width] == opaque))
                continue;

            for (int ky = 0; ky < kernelWidth; ky++)
            for (int kx = 0; kx < kernelWidth; kx++)
            {
                byte opacity = kernel[ky * kernelWidth + kx];
                int target = source + (ky - radius) * width + kx - radius;
                if (alpha[target] == 0 && glow[target] < opacity)
                    glow[target] = opacity;
            }
        }

        var pixels = new Color32[glow.Length];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(opaque, opaque, opaque, glow[i]);
        _outlineTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _outlineTexture.SetPixels32(pixels);
        _outlineTexture.Apply(false, true);
        _outlineSource = _hitMaskSource;
        _crossfireOnTarget.sprite = Sprite.Create(_outlineTexture, new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        RectTransform rect = _crossfireOnTarget.rectTransform;
        rect.anchorMin = new Vector2(-radius / (float)bodyWidth, -radius / (float)bodyHeight);
        rect.anchorMax = Vector2.one - rect.anchorMin;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ReleaseTargetOutline()
    {
        if (_crossfireOnTarget != null && _crossfireOnTarget.sprite != null)
        {
            Destroy(_crossfireOnTarget.sprite);
            _crossfireOnTarget.sprite = null;
        }
        if (_outlineTexture != null)
            Destroy(_outlineTexture);
        _outlineTexture = null;
        _outlineSource = null;
    }

    private void ShowGunFlash()
    {
        if (gunFlash == null)
            return;
        HideGunFlash();
        gunFlash.gameObject.SetActive(true);
        _gunFlashRoutine = StartCoroutine(GunFlashRoutine());
    }

    private IEnumerator GunFlashRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        gunFlash.gameObject.SetActive(false);
        _gunFlashRoutine = null;
    }

    private void HideGunFlash()
    {
        if (_gunFlashRoutine != null)
        {
            StopCoroutine(_gunFlashRoutine);
            _gunFlashRoutine = null;
        }
        if (gunFlash != null)
            gunFlash.gameObject.SetActive(false);
    }
}
