using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;
using Cinemachine;
using Photon.Realtime;
using System.Linq;

/// <summary>
/// プレイヤーオブジェクトを制御するクラス
/// </summary>
public class DodgeBallPlayerScript : MonoBehaviourPun
{
    // Animatorをanimという変数で定義する
    private Animator anim; 
    // 移動Joystickスクリプト
    private FloatingJoystick inputMove;
    // シーン管理用オブジェクト
    private GameObject photonControllerGameObject;
    // シーン管理スクリプト
    private DodgeBallScene dodgeBallSceneScript;
    // 仮想カメラオブジェクト
    private GameObject virtualCameraGameObject;
    // カメラコントローラ
    private CameraController cameraController;

    // 物理演算用
    private Rigidbody rb;
    
    // プレイヤーが移動する速度
    float playerMoveSpeed = 7f;
    // プレイヤーの向きを回転する速度
    float rotateSpeed = 3.0f;

    // プレイヤーの移動可否
    [SerializeField]
    public bool move = false;
    
    // 次にHit判定になるまでのクールダウン
    public float cooldownTime = 5.0f; 
    private float nextChangeTime = 0f; 

    // チームフラグ（True：Aチーム、False：Bチーム）
    public bool isAteam = true;

    // 内野・外野フラグ
    public bool isInfielder = true;

    // Aチームの内野の範囲設定
    private Vector3 aTeamInfielderMinPosition = new Vector3(-4.0f, 0, 0.5f);
    private Vector3 aTeamInfielderMaxPosition = new Vector3(4.0f, 0, 7.5f);
    // Bチームの内野の範囲設定
    private Vector3 bTeamInfielderMinPosition = new Vector3(-4.0f, 0, -7.5f);
    private Vector3 bTeamInfielderMaxPosition = new Vector3(4.0f, 0, -0.5f);

    // Bチームの外野の範囲設定
    private Vector3 bTeamOutfielderMinPosition = new Vector3(-4.0f, 0, 8.5f);
    private Vector3 bTeamOutfielderMaxPosition = new Vector3(4.0f, 0, 9.5f);
    // Aチームの外野の範囲設定
    private Vector3 aTeamOutfielderMinPosition = new Vector3(-4.0f, 0, -9.5f);
    private Vector3 aTeamOutfielderMaxPosition = new Vector3(4.0f, 0, -8.5f);

    // 南向き
    private Quaternion southInitialRotation = Quaternion.Euler(0, 180, 0);
    // 北向き
    private Quaternion northInitialRotation = Quaternion.Euler(0, 0, 0); 

    // キャッチボタンを押下中かどうかのフラグ
    private bool isCatching = false;

    // オブジェクトを点滅させるスクリプト
    private RendererOnOffExample flashScript;

    // ボールのスクリプト
    private DodgeBallScript ballScript;
    // ボールのView
    private PhotonView ballView;

    // 外野の時間をカウント  
    private float outfieldTime = 0f;
    
    // プレイヤーのCollider
    private Collider playerCol;


    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
        // 物理演算用のコンポーネント取得
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody コンポーネントが見つかりません");
        }

        anim = gameObject.GetComponent<Animator>();

        // シーン管理用オブジェクトの設定
        photonControllerGameObject = GameObject.Find("PhotonController");
        if (photonControllerGameObject == null)
        {
            Debug.LogError("PhotonControllerが見つかりません");
        }
        // シーン管理スクリプトの取得
        dodgeBallSceneScript = photonControllerGameObject.GetComponent<DodgeBallScene>();
        if (dodgeBallSceneScript == null)
        {
            Debug.LogError("dodgeBallSceneScriptが見つかりません");
        }

        // VirtualCameraの設定
        virtualCameraGameObject = GameObject.Find("Virtual Camera");
        if (virtualCameraGameObject == null)
        {
            Debug.LogError("Virtual Cameraが見つかりません");
        }

        // カメラControllerの設定
        cameraController = virtualCameraGameObject.GetComponent<CameraController>();

        // joystickの設定
        GameObject joystickGameObject = GameObject.Find("Floating Joystick");
        if (joystickGameObject == null)
        {
            Debug.LogError("Floating Joystickが見つかりません");
        }
        this.inputMove = joystickGameObject.GetComponent<FloatingJoystick>();

        // プレイヤーオブジェクトが生成されたときに所有権を確認
        int viewId = photonView.ViewID;
        if (photonView.IsMine)
        {
            Debug.Log($"このプレイヤーオブジェクト（ViewID:{viewId}）は {PhotonNetwork.LocalPlayer.UserId} に所有されています。");
        }
        else
        {
            Debug.Log($"このプレイヤーオブジェクト（ViewID:{viewId}）は、{PhotonNetwork.LocalPlayer.UserId}の所有権がありません。");
        }

        flashScript = gameObject.GetComponent<RendererOnOffExample>();
        if (flashScript == null)
        {
            Debug.LogError("flashScriptが見つかりません");
        }

        // プレイヤーのコライダーを取得
        playerCol = GetComponent<Collider>();
    }

    // 配置設定
    public void SetPosition()
    {
        Vector3 newPosition;
        Quaternion newRotation;

        if (isAteam)
        {
            if (isInfielder)
            {
                // Aチーム内野
                newPosition = new Vector3(
                    Random.Range(aTeamInfielderMinPosition.x, aTeamInfielderMaxPosition.x),
                    aTeamInfielderMinPosition.y,
                    Random.Range(aTeamInfielderMinPosition.z, aTeamInfielderMaxPosition.z)
                );
                newRotation = southInitialRotation;
                Debug.Log($"{PhotonNetwork.LocalPlayer.UserId} がAチームの内野に設定されました。");
            }
            else
            {
                // Aチーム外野
                newPosition = new Vector3(
                    Random.Range(aTeamOutfielderMinPosition.x, aTeamOutfielderMaxPosition.x),
                    aTeamOutfielderMinPosition.y,
                    Random.Range(aTeamOutfielderMinPosition.z, aTeamOutfielderMaxPosition.z)
                );
                newRotation = northInitialRotation;
                Debug.Log($"{PhotonNetwork.LocalPlayer.UserId} がAチームの外野に設定されました。");
            }
        }
        else
        {
            if (isInfielder)
            {
                // Bチーム内野
                newPosition = new Vector3(
                    Random.Range(bTeamInfielderMinPosition.x, bTeamInfielderMaxPosition.x),
                    bTeamInfielderMinPosition.y,
                    Random.Range(bTeamInfielderMinPosition.z, bTeamInfielderMaxPosition.z)
                );
                newRotation = northInitialRotation;
                Debug.Log($"{PhotonNetwork.LocalPlayer.UserId} がBチームの内野に設定されました。");
            }
            else
            {
                // Bチーム外野
                newPosition = new Vector3(
                    Random.Range(bTeamOutfielderMinPosition.x, bTeamOutfielderMaxPosition.x),
                    bTeamOutfielderMinPosition.y,
                    Random.Range(bTeamOutfielderMinPosition.z, bTeamOutfielderMaxPosition.z)
                );
                newRotation = southInitialRotation;
                Debug.Log($"{PhotonNetwork.LocalPlayer.UserId} がBチームの外野に設定されました。");
            }
        }

        // 位置と向きを設定
        transform.position = newPosition;
        transform.rotation = newRotation;

        // 初期状態は、ボールを持ってないパネルを表示
        photonView.RPC("UpdatePanelVisibility", photonView.Owner, false);
    }

    // ランダムな位置に生成
    private Vector3 GetRandomPosition(Vector3 minPosition, Vector3 maxPosition)
    {
        float randomX = Random.Range(minPosition.x, maxPosition.x);
        float randomY = Random.Range(minPosition.y, maxPosition.y);
        float randomZ = Random.Range(minPosition.z, maxPosition.z);
        return new Vector3(randomX, randomY, randomZ);
    }

    // 一定感覚（50回/秒）の呼び出し物理演算
    void FixedUpdate()
    {
        // このオブジェクトが、現在のクライアントによって制御されており、移動可能な場合
        if (photonView.IsMine && move) 
        {
            // Virtual Cameraを設定
            var cinemachineVirtualCamera = virtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
            cinemachineVirtualCamera.Follow = transform;
            cinemachineVirtualCamera.LookAt = transform;
            // プレイヤーの移動制御
            MovePlayer();
        }

        // // ボールを投げるアニメーションになったときだけ、Root Motionをtrueにする
        // AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        // bool isThrowing = stateInfo.IsName("Throw");
        // if (anim.applyRootMotion != isThrowing)// Root Motionの状態が変わったときだけ適用
        // {
        //     anim.applyRootMotion = isThrowing;
        // }

        // 外野の場合
        if (!isInfielder)
        {  
            // 外野の時間をカウント
            outfieldTime += Time.deltaTime;
            UpdateOutfieldTime();
        }
    }

    // 外野にいる時間の、ルームのカスタムプロパティを更新
    void UpdateOutfieldTime()
    {
        // 外野にいる時間をルームプロパティに更新
        string teamKey = isAteam ? "ATeam" : "BTeam";
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { photonView.Owner.UserId + "_" + teamKey + "_OutfieldTime", outfieldTime }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    // プレイヤーの移動処理（FPS視点）
    void MovePlayer()
    {        
        // プレイヤーオブジェクトの向きに基づいて移動方向を計算
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        // 上下の移動を無視
        forward.y = 0f;
        right.y = 0f;

        // ベクトルの正規化
        forward.Normalize();
        right.Normalize();

        // joystickの入力
        float vertical = inputMove.Vertical;
        float horizontal = inputMove.Horizontal;

        // 後ろ方向（180度±45度）の入力があった場合はカメラ位置の移動のみを行う
        if (vertical < 0 && Mathf.Abs(horizontal) <= Mathf.Abs(vertical) * Mathf.Tan(Mathf.Deg2Rad * 45))
        {
            cameraController.isBack = true;
            anim.SetFloat("speed", 0f);
            return;
        }
        else
        {
            cameraController.isBack = false;
        }

        // どの位置に移動するかのベクトルを設定
        Vector3 moveDirection = forward * vertical + right * horizontal;

        // 移動速度を計算
        Vector3 moveSpeed = moveDirection * playerMoveSpeed;
        
        // 移動速度に基づいて新しい位置を計算
        Vector3 newPosition = transform.position + moveSpeed * Time.deltaTime;

        // Rigidbodyを使用してプレイヤーの位置を安全に更新
        rb.MovePosition(newPosition);

        // 移動速度に基づいたアニメーション速度の設定
        float speed = moveSpeed.magnitude;
        anim.SetFloat("speed", speed);

        // 進行方向にプレイヤーオブジェクトの向きを変える
        if (speed > 0.01f)
        {
            Quaternion rotation = Quaternion.LookRotation(moveDirection);
            // プレイヤーオブジェクトの向き設定
            Quaternion newRotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * rotateSpeed);
            rb.MoveRotation(newRotation);
        }
    }

    // オブジェクトの衝突時に呼び出される
    void OnCollisionEnter(Collision collision)
    {
        // ボールと衝突した場合
        if (collision.gameObject.CompareTag("Ball"))
        {
            // 衝突したのがこのオブジェクトの全面かを判定するために、angleを設定
            Vector3 collisionDirection = collision.contacts[0].point - transform.position;
            float angle = Vector3.Angle(transform.forward, collisionDirection);

            // ボールを持つべきかを判定し、必要ならばボールを持つ
            if (ShouldHoldBall(collision, angle))
            {
                StartCoroutine(HoldBall(collision));
            }
            else
            {
                // 自身が外野に移動する
                isInfielder = false;
                SetPosition();
            }
        }
    }

    // ボールを持つべきかどうかを判定する
    private bool ShouldHoldBall(Collision collision, float angle)
    {
        // ボールのスクリプトを取得
        ballScript = collision.collider.GetComponent<DodgeBallScript>();

        // Hit判定なしのボールの場合
        if (!ballScript.isHitEnabled)
        {
            return true;
        }

        // 最後にボールを持っていたのが味方チームの場合
        if (IsTeammateHoldingLast() && !IsSamePlayerHoldingLast())
        {
            return true;
        }

        // 最後にボールを持っていたのが敵チームで、キャッチボタン押下中で、全面での衝突なら
        if (IsEnemyHoldingLast() && isCatching && angle < 45f)
        {
            return true;
        }

        // 自身が外野にいるとき
        if (!isInfielder)
        {
            return true;
        }

        return false;
    }

    // 最後にボールを持っていたのが味方チームかどうかを判定する
    private bool IsTeammateHoldingLast()
    {
        return ballScript.isHitEnabled && photonView.ViewID != ballScript.lastThrownPlayerViewID;
    }

    // 最後にボールを持っていたのが自身であるかどうかを判定する
    private bool IsSamePlayerHoldingLast()
    {
        return photonView.ViewID == ballScript.lastThrownPlayerViewID;
    }

    // 最後にボールを持っていたのが敵チームかどうかを判定する
    private bool IsEnemyHoldingLast()
    {
        return ballScript.isHitEnabled && photonView.ViewID != ballScript.lastThrownPlayerViewID;
    }

    // ボールを持つ
    private IEnumerator HoldBall(Collision collision)
    {
        // 衝突したボールオブジェクトのView取得
        ballView = collision.collider.GetComponent<PhotonView>();

        // ボールのVIEWが取得できてない場合
        if (ballView == null)
        {
            yield break;
        }

        // ボールの所有権がない場合
        if (!ballView.IsMine)
        {
            // ボールの所有権をリクエスト
            ballView.RequestOwnership();
            Debug.Log("ボールの所有権をリクエストしました");
            // 所有権が付与されるまで待つ
            yield return new WaitUntil(() => ballView.IsMine);
        }

        // 物理挙動を無効にする
        StartCoroutine(ballScript.ToggleKinematicState(true, playerCol));

        // 全てのクライアントに親子関係の追加を通知
        ballView.RPC("SetBallToPlayer", RpcTarget.AllViaServer, photonView.ViewID);

        // SetBallToPlayerの完了のため、待機
        yield return new WaitForSeconds(0.5f);

        // ボールのヒット判定を有効にする
        ballScript.isHitEnabled = true;
        
        // ボールを持っているパネルを表示
        photonView.RPC("UpdatePanelVisibility", photonView.Owner, true);

        Debug.Log($"{PhotonNetwork.LocalPlayer.UserId} が、ボールを所持しました");
    }

    private IEnumerator CooldownRoutine()
    {
        // 次に鬼になるまでのクールダウン時間を設定
        nextChangeTime = Time.time + cooldownTime;

        // クールダウン期間中、オブジェクトを点滅させる
        if (flashScript != null)
        {
            flashScript.isFlash = true;
        }

        // クールダウン時間を待つ
        yield return new WaitForSeconds(cooldownTime);

        // クールダウン終了後、点滅を止める
        if (flashScript != null)
        {
            flashScript.isFlash = false;
        }
    }

    // straightThrowのアニメーションイベントで実行されるメソッド
    public void StartStraightThrowCoroutine()
    {
        Debug.Log("ストレートのコルーチンを開始します。");
        StartCoroutine(ballScript.StraightThrowBall());
    }

    // lobPassのアニメーションイベントで実行されるメソッド
    public void StartLobPassCoroutine()
    {
        Debug.Log("山なりパスのコルーチンを開始します。");
        StartCoroutine(ballScript.LobPass());
    }

    // キャッチボタン押下中
    public void StartCatching()
    {
        isCatching = true;

        // Animator発動
        anim.SetTrigger("catch");
    }

    // キャッチボタンを離したとき 
    public void StopCatching()
    {
        isCatching = false;
    }


    // このプレイヤーを内野に復活させる
    public void ReviveInField()
    {
        // 外野にいる場合
        if (!isInfielder)
        {
            // 内野フラグオン
            isInfielder = true;
            // 内野に設定
            SetPosition();

            // 復活中のためオブジェクトを点滅させる
            // StartCoroutine(CooldownRoutine());
        }
        else
        {
            // 自チームの外野が、1人以下の場合スキップ
            // if(){
            //     return;
            // }

            // 同じチームで外野にいるプレイヤーで、外野にいる時間が長いプレイヤーを復活させる
            string teamKey = isAteam ? "ATeam" : "BTeam";
            var playerOutfieldTimes = PhotonNetwork.CurrentRoom.Players.Values
                .Where(p => p.CustomProperties.ContainsKey(p.UserId + "_" + teamKey + "_OutfieldTime"))
                .Select(p => new
                {
                    Player = p,
                    OutfieldTime = (float)p.CustomProperties[p.UserId + "_" + teamKey + "_OutfieldTime"]
                })
                .OrderByDescending(p => p.OutfieldTime)
                .ToList();

            // 外野時間の長いプレイヤーからループ処理
            foreach (var playerOutfieldTime in playerOutfieldTimes)
            {
                var longestOutfieldPlayer = playerOutfieldTime.Player;
                // 最も外野にいたプレイヤーのオブジェクトを取得
                GameObject playerObject = GetPlayerObject(longestOutfieldPlayer);
                if (playerObject != null)
                {
                    // プレイヤーオブジェクトからスクリプトを取得して復活処理を呼び出す
                    DodgeBallPlayerScript longestOutfieldPlayerScript = playerObject.GetComponent<DodgeBallPlayerScript>();
                    if (longestOutfieldPlayerScript != null)
                    {
                        longestOutfieldPlayerScript.ReviveInField();

                        // ルームプロパティの値を0にリセット
                        ResetOutfieldTime(longestOutfieldPlayer.UserId, teamKey);
                        // 成功したらループを抜ける
                        break; 
                    }
                }
                else
                {
                    // プレイヤーオブジェクトが存在しない場合、ルームプロパティを削除
                    RemoveOutfieldTime(longestOutfieldPlayer.UserId, teamKey);
                }
            }
        }   
    }

    private GameObject GetPlayerObject(Photon.Realtime.Player player)
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("DodgeBallPlayer"))
        {
            if (go.GetComponent<PhotonView>().Owner == player)
            {
                return go;
            }
        }
        return null;
    }

    // 外野時間をリセット
    private void ResetOutfieldTime(string userId, string teamKey)
    {
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { userId + "_" + teamKey + "_OutfieldTime", 0f }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    // 外野時間を削除
    private void RemoveOutfieldTime(string userId, string teamKey)
    {
        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
        {
            { userId + "_" + teamKey + "_OutfieldTime", null }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    // デバッグ用（スクリプトが無効になったときに呼ばれるコールバック）
    private void OnDisable()
    {
        Debug.Log($"OnDisable: プレイヤースクリプトが無効になりました。{Time.time}");
    }
}
