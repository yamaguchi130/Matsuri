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

    // viewID、プレイヤーネーム、プレイヤースコアを格納する辞書
    private Dictionary<int, KeyValuePair<string, int>> playerInfoDictionary = new Dictionary<int, KeyValuePair<string, int>>();

    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
       scoreTmp = GetComponentInChildren<TextMeshProUGUI>();
    }

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

        // チームごとのViewIDsをルームプロパティから取得
        int[] aTeamViewIDs = (int[])PhotonNetwork.CurrentRoom.CustomProperties["aTeamViewIDs"];
        int[] bTeamViewIDs = (int[])PhotonNetwork.CurrentRoom.CustomProperties["bTeamViewIDs"];

        // スコアをクリア
        playerInfoDictionary.Clear();

        // AチームとBチームのスコア合計を保持する変数
        int aTeamTotalScore = 0;
        int bTeamTotalScore = 0;

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

                // 辞書にviewID、プレイヤーネーム、プレイヤースコアを追加
                playerInfoDictionary[viewId] = new KeyValuePair<string, int>(playerName, playerScore);

                // チームを判別し、チームごとのスコアに加算
                if (aTeamViewIDs.Contains(viewId))
                {
                    aTeamTotalScore += playerScore; // Aチームの合計スコアを加算
                }
                else if (bTeamViewIDs.Contains(viewId))
                {
                    bTeamTotalScore += playerScore; // Bチームの合計スコアを加算
                }
            }
        }

        // スコアテキストの初期化
        scoreTmp.text = "スコアボード\n";

        // Aチームのスコア表示
        scoreTmp.text += "Aチーム合計スコア: " + aTeamTotalScore + "点\n";
        scoreTmp.text += "個人スコア:\n";
        foreach (int viewId in aTeamViewIDs)
        {
            if (playerInfoDictionary.ContainsKey(viewId))
            {
                var playerInfo = playerInfoDictionary[viewId];
                scoreTmp.text += $"{playerInfo.Key}: {playerInfo.Value}点\n";
            }
        }

        // Bチームのスコア表示
        scoreTmp.text += "\nBチーム合計スコア: " + bTeamTotalScore + "点\n";
        scoreTmp.text += "個人スコア:\n";
        foreach (int viewId in bTeamViewIDs)
        {
            if (playerInfoDictionary.ContainsKey(viewId))
            {
                var playerInfo = playerInfoDictionary[viewId];
                scoreTmp.text += $"{playerInfo.Key}: {playerInfo.Value}点\n";
            }
        }

        // デバッグログに結果を表示
        Debug.Log($"Aチーム合計スコア: {aTeamTotalScore}, Bチーム合計スコア: {bTeamTotalScore}");
    }

    // 最終スコア集計
    public void SummaryScore(int timeLimitSeconds)
    {
        // ローカルプレイヤーのviewIDを取得
        int localPlayerViewID = PhotonNetwork.LocalPlayer.ActorNumber; // ローカルプレイヤーのviewIDを取得

        // プレイヤーのviewIDが辞書に存在するか確認
        if (!playerInfoDictionary.ContainsKey(localPlayerViewID)) 
        {
            Debug.LogWarning("ローカルプレイヤーのスコアが見つかりません。");
            return;
        }

        // プレイヤーのスコアとランクを取得
        string localPlayerName = PhotonNetwork.LocalPlayer.NickName;
        int playerScore = playerInfoDictionary[localPlayerViewID].Value;

        // ランキングに応じた基準値
        float[] rankingStandardValueArray = new float[] {1f, 0.86f, 0.72f, 0.58f, 0.44f, 0.30f};

        // スコア調整係数（この値は調整が必要）
        float scoreAdjustmentFactor = 100.0f; // 生存時間をスケールアップするための係数

        // ソートされたプレイヤースコアのリストを作成
        var sortedScores = playerInfoDictionary.OrderByDescending(pair => pair.Value.Value).ToList();

        // ランクを取得
        int rank = sortedScores.FindIndex(e => e.Key == localPlayerViewID);

        // ランキングの範囲外の場合は処理を終了
        if (rank >= rankingStandardValueArray.Length)
        {
            Debug.LogWarning("ランクが基準値配列の範囲外です。");
            return;
        }

        // 自分の生存時間を算出
        int timeSurvived = timeLimitSeconds - playerScore;

        // 自分の順位に基づいた追加ポイントを計算
        float rankBonus = (rankingStandardValueArray.Length - rank) * 10;

        // 自分の生存時間、基準値、順位ボーナスを乗算してポイントを計算
        float gamePoints = (timeSurvived * rankingStandardValueArray[rank] * scoreAdjustmentFactor) + rankBonus;
        int adjustedGamePoints = (int)gamePoints;

        // Playfabのアカウントにポイント付与
        AddPointsToPlayfabAccount(localPlayerName, adjustedGamePoints);

        // 最終スコアパネルを表示する
        finalScorePanel.SetActive(true);
        TextMeshProUGUI finalScoreText = finalScorePanel.GetComponentInChildren<TextMeshProUGUI>();

        // 最終スコアテキストを設定
        finalScoreText.text = "あなたの獲得ポイント：" + adjustedGamePoints.ToString() + "\n";

        // 全プレイヤーのスコアを表示
        foreach (var entry in sortedScores)
        {
            string playerName = entry.Value.Key;
            int score = entry.Value.Value;
            finalScoreText.text += $"{playerName}: {score}点\n";
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
