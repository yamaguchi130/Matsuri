using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;  
using System.Linq;
using ExitGames.Client.Photon;

/// <summary>
/// ドッジボールのシーンを制御するクラス
/// </summary>
public class DodgeBallScene : MonoBehaviourPunCallbacks
{
    // プレイヤーオブジェクトのプレハブ設定
    public GameObject playerPrefab;

    // ボールオブジェクトのプレハブ設定
    public GameObject ballPrefab;

    // カメラ
    public GameObject subCamera;

    // マテリアルを保持するための変数
    public Material aTeamMaterial;
    public Material bTeamMaterial;

    // シーンに参加できる最小/最大人数
    int minPlayers = 2;
    int maxPlayers = 6;

    // プレイヤーの初期位置の範囲設定
    Vector3 initialMinPosition = new Vector3(-7.5f, 0, -15f);
    Vector3 initialMaxPosition = new Vector3(7.5f, 0, 15f);

    // ボールの初期位置
    Vector3 initialPosition = new Vector3(0, 5, 0);

    // ランダムな向き
    Quaternion randomRotation;

    // プレイヤーオブジェクトの配列
    GameObject[] playerObjs;

    // マッチメイキングの待ち時間（デフォルト10秒）
    public int matchMakingTime = 10;

    // ゲーム開始前のカウントダウンの時間
    public int gameStartCountdownSeconds = 5;

    // タイマースクリプト
    DodgeBallTimerDisplay timerDisplay;
    // スコアスクリプト
    DodgeBallScore scoreScript;
    // プレイヤーオブジェクト
    GameObject playerObject;
    // ボールオブジェクト
    GameObject ballObject;

    // オブジェクトがアクティブな場合の、初期処理
    void Start()
    {
        // デフォルトの同期頻度（20秒/回）だと、他のプレイヤーがラグいため、調整した
        // SendRate を80に設定
        PhotonNetwork.SendRate = 80;
        // SerializationRate を80に設定
        PhotonNetwork.SerializationRate = 80;

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

    // ロビー入室後に、呼び出される
    public override void OnJoinedLobby()
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
        // 非同期で、初期位置にオブジェクト生成
        Vector3 spawnPosition = GetRandomPosition(initialMinPosition, initialMaxPosition);
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
        Debug.Log("ルームから退室しました");
        // 非同期でオブジェクト削除
        PhotonNetwork.Destroy(playerObject);
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

        // ルーム内のプレイヤー数が最少人数以上、かつ偶数であれば
        if (PhotonNetwork.CurrentRoom.PlayerCount >= minPlayers && PhotonNetwork.CurrentRoom.PlayerCount % 2 == 0) 
        {
            // マスタークライアントでのみ実行
            if (PhotonNetwork.IsMasterClient)
            {
                // 鬼の選定
                SelectTeam();
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
        timerDisplay = timerPanel.GetComponent<DodgeBallTimerDisplay>();
        if (timerDisplay == null)
        {
            Debug.LogError("TimerDisplayのスクリプトが見つかりません");
        }

        // スコアスクリプト取得
        GameObject scorePanel = GameObject.Find("ScorePanel");
        if (scorePanel == null)
        {
            Debug.LogError("ScorePanelが見つかりません");
        }
        scoreScript = scorePanel.GetComponent<DodgeBallScore>();
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
                DodgeBallPlayerScript playerScript = view.gameObject.GetComponent<DodgeBallPlayerScript>();
                playerScript.move = true;
            }
        }

        // マスタークライアントでのみ実行
        if (PhotonNetwork.IsMasterClient)
        {
            // ボールの生成
            ballObject = PhotonNetwork.Instantiate(ballPrefab.name, initialPosition, randomRotation);
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

        // どちらかのチーム一方しかいない場合

    }

    // 指定した秒数、待機するコルーチン
    IEnumerator WaitForSecondsCoroutine(int seconds)
    {
        Debug.Log("Wait starts");
        // マッチメイキングの待機
        yield return new WaitForSeconds(seconds);
        Debug.Log($"{seconds} seconds have passed");
    }

    // チームの選定
    private void SelectTeam()
    {
        // 全プレイヤーオブジェクトを取得
        playerObjs = GameObject.FindGameObjectsWithTag("Player");

        // 全プレイヤーオブジェクトのViewIDを、カスタムプロパティに格納しておく
        ExitGames.Client.Photon.Hashtable playerIDs = new ExitGames.Client.Photon.Hashtable();
        int[] viewIDs = playerObjs.Select(obj => obj.GetComponent<PhotonView>().ViewID).ToArray();
        playerIDs["playerObjectIDs"] = viewIDs;                
        PhotonNetwork.CurrentRoom.SetCustomProperties(playerIDs);
        Debug.Log("プレイヤーViewID一覧: " + string.Join(", ", viewIDs));

        // プレイヤーオブジェクトをランダムにAチームとBチームに分ける
        List<int> aTeamViewIDs = new List<int>();
        List<int> bTeamViewIDs = new List<int>();

        // 外野設定済みフラグ
        bool aTeamOutfielderSet = false;
        bool bTeamOutfielderSet = false;

        // プレイヤーの数だけループ
        for (int i = 0; i < playerObjs.Length; i++)
        {
            GameObject playerObj = playerObjs[i];
            PhotonView view = playerObj.GetComponent<PhotonView>();
            DodgeBallPlayerScript playerScript = playerObj.GetComponent<DodgeBallPlayerScript>();

            // 交互に割り当て
            if (i % 2 == 0)
            {
                // Aチームに割り当て
                playerScript.isAteam = true;
                aTeamViewIDs.Add(view.ViewID);

                // プレイヤー数が4人以上の場合、各チームに isInfielder が false のプレイヤーを一名含める
                if (!aTeamOutfielderSet && !playerScript.isInfielder && playerObjs.Length >= 4)
                {
                    playerScript.isInfielder = false;
                    aTeamOutfielderSet = true;
                }
            }
            else
            {
                // Bチームに割り当て
                playerScript.isAteam = false;
                bTeamViewIDs.Add(view.ViewID);

                // プレイヤー数が4人以上の場合、各チームに isInfielder が false のプレイヤーを一名含める
                if (!bTeamOutfielderSet && !playerScript.isInfielder && playerObjs.Length >= 4)
                {
                    playerScript.isInfielder = false;
                    bTeamOutfielderSet = true;
                }
            }

            // プレイヤーオブジェクトの配置設定
            playerScript.SetPosition();
        }

        // ルーム内のチームのViewIDをカスタムプロパティとして保持
        var roomProperties = new ExitGames.Client.Photon.Hashtable();
        roomProperties["aTeamViewIDs"] = aTeamViewIDs.ToArray();
        roomProperties["bTeamViewIDs"] = bTeamViewIDs.ToArray();
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

        Debug.Log("AチームのプレイヤーViewID一覧: " + string.Join(", ", aTeamViewIDs));
        Debug.Log("BチームのプレイヤーViewID一覧: " + string.Join(", ", bTeamViewIDs));

        // すべてのプレイヤーオブジェクトに対して処理
        foreach (GameObject playerObj in playerObjs)
        {
            PhotonView view = playerObj.GetComponent<PhotonView>();
            DodgeBallPlayerScript playerScript = playerObj.GetComponent<DodgeBallPlayerScript>();

            if (bTeamViewIDs.Contains(view.ViewID))
            {
                // Bチームの設定
                playerScript.isAteam = false;
            }
        }

        // プレイヤーオブジェクトの色変更（マスタークライアント&それ以外のクライアントで、通信を介してスクリプト実行）
        photonView.RPC(nameof(ControllColor), RpcTarget.AllViaServer, aTeamViewIDs.ToArray(), bTeamViewIDs.ToArray());
    }

    // オブジェクトの色変更(全クライアントで実行) 
    [PunRPC]
    public void ControllColor(int[] aTeamViewIDs, int[] bTeamViewIDs)
    {
        // Aチームの設定
        SetTeamColor(aTeamViewIDs, aTeamMaterial);

        // Bチームの設定
        SetTeamColor(bTeamViewIDs, bTeamMaterial);
    }

    // チームごとに色を設定するメソッド
    private void SetTeamColor(int[] teamViewIDs, Material teamMaterial)
    {
        foreach (int viewID in teamViewIDs)
        {
            GameObject playerObj = PhotonView.Find(viewID).gameObject;
            // "CH09"子オブジェクトのSkinned Mesh Rendererを取得
            SkinnedMeshRenderer skinnedMeshRenderer = playerObj.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                // SkinnedMeshRendererから現在のマテリアル配列を取得
                Material[] materials = skinnedMeshRenderer.materials;

                // チームのマテリアル設定
                materials[0] = teamMaterial;

                // SkinnedMeshRendererのmaterialsプロパティに更新された配列を再設定
                skinnedMeshRenderer.materials = materials;
            }
        }
    }
}
