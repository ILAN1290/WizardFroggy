using UnityEngine;
using UnityEngine.UI;

public class MainMenuLoadButton : MonoBehaviour
{
    private void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OpenLoadMenu);
        }

        Image image = GetComponent<Image>();
        if (image != null && image.sprite == null)
            image.color = new Color(0.34f, 0.23f, 0.15f, 1f);

        if (GetComponentInChildren<Text>() == null)
            CreateLabel();
    }

    private void OpenLoadMenu()
    {
        if (SaveGameSystem.Instance != null)
            SaveGameSystem.Instance.OpenLoadMenu();
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);

        Text label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = "ЗАГРУЗИТЬ ИГРУ";
        label.fontSize = 36;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
    }
}
