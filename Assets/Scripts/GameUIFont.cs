using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the project's shared TMP font to scene UI and exposes the matching
/// source Font to Unity's legacy IMGUI system.
/// </summary>
[DefaultExecutionOrder(-32000)]
public sealed class GameUIFont : MonoBehaviour
{
    private static GameUIFont instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var root = new GameObject("Game UI Font");
        root.AddComponent<GameUIFont>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += ApplyFontToScene;
    }

    private void Start()
    {
        ApplyFontToScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (instance != this) return;

        SceneManager.sceneLoaded -= ApplyFontToScene;
        instance = null;
    }

    private static void ApplyFontToScene(Scene scene, LoadSceneMode mode)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null) return;

        // Include inactive labels so menus and panels use the same font when shown.
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text == null || text.gameObject.scene != scene) continue;
            text.font = font;
        }
    }

    private void OnGUI()
    {
        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
        Font sourceFont = fontAsset != null ? fontAsset.sourceFontFile : null;
        if (sourceFont == null || GUI.skin == null) return;

        // IMGUI uses UnityEngine.Font rather than TMP_FontAsset. The source Font
        // belongs to TDAText SDF and preserves the same typeface for OnGUI menus.
        GUI.skin.font = sourceFont;
        GUI.skin.label.font = sourceFont;
        GUI.skin.button.font = sourceFont;
        GUI.skin.box.font = sourceFont;
        GUI.skin.textField.font = sourceFont;
        GUI.skin.textArea.font = sourceFont;
        GUI.skin.toggle.font = sourceFont;
    }
}
