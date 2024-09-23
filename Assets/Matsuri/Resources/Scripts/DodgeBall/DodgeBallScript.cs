using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Cinemachine;

/// <summary>
/// ドッヂボール用のボールを制御するクラス
/// </summary>
public class DodgeBallScript : MonoBehaviourPun, IPunOwnershipCallbacks
{
    // Animatorをanimという変数で定義する
    private Animator anim; 
    // コライダー
    private Collider ballCol;
    // シーン管理用オブジェクト
    private GameObject photonControllerGameObject;

    // 物理演算用
    private Rigidbody rb;
    
    // プレイヤーの移動可否
    [SerializeField]
    public bool move = true;

    // 敵チームのプライヤーオブジェクトのViewIDリスト
    object enemyTeamViewIDs;

    // 最後に投げたプレイヤーのViewID
    public int lastThrownPlayerViewID = -1;

    // Hit判定が有効かどうかのフラグ
    public bool isHitEnabled = false;

    // ボールのリスポーン時間(秒)
    float ballRespawnTime = 10f;

    // ボールの初期位置
    Vector3 initialPosition = new Vector3(0, 1, 0);

    // ボールをリスポーンしたかどうかのフラグ（デフォルトfalse）
    private bool isBallRespawned = false;

    // ボールを持つプレイヤーオブジェクトのTransform
    private Transform ballHolderTransform;

    // ボールを持つプレイヤーオブジェクトのPhotonView
    private PhotonView ballHolderView;

    // 前回取得した、ボールを保持してるプレイヤーのCollider
    private Collider previousPlayerCol;


    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
        // 物理演算用のコンポーネント取得
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody コンポーネントが見つかりません");
        }

        // アニメーションのコンポーネントを取得
        anim = GetComponent<Animator>();

        // シーン管理用オブジェクトの設定
        photonControllerGameObject = GameObject.Find("PhotonController");
        if (photonControllerGameObject == null)
        {
            Debug.LogError("PhotonControllerが見つかりません");
        }

        // ボールのコライダーを取得
        ballCol = GetComponent<Collider>();
    }

    // オブジェクトの衝突時
    void OnCollisionEnter(Collision collision)
    {
        // このオブジェクトが自分のものでなければ処理を行わない
        if (!photonView.IsMine)
        {
            return;
        }

        // ボールが地面にバウンドしたら
        if (collision.gameObject.CompareTag("Terrain"))
        {
            // RPCで全クライアントにHIt判定なしを同期
            photonView.RPC("HitDetectionRPC", RpcTarget.AllBuffered, false);

            // ボールのＸ軸,Ｚ軸が初期位置でない、かつ未リスポーンなら
            if ((rb.position.x != initialPosition.x || rb.position.y != initialPosition.y) && !isBallRespawned)
            {
                // ボールをリスポーンするかのチェック
                StartCoroutine(ShouldRespawnBall());
            }
        }

        // プレイヤーに衝突したら
        if (collision.gameObject.CompareTag("DodgeBallPlayer"))
        {     

            // 衝突したプレイヤーのenemyViewIDsを取得
            DodgeBallPlayerScript collidingPlayerScript = collision.collider.GetComponent<DodgeBallPlayerScript>();
            int[] enemyViewIDs = collidingPlayerScript.enemyViewIDs;

            // enemyViewIDs が null の場合（読み込み前）は処理を中断
            if (enemyViewIDs == null)
            {
                Debug.Log("enemyViewIDs が null です");
                return;
            }

            // 衝突したプレイヤーオブジェクトのView取得
            PhotonView playerView = collision.collider.GetComponent<PhotonView>();

            // 敵チームのプレイヤーオブジェクトのViewID分、ループする
            foreach (int viewId in enemyViewIDs)
            {
                // プレイヤーが投げたボールが、ノーバウンドで、相手チームのプレイヤーに衝突した場合
                if (viewId == playerView.ViewID && isHitEnabled)
                {
                    OnBallHit();
                }
            }
        }    
    }

    // ボールのリスポーン処理
    private IEnumerator ShouldRespawnBall()
    {
        // ボールのHit判定がない場合
        if (!isHitEnabled)
        {
            // ボールを○○秒後にリスポーンします..のようなカウント表示を入れたい

            // 指定秒間待機
            yield return new WaitForSeconds(ballRespawnTime);

            // 再度ボールの状態を確認して、ボールのHit判定がない場合
            if (!isHitEnabled)
            {
                if (photonView.IsMine)
                { 
                    // ボールの物理挙動をリセット
                    ResetRigidbody();

                    // 初期位置に戻す
                    Debug.Log("ボールを初期位置に戻します。");
                    rb.position = initialPosition;
                }

                // RPCで全クライアントにリスポーン処理を同期
                photonView.RPC("RespawnBallRPC", RpcTarget.AllBuffered);
            }
        }
    }

    // リスポーン処理
    [PunRPC]
    private void RespawnBallRPC()
    {
        // ボールがリスポーン済みと設定
        isBallRespawned = true;

        // ボールを持っているプレイヤーのTransformを削除
        ballHolderTransform = null;
    }

    // ボールのHit判定
    [PunRPC]
    private void HitDetectionRPC(bool isHitEnable)
    {
        // ボールのHit判定を設定
        isHitEnabled = isHitEnable;
    }

    // ボールの物理挙動のリセット
    private void ResetRigidbody()
    {
        // 速度をリセット
        rb.velocity = Vector3.zero;
        // 回転もリセットする
        rb.angularVelocity = Vector3.zero;
        // 慣性テンソルの回転をリセット
        rb.ResetInertiaTensor();
    }

    // ボールがヒットしたとき
    public void OnBallHit()
    {
        // ルームプロパティを取得
        ExitGames.Client.Photon.Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;

        // プレイヤーのスコアを取得、存在しない場合は初期化
        if (!roomProperties.ContainsKey(lastThrownPlayerViewID))
        {
            roomProperties[lastThrownPlayerViewID] = 0;
        }

        // スコアを更新
        int currentScore = (int)roomProperties[lastThrownPlayerViewID];
        roomProperties[lastThrownPlayerViewID] = currentScore + 1;

        // ルームプロパティを設定
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

        // 最後にボールを投げたプレイヤーで、内野に復活するメソッドを実行
        GameObject lastThrownPlayerObject = PhotonView.Find(lastThrownPlayerViewID).gameObject;
        DodgeBallPlayerScript lastThrownPlayerScript = lastThrownPlayerObject.GetComponent<DodgeBallPlayerScript>();
        lastThrownPlayerScript.ReviveInField();
    }

    // スクリプトが有効になったときに呼び出される
    // このオブジェクトをコールバックターゲットとして登録します
    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    // スクリプトが無効になったときに呼び出される
    // このオブジェクトをコールバックターゲットから解除します
    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // 所有権のリクエストがあったときに呼び出される
    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {
        Debug.Log("ボールの所有権のリクエストがありました。");
        if (PhotonNetwork.IsMasterClient)
        {
            // マスタークライアントが所有権を渡すかどうかを決定します
            targetView.TransferOwnership(requestingPlayer);
        }
    }

    // 所有権が転送されたときに呼び出される
    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        Debug.Log($"ボールの所有権を:{targetView.Owner.NickName}に転送しました");
    }

    // 所有権の転送が失敗したときに呼び出される
    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        Debug.LogError("ボールの所有権の転送に失敗しました。");
    }

    // ボールをストレートに投げるコルーチン
    public IEnumerator StraightThrowBall()
    {
        // プレイヤーからボールを切り離し
        yield return StartCoroutine(DetachBallFromPlayer());

        try
        {
            if (rb == null)
            {
                Debug.LogError("StraightThrowBallで、Rigidbodyが取得できません");
            }

            if (ballHolderTransform == null)
            {
                Debug.LogError("StraightThrowBallで、ballHolderTransformが取得できません");
            }

            // ボールをプレイヤーの前方1ユニット、高さ1ユニットに配置
            rb.position = ballHolderTransform.position + ballHolderTransform.forward * 1f + ballHolderTransform.up * 2f;

            // 力の強さ
            float shootForce = 10.0f;

            // 向きと大きさからボールに加わる力を計算する
            Vector3 force = ballHolderTransform.forward.normalized * shootForce;

            // 力を加える
            rb.AddForce(force, ForceMode.Impulse);
        }
        catch (Exception ex)
        {
            Debug.LogError($"StraightThrowBallで例外が発生しました: {ex.Message}");
        }
    }

    // ボールをやまなりにパスするコルーチン
    public IEnumerator LobPass()
    {
        // プレイヤーからボールを切り離し
        yield return StartCoroutine(DetachBallFromPlayer());
        
        try
        {
            if (rb == null)
            {
                Debug.LogError("LobPassで、Rigidbodyが取得できません");
            }

            if (ballHolderTransform == null)
            {
                Debug.LogError("LobPassで、ballHolderTransformが取得できません");
            }

            // ボールをプレイヤーの前方1ユニット、高さ1ユニットに配置
            rb.position = ballHolderTransform.position + ballHolderTransform.forward * 1f + ballHolderTransform.up * 2f;

            // 力を加える向きをVector3型で定義
            Vector3 forceDirection = (ballHolderTransform.forward + ballHolderTransform.up).normalized;

            // 力の強さ
            float shootForce = 8.0f;

            // 向きと大きさからボールに加わる力を計算する
            Vector3 force = forceDirection * shootForce;

            // 力を加える
            rb.AddForce(force, ForceMode.Impulse);
        }
        catch (Exception ex)
        {
            Debug.LogError($"LobPassで例外が発生しました: {ex.Message}");
        }
    }

    // ボールを落とすコルーチン
    public IEnumerator DropBall()
    {
        // プレイヤーからボールを切り離し
        yield return StartCoroutine(DetachBallFromPlayer());

        try
        {
            if (rb == null)
            {
                Debug.LogError("DropBallで、Rigidbodyが取得できません");
            }

            // ボールをプレイヤーの前方1ユニット、高さ1ユニットに配置
            rb.position = ballHolderTransform.position + ballHolderTransform.forward * 1f + ballHolderTransform.up * 2f;
        }
        catch (Exception ex)
        {
            Debug.LogError($"DropBallで例外が発生しました: {ex.Message}");
        }
    }

    // コルーチンで親子関係を解除し、完了後にコールバックを実行
    private IEnumerator DetachBallFromPlayer()
    {
        // ボールを持っているプレイヤーのTransformを取得しておく
        ballHolderTransform = ballHolderView.GetComponent<Transform>();
        if (ballHolderTransform == null)
        {
            Debug.LogError($"ViewID:{ballHolderView.ViewID} のプレイヤーのtransformが見つかりません");
        }

        // 最後に投げたプレイヤーのphotonViewIDを取得しておく
        lastThrownPlayerViewID = ballHolderView.ViewID;

        // 全てのクライアントに、プレイヤーとボール親子関係の解除を通知
        photonView.RPC("UnsetBallToPlayer", RpcTarget.AllViaServer);

        // 全てのクライアントに、ボールのHIt判定ありを同期
        photonView.RPC("HitDetectionRPC", RpcTarget.AllBuffered, true);

        // RPCの完了のため、待機
        yield return new WaitForSeconds(2.0f);

        // ボールの物理挙動を有効にする
        yield return StartCoroutine(ToggleKinematicState(false, null));

        // ボールを持ってないパネルを表示
        ballHolderView.RPC("UpdatePanelVisibility", ballHolderView.Owner, false);

        // ボールを未リスポーンに設定
        isBallRespawned = false;
    }

    // Rigidbodyの物理挙動を有効化または無効化する(falseなら物理挙動有効/trueなら物理挙動無効)
    public IEnumerator ToggleKinematicState(bool enable, Collider ballHolderPlayerCol)
    {        
        // ballHolderPlayerColがnullの場合、前回のballHolderPlayerColを使う
        if (ballHolderPlayerCol == null)
        {
            ballHolderPlayerCol = previousPlayerCol;  // 前回のColliderを使用
            if (ballHolderPlayerCol == null)
            {
                Debug.LogError("前回のColliderも存在しません");
                yield break;  // 何もできないので処理を終了
            }
        }
        else
        {
            // playerColが渡された場合は、保存しておく
            previousPlayerCol = ballHolderPlayerCol;
        }

        // trueなら
        if(enable)
        {
            // ボールの位置と回転の制御をロック
            rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            // ボールのバウンドを無効
            ballCol.material.bounciness = 0f;
            // ボールと、ボールを持っているプレイヤーの衝突を無視する
            Physics.IgnoreCollision(ballCol, ballHolderPlayerCol, enable);
        }
        // false/物理挙動有効にするなら
        else
        {
            // ボールの位置と回転の制御を解除して物理挙動を再開
            rb.constraints = RigidbodyConstraints.None;
            // ボールのバウンドを有効（デフォルト:1）
            ballCol.material.bounciness = 1f;
            // ボールと、ボールを持っているプレイヤーの衝突を無視しない（デフォルト）
            Physics.IgnoreCollision(ballCol, ballHolderPlayerCol, enable);
        };
    }
    
    // RPCで他のクライアントに、ボールの親子関係の解除を反映
    // PhotonTransformViewは主に位置（Position）、回転（Rotation）、およびスケール（Scale）を同期するために設計されていますが、親子関係（Parenting）自体の同期は行わないので、このRPCは必須。
    [PunRPC]
    void UnsetBallToPlayer()
    {        
        Debug.Log("ボールの親子関係を解除します");

        // ボールとプレイヤーの親子関係を解除
        if (transform.parent != null)
        {
            transform.parent = null;  // 親オブジェクトとのリンクを解除
            Debug.Log("ボールの親子関係を解除しました");
        }
        else
        {
            Debug.LogWarning("ボールの親子関係はすでに解除されています");
        }
    }

    // RPCで他のクライアントに、ボールの親子関係の追加する
    // 別プレイヤーが投げたボールをキャッチした場合、ボール位置がおかしくなる問題あり
    [PunRPC]
    void SetBallToPlayer(int ballHolderViewId)
    {
        Debug.Log($"ViewID:{ballHolderViewId}にボールの親子関係を設定します");

        // 対象のプレイヤーのViewを取得
        ballHolderView = PhotonView.Find(ballHolderViewId);
        if (ballHolderView == null)
        {
            Debug.LogError($"ViewID:{ballHolderViewId} のプレイヤーが見つかりません");
        }

        // 対象のプレイヤーの右手のボーンを見つける
        Transform rightHandBone = ballHolderView.transform.Find("mixamorig6:Hips/mixamorig6:Spine/mixamorig6:Spine1/mixamorig6:Spine2/mixamorig6:RightShoulder/mixamorig6:RightArm/mixamorig6:RightForeArm/mixamorig6:RightHand");
        if (rightHandBone == null)
        {
            Debug.LogError("右手のボーンが見つかりません");
        }

        // ボール位置を、右の手のひらに設定(親オブジェクトに追従させるので、ローカル座標にする)
        transform.SetParent(rightHandBone, false);
    }
}
