using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;  
using System.Linq;
using ExitGames.Client.Photon;
using UnityEngine.SceneManagement;

/// <summary>
/// 鬼ごっこのシーンを制御するクラス
/// </summary>
public class OnigokkoScene : MonoBehaviourPunCallbacks
{
    // プレイヤーオブジェクトのプレハブ設定
    public GameObject playerPrefab;

    // カメラ
    public GameObject subCamera;

    // マテリアルを保持するための変数
    public Material oniMaterial;
    public Material runnerMaterial;

    // シーンに参加できる最小/最大人数
    int minPlayers = 2;
    int maxPlayers = 6;

    // 初期位置の範囲設定
    Vector3 minPosition = new Vector3(-10f, 0, -10f);
    Vector3 maxPosition = new Vector3(10f, 0, 10f);
    
    // ランダムな向き
    Quaternion randomRotation;

    // プレイヤーオブジェクトの配列
    GameObject[] playerObjs;

    // マッチメイキングの待ち時間（デフォルト10秒）
    public int matchMakingTime = 10;

    // ゲーム開始前のカウントダウンの時間
    public int gameStartCountdownSeconds = 5;

    // タイマースクリプト
    OnigokkoTimerDisplay timerDisplay;
    // スコアスクリプト
    OnigokkoScore scoreScript;
    // プレイヤーオブジェクト
    GameObject playerObject;

    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
        // デフォルトの同期頻度（20秒/回）だと、他のプレイヤーがラグいため、調整した
        // SendRate を80に設定
        PhotonNetwork.SendRate = 60;
        // SerializationRate を80に設定
        PhotonNetwork.SerializationRate = 60;

        // マルチプレイヤー対応
        PhotonNetwork.ConnectUsingSettings();

        // ランダムなY軸回転を生成
        float randomYRotation = Random.Range(0f, 360f);
        randomRotation = Quaternion.Euler(0, randomYRotation, 0);
    }

    // サーバ接続後に呼び出される
    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    // ルーム入室失敗時に、呼び出される
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // 新しいルームを作成する
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = maxPlayers;
        PhotonNetwork.CreateRoom(null, roomOptions);
        Debug.Log("新しいルームを作成しました");
    }

    // ルーム入室時に、呼び出される
    public override void OnJoinedRoom()
    {
        Debug.Log("ルームに入室しました");
        // 非同期でオブジェクト生成
        Vector3 spawnPosition = GetRandomPosition(minPosition, maxPosition);
        playerObject = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, randomRotation);
        Debug.Log($"{PhotonNetwork.LocalPlayer.UserId}の、プレイヤーオブジェクトを生成しました。");
    }

    private Vector3 GetRandomPosition(Vector3 minPosition, Vector3 maxPosition)
    {
        float randomX = Random.Range(minPosition.x, maxPosition.x);
        float randomY = Random.Range(minPosition.y, maxPosition.y);
        float randomZ = Random.Range(minPosition.z, maxPosition.z);
        return new Vector3(randomX, randomY, randomZ);
    }

    // ルーム退室したときに、呼び出される
    public override void OnLeftRoom()
    {
        Debug.Log("ルームから退室します");
        // ルームからの切断（これをしないと、次のルームでオブジェクト生成されない）
        PhotonNetwork.Disconnect();
    }

    // ルームとの接続が切れた場合
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log("ルームから切断しました: " + cause.ToString());
        // メインメニューシーンに戻る
        SceneManager.LoadScene("MainMenu");
    }

    // 他のプレイヤーが、ルームに参加したときに呼び出される
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // プレイヤーの参加ログ
        Debug.Log($"他のプレイヤー：{newPlayer.NickName}が参加しました");

        // 待機処理の後、ゲーム開始
        StartCoroutine(WaitAndStartGame());
    }

    private IEnumerator WaitAndStartGame()
    {
        // マッチメイキングの待機
        yield return new WaitForSeconds(matchMakingTime);

        // 以降、途中参加不可能にする
        PhotonNetwork.CurrentRoom.IsOpen = false;

        // ルーム内のプレイヤー数が最少人数以上であれば
        if (PhotonNetwork.CurrentRoom.PlayerCount >= minPlayers) 
        {
            // マスタークライアントでのみ実行
            if (PhotonNetwork.IsMasterClient)
            {
                // 鬼の選定
                SelectOni();
            }
            // ゲーム開始の処理をここに記述
            Debug.Log("すべてのプレイヤーが準備完了。ゲーム開始！");

            // カメラ/タイマー/スコアボードの制御（マスタークライアント&それ以外のクライアントで、通信を介してスクリプト実行）
            photonView.RPC(nameof(StartGame), RpcTarget.AllViaServer);
        }
    }

    // 鬼ごっこの開始
    [PunRPC]
    public void StartGame()
    {
        StartCoroutine(StartGameCoroutine());
    }
    
    // ゲームスタート処理を行うコルーチン
    IEnumerator StartGameCoroutine()
    {
        // タイマースクリプト取得
        GameObject timerPanel = GameObject.Find("TimerPanel");
        if (timerPanel == null)
        {
            Debug.LogError("TimerPanelが見つかりません");
        }
        timerDisplay = timerPanel.GetComponent<OnigokkoTimerDisplay>();
        if (timerDisplay == null)
        {
            Debug.LogError("TimerDisplayのスクリプトが見つかりません");
        }

        // タイマースクリプト取得
        GameObject scorePanel = GameObject.Find("ScorePanel");
        if (scorePanel == null)
        {
            Debug.LogError("ScorePanelが見つかりません");
        }
        scoreScript = scorePanel.GetComponent<OnigokkoScore>();
        if (scoreScript == null)
        {
            Debug.LogError("Scoreのスクリプトが見つかりません");
        }

        // カウントダウンタイマーのフラグ設定
        timerDisplay.gameStartCountdown = true;

        //　ゲームスタート前のカウントダウン時間の設定
        yield return new WaitForSeconds(gameStartCountdownSeconds);

        // 待機カメラを非アクティブ（プレイヤーカメラに切り替わる）
        subCamera.SetActive(false);

        // タイマー/スコア開始
        timerDisplay.isStartGame = true;
        scoreScript.isStartGame = true;

        // プレイヤーオブジェクトのスクリプトを取得
        object playerObjectIDs;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("playerObjectIDs", out playerObjectIDs))
        {
            Debug.LogError("プレイヤーオブジェクトが見つかりません。");
            yield break;
        }
        // プレイヤーを移動可能にする
        int[] ids = (int[])playerObjectIDs;
        foreach (int id in ids)
        {
            PhotonView view = PhotonView.Find(id);
            if (view != null)
            {
                OnigokkoPlayerScript playerScript = view.gameObject.GetComponent<OnigokkoPlayerScript>();
                playerScript.move = true;
            }
        }
    }


    // 他プレイヤーがルームから退出した時に呼ばれるコールバック
    public override void OnPlayerLeftRoom(Player otherPlayer) {
        Debug.LogWarning($"他のプレイヤー：{otherPlayer.NickName}が退出しました");

        // ルームにプレイヤーが自分しかいない場合、ルームから離脱
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            Debug.LogWarning("他のプレイヤーがいないため、ルームから離脱します。");
            // タイマー強制ストップ
            timerDisplay.StopTimer();
            return;
        }

        // 今の鬼のオーナーを取得
        int oniViewId = (int)PhotonNetwork.CurrentRoom.CustomProperties["oniViewId"];
        PhotonView leftOniView = PhotonView.Find(oniViewId);
        Player LeftOniOwner;
        // 今の鬼のViewを取得できれば
        if (leftOniView != null)
        {
            // オーナー情報を取得
            LeftOniOwner = leftOniView.Owner;
        }
         // 今の鬼のViewを取得できなければ
        else
        {
            Debug.LogWarning("鬼のプレイヤーが離脱しました。ゲームを終了します。");
            // タイマー強制ストップ
            timerDisplay.StopTimer();
            return;
        }

        // 今の鬼が退出した場合
        if (otherPlayer == LeftOniOwner)
        {
            // マスタークライアントでのみ実行
            if (PhotonNetwork.IsMasterClient)
            {
                Debug.LogWarning("鬼のプレイヤーが離脱したため、鬼を再選定します。");
                // 鬼の選定
                SelectOni();
            }
        }
    }

    // 指定した秒数、待機するコルーチン
    IEnumerator WaitForSecondsCoroutine(int seconds)
    {
        Debug.Log("Wait starts");
        // マッチメイキングの待機
        yield return new WaitForSeconds(seconds);
        Debug.Log($"{seconds} seconds have passed");
    }

    // 鬼の選定
    private void SelectOni()
    {
        // 全プレイヤーオブジェクトを取得
        playerObjs = GameObject.FindGameObjectsWithTag("Player");

        // 全プレイヤーオブジェクトのViewIDを、カスタムプロパティに格納しておく
        ExitGames.Client.Photon.Hashtable playerIDs = new ExitGames.Client.Photon.Hashtable();
        int[] viewIDs = playerObjs.Select(obj => obj.GetComponent<PhotonView>().ViewID).ToArray();
        playerIDs["playerObjectIDs"] = viewIDs;                
        PhotonNetwork.CurrentRoom.SetCustomProperties(playerIDs);
        Debug.Log("プレイヤーViewID一覧: " + string.Join(", ", viewIDs));

        // プレイヤーオブジェクトからランダムに1人を鬼として選ぶ
        GameObject selectedPlayerObj = playerObjs[Random.Range(0, playerObjs.Length)];
        PhotonView selectedPlayerView = selectedPlayerObj.GetComponent<PhotonView>();

        // 鬼のプレイヤーオブジェクトのIDを保持
        int initialOniViewId = selectedPlayerView.ViewID;
        
        // ルーム内の鬼のViewIDをカスタムプロパティとして保持
        var roomProperties = new ExitGames.Client.Photon.Hashtable();
        roomProperties["oniViewId"] = initialOniViewId;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

        // すべてのプレイヤーオブジェクトに対して処理
        foreach (GameObject playerObj in playerObjs)
        {
            PhotonView view = playerObj.GetComponent<PhotonView>();
            OnigokkoPlayerScript playerScript = playerObj.GetComponent<OnigokkoPlayerScript>();

            if (view.ViewID == initialOniViewId)
            {
                // 鬼の設定
                playerScript.isOni = true;
            }
        }

        // 鬼のオブジェクトの色変更（マスタークライアント&それ以外のクライアントで、通信を介してスクリプト実行）
        photonView.RPC(nameof(ControllOniColor), RpcTarget.AllViaServer, initialOniViewId);
    }

    // 鬼のオブジェクトの色変更(全クライアントで実行) 
    [PunRPC]
    public void ControllOniColor(int oniViewId)
    {
        object playerObjectIDs;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("playerObjectIDs", out playerObjectIDs))
        {
            Debug.LogError("プレイヤーオブジェクトが見つかりません。");
            return;
        }

        int[] ids = (int[])playerObjectIDs;
        foreach (int id in ids)
        {
            PhotonView view = PhotonView.Find(id);
            if (view != null)
            {
                GameObject playerObject = view.gameObject;
                // "CH09"子オブジェクトのSkinned Mesh Rendererを取得
                SkinnedMeshRenderer skinnedMeshRenderer = playerObject.GetComponentInChildren<SkinnedMeshRenderer>();

                if (skinnedMeshRenderer != null)
                {
                    // SkinnedMeshRendererから現在のマテリアル配列を取得
                    Material[] materials = skinnedMeshRenderer.materials;

                    // 鬼のマテリアル名の修正
                    string fixMaterialName = oniMaterial.name + " (Instance)";

                    // 次の鬼のマテリアル設定
                    if (view.ViewID == oniViewId)
                    {
                        // 鬼のマテリアルを設定
                        materials[0] = oniMaterial;
                    }
                    // 前の鬼のマテリアル設定
                    else if (materials[0].name == fixMaterialName)
                    {
                        // ランナーのマテリアルを設定
                        materials[0] = runnerMaterial;
                    }

                    // ここでSkinnedMeshRendererのmaterialsプロパティに更新された配列を再設定
                    skinnedMeshRenderer.materials = materials;
                }
                else
                {
                    Debug.LogError("SkinnedMeshRendererが見つかりません。");
                    return;
                }
            }
        }
    }
}
