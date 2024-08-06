using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// 最終スコアを制御するクラス
/// </summary>
public class DodgeBallFinalScore : MonoBehaviourPunCallbacks
{
    // スコアパネル
    public GameObject scorePanel;

    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    { 
        // スコアパネルを非表示
        scorePanel.SetActive(false);
        // 15秒スリープしてメインメニューに戻る
        StartCoroutine(BackSceneCoroutine(15));
    }

    // シーン移動までのスリープ処理
    IEnumerator BackSceneCoroutine(float sleepMinutes)
    {
        // 15秒間待機
        yield return new WaitForSeconds(sleepMinutes);

        // 15秒後に実行したい処理をここに書く
        Debug.Log(sleepMinutes.ToString() + "秒スリープしました");

        // メインメニューに戻る
        BackScene("MainMenu");
    }

    // 特定のシーンに戻る
    private void BackScene(string sceneName) {
        // メインメニューシーンを同期的にロードする
        SceneManager.LoadScene(sceneName);
        Debug.Log($"{sceneName}のシーンに戻ります。");
    }
}
