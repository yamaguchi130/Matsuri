using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PlayFab;
using PlayFab.ClientModels;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;

/// <summary>
/// PlayFabのログイン処理を行うクラス
/// </summary>
public class PlayFabLogin : MonoBehaviour {

    // システム通知用テキスト
    public TMP_Text notifyText;

    //=================================================================================
    // ログイン処理　(リリース前に手動でのAppleアカウントログインのみに修正する。Apple Developer Program(12800円/年)に契約必要)
    //=================================================================================

    // ユーザープロファイル表示用
    public TMP_Text userProfileText;

    // アカウントを作成するか
    private bool _shouldCreateAccount;

    // ログイン時に使うID
    private string _customID;

    // スクリプトが有効になってから、最初のフレームの更新が行われる前に呼び出し
    public void Start() {
        AutoLogin();
    }

    // 匿名ログイン
    private void AutoLogin() {
        // デバイスごとにユニークなカスタムIDを設定
        _customID = LoadCustomID();
        var request = new LoginWithCustomIDRequest { CustomId = _customID, CreateAccount = _shouldCreateAccount };
        PlayFabClientAPI.LoginWithCustomID(request, OnAutoLoginSuccess, OnAutoLoginFailure);

        // Playfabのユーザー名が未設定の場合、Playfabアカウントの設定をするように通知
    }

    // 匿名ログイン成功
    private void OnAutoLoginSuccess(LoginResult result) {
        // アカウントを作成しようとしたのに、IDが既に使われていて、出来なかった場合
        if (_shouldCreateAccount && !result.NewlyCreated) {
            NotifyWarning($"CustomId : {_customID} は既に使われています。");
            AutoLogin(); // ログインしなおし
            return;
        }
        // アカウント作成時にIDを保存
        if (result.NewlyCreated) {
            SaveCustomID();
        }
        // ユーザー名の設定
        SetUsername(result.PlayFabId);
        NotifyInfo("匿名ログインに成功しました。");
    }

    // 匿名ログイン失敗
    private void OnAutoLoginFailure(PlayFabError error) {
        NotifyError($"匿名ログインに失敗しました。\n{error.GenerateErrorReport()}");
    }

    // 手動ログイン
    public void ManualLogin() {
        // inputからをユーザー名、パスワードを読み込み
        TMP_InputField userNameInputField = GameObject.Find("LoginUserNameInputField").GetComponentInChildren<TMP_InputField>();
        TMP_InputField passwordInputField = GameObject.Find("LoginUserPasswordInputField").GetComponentInChildren<TMP_InputField>();
        var request = new LoginWithPlayFabRequest { Username = userNameInputField.text, Password = passwordInputField.text };
        PlayFabClientAPI.LoginWithPlayFab(request, OnManualLoginSuccess, OnManualLoginFailure);
    }

    // 手動ログイン成功
    private void OnManualLoginSuccess(LoginResult result) {
        // ユーザー名の設定
        SetUsername(result.PlayFabId);
        NotifyInfo("手動ログインに成功しました。");
    }

    // 手動ログイン失敗
    private void OnManualLoginFailure(PlayFabError error) {
        NotifyError($"手動ログインに失敗しました。\n{error.GenerateErrorReport()}");
    }


    //=================================================================================
    // ユーザー名の設定
    //=================================================================================

    // ユーザー名の設定
    private void SetUsername(string playfabId) {
        // playfabからログインユーザーのプロファイル取得
        var request = new GetPlayerProfileRequest
        {
            PlayFabId = playfabId,
            ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true
            }
        };
        // プロファイル取得
        PlayFabClientAPI.GetPlayerProfile(request, OnGetProfileSuccess, OnGetProfileFailure);
    }

    // プロファイル取得成功時
    private void OnGetProfileSuccess(GetPlayerProfileResult result) {
        NotifyInfo($"ユーザープロファイルの取得に成功しました。CustomID:{_customID}");
        // ユーザーのプロファイルにユーザ名の設定がある場合
        if (result.PlayerProfile != null && result.PlayerProfile.DisplayName != null)
        {
            // ユーザー名を設定
            PhotonNetwork.NickName = result.PlayerProfile.DisplayName;
            NotifyInfo($"ユーザー名の取得に成功しました。ユーザー名:{PhotonNetwork.NickName}");
        }
        else
        {

            // ユーザー名を設定
            PhotonNetwork.NickName = $"Player({_customID})";
            NotifyInfo($"ユーザー名を自動設定しました。ユーザー名:{PhotonNetwork.NickName}");
        }
        userProfileText.text = $"ログインユーザー情報\nカスタムID: {_customID}\nユーザ―名: {PhotonNetwork.NickName}\n";
    }

    // プロファイル取得失敗時
    private void OnGetProfileFailure(PlayFabError error) {
        NotifyError($"ユーザープロファイルの取得に失敗しました。CustomID:{_customID}、 error:{error.GenerateErrorReport()}");
    }

    // ユーザー名を更新する関数
    public void UpdateDisplayName() {
        // inputからをユーザー名、パスワードを読み込み
        TMP_InputField newUserNameInputField = GameObject.Find("NewUserNameInputField").GetComponentInChildren<TMP_InputField>();
        var request = new UpdateUserTitleDisplayNameRequest { DisplayName = newUserNameInputField.text };
        
        PlayFabClientAPI.UpdateUserTitleDisplayName(request, OnDisplayNameUpdateSuccess, OnDisplayNameUpdateFailure);
    }

    // ユーザー名の更新が成功したときのコールバック
    private void OnDisplayNameUpdateSuccess(UpdateUserTitleDisplayNameResult result)
    {
        NotifyInfo("ユーザー名の更新に成功しました！: " + result.DisplayName);
    }

    // ユーザー名の更新に失敗したときのコールバック
    private void OnDisplayNameUpdateFailure(PlayFabError error)
    {
        NotifyError("ユーザー名の更新に失敗しました: " + error.GenerateErrorReport());
    }

    //=================================================================================
    // カスタムID
    //=================================================================================

    // IDを保存する時のKEY
    private static readonly string CUSTOM_ID_SAVE_KEY = "CUSTOM_ID_SAVE_KEY";

    // IDを取得
    private string LoadCustomID() {
        string id = PlayerPrefs.GetString(CUSTOM_ID_SAVE_KEY);
        _shouldCreateAccount = string.IsNullOrEmpty(id);
        return _shouldCreateAccount ? GenerateCustomID() : id;
    }

    // IDの保存
    private void SaveCustomID() {
        PlayerPrefs.SetString(CUSTOM_ID_SAVE_KEY, _customID);
    }

    // カスタムIDの生成

    // IDに使用する文字
    private static readonly string ID_CHARACTERS = "0123456789abcdefghijklmnopqrstuvwxyz";

    // IDを生成する
    private string GenerateCustomID() {
        int idLength = 32; // IDの長さ
        StringBuilder stringBuilder = new StringBuilder(idLength);
        var random = new System.Random();

        for (int i = 0; i < idLength; i++) {
            stringBuilder.Append(ID_CHARACTERS[random.Next(ID_CHARACTERS.Length)]);
        }

        return stringBuilder.ToString();
    }


    //=================================================================================
    // 匿名アカウントに、Playfab用のEメール、ユーザー名、パスワードの設定
    //=================================================================================

    // 匿名アカウントに、Playfab用のEメール、ユーザー名、パスワードをリンクする
    public void LinkPlayfab() {
        TMP_InputField emailInputField = GameObject.Find("LinkEmailInputField").GetComponentInChildren<TMP_InputField>();
        TMP_InputField userNameInputField = GameObject.Find("LinkUserNameInputField").GetComponentInChildren<TMP_InputField>();
        TMP_InputField passwordInputField = GameObject.Find("LinkPasswordInputField").GetComponentInChildren<TMP_InputField>();

        string email = emailInputField.text;
        string username = userNameInputField.text;
        string password = passwordInputField.text;

        // Eメールのバリデーション
        if (!IsValidEmail(email))
        {
            NotifyError("入力されたEメールアドレスが無効です。");
            return;
        }

        // ユーザー名バリデーション
        if (!IsValidUsername(username)) {
            NotifyError("ユーザー名は3文字以上、20文字以下でなければなりません。");
            return;
        }

        // パスワードバリデーション
        if (!IsValidPassword(password)) {
            NotifyError("パスワードは8文字以上で、大文字、小文字、数字、特殊文字をそれぞれ1つ以上含む必要があります。");
            return;
        }

        var linkRequest = new AddUsernamePasswordRequest {
            Username = username,
            Email = email,
            Password = password
        };

        PlayFabClientAPI.AddUsernamePassword(linkRequest, OnLinkSuccess, OnLinkFailure);
    }

    // リンク成功時の処理
    private void OnLinkSuccess(AddUsernamePasswordResult result)
    {
        NotifyInfo("アカウントのリンクに成功しました。");
    }

    // リンク失敗時の処理
    private void OnLinkFailure(PlayFabError error)
    {
        NotifyError($"アカウントのリンクに失敗しました。\n{error.GenerateErrorReport()}");
    }


    //=================================================================================
    // パスワードのリセット
    //=================================================================================

    // パスワードリセットメールを送信する関数
    public void SendPasswordResetEmail()
    {
        // inputからメールアドレスを読み込み
        TMP_InputField toMailAddressInputField = GameObject.Find("ToMailAddressInputField").GetComponentInChildren<TMP_InputField>();
        
        var request = new SendAccountRecoveryEmailRequest
        {
            // 入力されたメールアドレス
            Email = toMailAddressInputField.text, 
            // あなたの PlayFab タイトルID
            TitleId = PlayFabSettings.TitleId
        };

        PlayFabClientAPI.SendAccountRecoveryEmail(request, OnPasswordResetMailSent, OnError);
    }

    // メール送信成功時のコールバック
    private void OnPasswordResetMailSent(SendAccountRecoveryEmailResult result)
    {
        NotifyInfo("パスワードリセットメールを送付しました。");
    }

    // エラー発生時のコールバック
    private void OnError(PlayFabError error)
    {
        NotifyError("パスワードリセットメールの送付に失敗しました。 :" + error.GenerateErrorReport());
    }


    //=================================================================================
    // Eメール、ユーザー名、パスワードのバリデーション
    //=================================================================================

    // Eメールのバリデーション
    private bool IsValidEmail(string email)
    {
        // ユーザー名@ドメイン.トップレベルドメインになっているか
        return Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase);
    } 

    // ユーザー名のバリデーション
    private bool IsValidUsername(string username) {
        // 3~10文字（全角、半角の区別はなし）
        return username.Length >= 3 && username.Length <= 10;
    }

    // パスワードのバリデーション
    private bool IsValidPassword(string password) {
        // 8文字以上か（全角、半角の区別はなし）
        if (password.Length < 8) return false;
        // 大文字を含むか
        if (!password.Any(char.IsUpper)) return false;
        // 小文字を含むか
        if (!password.Any(char.IsLower)) return false;
        // 数字を含むか
        if (!password.Any(char.IsDigit)) return false;
        return true;
    }


    //=================================================================================
    // 通知処理
    //=================================================================================

    private void NotifyError(string text)
    {
        Debug.LogError(text);
        notifyText.text = text;
    }

    private void NotifyWarning(string text)
    {
        Debug.LogWarning(text);
        notifyText.text = text;
    }


    private void NotifyInfo(string text)
    {
        Debug.Log(text);
        notifyText.text = text;
    }
}
