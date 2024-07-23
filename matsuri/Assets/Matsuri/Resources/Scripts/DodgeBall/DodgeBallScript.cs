using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;
using Cinemachine;

/// <summary>
/// ドッヂボール用のボールを制御するクラス
/// </summary>
public class DodgeBallScript : MonoBehaviourPun
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

    // 最後に投げたプレイヤーのViewID
    private int lastThrownPlayerViewID = -1;

    // 敵チームのプライヤーオブジェクトのViewIDリスト
    object enemyTeamViewIDs;

    // プレイヤーがボールを持ってるかどうかのフラグ
    public bool hasBall = false;

    // Hit判定が有効かどうかのフラグ
    public bool isHitEnabled = false;

    // ボールのリスポーン時間(5秒)
    float ballRespawnTime = 5f;

    // ボールの初期位置
    Vector3 initialPosition = new Vector3(0, 5, 0);

    // ボールの回転速度の係数
    [SerializeField] private float _rotationSpeedFactor = 10f;

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
    }

    // 一定感覚（50回/秒）の呼び出し物理演算
    void FixedUpdate()
    {
        // プレイヤーがボールを持ってなかったら
        if(!hasBall)
        {
            // ボールをリスポーンするかのチェック
            // StartCoroutine(ShouldRespawnBall());
        }

    }

    // ボールが投げられたとき
    public void OnBallThrown(int ThrownPlayerViewID, bool isATeam)
    {
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

    IEnumerator ShouldRespawnBall()
    {
        while (true)
        {
            // ボールのHit判定がない場合
            if (!isHitEnabled)
            {
                // 5秒間待機
                yield return new WaitForSeconds(ballRespawnTime);

                // 再度ボールの状態を確認して、ボールのHit判定がない場合
                if (!isHitEnabled)
                {
                    // 初期位置に戻す
                    Debug.Log("ボールを初期位置に戻します。");
                    transform.position = initialPosition;
                }
            }
            else
            {
                // 次のフレームまで待機
                yield return null;
            }
        }
    }

    // オブジェクトの衝突時
    void OnCollisionEnter(Collision collision)
    {
        // 敵チームのプレイヤーオブジェクトのViewIDループする
        int[] ids = (int[])enemyTeamViewIDs;

        // 敵チームのプレイヤーオブジェクトがない場合
        if (ids == null || ids.Length == 0)
        {
            // 何もしない
            return;
        }

        // 地面に1バウンドしたら
        if (collision.gameObject.CompareTag("Plane"))
        {
            // Hit判定をなくす
            isHitEnabled = false;
        }

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
}
