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
    float ballRespawnTime = 5f;

    // ボールの初期位置
    Vector3 initialPosition = new Vector3(0, 1, 0);

    // ボールをリスポーンしたかどうかのフラグ（デフォルトfalse）
    private bool isBallRespawned = false;

    // ボールを持つプレイヤーオブジェクトのtransform
    private Transform ballHolderTransform;

    // ボールを持つプレイヤーオブジェクトのPhotonView
    private PhotonView ballHolderView;

    // isKinematic の設定完了フラグ（trueなら完了/falseなら未完了）
    public bool isKinematicSet = false;


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

        // コライダーを取得
        ballCol = GetComponent<Collider>();
    }

    // オブジェクトの衝突時
    void OnCollisionEnter(Collision collision)
    {
        // ボールが地面にバウンドしたら
        if (collision.gameObject.CompareTag("Terrain"))
        {
            // Hit判定をなくす
            isHitEnabled = false;

            // ボールのＸ軸,Ｚ軸が初期位置でない、かつ未リスポーンなら
            if ((transform.position.x != initialPosition.x || transform.position.y != initialPosition.y) && !isBallRespawned)
            {
                // ボールをリスポーンするかのチェック
                StartCoroutine(ShouldRespawnBall());
            }
        }

        // 敵チームのプレイヤーオブジェクトのViewIDループする
        int[] ids = (int[])enemyTeamViewIDs;
        // 敵チームのプレイヤーオブジェクトがない場合
        if (ids == null || ids.Length == 0)
        {
            // 何もしない
            return;
        }

        // プレイヤーに衝突したら
        if (collision.gameObject.CompareTag("DodgeBallPlayer"))
        {
            // 衝突したプレイヤーオブジェクトのView取得
            PhotonView playerView = collision.collider.GetComponent<PhotonView>();

            foreach (int viewId in ids)
            {
                // プレイヤーが投げたボールが、相手チームのプレイヤーに衝突した場合
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
            // 指定秒間待機
            yield return new WaitForSeconds(ballRespawnTime);

            // 再度ボールの状態を確認して、ボールのHit判定がない場合
            if (!isHitEnabled)
            {
                // RPCで全クライアントにリスポーン処理を同期
                photonView.RPC("RespawnBallRPC", RpcTarget.AllBuffered);
            }
        }
    }

    // RPCで全クライアントに同期されるリスポーン処理
    [PunRPC]
    private void RespawnBallRPC()
    {
        // ボールの物理挙動をリセット
        ResetRigidbody();

        // 初期位置に戻す
        Debug.Log("ボールを初期位置に戻します。");
        transform.position = initialPosition;

        // ボールがリスポーン済みと設定
        isBallRespawned = true;

        // ボールのHit判定をリセット
        isHitEnabled = false;

        // ボールの物理挙動を再度有効化
        rb.isKinematic = false;

        // ボールを持っているプレイヤーのtransformを削除
        ballHolderTransform = null;
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
    public void StraightThrowBall()
    {
        // プレイヤーからボールを切り離す
        DetachBallFromPlayer();

        try
        {
            // ボールをプレイヤーの前方1ユニット、高さ1ユニットに配置
            transform.position = ballHolderTransform.position + ballHolderTransform.forward * 1f + ballHolderTransform.up * 2f;

            // Animator発動
            // anim.SetTrigger("straightThrow");

            // 射出する力の大きさ 
            float shootForce = 8.0f;

            // 向きと大きさからボールに加わる力を計算する
            Vector3 force = ballHolderTransform.forward.normalized * shootForce;

            // 物理挙動の設定が有効な場合
            if (rb != null)
            {
                // 力を加えるメソッド(ForceMode.Impulseで短時間に大きな力を加える)
                rb.AddForce(force, ForceMode.Impulse);
            }
            else
            {
                Debug.LogError("リジッドボディ (Rigidbody) が見つかりません");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"StraightThrowBallで例外が発生しました: {ex.Message}");
        }
    }

    // ボールをやまなりにパスするコルーチン
    public void LobPass()
    {
        // プレイヤーからボールを切り離す
        DetachBallFromPlayer();

        try
        {
            // ボールをプレイヤーの前方1ユニット、高さ1ユニットに配置
            transform.position = ballHolderTransform.position + ballHolderTransform.forward * 1f + ballHolderTransform.up * 2f;

            // Animator発動
            // anim.SetTrigger("lobPass");

            // 射出する力の大きさ 
            float shootForce = 8.0f;

            // 力を加える向きをVector3型で定義
            Vector3 forceDirection = (ballHolderTransform.forward + ballHolderTransform.up).normalized;

            // 向きと大きさからボールに加わる力を計算する
            Vector3 force = forceDirection * shootForce;

            // 物理挙動の設定が有効な場合
            if (rb != null)
            {
                // 力を加えるメソッド(ForceMode.Impulseで短時間に大きな力を加える)
                rb.AddForce(force, ForceMode.Impulse);
            }
            else
            {
                Debug.LogError("リジッドボディ (Rigidbody) が見つかりません");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"LobPassで例外が発生しました: {ex.Message}");
        }
    }

    // ボールを落とすコルーチン
    public void DropBall()
    {
        // プレイヤーからボールを切り離す
        DetachBallFromPlayer();

        try
        {
            // ボールをプレイヤーの前方1ユニット、高さ1ユニットに配置
            transform.position = ballHolderTransform.position + ballHolderTransform.forward * 1f + ballHolderTransform.up * 2f;
        }
        catch (Exception ex)
        {
            Debug.LogError($"DropBallで例外が発生しました: {ex.Message}");
        }
    }

    // プレイヤーからボールを切り離すコルーチン
    private void DetachBallFromPlayer()
    {
        // 物理挙動を有効にする
        photonView.RPC("ToggleKinematicState", RpcTarget.AllBuffered, false);

        // ボールの物理挙動の設定が完了していたら
        if(isKinematicSet)
        {
            // 全てのクライアントに、プレイヤーとボール親子関係の解除を通知
            photonView.RPC("UnsetBallToPlayer", RpcTarget.AllViaServer);
            Debug.Log("ボールは未所持の状態");

            // ボールを持ってないパネルを表示
            ballHolderView.RPC("UpdatePanelVisibility", ballHolderView.Owner, false);
            Debug.Log("ボールを持ってないパネルを表示しました");
            
            // falseに戻す
            isKinematicSet = false;
        }
    }

    // RPCで他のクライアントに、ボールの親子関係の追加を反映
    [PunRPC]
    void SetBallToPlayer(int ballHolderViewId)
    {
        Debug.Log("ボールの親子関係を設定します");

        // 対象のプレイヤーのViewを取得
        ballHolderView = PhotonView.Find(ballHolderViewId);
        if (ballHolderView == null)
        {
            Debug.LogError($"ViewID:{ballHolderViewId} のプレイヤーが見つかりません");
        }

        // ボールを持っているプレイヤーのtransformを取得
        ballHolderTransform = ballHolderView.transform;
        // 対象のプレイヤーの右手のボーンを見つける
        Transform rightHandBone = ballHolderView.transform.Find("mixamorig6:Hips/mixamorig6:Spine/mixamorig6:Spine1/mixamorig6:Spine2/mixamorig6:RightShoulder/mixamorig6:RightArm/mixamorig6:RightForeArm/mixamorig6:RightHand");
        if (rightHandBone == null)
        {
            Debug.LogError("右手のボーンが見つかりません");
        }

        // ボール位置を、右の手のひらに設定(親オブジェクトに追従させるので、ローカル座標にする)
        transform.SetParent(rightHandBone, false);
    }

    // RPCで他のクライアントに、ボールの親子関係の解除を反映
    [PunRPC]
    void UnsetBallToPlayer()
    {
        Debug.Log("ボールの親子関係を解除します");

        // ボールとプレイヤーの親子関係を解除
        if (transform.parent != null)
        {
            transform.parent = null;  // 親オブジェクトとのリンクを解除
        }
        else
        {
            Debug.LogWarning("ボールの親子関係はすでに解除されています");
        }

        // 未リスポーンに設定
        isBallRespawned = false;
        Debug.Log("ボールはリスポーンしていません");
    }

    // Rigidbodyの物理挙動を有効化または無効化する(falseなら物理挙動有効/trueなら物理挙動無効)
    [PunRPC]
    public void ToggleKinematicState(bool enable)
    {
        // 課題
        // １、まれにボールを持ったプレイヤーの挙動がおかしい(青のマスターじゃないほうが、回転。オレンジのマスターの方が、後方移動)
        // ２、まれにボールの位置が、右手から離れてる。（アニメーションの影響？）
        
        // 物理挙動の設定
        rb.isKinematic = enable;
        Debug.Log($"RigidbodyのisKinematicを {enable} に設定しました");

        // true/物理挙動無効にするなら
        if(enable)
        {
            // ボールのバウンドを無効
            ballCol.material.bounciness = 0f;
        }
        // false/物理挙動有効にするなら
        else
        {
            // ボールのバウンドを有効（デフォルト:1）
            ballCol.material.bounciness = 1f;
        }

        // 物理処理が完了したことをフラグで通知
        isKinematicSet = true;
    }
}
