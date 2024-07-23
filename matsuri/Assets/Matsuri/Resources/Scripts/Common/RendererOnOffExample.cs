using UnityEngine;

public class RendererOnOffExample : MonoBehaviour
{
    // 点滅フラグ（Falseになった場合、一度だけ、メソッド実行）
    private bool _isFlash = false;
    public bool isFlash
    {
        get { return _isFlash; }
        set
        {
            if (_isFlash != value)
            {
                _isFlash = value;
                OnFlashStateChanged();
            }
        }
    }

    // 点滅させる対象
    [SerializeField] private Renderer _target;
    // 点滅周期[s]
    [SerializeField] private float _cycle = 1;

    private double _time;

    private void Update()
    {
        if (!_isFlash)
        {
            return;
        }

        // 内部時刻を経過させる
        _time += Time.deltaTime;

        // 周期cycleで繰り返す値の取得
        // 0～cycleの範囲の値が得られる
        var repeatValue = Mathf.Repeat((float)_time, _cycle);

        // 内部時刻timeにおける明滅状態を反映
        _target.enabled = repeatValue >= _cycle * 0.5f;
    }

    private void OnFlashStateChanged()
    {
        if (!_isFlash)
        {
            // isFlashがfalseになったときにオブジェクトを表示する
            _target.enabled = true;
        }
    }
}