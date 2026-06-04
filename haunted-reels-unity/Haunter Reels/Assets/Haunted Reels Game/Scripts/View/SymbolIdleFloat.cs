using DG.Tweening;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Animação idle de flutuação para símbolos sem Spine.
/// Adicione este componente ao prefab do símbolo — ele se auto-desativa
/// se detectar um SkeletonGraphic no GameObject.
/// </summary>
public class SymbolIdleFloat : MonoBehaviour
{
    [SerializeField] [Range(0f, 20f)] private float _floatDistance = 5f;
    [SerializeField] [Range(0.3f, 4f)] private float _duration     = 1.5f;

    private Vector3 _originLocalPos;
    private Tween   _tween;
    private bool    _hasSpine;

    private void Awake()
    {
        _hasSpine       = GetComponent<SkeletonGraphic>() != null
                       || GetComponentInChildren<SkeletonGraphic>() != null;
        _originLocalPos = transform.localPosition;
    }

    private void OnEnable()
    {
        if (_hasSpine) return;
        _originLocalPos = transform.localPosition;
        PlayFloat();
    }

    private void OnDisable()
    {
        StopFloat();
    }

    private void OnDestroy()
    {
        StopFloat();
    }

    private void PlayFloat()
    {
        _tween?.Kill();
        transform.localPosition = _originLocalPos;
        _tween = transform
            .DOLocalMoveY(_originLocalPos.y + _floatDistance, _duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopFloat()
    {
        _tween?.Kill();
        _tween = null;
        if (this != null)
            transform.localPosition = _originLocalPos;
    }
}
