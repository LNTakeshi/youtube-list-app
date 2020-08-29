using System;
using System.Net.Mime;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Web;
using System.Text.RegularExpressions;


public class ButtonController : BaseButtonController
{
    [SerializeField]
    private List<GameObject> objList = new List<GameObject>();
    private Dictionary<string, GameObject> dict;


    private void Start() {
        dict = objList.ToDictionary(objectLists => objectLists.name);
        dict["nicoMailField"].GetComponent<InputField>().text = PlayerPrefs.GetString("nicoMail");
        dict["nicoPasswordField"].GetComponent<InputField>().text = PlayerPrefs.GetString("nicoPassword");

    }

    protected override void OnClick(GameObject gameObject)
    {
        // 渡されたオブジェクト名で処理を分岐
        //（オブジェクト名はどこかで一括管理した方がいいかも）
        switch (gameObject.name){
            case "SubmitRoomButton":
                SubmitRoomButtonClick();
                break;
            case "VideoSkipButton":
                VideoSkipButtonClick();
                break;
            case "OpenURLButton":
                OpenURLButtonClick();
                break;
            case "AutoSkipToggle":
                AutoSkipToggleClicked(gameObject);
                break;
            case "AutoSkipTimeInputField":
                AutoSkipTimeInputFieldEdit(gameObject);
                break;
            case "AutoSkipStackInputField":
                AutoSkipStackInputFieldEdit(gameObject);
                break;
            case "nicoSubmitButton":
                NicoSubmitButtonClicked();
                break;
            case "nicoCommentToggle":
                NicoCommentToggleClicked(gameObject);
                break;
            default:
                if(CommentData.NG_RADIO_BUTTONS.ContainsValue(gameObject.name)){
                    NicoCommentToggleClicked(gameObject);
                }else{
                    throw new System.Exception("Not implemented!!");
                }
                break;
        }

    }
    private void AutoSkipToggleClicked(GameObject gameObject){

        try
        {
            GameObject obj;
            dict.TryGetValue("YoutubeListPlayer", out obj);
            bool enable = gameObject.GetComponent<Toggle>().isOn;
            PlayerPrefs.SetInt("autoSkipEnabled",enable ? 1 : 0);
            PlayerPrefs.Save();
            dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>().PrepareAutoSkip();
        }
        catch (Exception e)
        {
            
        }

    }


    private void AutoSkipTimeInputFieldEdit(GameObject gameObject){
        int time = 360;
        try
        {
            time = Int32.Parse(gameObject.GetComponent<InputField>().text);
        }
        catch (Exception e)
        {
            
        } 
        time = time > 60 ? time : 60;
        gameObject.GetComponent<InputField>().text = time.ToString();
        PlayerPrefs.SetInt("autoSkipSecond", time);
        PlayerPrefs.Save();

        dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>().PrepareAutoSkip();
    }

    private void AutoSkipStackInputFieldEdit(GameObject gameObject){
        int stack = 1;
        try
        {
            stack = Int32.Parse(gameObject.GetComponent<InputField>().text);
        }
        catch (Exception e)
        {
            
        } 
        stack = stack > 0 ? stack : 1;
        gameObject.GetComponent<InputField>().text = stack.ToString();
        PlayerPrefs.SetInt("autoSkipStack", stack);
        PlayerPrefs.Save();

        dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>().PrepareAutoSkip();
    }

    private void SubmitRoomButtonClick()
    {
        Debug.Log("Button1 Click");
        YoutubeListPlay youtubeListPlay = dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>();
        youtubeListPlay.setRoomId(dict["RoomIdText"].GetComponent<Text>().text);
        youtubeListPlay.setMasterId(dict["masterIdText"].GetComponent<Text>().text);
        if(!youtubeListPlay.isNicoLoginSuccess){
            NicoSubmitButtonClicked();
        }


        youtubeListPlay.startGetJSONCorutine();
        dict["AutoSkipTimeInputField"].GetComponent<InputField>().text = PlayerPrefs.GetInt("autoSkipSecond", 360).ToString();
        dict["AutoSkipStackInputField"].GetComponent<InputField>().text = PlayerPrefs.GetInt("autoSkipStack", 1).ToString();
        dict["AutoSkipToggle"].GetComponent<Toggle>().isOn = PlayerPrefs.GetInt("autoSkipEnabled",0) == 1;
        
        dict["CreateRoomObject"].SetActive(false);
        dict["InactiveObject"].SetActive(true);
        dict["VideoControllerObject"].SetActive(true);
        dict["RoomNumberText"].GetComponent<Text>().text = "https://lntk.info/"+ dict["RoomIdText"].GetComponent<Text>().text + " にyoutubeのURLを投げつけると再生されます";
    }

    private void VideoSkipButtonClick(){
        if(dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>().IsMoviePlaying()){
            dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>().SkipMovie();
        }
    }

    private void OpenURLButtonClick(){
        Application.OpenURL("https://lntk.info/youtube-list/");

    }

    private void NicoSubmitButtonClicked(){
        StartCoroutine(SendIndexInfo());
    }

    
    public IEnumerator SendIndexInfo(){

        WWWForm form = new WWWForm();
        form.AddField("mail_tel", dict["nicoMailField"].GetComponent<InputField>().text);
        form.AddField("password", dict["nicoPasswordField"].GetComponent<InputField>().text);

        UnityWebRequest request = UnityWebRequest.Post("https://account.nicovideo.jp/api/v1/login", form);
        request.SetRequestHeader("User-Agent", "youtube-list@LNTakeshi");
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        yield return request.SendWebRequest();
        var header = String.Join(",", request.GetResponseHeaders().Select(r => r.Key + ":" + r.Value));
        Debug.Log( request.error + "Set-Cookie:" + header + " form:" + System.Text.Encoding.UTF8.GetString(form.data));

        request = UnityWebRequest.Get("http://flapi.nicovideo.jp/api/getflv/sm9");
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        yield return request.SendWebRequest();
        // header = String.Join(",", request.GetResponseHeaders().Select(r => r.Key + ":" + r.Value));

        // String threadId = null;
        // var param = GetParams(request.downloadHandler.text);
        // param.TryGetValue("thread_id", out threadId);
        // Debug.Log( request.error + " Set-Cookie:" + header +" body:" + request.downloadHandler.text + " thread_id:" + threadId +  " form:" + System.Text.Encoding.UTF8.GetString(form.data));


        PlayerPrefs.SetString("nicoMail", dict["nicoMailField"].GetComponent<InputField>().text);
        PlayerPrefs.SetString("nicoPassword", dict["nicoPasswordField"].GetComponent<InputField>().text);
        PlayerPrefs.Save();
        if(Int32.Parse(request.GetResponseHeader("x-niconico-authflag")) == 1){
            dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>().SetNicoCommentEnabled();
            dict["nicoMailField"].SetActive(false);
            dict["nicoPasswordField"].SetActive(false);
            dict["nicoSubmitButton"].SetActive(false);
        }
    }

    public void NicoCommentToggleClicked(GameObject gameObject){
        NGType? type = CommentData.NG_RADIO_BUTTONS.Where(b=> b.Value == gameObject.name).FirstOrDefault().Key;
        if(type != null){
            dict["NicoCommentController"].GetComponent<NicoCommentController>().setNGLevel((NGType)type);
        }
    }

}

