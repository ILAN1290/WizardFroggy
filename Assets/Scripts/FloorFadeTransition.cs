using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FloorFadeTransition : MonoBehaviour
{
    [SerializeField] private float defaultDuration = 0.4f;

    private CanvasGroup fadeGroup;

    private void Awake()
    {
        CreateOverlay();
    }

    private void CreateOverlay()
    {
        if (fadeGroup != null)
            return;

        int uiLayer = LayerMask.NameToLayer("UI");

        GameObject canvasObject = new GameObject(
            "Tower Defence Fade Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        if (uiLayer >= 0)
            canvasObject.layer = uiLayer;

        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject(
            "Black Fade",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));

        if (uiLayer >= 0)
            panel.layer = uiLayer;

        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        fadeGroup = panel.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;
    }

    public IEnumerator FadeToBlack()
    {
        return FadeToBlack(defaultDuration);
    }

    public IEnumerator FadeToBlack(float duration)
    {
        if (fadeGroup == null)
            CreateOverlay();

        yield return Fade(1f, duration);
    }

    public IEnumerator FadeFromBlack()
    {
        return FadeFromBlack(defaultDuration);
    }

    public IEnumerator FadeFromBlack(float duration)
    {
        if (fadeGroup == null)
            CreateOverlay();

        yield return Fade(0f, duration);
    }

    // Вызывать при переходе на следующий этаж:
    // StartCoroutine(fade.TransitionBetweenFloors(() => LoadNextFloor()));
    public IEnumerator TransitionBetweenFloors(Action changeFloor)
    {
        yield return FadeToBlack();
        if (changeFloor != null)
            changeFloor.Invoke();
        yield return FadeFromBlack();
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (fadeGroup == null)
            CreateOverlay();

        float startAlpha = fadeGroup.alpha;
        if (duration <= 0f)
        {
            fadeGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fadeGroup.alpha = targetAlpha;
    }
}
