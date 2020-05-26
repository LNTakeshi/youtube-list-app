using System;
using System.Net.Mime;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;


public class ButtonController : BaseButtonController
{
    [SerializeField]
    private List<GameObject> objList = new List<GameObject>();
    private Dictionary<string, GameObject> dict;

    private void Start() {
        dict = objList.ToDictionary(objectLists => objectLists.name);
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
            default:
                throw new System.Exception("Not implemented!!");

        }

    }
    private void AutoSkipToggleClicked(GameObject gameObject){

        try
        {
            GameObject obj;
            dict.TryGetValue("YoutubeListPlayer", out obj);
            bool enable = gameObject.GetComponent<Toggle>().isOn;
            PlayerPrefs.SetInt("autoSkipEnabled",enable ? 1 : 0);
            Debug.Log(enable ? 1 : 0);
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

    private void SubmitRoomButtonClick()
    {
        Debug.Log("Button1 Click");
        YoutubeListPlay youtubeListPlay = dict["YoutubeListPlayer"].GetComponent<YoutubeListPlay>();
        youtubeListPlay.setRoomId(dict["RoomIdText"].GetComponent<Text>().text);
        youtubeListPlay.setMasterId(dict["masterIdText"].GetComponent<Text>().text);


        youtubeListPlay.startGetJSONCorutine();
        dict["AutoSkipTimeInputField"].GetComponent<InputField>().text = PlayerPrefs.GetInt("autoSkipSecond", 360).ToString();
        dict["AutoSkipToggle"].GetComponent<Toggle>().isOn = PlayerPrefs.GetInt("autoSkipEnabled",0) == 1;
        Debug.Log(PlayerPrefs.GetInt("autoSkipEnabled",0) );
        
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

}