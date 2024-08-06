using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class DodgeBallTimerDisplay : MonoBehaviour {

    // ゲームの制限時間
    public float limitSeconds = 60.0f;
    private int initialLimitSeconds;
    // ゲーム開始直後の秒数
    private int gameStartedSeconds;
    // ゲーム開始前のカウントダウンの時間
    public float gameStartCountdownSeconds = 5.0f;

    private TextMeshProUGUI timerTmp;
    public GameObject scoreObject;
    private DodgeBallScore scoreScript;

    // ゲーム開始前のカウントダウンのフラグ
    public bool gameStartCountdown = false;

	// ゲームが開始してるかどうかのフラグ
	public bool isStartGame = false;

    // タイマーを止めるフラグ
    public bool isStopTimer = false;

    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start () {
        initialLimitSeconds = (int) limitSeconds;
        gameStartedSeconds = initialLimitSeconds - 3;
        timerTmp = GetComponentInChildren<TextMeshProUGUI>();
        timerTmp.text = "マッチメイキング中...";
        scoreScript = scoreObject.GetComponent<DodgeBallScore>();
    }

    // タイマーを止める公開メソッド
    public void StopTimer()
    {
        isStopTimer = true;
        HandleStopTimer();
    }

    // タイマーがストップされたときの処理
    private void HandleStopTimer()
    {
        // 経過時間をもとにスコア集計
        int elapsedTime = initialLimitSeconds - (int)limitSeconds;
        scoreScript.SummaryScore(elapsedTime);

        // Timerパネルを非表示にする
        gameObject.SetActive(false);
    }

    void Update()
    {
        // ゲーム開始前のカウントダウン
        if(gameStartCountdown)
        {
            if(gameStartCountdownSeconds > 0)
            {
                int countdownSecond = (int)gameStartCountdownSeconds;
                timerTmp.text = $"{countdownSecond}秒後に、ドッヂボールスタートします。";

                // 1秒をマイナス
                gameStartCountdownSeconds -= Time.deltaTime;
                
            }
            else
            {
                gameStartCountdown = false;
            }
        }

		// ゲーム開始してる場合
		if (isStartGame)
		{
            // 制限時間が0より大きい場合
            if (limitSeconds > 0)
            {
                // 1秒をマイナス
                limitSeconds -= Time.deltaTime;

                // 制限時間が0秒以下にならないように制限
                limitSeconds = Mathf.Max(0, limitSeconds);

                // 分（minute）と秒（seconds）を再計算
                int minute = (int)limitSeconds / 60;
                float seconds = limitSeconds % 60;

                timerTmp.text = string.Format("{0:00}:{1:00}", minute, seconds);
            }

            // スタート直後
            if ((int)limitSeconds >= gameStartedSeconds)
            {
                timerTmp.text += "\nドッヂボールスタート！！";
            }

            // 制限時間が0秒以下になった場合
            if (limitSeconds <= 0f && !isStopTimer)
            {
                timerTmp.text += "\nドッヂボール終了！！";
                StopTimer();
            }
		}

		
    }
}
