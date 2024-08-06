using UnityEngine;

public class AnimationEventFootstep : MonoBehaviour
{
    AudioSource audioSource;
    [SerializeField]
    AudioClip footstepSound;
    
    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    void Start()
    {
        // シーン管理用オブジェクトの設定
        GameObject photonControllerGameObject = GameObject.Find("PhotonController");
        if (photonControllerGameObject == null)
        {
            Debug.LogError("PhotonControllerが見つかりません");
        }
        this.audioSource = photonControllerGameObject.GetComponent<AudioSource>();
    }

    public void PlayFootstepSound()
    {
        audioSource.PlayOneShot(footstepSound);
    }

}