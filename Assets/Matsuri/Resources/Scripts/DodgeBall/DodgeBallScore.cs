using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;

/// <summary>
/// プレイヤースコアを制御するクラス
/// </summary>
public class DodgeBallScore : MonoBehaviourPunCallbacks
{
    private TextMeshProUGUI scoreTmp;
    private List<KeyValuePair<string, int>> playerScores = new List<KeyValuePair<string, int>>();

    // リーダーボード名
    public string leaderboardName = "ドッジボールランキング";
    // 最終スコアパネル
    public GameObject finalScorePanel;

    // ゲームが開始してるかどうかのフラグ
	public bool isStartGame = false;

    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
       scoreTmp = GetComponentInChildren<TextMeshProUGUI>();
    }

    // スコア更新
    void Update()
    {}


    // ルームプロパティに変更があれば、呼ばれる
    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        // ゲーム開始してない場合
        if (!isStartGame)
        {
            return;
        }

        // プレイヤーオブジェクトのViewIDが取得出来ない場合
        object playerObjectIDs;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("playerObjectIDs", out playerObjectIDs))
        {
            return;
        }

        // スコアをクリア
        playerScores.Clear();

        // ルームプロパティからスコアを取得
        int[] ids = (int[])playerObjectIDs;
        foreach (int viewId in ids)
        {
            PhotonView view = PhotonView.Find(viewId);

            // viewが見つからない場合
            if (view == null)
            {
                Debug.LogWarning($"ViewID:{viewId}に対応するPhotonViewが見つかりませんでした。");
                continue;
            }

            // オブジェクトのviewのオーナーが存在しない場合
            if (view.Owner == null)
            {
                Debug.LogWarning($"ViewID:{viewId}のオブジェクトのオーナーがいません。");
                continue;
            }

            // ルームプロパティからスコアを取得
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(viewId, out object scoreValue))
            {
                string playerName = view.Owner.NickName;
                int playerScore = (int)scoreValue;
                playerScores.Add(new KeyValuePair<string, int>(playerName, playerScore));
            }
        }

        // スコアの辞書をテキストオブジェクトに反映 
        scoreTmp.text = "得点\n";

        // entry.Valueを降順でソートする
        playerScores.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));

        // スコアテキストを更新
        foreach (KeyValuePair<string, int> entry in playerScores)
        {
            scoreTmp.text += $"{entry.Key}: {entry.Value}点\n";
        }
    }

    // 最終スコア集計
    public void SummaryScore(int timeLimitSeconds) {
        // プレイヤーのインデックスを取得
        int rank = playerScores.FindIndex(e => e.Key == PhotonNetwork.LocalPlayer.NickName);
        
        // プレイヤーが見つからない場合は処理を終了
        if (rank == -1) return;

        // プレイヤーのスコアを取得
        int playerScore = playerScores[rank].Value;

        // ランキングに応じた基準値
        float[] rankingStandardValueArray = new float[] {1f, 0.86f, 0.72f, 0.58f, 0.44f, 0.30f};

        // スコア調整係数（この値は調整が必要）
        float scoreAdjustmentFactor = 100.0f; // 生存時間をスケールアップするための係数

        // ランキングの範囲外の場合は処理を終了
        if (rank >= rankingStandardValueArray.Length) return;

        // 自分の生存時間を算出
        int timeSurvived = timeLimitSeconds - playerScore;

        // 自分の順位に基づいた追加ポイントを計算
        float rankBonus = (rankingStandardValueArray.Length - rank) * 10;

        // 自分の生存時間、基準値、順位ボーナスを乗算してポイントを計算
        float gamePoints = (timeSurvived * rankingStandardValueArray[rank] * scoreAdjustmentFactor) + rankBonus;
        int adjustedGamePoints = (int)gamePoints;

        // Playfabのアカウントにポイント付与
        AddPointsToPlayfabAccount(PhotonNetwork.LocalPlayer.NickName, adjustedGamePoints);

        // 最終スコアパネルを表示する
        finalScorePanel.SetActive(true);
        TextMeshProUGUI finalScoreText = finalScorePanel.GetComponentInChildren<TextMeshProUGUI>();
        
        // 最終スコアテキストを設定
        finalScoreText.text = "あなたの獲得ポイント：" + adjustedGamePoints.ToString();
        foreach (KeyValuePair<string, int> entry in playerScores)
        {
            finalScoreText.text += $"{entry.Key}: {entry.Value}点\n";
        }
    }

    // Playfabのリーダーボードにポイントを付与する
    void AddPointsToPlayfabAccount(string playerName, int points) {
        // PlayFabへポイントを追加するAPI呼び出し
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate { StatisticName = leaderboardName, Value = points }
            }
        };

        PlayFabClientAPI.UpdatePlayerStatistics(request, OnScoreSubmitSuccess, OnError);
        Debug.Log($"{playerName} は {points} ポイント取得しました.\nメインメニューに戻ります。");
    }

    // リーダーボードの更新成功
    private void OnScoreSubmitSuccess(UpdatePlayerStatisticsResult result)
    {
        Debug.Log("リーダーボードの更新成功");
    }

    // リーダーボードの更新失敗
    private void OnError(PlayFabError error)
    {
        Debug.LogError("リーダーボードの更新失敗: " + error.GenerateErrorReport());
    }
}
