using System;
using Windows = System.Diagnostics;
using System.Net.Mime;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Web;
using System.IO;
using System.Text.RegularExpressions;
using MiniJSON;
using SpotifyAPI.Web;

public class Spotify : MonoBehaviour
{
    private string refreshToken;
    private string accessToken;
    private ISpotifyClient client;
    [SerializeField]
    private ButtonController buttonController;
    [SerializeField]
    private YoutubeListPlay youtubeListPlay;

    private DateTime lastUpdateDate = DateTime.MinValue;

    public Boolean isEnable = false;
    [SerializeField]
    private Text errorText;


    public IEnumerator NewClient()
    {
        var refreshToken = PlayerPrefs.GetString("spotifyRefreshToken");
        UnityWebRequest request = UnityWebRequest.Get("https://lntk.info/youtube-list/api/youtubelist/spotifyRefresh?token=" + refreshToken);
        yield return request.SendWebRequest();

        var handler = request.downloadHandler;
        Debug.Log(handler.text);
        var json = Json.Deserialize(handler.text) as Dictionary<string,object>;

        if(!json.ContainsKey("Code")){
            errorText.text = "Code not found";
            yield return youtubeListPlay.sendError("Spotify Auth","Code not found");
            yield break;
        }

        accessToken = json["Code"] as string;
        var c =  new SpotifyClient(accessToken);
        this.refreshToken = refreshToken;
        client = c;
        var h = c.Player.SeekTo(new PlayerSeekToRequest(0));
        var awaiter = h.GetAwaiter();
        yield return new WaitUntil(()=> awaiter.IsCompleted);
        if (h.IsFaulted){
            errorText.text = "Spotify SeekTo:" + h.Exception.Message;
            yield return youtubeListPlay.sendError("Spotify SeekTo",h.Exception.Message);
            yield break;
        }


        buttonController.dict["spotifyCodeField"].SetActive(false);
        buttonController.dict["spotifySubmitButton"].SetActive(false);
        buttonController.dict["spotifyAuthButton"].SetActive(false);

        lastUpdateDate = DateTime.Now;
        isEnable = true;
    }

    private async void Start () {
        StartCoroutine(UpdateStatus());
     }

    private IEnumerator UpdateStatus(){
        while (true){
            if(isEnable){
                yield return StartCoroutine(UpdateToken());
                var c = client.Player.SetRepeat(new PlayerSetRepeatRequest(PlayerSetRepeatRequest.State.Track));
                yield return new WaitUntil(()=> c.IsCompleted);
                if (c.IsFaulted){
                    errorText.text = "Spotify UpdateStatus:" + c.Exception.Message;
                    yield return youtubeListPlay.sendError("Spotify UpdateStatus", c.Exception.Message);
                }
            }
            yield return new WaitForSeconds(300);
        }
    }

    public IEnumerator StartPlaying(string URL,int startTime)
    {
        yield return StartCoroutine(UpdateToken());
        var uri = new Uri(URL);
        yield return StartCoroutine(PausePlayback());

        yield return StartCoroutine(AddToQueue(uri));
        // yield return StartCoroutine(SkipNext());
        yield return StartCoroutine(ResumePlayback());
        yield return StartCoroutine(SeekTo(startTime));
        StartCoroutine(FetchAlbumArt(uri.AbsolutePath.Split('/').Last()));
    }

    private IEnumerator PausePlayback(){
        var c = client.Player.PausePlayback();
        yield return new WaitUntil(()=> c.IsCompleted);
        if (c.IsFaulted){
            errorText.text = "Spotify IsCompleted:" + c.Exception.Message;
            yield return youtubeListPlay.sendError("Spotify PausePlayback", c.Exception.Message);
            StartCoroutine(youtubeListPlay.ShowTitlePanel("Spotify PausePlayback Error", c.Exception.Message));
            yield break;
        }
    }

    private IEnumerator AddToQueue(Uri uri){
        yield return new WaitForSeconds(1);
        var c = client.Player.AddToQueue(new PlayerAddToQueueRequest("spotify:track:" + uri.AbsolutePath.Split('/').Last() ));
        yield return new WaitUntil(()=> c.IsCompleted);
            if (c.IsFaulted){
            errorText.text = "Spotify IsCompleted:" + c.Exception.Message;
            yield return youtubeListPlay.sendError("Spotify PausePlayAddToQueue", c.Exception.Message);
            StartCoroutine(youtubeListPlay.ShowTitlePanel("Spotify AddToQueue Error", c.Exception.Message));
            yield break;
        }
    }

    private IEnumerator SkipNext(){
        yield return new WaitForSeconds(1);
        var c = client.Player.SkipNext().GetAwaiter();
        yield return new WaitUntil(()=> c.IsCompleted);
    }

    private IEnumerator SeekTo(int startTime){
        Debug.Log("SeekTo:" + startTime);
        if (startTime > 0){
            yield return new WaitForSeconds(1);
            var c = client.Player.SeekTo(new PlayerSeekToRequest(startTime * 1000));
            yield return new WaitUntil(()=> c.IsCompleted);
                if (c.IsFaulted){
                errorText.text = "Spotify IsCompleted:" + c.Exception.Message;
                yield return youtubeListPlay.sendError("Spotify SeekTo", c.Exception.Message);
                StartCoroutine(youtubeListPlay.ShowTitlePanel("Spotify SeekTo Error", c.Exception.Message));
                yield break;
            }
        }
    }
    private IEnumerator ResumePlayback(){
        yield return new WaitForSeconds(1);
        var c = client.Player.SkipNext();
        yield return new WaitUntil(()=> c.IsCompleted);
        if (c.IsFaulted && !c.Exception.Message.Contains("Restriction violated")){
            errorText.text = "Spotify ResumePlayback:" + c.Exception.Message;
            yield return youtubeListPlay.sendError("Spotify ResumePlayback", c.Exception.Message);
            StartCoroutine(youtubeListPlay.ShowTitlePanel("Spotify ResumePlayback Error", c.Exception.Message));
            yield break;
        }
    }

    private IEnumerator FetchAlbumArt(string trackID){
        var track = client.Tracks.Get(trackID);
        var awaiter = track.GetAwaiter();
        yield return new WaitUntil(()=> awaiter.IsCompleted);
        Debug.Log(track.Result.Album.Images.Count);
        var maxWidth = 0;
        SpotifyAPI.Web.Image selectImage = null;
        track.Result.Album.Images.ForEach((image)=>{
            if(maxWidth < image.Width){
                maxWidth = image.Width;
                selectImage = image;
            }
        });
        var url =  selectImage.Url ?? track.Result.Album.Images.First().Url ?? null;
        Debug.Log("Album URL:" + url);
        if (url != null) {
            var www = UnityWebRequestTexture.GetTexture(url);
            yield return www.SendWebRequest();
            youtubeListPlay.SetAlbumTexture(((DownloadHandlerTexture)www.downloadHandler).texture);
        }
    }

        public IEnumerator Stop()
    {
        if (client != null)
        {
            yield return StartCoroutine(UpdateToken());
            yield return client.Player.PausePlayback();
        }
    }

    private IEnumerator UpdateToken(){
        if(lastUpdateDate.AddMinutes(50) < DateTime.Now){
            Debug.LogWarning("Spotify Refresh Token");
            yield return NewClient();
        }
    }
}