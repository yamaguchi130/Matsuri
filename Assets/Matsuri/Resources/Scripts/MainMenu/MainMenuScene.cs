using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// メインメニューのシーンを制御するクラス
/// </summary>
public class MainMenuScene : MonoBehaviour
{
    // ゲームのボタンを押したとき
    public void ClickScene(string sceneName)
    {
        // シーンを同期的にロードする
        SceneManager.LoadScene(sceneName);
        Debug.Log($"{sceneName}のシーンをロードしました。");
    }

    // 管理するパネルの配列
    public GameObject[] panels;

    // 指定されたパネルを表示し、他のパネルを非表示にするメソッド
    public void ShowOnlyThisPanel(GameObject activePanel)
    {
        foreach (GameObject panel in panels)
        {
            if (panel == activePanel)
            {
                panel.SetActive(true);
            }
            else
            {
                panel.SetActive(false);
            }
        }
    }
}
