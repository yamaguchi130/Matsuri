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

    // プレイヤーがボールを持ってるかどうかのフラグ
    public bool hasBall = false;

    // Hit判定が有効かどうかのフラグ
    public bool isHitEnabled = false;

    // ボールのリスポーン時間(秒)
    float ballRespawnTime = 10f;

    // ボールの初期位置
    Vector3 initialPosition = new Vector3(0, 2, 0);

    // ボールの回転速度の係数
    [SerializeField] private float _rotationSpeedFactor = 10f;

    // ボールをリスポーンしたかどうかのフラグ（デフォルトfalse）
    private bool isBallRespawned = false;

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
    }

    // 一定感覚（50回/秒）の呼び出し物理演算
    void FixedUpdate()
    {
        // プレイヤーがボールを持ってたら
        if(hasBall)
        {
            // 物理的な力（例えば、AddForce）や衝突によって移動しない
            // これを入れないと、プレイヤーがボールを持っているときに、プレイヤーがはねてしまう
            rb.isKinematic = true;

            // 未リスポーンに設定
            isBallRespawned = false;
        }
        // プレイヤーがボールを持ってなかったら
        else
        {   
            // 重力、衝突無効を設定しない（デフォルト）
            rb.isKinematic = false;

            // ボールのＸ軸,Ｚ軸が初期位置でない、かつ未リスポーンなら
            if ((transform.position.x != initialPosition.x || transform.position.y != initialPosition.y) && !isBallRespawned)
            {
                // ボールをリスポーンするかのチェック
                StartCoroutine(ShouldRespawnBall());
            }
        }
    }

    IEnumerator ShouldRespawnBall()
    {
        // ボールのHit判定がない場合
        if (!isHitEnabled)
        {
            // 指定秒間待機
            yield return new WaitForSeconds(ballRespawnTime);

            // 再度ボールの状態を確認して、ボールのHit判定がない場合
            if (!isHitEnabled)
            {
                // 速度をリセット
                rb.velocity = Vector3.zero;
                // 回転もリセットする
                rb.angularVelocity = Vector3.zero;
                // 慣性テンソルの回転をリセット
                rb.inertiaTensorRotation = Quaternion.identity;

                // 初期位置に戻す
                Debug.LogError("ボールを初期位置に戻します。");
                transform.position = initialPosition;

                // リスポーン済みに設定
                isBallRespawned = true;
            }
        }
    }

    // ボールが投げられたとき
    public void OnBallThrown(int ThrownPlayerViewID, bool isATeam)
    {
        // 誰もボールを持ってない判定にする
        hasBall = false;

        // 最後に投げたプレイヤーのViewIDを取得
        lastThrownPlayerViewID = ThrownPlayerViewID;

        // Aチームなら
        if (isATeam)
        {
            // 敵チーム（B）のViewIDリストを取得
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("bTeamViewIDs", out enemyTeamViewIDs))
            {
                Debug.LogError("敵のBチームのViewIDリストが取得できません");
            }
        }
        // Bチームなら
        else
        {
            // 敵チーム（A）のViewIDリストを取得
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("aTeamViewIDs", out enemyTeamViewIDs))
            {
                Debug.LogError("敵のAチームのViewIDリストが取得できません");
            }
        }
    }

    // オブジェクトの衝突時
    void OnCollisionEnter(Collision collision)
    {
        // 地面にバウンドしたら
        if (collision.gameObject.CompareTag("Terrain"))
        {
            Debug.LogError("OnCollisionEnter: Terrainとボールが衝突しました。");
            // Hit判定をなくす
            isHitEnabled = false;
            // 誰も持ってない判定にする
            hasBall = false;
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
            PhotonView collisionView = collision.collider.GetComponent<PhotonView>();

            foreach (int viewId in ids)
            {
                // プレイヤーが投げたボールが、相手チームのプレイヤーに衝突した場合
                if (viewId == collisionView.ViewID && isHitEnabled)
                {
                    OnBallHit();
                }
            }
        }    
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

    // RPCで他のクライアントに、ボールの親子関係の追加を反映
    [PunRPC]
    void SetBallToPlayer(int targetPlayerViewId)
    {
        // 対象のプレイヤーのViewを取得
        PhotonView targetPlayerView = PhotonView.Find(targetPlayerViewId);
        if (targetPlayerView == null)
        {
            Debug.LogError($"ViewID:{targetPlayerViewId} のプレイヤーが見つかりません");
        }

        // 対象のプレイヤーの右手のボーンを見つける
        Transform rightHandBone = targetPlayerView.transform.Find("mixamorig6:Hips/mixamorig6:Spine/mixamorig6:Spine1/mixamorig6:Spine2/mixamorig6:RightShoulder/mixamorig6:RightArm/mixamorig6:RightForeArm/mixamorig6:RightHand");
        if (rightHandBone == null)
        {
            Debug.LogError("右手のボーンが見つかりません");
        }

        // ボール位置を、右の手のひらに設定(親オブジェクトに紐づけるので、ローカル座標にする)
        transform.SetParent(rightHandBone, false);
    }

    // RPCで他のクライアントに、ボールの親子関係の解除を反映
    [PunRPC]
    void UnsetBallToPlayer()
    {
        // ボールとプレイヤーの親子関係をリセット
        // transform.SetParent(null);じゃダメ
        transform.parent = null;
    }
}
