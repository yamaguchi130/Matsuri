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
    // 自身のプレイヤーオブジェクト
    GameObject myPlayerObject;
    // 自身のプレイヤースクリプト
    DodgeBallPlayerScript myPlayerScript;
    // 自身のプレイヤーのアニメーションコンポーネント
    private Animator myPlayerAnim; 
    // ボールオブジェクト
    GameObject ballObject;
    // ボールのスクリプト
    DodgeBallScript ballScript;


    // プレイヤーオブジェクトをランダムにAチームとBチームに分ける為のリスト
    List<int> aTeamViewIDs = new List<int>();
    List<int> bTeamViewIDs = new List<int>();



    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
        // デフォルトの同期頻度（20秒/回）だと、他のプレイヤーがラグいため、調整した
        // SendRate を設定
        PhotonNetwork.SendRate = 60;
        // SerializationRate を設定
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
        myPlayerObject = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, randomRotation);
        // 自身のプレイヤーオブジェクトのアニメーションのコンポーネントを取得
        myPlayerAnim = myPlayerObject.GetComponent<Animator>();
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
        PhotonNetwork.Destroy(myPlayerObject);
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
                // チームの割り振り
                SelectTeam();
            }

            // プレイヤーオブジェクトの色変更（マスタークライアント&それ以外のクライアントで、通信を介してスクリプト実行）
            photonView.RPC(nameof(ControllColor), RpcTarget.AllViaServer, aTeamViewIDs.ToArray(), bTeamViewIDs.ToArray());
            
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
        // マスタークライアントでのみ実行
        if (PhotonNetwork.IsMasterClient)
        {
            // ボールの生成
            PhotonNetwork.Instantiate(ballPrefab.name, initialPosition, randomRotation);
        }

        // これがBチームのプレイヤーもtrueになってる
        Debug.Log($"Aチーム:{myPlayerScript.isAteam}でスタートします。"); 
        // 自身のプレイヤーオブジェクトの配置設定
        myPlayerScript.SetPosition();

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

        // 自身のプレイヤーオブジェクトを移動可能に設定
        myPlayerScript.move = true;

        // ボールのスクリプトを取得（マスタークライアント以外でも取得するため、Findを使用）
        ballObject = GameObject.FindWithTag("Ball");
        if (ballObject == null)
        {
            Debug.LogError("ボールのオブジェクトが見つかりません");
        }

        ballScript = ballObject.GetComponent<DodgeBallScript>();
        if (ballScript == null)
        {
            Debug.LogError("ボールのスクリプトが見つかりません");
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
        playerObjs = GameObject.FindGameObjectsWithTag("DodgeBallPlayer");

        // 全プレイヤーオブジェクトのViewIDを、カスタムプロパティに格納しておく
        ExitGames.Client.Photon.Hashtable playerIDs = new ExitGames.Client.Photon.Hashtable();
        int[] viewIDs = playerObjs.Select(obj => obj.GetComponent<PhotonView>().ViewID).ToArray();
        playerIDs["playerObjectIDs"] = viewIDs;                
        PhotonNetwork.CurrentRoom.SetCustomProperties(playerIDs);
        Debug.Log("プレイヤーViewID一覧: " + string.Join(", ", viewIDs));

        // 外野設定済みフラグ
        bool aTeamOutfielderSet = false;
        bool bTeamOutfielderSet = false;

        // プレイヤーの数だけループして、チーム割り振り
        for (int i = 0; i < playerObjs.Length; i++)
        {
            GameObject playerObj = playerObjs[i];
            PhotonView view = playerObj.GetComponent<PhotonView>();

            bool isAteam = (i % 2 == 0);
            bool isInfielder = true; // デフォルトはtrueで設定

            // view.ViewIDをチームリストに追加
            if (isAteam)
            {
                aTeamViewIDs.Add(view.ViewID);
            }
            else
            {
                bTeamViewIDs.Add(view.ViewID);
            }

            // 各チームに少なくとも一人の外野プレイヤーを含める
            if (playerObjs.Length >= 4)
            {
                if (isAteam && !aTeamOutfielderSet)
                {
                    isInfielder = false;
                    aTeamOutfielderSet = true;
                }
                else if (!isAteam && !bTeamOutfielderSet)
                {
                    isInfielder = false;
                    bTeamOutfielderSet = true;
                }
            }

            // カスタムプロパティを設定してプレイヤーに適用
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
            {
                {"IsAteam", isAteam},
                {"IsInfielder", isInfielder}
            };
            view.Owner.SetCustomProperties(props);          
        }

        // ルーム内のチームのViewIDをカスタムプロパティとして保持
        var roomProperties = new ExitGames.Client.Photon.Hashtable();
        roomProperties["aTeamViewIDs"] = aTeamViewIDs.ToArray();
        roomProperties["bTeamViewIDs"] = bTeamViewIDs.ToArray();
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

        Debug.Log("AチームのプレイヤーViewID一覧: " + string.Join(", ", aTeamViewIDs));
        Debug.Log("BチームのプレイヤーViewID一覧: " + string.Join(", ", bTeamViewIDs));
    }

    // プレイヤーのカスタムプロパティが変更されたとき
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // 自身のプレイヤーオブジェクトのPhotonViewを検索
        PhotonView view = myPlayerObject.GetComponent<PhotonView>();
        // 自身のプレイヤーオブジェクトがターゲットなら
        if (view.Owner == targetPlayer)
        {
            // チーム・内野or外野のプロパティの変更時
            if (changedProps.ContainsKey("IsAteam") || changedProps.ContainsKey("IsInfielder"))
            {
                // 自身のプレイヤーのスクリプトを取得
                myPlayerScript = myPlayerObject.GetComponent<DodgeBallPlayerScript>();
                // チーム設定
                myPlayerScript.isAteam = (bool)changedProps["IsAteam"];
                // 内野/外野設定
                myPlayerScript.isInfielder = (bool)changedProps["IsInfielder"];
            }
        }
    }

    // オブジェクトの色変更(全クライアントで実行) 
    [PunRPC]
    public void ControllColor(int[] aTeamViewIDs, int[] bTeamViewIDs)
    {   
        // Aチームのカラー設定
        SetTeamColor(aTeamViewIDs, aTeamMaterial);
        // Bチームのカラー設定
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
            else
            {
                Debug.LogError($"ViewID:{viewID}のプレイヤーのSkinnedMeshRendererが取得できません");
            }
        }
    }

    // ボールをストレートに投げるボタンをクリックしたとき
    public void OnClickStraightThrowBall()
    {
        Debug.Log("ストレートをクリックしました");
        // Animator発動（アニメーションイベント経由でボールスクリプトのStraightThrowがよばれる）
        myPlayerAnim.SetTrigger("straightThrow");
    }

    // ボールをやまなりにパスするボタンをクリックしたとき
    public void OnClickLobPass()
    {
        Debug.Log("山なりパスをクリックしました");
        // Animator発動（アニメーションイベント経由でボールスクリプトのLobPassがよばれる）
        myPlayerAnim.SetTrigger("lobPass");
    }

    // ボールを落とすボタンをクリックしたとき
    public void OnClickDropBall()
    {
        Debug.Log("落とすをクリックしました");
        // ボールスクリプトのDropBallをそのまま呼ぶ
        StartCoroutine(ballScript.DropBall());
    }

    // ボールをキャッチするボタンをクリックしたとき
    public void OnClickCatching()
    {
        myPlayerScript.StartCatching();
    }

    // ボールをキャッチするボタンを離したとき
    public void OnPointerUpCatching()
    {
        myPlayerScript.StopCatching();
    }

}
