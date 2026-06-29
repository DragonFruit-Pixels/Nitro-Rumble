using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ClashHUD : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject _root;

    [Header("Bars")]
    [SerializeField] private Image        _localBarFill;
    [SerializeField] private Image        _opponentBarFill;
    [SerializeField] private RectTransform _localBarContainer;
    [SerializeField] private RectTransform _opponentBarContainer;

    [Header("Space Prompt")]
    [SerializeField] private RectTransform    _spaceKeyRT;
    [SerializeField] private RectTransform    _arrow0RT;
    [SerializeField] private RectTransform    _arrow1RT;
    [SerializeField] private RectTransform    _arrow2RT;
    [SerializeField] private TextMeshProUGUI  _timerText;

    private CarClashMinigame _localClash;
    private CarClashMinigame _opponentClash;
    private float _elapsed;
    private float _clashDuration;
    private Coroutine _punchRoutine;

    private void Awake()
    {
        if (_root != null) _root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void Show(CarClashMinigame local, CarClashMinigame opponent)
    {
        _localClash    = local;
        _opponentClash = opponent;
        _elapsed       = 0f;
        _clashDuration = local != null ? local.Duration : 3f;

        SetBarFill(_localBarFill,    0f);
        SetBarFill(_opponentBarFill, 0f);

        if (_localBarContainer != null)
            _localBarContainer.localScale = Vector3.one;
        if (_opponentBarContainer != null)
            _opponentBarContainer.localScale = Vector3.one;

        if (_root != null) _root.SetActive(true);
    }

    public void Hide()
    {
        _localClash    = null;
        _opponentClash = null;
        if (_root != null) _root.SetActive(false);
    }

    public void PunchLocalBar()
    {
        if (_localBarContainer == null) return;
        if (_punchRoutine != null) StopCoroutine(_punchRoutine);
        _punchRoutine = StartCoroutine(PunchRoutine(_localBarContainer));
    }

    // ── Unity ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_localClash == null || !_localClash.IsInClash)
        {
            if (_root != null && _root.activeSelf)
                Hide();
            return;
        }

        _elapsed += Time.deltaTime;

        SetBarFill(_localBarFill,    _localClash.Progress);
        SetBarFill(_opponentBarFill, _opponentClash != null ? _opponentClash.Progress : 0f);

        float remaining = Mathf.Max(0f, _clashDuration - _elapsed);
        if (_timerText != null)
            _timerText.text = remaining.ToString("F1");

        AnimateSpaceKey();
        AnimateArrows();
    }

    // ── Animations ────────────────────────────────────────────────────────

    private void AnimateSpaceKey()
    {
        if (_spaceKeyRT == null) return;
        float rock  = Mathf.Sin(Time.time * 3.0f) * 12f;
        float pulse = 1f + Mathf.Sin(Time.time * 5.5f) * 0.12f;
        _spaceKeyRT.localRotation = Quaternion.Euler(0f, 0f, rock);
        _spaceKeyRT.localScale    = Vector3.one * pulse;
    }

    private void AnimateArrows()
    {
        AnimateSingleArrow(_arrow0RT, 0);
        AnimateSingleArrow(_arrow1RT, 1);
        AnimateSingleArrow(_arrow2RT, 2);
    }

    private static void AnimateSingleArrow(RectTransform rt, int index)
    {
        if (rt == null) return;
        float phase = index * (Mathf.PI * 2f / 3f);
        float bob = Mathf.Sin(Time.time * 7f + phase) * 10f;
        Vector2 pos = rt.anchoredPosition;
        pos.y = bob;
        rt.anchoredPosition = pos;
    }

    private IEnumerator PunchRoutine(RectTransform rt)
    {
        Vector3 normal = Vector3.one;
        Vector3 squish = new Vector3(1.06f, 0.62f, 1f);

        float t = 0f;
        while (t < 0.07f)
        {
            rt.localScale = Vector3.Lerp(normal, squish, t / 0.07f);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < 0.14f)
        {
            rt.localScale = Vector3.Lerp(squish, normal, t / 0.14f);
            t += Time.deltaTime;
            yield return null;
        }

        rt.localScale    = normal;
        _punchRoutine    = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Anchor-based fill: moves the right anchor of the image rect from 0 (empty) to 1 (full).
    // Does NOT depend on Image.type or fillAmount — works with Simple images.
    private static void SetBarFill(Image img, float value)
    {
        if (img == null) return;
        var rt  = img.rectTransform;
        var max = rt.anchorMax;
        max.x   = Mathf.Clamp01(value);
        rt.anchorMax  = max;
        rt.offsetMax  = Vector2.zero;
        rt.offsetMin  = Vector2.zero;
    }
}
