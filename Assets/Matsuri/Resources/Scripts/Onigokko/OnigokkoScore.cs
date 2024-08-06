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
public class OnigokkoScore : MonoBehaviourPunCallbacks
{
    private TextMeshProUGUI scoreTmp;
    private List<KeyValuePair<string, int>> playerScores = new List<KeyValuePair<string, int>>();
    private int lastOniViewId = -1;
    // リーダーボード名
    public string leaderboardName = "鬼ごっこランキング";
    // 最終スコアパネル
    public GameObject finalScorePanel;

    // ゲームが開始してるかどうかのフラグ
    public bool isStartGame = false;

    // 1秒カウント用
    private float countTime;

    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
    scoreTmp = GetComponentInChildren<TextMeshProUGUI>();
    countTime = 0;
    }

    // スコア更新
    void Update()
    {
        // ゲーム開始してない場合
        if (!isStartGame)
        {
            return;
        }

        countTime += Time.deltaTime;

        // 1秒経ってない場合
        if (countTime < 1)
        {
            return;
        }

        // プレイヤーオブジェクトのViewIDが取得出来ない場合
        object playerObjectIDs;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("playerObjectIDs", out playerObjectIDs))
        {
            return;
        }

        // ViewID分ループする
        int[] ids = (int[])playerObjectIDs;
        foreach (int id in ids)
        {
            PhotonView view = PhotonView.Find(id);

            // viewが取得できない場合
            if (view == null)
            {
                Debug.LogWarning($"ViewID:{id}のオブジェクトが見つかりません");
                continue;
            } 

            // オブジェクトのviewのオーナーが存在する場合
            if (view.Owner != null)
            {
                string playerName = view.Owner.NickName;
                // プレイヤーがリストに存在するかチェックし、存在しなければ追加
                var entry = playerScores.Find(e => e.Key == playerName);
                if (entry.Equals(default(KeyValuePair<string, int>)))
                {
                    playerScores.Add(new KeyValuePair<string, int>(playerName, 0));
                }
                else
                {
                    // 鬼のスコアをインクリメント
                    if (view.ViewID == lastOniViewId)
                    {
                        int newScore = entry.Value + 1;
                        playerScores.Remove(entry);
                        playerScores.Add(new KeyValuePair<string, int>(playerName, newScore));
                    }
                }
            }
            else
            {
                Debug.LogWarning($"ViewID:{id}のオブジェクトのオーナーがいません。");
            }
        }

        // スコアの辞書をテキストオブジェクトに反映 
        scoreTmp.text = "鬼になった秒数\n";

        // entry.Valueを降順でソートする
        playerScores.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

        // スコアテキストを更新
        foreach (KeyValuePair<string, int> entry in playerScores)
        {
            scoreTmp.text += $"{entry.Key}: {entry.Value}秒\n";
        }

        // カウントリセット
        countTime = 0;
    }

    // カスタムプロパティに変更があれば、呼ばれる
    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        // 鬼が切り換われば
        if (propertiesThatChanged.ContainsKey("oniViewId"))
        {
            lastOniViewId = (int)propertiesThatChanged["oniViewId"];
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
            finalScoreText.text += $"\n{entry.Key}: {entry.Value}秒";
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
