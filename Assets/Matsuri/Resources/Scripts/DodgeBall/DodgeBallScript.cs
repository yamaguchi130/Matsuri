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
    // 最後に投げたプレイヤーのスクリプト
    DodgeBallPlayerScript lastThrownPlayerScript;

    // Hit判定が有効かどうかのフラグ
    public bool isHitEnabled = false;

    // ボールのリスポーン時間(秒)
    float ballRespawnTime = 10f;

    // ボールの初期位置
    Vector3 initialPosition = new Vector3(0, 1, 0);

    // ボールをリスポーンしたかどうかのフラグ（ゲームスタート直後にリスポーンしないように、デフォルトtrue）
    private bool isBallRespawned = true;

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
                StartCoroutine(ShouldRespawnBall(ballRespawnTime));
            }
        }

        // プレイヤーに衝突したとき
        if (collision.gameObject.CompareTag("DodgeBallPlayer"))
        {     
            // 衝突したプレイヤーのスクリプトを取得
            DodgeBallPlayerScript collidingPlayerScript = collision.collider.GetComponent<DodgeBallPlayerScript>();

            if (collidingPlayerScript == null)
            {
                Debug.LogError("衝突したオブジェクトに DodgeBallPlayerScript が見つかりません。処理を中断します。");
                return;
            }

            // 衝突したプレイヤーオブジェクトのPhotonViewを取得
            PhotonView playerView = collision.collider.GetComponent<PhotonView>();
            // nullチェックを追加
            if (playerView == null)
            {
                Debug.LogError("衝突したオブジェクトに PhotonView が見つかりません。処理を中断します。");
                return;
            }

            // 敵チームのプレイヤーオブジェクトのViewID配列
            int[] enemyViewIDs;

            // ルームプロパティを取得
            var roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
            // 最後にボールを投げたプレイヤーがAチームの場合
            if(lastThrownPlayerScript.isAteam)
            {
                enemyViewIDs = (int[])roomProperties["bTeamViewIDs"];
            }
            // 最後にボールを投げたプレイヤーがBチームの場合
            else
            {
                enemyViewIDs = (int[])roomProperties["aTeamViewIDs"];
            }

            // 敵チームのプレイヤーオブジェクトのViewID分、ループする
            foreach (int viewId in enemyViewIDs)
            {
                // 各条件の詳細なログを追加
                Debug.Log("現在の enemyViewID: " + viewId);
                Debug.Log("衝突したプレイヤーの ViewID: " + playerView.ViewID);
                Debug.Log("isHitEnabled の値: " + isHitEnabled);

                // プレイヤーが投げたボールが、ノーバウンドで、相手チームのプレイヤーに衝突した場合
                if (viewId == playerView.ViewID && isHitEnabled)
                {
                    Debug.Log("ノーバウンドで敵プレイヤーにヒットしました！ViewID: " + viewId);
                    OnBallHit();  // ボールがヒットした際の処理を実行
                }
                else
                {
                    Debug.LogWarning("ヒット条件が満たされませんでした。ViewID: " + viewId);
                }
            }
        }
    }

    // ボールのリスポーン処理
    private IEnumerator ShouldRespawnBall(float RespawnTime)
    {
        // ボールのHit判定がない場合
        if (!isHitEnabled)
        {
            // ボールを○○秒後にリスポーンします..のようなカウント表示を入れたい

            // 指定秒間待機
            yield return new WaitForSeconds(RespawnTime);

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
                photonView.RPC("RespawnBallRPC", RpcTarget.AllViaServer);
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

        // デバッグログ：ルームプロパティの状態を確認
        Debug.Log("ルームプロパティを取得しました。");

        // プレイヤーのスコアを取得、存在しない場合は初期化
        if (!roomProperties.ContainsKey(lastThrownPlayerViewID))
        {
            Debug.Log("プレイヤーID " + lastThrownPlayerViewID + " のスコアが見つかりません。スコアを初期化します。");
            roomProperties[lastThrownPlayerViewID] = 0;
        }

        // スコアを更新
        int currentScore = (int)roomProperties[lastThrownPlayerViewID];
        Debug.Log("プレイヤーID " + lastThrownPlayerViewID + " の現在のスコア: " + currentScore);
        
        roomProperties[lastThrownPlayerViewID] = currentScore + 1;
        Debug.Log("プレイヤーID " + lastThrownPlayerViewID + " のスコアを更新しました: " + (currentScore + 1));

        // ルームプロパティを設定
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
        Debug.Log("ルームプロパティを更新しました。");

        // 最後にボールを投げたプレイヤーで、内野に復活するメソッドを実行
        Debug.Log("プレイヤーViewID： " + lastThrownPlayerViewID + " を内野に復活させます。");
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
        Debug.LogError("ボールの所有権の転送に失敗しました。ボールをリスポーンさせます。");
        // 即時リスポーン
        StartCoroutine(ShouldRespawnBall(0f));
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
            float shootForce = 12.0f;

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

        // 全てのクライアントに、ボールのHIt判定ありを同期
        photonView.RPC("HitDetectionRPC", RpcTarget.AllViaServer, true);

        // 全てのクライアントに、プレイヤーとボール親子関係の解除を通知
        photonView.RPC("StartUnsetBallToPlayerRPC", RpcTarget.AllViaServer, ballHolderView.ViewID);

        // 全てのクライアントでのRPC処理が完了してるかのチェック
        bool allCompleted = false;

        while (!allCompleted)
        {
            allCompleted = true;  // 一度 true に設定してからチェック

            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.CustomProperties.ContainsKey("UnsetCompleted_" + player.ActorNumber))
                {
                    if ((bool)player.CustomProperties["UnsetCompleted_" + player.ActorNumber] == false)
                    {
                        allCompleted = false;
                        break;
                    }
                }
                else
                {
                    allCompleted = false;
                    break;
                }
            }

            if (!allCompleted)
            {
                Debug.Log("まだ全クライアントの親子関係解除が完了していません。");
                yield return new WaitForSeconds(0.5f);  // 0.5秒待機して再チェック
            }
        }
        Debug.Log("全クライアントが親子関係の解除を完了しました");

        // ルームプロパティのボール保持者をリセット（-1にする）
        ExitGames.Client.Photon.Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
        roomProperties["currentBallHolderViewID"] = -1;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

        // プレイヤーの動きを全クライアントに同期するまで待機
        yield return new WaitForSeconds(1f);

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
    [PunRPC]
    void StartUnsetBallToPlayerRPC(int ballHolderViewID)
    {
        // コルーチンを開始
        StartCoroutine(UnsetBallToPlayer());

        // 最後に投げたプレイヤーのphotonViewIDを取得しておく
        lastThrownPlayerViewID = ballHolderViewID;

        // 最後にボールを投げたプレイヤーを取得
        PhotonView lastThrownPlayerPhotonView = PhotonView.Find(lastThrownPlayerViewID);
        if (lastThrownPlayerPhotonView == null)
        {
            Debug.LogError("プレイヤーID " + lastThrownPlayerViewID + " に対応する PhotonView が見つかりません。処理を中断します。");
            return;
        }

        GameObject lastThrownPlayerObject = lastThrownPlayerPhotonView.gameObject;
        if (lastThrownPlayerObject == null)
        {
            Debug.LogError("プレイヤーID " + lastThrownPlayerViewID + " に対応する GameObject が見つかりません。処理を中断します。");
            return;
        }

        // プレイヤーのスクリプトを取得
        lastThrownPlayerScript = lastThrownPlayerObject.GetComponent<DodgeBallPlayerScript>();
        if (lastThrownPlayerScript == null)
        {
            Debug.LogError("プレイヤーID " + lastThrownPlayerViewID + " に対応する DodgeBallPlayerScript が見つかりません。処理を中断します。");
            return;
        }
    }

    // ボールの親子関係解除のコルーチン
    IEnumerator UnsetBallToPlayer()
    {        
        Debug.Log("ボールの親子関係を解除します");

        // ボールとプレイヤーの親子関係を解除
        if (transform.parent != null)
        {
            // 親子関係を解除する前にボールの位置をワールド座標に変換
            transform.SetParent(null, true); // 第二引数trueで、ワールド座標を保持

            // 解除されるまでチェック
            while (transform.parent != null)
            {
                yield return null; // 1フレーム待機
                Debug.LogError("まだ親子関係が解除されていません。再確認中...");
            }

            Debug.Log("親子関係の解除が確認されました");
        }
        else
        {
            Debug.LogWarning("ボールの親子関係はすでに解除されています");
        }

        // カスタムプロパティを使用
        ExitGames.Client.Photon.Hashtable customProperties = new ExitGames.Client.Photon.Hashtable();
        // このクライアントでの処理完了を通知
        customProperties["UnsetCompleted_" + PhotonNetwork.LocalPlayer.ActorNumber] = true;
        PhotonNetwork.LocalPlayer.SetCustomProperties(customProperties);
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

        // 右手のひら＞ボールの親子関係を設定（手のひらに、ボールを追従させるため、ローカル座標にする）
        transform.SetParent(rightHandBone, false);

        // ボールのローカル座標と回転をリセットして、手のひらの中心に合わせる
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
