using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーオブジェクト上部に表示する、プレイヤー名を制御するクラス
/// </summary>
public class PlayerNameDisplay : MonoBehaviourPunCallbacks
{
    private GameObject cameraGameObject; 
    private TextMeshPro nameLabel;

    private void Update()
    {
        // カメラの設定
        cameraGameObject = GameObject.Find("Camera");
        // プレイヤーのニックネームを設定
        nameLabel = GetComponent<TextMeshPro>();
        // ネームラベルをカメラに向ける
        nameLabel.transform.rotation = Quaternion.LookRotation(nameLabel.transform.position - cameraGameObject.transform.position);

        // オブジェクトのオーナーが取得できた場合（Updateが速すぎて、オーナーの読み込みができてない対策）
        if (photonView.Owner != null)
        {
            nameLabel.text = $"{photonView.Owner.NickName}";
           
        }
        else
        {
            nameLabel.text = "読み込み中...";
        }
    }
}
