using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Pressing Play in the Editor always starts from the MainMenu scene (like the built game),
/// whichever scene is open. Toggle with FPS/Play From Main Menu.
/// </summary>
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    const string MenuPath = "Assets/Scenes/MainMenu.unity";
    const string PrefKey = "FPS_PlayFromMainMenu";
    const string MenuItemPath = "FPS/Play From Main Menu";

    static PlayFromMainMenu() => EditorApplication.delayCall += Apply;

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) : null;
    }

    [MenuItem(MenuItemPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuItemPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuItemPath, Enabled);
        return true;
    }
}
