using System.Collections;
using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    // 通常時のカメラオフセット
    private Vector3 defaultOffset = new Vector3(0, 1f, -2.5f); 
    // 後ろ移動時のカメラオフセット
    private Vector3 backMovementOffset = new Vector3(0, 1f, 2.5f); 
    private float lerpSpeed = 10.0f; // オフセットの補間速度

    // 追従するカメラのオブジェクト、位置、角度
    private CinemachineVirtualCamera _virtualCamera;
    private CinemachineFramingTransposer _framingTransposer;

    // バック移動してるか
    public bool isBack = false;

    void Start()
    {
        // カメラコンポーネントの取得
        _virtualCamera = GetComponent<CinemachineVirtualCamera>();
        if (_virtualCamera == null)
        {
            Debug.LogError("CinemachineVirtualCameraコンポーネントが見つかりません");
            return;
        }

        // FramingTransposerコンポーネントの取得
        _framingTransposer = _virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (_framingTransposer == null)
        {
            Debug.LogError("CinemachineFramingTransposerコンポーネントが見つかりません");
            return;
        }

        // 初期状態でのオフセットを設定する
        _framingTransposer.m_TrackedObjectOffset = defaultOffset;
    }

    void Update()
    {
        float lerpFactor = Time.deltaTime * lerpSpeed;
        if (!isBack) 
        {
            // デフォルトのオフセットのまま追従
            _framingTransposer.m_TrackedObjectOffset = Vector3.Lerp(_framingTransposer.m_TrackedObjectOffset, defaultOffset, lerpFactor);
            _virtualCamera.transform.rotation = Quaternion.Lerp(_virtualCamera.transform.rotation, Quaternion.Euler(30, 0, 0), lerpFactor);
        }
        // プレイヤーがバックしてる場合
        else
        {
            // バック移動のオフセットで追従し、カメラの角度は反対にする。
            _framingTransposer.m_TrackedObjectOffset = Vector3.Lerp(_framingTransposer.m_TrackedObjectOffset, backMovementOffset, lerpFactor);
            _virtualCamera.transform.rotation = Quaternion.Lerp(_virtualCamera.transform.rotation, Quaternion.Euler(45, 0, 0), lerpFactor);
        }
    }
}
