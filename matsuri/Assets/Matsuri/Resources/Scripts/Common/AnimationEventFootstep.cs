using UnityEngine;

public class AnimationEventFootstep : MonoBehaviour
{
    AudioSource audioSource;
    [SerializeField]
    AudioClip footstepSound;
    
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