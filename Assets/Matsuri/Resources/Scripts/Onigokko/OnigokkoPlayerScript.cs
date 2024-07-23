using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;
using Cinemachine;

/// <summary>
/// プレイヤーオブジェクトを制御するクラス
/// </summary>
public class OnigokkoPlayerScript : MonoBehaviourPun
{
    // Animatorをanimという変数で定義する
    private Animator anim; 
    // 移動Joystickスクリプト
    private FloatingJoystick inputMove;
    // シーン管理用オブジェクト
    private GameObject photonControllerGameObject;
    // 仮想カメラオブジェクト
    private GameObject virtualCameraGameObject;
    // カメラコントローラ
    private CameraController cameraController;

    // 物理演算用
    private Rigidbody rb;
    
    // プレイヤーが移動する速度
    float playerMoveSpeed = 7f;
    // 鬼が移動する速度
    float oniMoveSpeed = 8f;
    // プレイヤーの向きを回転する速度
    float rotateSpeed = 3.0f;

    // プレイヤーの移動可否
    [SerializeField]
    public bool move = false;
    
    // 次に鬼になるまでのクールダウン
    public float cooldownTime = 5.0f; 
    private float nextChangeTime = 0f; 

    // 鬼かどうかのフラグ
    public bool isOni = false;

    // オブジェクトを点滅させるスクリプト
    private RendererOnOffExample flashScript;

    // アタッチしたゲームオブジェクトが有効になったとき
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

        // 後ろ方向の入力があった場合はカメラ位置の移動のみを行う
        if (vertical < 0)
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
        Vector3 moveSpeed = moveDirection * (isOni ? oniMoveSpeed : playerMoveSpeed);
        
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

    // オブジェクトの衝突時
    void OnCollisionEnter(Collision collision)
    {
        // クールダウン中の場合、衝突処理をスキップ
        if (Time.time < nextChangeTime) return;

        // 衝突したオブジェクトがPlayerScriptを持っていなければスキップ
        OnigokkoPlayerScript collisionUserScript = collision.collider.GetComponent<OnigokkoPlayerScript>();
        if (collisionUserScript == null) return;
        Debug.Log("プレイヤー同士の衝突を検知しました。");


        // 1回衝突したら、次の鬼を1人設定して、以降の衝突は検知しないようにする（カスタムプロパティで現在の鬼のVIewIDを保持する）
        // ルームのカスタムプロパティの鬼のViewIdを取得する。
        int oniViewId = (int)PhotonNetwork.CurrentRoom.CustomProperties["oniViewId"];

        // 衝突したプレイヤーオブジェクトのView取得
        PhotonView collisionView = collision.collider.GetComponent<PhotonView>();

        // 自身がランナーで、現在の鬼と衝突したとき
        if (!this.isOni && oniViewId == collisionView.ViewID)
        {
            StartCoroutine(HandleRunnerCollisionWithOni());
        }
        // 自身が現在の鬼で、ランナーと衝突したとき
        else if (this.isOni && oniViewId != collisionView.ViewID)
        {
            Debug.Log($"鬼：{PhotonNetwork.LocalPlayer.UserId}が、ランナーをタッチしました。");

            // Animatorの'touch'発動
            anim.SetTrigger("touch");

            // 鬼フラグをオフ
            this.isOni = false;

            // クールダウン処理
            StartCoroutine(CooldownRoutine());
        }
    }

    // 自身がランナーで、現在の鬼と衝突したとき
    private IEnumerator HandleRunnerCollisionWithOni()
    {
        // 移動不可にする
        move = false;

        Debug.Log($"ランナー：{PhotonNetwork.LocalPlayer.UserId}が、鬼にタッチされました。");

        // Animatorの'knockedOut'発動
        anim.SetTrigger("knockedOut");

        // ルーム内の鬼の数を制御するため、次の鬼のViewidを、カスタムプロパティとして保持しておく
        var roomProperties = new ExitGames.Client.Photon.Hashtable();
        roomProperties["oniViewId"] = photonView.ViewID;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
        // 鬼フラグをオン
        this.isOni = true;

        // シーン用スクリプト、Viewを取得
        OnigokkoScene onigokkoScene = photonControllerGameObject.GetComponent<OnigokkoScene>();
        PhotonView sceneView = photonControllerGameObject.GetComponent<PhotonView>();

        // 鬼のオブジェクトの色変更（マスタークライアント&それ以外のクライアントで、通信を介してスクリプト実行）
        // 注意：RpcTarget.Allにすると、マスターで即時実行されて、後続処理がエラーになる
        sceneView.RPC(nameof(onigokkoScene.ControllOniColor), RpcTarget.AllViaServer, photonView.ViewID);

        // 2秒の待機処理（動けなくする）
        yield return new WaitForSeconds(2);

        // 移動可に戻す
        move = true;
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
}
