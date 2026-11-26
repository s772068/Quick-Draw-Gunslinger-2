using System.Collections.Generic;
using UnityEngine;

public partial class RoundController
{
    private readonly List<(RectTransform Panel, Vector2 Position, Vector3 Scale)> _duelDialogs = new();
    private SafeAreaPadding _safeAreaPadding;
    private Rect _lastDuelView;
    private bool _duelLayoutWasLandscape;
    private RectTransform _enemyLayout;
    private RectTransform _crossfireLayout;
    private Vector3 _enemyDesignScale;
    private Vector3 _crossfireDesignScale;
    private Vector3 _gunDesignScale;
    private Vector3 _gunPortraitPosition;
    private RectTransform _trainingPanel;
    private Vector2 _trainingPortraitPosition;
    private float _duelLayoutScale = 1f;

    private void CaptureDuelLayout()
    {
        _enemyLayout = enemyBody != null ? enemyBody.parent as RectTransform : null;
        if (_enemyLayout != null)
            _enemyDesignScale = _enemyLayout.localScale;

        _crossfireLayout = crossfire != null ? crossfire.parent as RectTransform : null;
        if (_crossfireLayout != null)
            _crossfireDesignScale = _crossfireLayout.localScale;

        if (playerGunRect != null)
        {
            _gunDesignScale = playerGunRect.localScale;
            _gunPortraitPosition = _gunRestLocalPos;
        }

        foreach (GameObject dialog in new[]
        {
            roundStartPanel, roundFinishPanel, roundWinPanel, roundLosePanel,
            levelCompletePanel, areYouSureAbortPanel, pauseMenuRoot
        })
        {
            if (dialog == null)
                continue;

            var panel = (dialog.transform.Find("Panel") ?? dialog.transform) as RectTransform;
            if (panel != null && panel.anchorMin == panel.anchorMax)
                _duelDialogs.Add((panel, panel.anchoredPosition, panel.localScale));
        }

        if (_trainingModal != null)
        {
            _trainingPanel = _trainingModal.transform.Find("Panel") as RectTransform;
            _trainingPortraitPosition = ((RectTransform)_trainingModal.transform).anchoredPosition;
        }

        ApplyDuelLayout();
    }

    private void ApplyDuelLayout()
    {
        if (_safeAreaPadding == null)
            return;

        bool landscape = _safeAreaPadding.IsLandscape;
        Rect view = _safeAreaPadding.UsableRect;
        if (view.width <= 1f || view.height <= 1f ||
            (landscape == _duelLayoutWasLandscape && (!landscape || view == _lastDuelView)))
            return;

        _duelLayoutWasLandscape = landscape;
        _lastDuelView = view;

        // Scale the opponent and reticle together. Their local aim path and hit geometry
        // stay valid during a resize, including when aiming is paused.
        _duelLayoutScale = 1f;
        if (landscape && _enemyLayout != null && _crossfireLayout != null)
        {
            float aimExtent = Mathf.Abs(AimTrajectory.MiddlePointLocal(enemyBody).y)
                              + CurrentAimRadius() * 1.1f + crossfire.rect.height * 0.5f;
            float halfHeight = Mathf.Max(1f, Mathf.Min(view.yMax, -view.yMin));
            _duelLayoutScale = Mathf.Min(1f, halfHeight / Mathf.Max(enemyBody.rect.height * 0.5f, aimExtent));
        }

        if (_enemyLayout != null)
            _enemyLayout.localScale = _enemyDesignScale * _duelLayoutScale;
        if (_crossfireLayout != null)
            _crossfireLayout.localScale = _crossfireDesignScale * _duelLayoutScale;

        if (playerGunRect != null)
        {
            float scale = landscape ? Mathf.Min(1f, view.height / 1200f) : 1f;
            Vector3 position = _gunPortraitPosition;
            if (landscape)
            {
                position.x = view.center.x + view.width * 0.24f;
                position.y = view.yMin - playerGunRect.rect.height * scale * 0.13f;
            }

            Vector3 animationOffset = playerGunRect.localPosition - _gunRestLocalPos;
            _gunRestLocalPos = position;
            playerGunRect.localPosition = _gunRestLocalPos + animationOffset;
            playerGunRect.localScale = _gunDesignScale * scale;
        }

        foreach (var (panel, position, designScale) in _duelDialogs)
        {
            Vector2 size = Vector2.Scale(panel.rect.size, new Vector2(designScale.x, designScale.y));
            float scale = landscape ? Mathf.Min(1f, view.width / size.x, view.height / size.y) : 1f;
            panel.localScale = designScale * scale;
            Vector2 halfSize = size * (scale * 0.5f);
            panel.anchoredPosition = landscape ? new Vector2(
                Mathf.Clamp(position.x, view.xMin + halfSize.x, view.xMax - halfSize.x),
                Mathf.Clamp(position.y, view.yMin + halfSize.y, view.yMax - halfSize.y)) : position;
        }

        if (_trainingPanel != null)
        {
            Vector2 position = _trainingPortraitPosition;
            if (landscape)
            {
                // Keep the instructions beside the target and above the action controls.
                Vector2 center = view.center + new Vector2(-view.width * 0.25f, Mathf.Min(80f, view.height * 0.1f));
                position = center - _trainingPanel.anchoredPosition;
            }

            ((RectTransform)_trainingModal.transform).anchoredPosition = position;
        }
    }
}
