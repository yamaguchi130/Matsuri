using UnityEngine;
using Photon.Pun;

public class DodgeBallPanelController : MonoBehaviour
{
    private CanvasGroup whenHoldingTheBallPanel; // ボールを持っているパネル
    private CanvasGroup whenNotHoldingTheBallPanel; // ボールを持ってないパネル

    private PhotonView photonView;


    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
        // パネルのCanvasGroupを取得
        whenHoldingTheBallPanel = GameObject.Find("WhenHoldingTheBallPanel").GetComponent<CanvasGroup>();
        whenNotHoldingTheBallPanel = GameObject.Find("WhenNotHoldingTheBallPanel").GetComponent<CanvasGroup>();
    }

    // ネットワーク経由でフラグに基づいて、
    // 対象のクライアントでのみパネルの表示/非表示を切り替える
    [PunRPC]
    public void UpdatePanelVisibility(bool isHoldingBall)
    {
        // ボールを持ってる場合
        if (isHoldingBall)
        {
            SetPanelVisibility(whenHoldingTheBallPanel, true);
            SetPanelVisibility(whenNotHoldingTheBallPanel, false);
        }
        // ボールを持ってない場合
        else
        {
            SetPanelVisibility(whenHoldingTheBallPanel, false);
            SetPanelVisibility(whenNotHoldingTheBallPanel, true);
        }
    }

    // パネルの表示/非表示とインタラクティビティを設定する
    private void SetPanelVisibility(CanvasGroup panel, bool isVisible)
    {
        panel.alpha = isVisible ? 1f : 0f;
        panel.interactable = isVisible;
        panel.blocksRaycasts = isVisible;
    }

}