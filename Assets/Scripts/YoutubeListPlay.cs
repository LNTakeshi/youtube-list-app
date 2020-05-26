using Windows = System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net.Mime;
using System.Dynamic;
using System; //NonSerializedなど
using System.IO; //入出力
using VideoLibrary; //Youtubeデータ取得
using System.Threading.Tasks; //非同期処理
using UnityEngine.Video; //動画再生
using UnityEditor; //アセット操作
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Networking;
using MiniJSON;
using Asyncoroutine;


public class YoutubeListPlay : MonoBehaviour
{
    private enum FileType
    {
        sound,
        movie
    }

    private class Movie{
        public string title;
        public string filePath;
        public string username;
        public FileType fileType;
    }

    private class JsonInfo{
        public string username;
        public string title;
        public string url;
    }

    private string roomId = "";

    private string masterId = "";


    [SerializeField]
    private VideoPlayer videoPlayer;
    
    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private GameObject titlePanel;
    [SerializeField]
    private GameObject videoPlayerObject;
    [SerializeField]
    private GameObject infoPanel;
    [SerializeField]
    private GameObject addMoviePanel;
    [SerializeField]
    private GameObject inactiveObject;
    [SerializeField]
    private GameObject videoController;

    private AudioClip audioClip;

    private enum CurrentStatus{
        waiting,
        loading,
        playing
    }

    private CurrentStatus currentStatus = CurrentStatus.waiting;

    private int currentIndex = 0;
    private int loadingIndex = 0;
    private List<Movie> movieList = new List<Movie>();

    private List<JsonInfo> jsonList;
    private float elapsedTime;
    private float endTime;
    private float startTime;
    private float length;
    private float skipTime = (float)Double.MaxValue;

    private void Update () {
        elapsedTime += Time.deltaTime;
        if(currentStatus == CurrentStatus.waiting && IsNextMovieExists()) {
            Debug.Log("ファイル再生準備開始");
            currentStatus = CurrentStatus.loading;

            if(!IsFileExist(movieList[currentIndex].filePath)){
                Debug.Log("ファイルなし");
                titlePanel.transform.Find("TitleText").GetComponent<Text>().text = "再生失敗のためスキップされました：" +movieList[currentIndex].title;
                titlePanel.transform.Find("InfoText").GetComponent<Text>().text = movieList[currentIndex].username;
                StartCoroutine(ShowTitlePanel());
                currentIndex++;
                currentStatus = CurrentStatus.waiting;

                videoPlayer.Stop();
                audioSource.Stop();
                return;
            }
            Debug.Log("動画読み込み");
            if(masterId.Length != 0){
                StartCoroutine(SendIndexInfo());
            }
            switch (movieList[currentIndex].fileType)
            {
                case FileType.movie:
                    videoPlayerObject.SetActive(true);
                    infoPanel.SetActive(true);
                    Debug.Log("動画" + movieList[currentIndex].filePath);
                    videoPlayer.url = movieList[currentIndex].filePath;
                    videoPlayer.isLooping =true;
                    videoPlayer.Prepare();
                    break;
                case FileType.sound:
                    audioSource.gameObject.SetActive(true);
                    infoPanel.SetActive(true);
                    Debug.Log("音声");
                    StartCoroutine(LoadSoundAndPlay());
                    break;
                default:
                    break;
            }

        }

        if(currentStatus == CurrentStatus.waiting && (videoPlayerObject.activeSelf == true || audioSource.gameObject.activeSelf == true)){
            videoPlayerObject.SetActive(false);
            infoPanel.SetActive(false);
            audioSource.gameObject.SetActive(false);

        }

        if(videoPlayerObject.activeSelf == true && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.movie && !Double.IsNaN(videoPlayer.time / length)){
            Vector3 vector3 = infoPanel.transform.Find("SeekBar").transform.localScale;
            vector3.x = (float)(videoPlayer.time / length);
            infoPanel.transform.Find("SeekBar").transform.localScale = vector3;
            infoPanel.transform.Find("InfoText").GetComponent<Text>().text = "stack:" + (movieList.Count - currentIndex) + " " + Math.Floor(videoPlayer.time / 60).ToString("00") + ":" + Math.Floor(videoPlayer.time % 60).ToString("00") + "/" + Math.Floor(length / 60).ToString("00") + ":" + Math.Floor(length % 60).ToString("00");
        }else if(audioSource.gameObject.activeSelf == true && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.sound && audioClip != null && !Double.IsNaN(audioSource.time / length)){
            Vector3 vector3 = infoPanel.transform.Find("SeekBar").transform.localScale;
            vector3.x = (float)(audioSource.time / length);
            infoPanel.transform.Find("SeekBar").transform.localScale = vector3;
            infoPanel.transform.Find("InfoText").GetComponent<Text>().text = "stack:" + (movieList.Count - currentIndex) + " " + Math.Floor(audioSource.time / 60).ToString("00") + ":" + Math.Floor(audioSource.time % 60).ToString("00") + "/" + Math.Floor(length / 60).ToString("00") + ":" + Math.Floor(length % 60).ToString("00");
        }

        if(currentStatus == CurrentStatus.playing && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.sound && elapsedTime > endTime){
            Debug.Log("音声による再生停止");
            currentStatus = CurrentStatus.waiting;
            videoPlayer.Stop();
            audioSource.Stop();
            audioSource.GetComponent<RawImage>().enabled = false;

        }

        if(elapsedTime > skipTime){
            Debug.Log("オートスキップ");
            currentStatus = CurrentStatus.waiting;
            videoPlayer.Stop();
            audioSource.Stop();
            audioSource.GetComponent<RawImage>().enabled = false;
            skipTime = (float) Double.MaxValue;
        }
        // Debug.Log(movieList.Count);
    }
     private async void Start () {
            videoPlayer.loopPointReached += StopMoviePlay;
            videoPlayer.prepareCompleted += StartNextMovie;
 
            RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.ARGB32);
            videoPlayer.GetComponent<VideoPlayer>().targetTexture = renderTexture;
            videoPlayer.GetComponent<RawImage>().texture = renderTexture;
            

     }
    //     // var v = await GetVideoInforationAsync(uri);
    //     // if(v == null) return;

    //     // if(!(IsFormatSupport(v.FileExtension))) return;

    //     // movieTitle = v.FullName;
    //     // moviePath = Application.dataPath + @"/Movies/" + movieTitle;
    //     // if(IsFileExist(moviePath)) return;

    //     // var t = await DownLoadMovieFromYoutubeAsync(v);
    //     // if(t == null) return;

    //     // File.WriteAllBytes(moviePath,t);
    //     // AssetDatabase.Refresh();
    // }
    private void StartNextMovie(VideoPlayer vp){
        Debug.Log("startNext");
        // StartCoroutine(ShowTitlePanel());
        infoPanel.transform.Find("TitleText").GetComponent<Text>().text = movieList[currentIndex].title;
        infoPanel.transform.Find("UsernameText").GetComponent<Text>().text = movieList[currentIndex].username;
        videoPlayer.Play();
        startTime = elapsedTime;
        endTime = elapsedTime + (float)videoPlayer.length;
        length = (float)videoPlayer.length;
        currentStatus = CurrentStatus.playing;
        currentIndex++;
        PrepareAutoSkip();
        Debug.Log("start:"+ startTime + " end:" + endTime + "length:" + length + "skiptime:" + skipTime);
    }


    private void StopMoviePlay(VideoPlayer vp){
        currentStatus = CurrentStatus.waiting;
        videoPlayer.Stop();
    }

    private IEnumerator LoadSoundAndPlay(){
        Debug.Log("audioStart");
        UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(movieList[currentIndex].filePath,AudioType.OGGVORBIS);
        yield return request.SendWebRequest();
        audioClip = DownloadHandlerAudioClip.GetContent(request);
        audioSource.clip = audioClip;
        infoPanel.transform.Find("TitleText").GetComponent<Text>().text = movieList[currentIndex].title;
        infoPanel.transform.Find("UsernameText").GetComponent<Text>().text = movieList[currentIndex].username;
        audioSource.Play();
        endTime = elapsedTime + audioClip.length;
        startTime = elapsedTime;
        length = audioClip.length;
        currentIndex++;
        currentStatus = CurrentStatus.playing;
        audioSource.GetComponent<RawImage>().enabled = true;
        PrepareAutoSkip();

    }


    private IEnumerator JSONGetCoroutine(){
        while(true){
            Debug.Log(roomId);
            UnityWebRequest request = new UnityWebRequest("https://lntk.info/youtube-list/api/youtubelist/getList?room_id=" + roomId, "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();


            // リクエスト送信
            yield return request.SendWebRequest();

            // 通信エラーチェック
            if (request.isNetworkError)
            {
                Debug.LogError(request.error);
            }
            else
            {
                Debug.Log(request.responseCode);
                if (request.responseCode == 200)
                {
                    Debug.Log(request.downloadHandler.text);
                    Dictionary<string, object> jsonDic = Json.Deserialize (request.downloadHandler.text) as Dictionary<string, object>;
                    List<object> json = jsonDic["data"] as List<object>;
                    jsonList = new List<JsonInfo>();
                    foreach (Dictionary<string, object> jsonObj in json){
                        JsonInfo jsonInfo = new JsonInfo();
                        jsonInfo.username = jsonObj["username"] as string;
                        jsonInfo.title = jsonObj["title"] as string;
                        jsonInfo.url = jsonObj["url"] as string;
                        jsonList.Add(jsonInfo);
                    }

                    if(jsonList.Count > loadingIndex){
                        SafeCreateDirectory(Application.temporaryCachePath + "/Movies/");

                        Movie movie = new Movie();
                        movie.title = jsonList[loadingIndex].title;
                        movie.username = jsonList[loadingIndex].username;

                        string fileExtension = "";
                        if(jsonList[loadingIndex].url.StartsWith("https://soundcloud.com")){
                            movie.fileType = FileType.sound;
                            fileExtension = ".mp3";
                        }else{
                            movie.fileType = FileType.movie;
                            fileExtension = ".mp4";
                        }
                        movie.filePath = Application.temporaryCachePath + "/Movies/" + Regex.Replace(movie.title, "[\\/:*\"<>|%?]","") + fileExtension;
                        string youtubeDlPath = Directory.GetCurrentDirectory() + "/youtube-dl.exe";
                        Debug.Log(movie.filePath);

                        Windows.ProcessStartInfo pInfo = new Windows.ProcessStartInfo();
                        pInfo.FileName = youtubeDlPath;
                        if(jsonList[loadingIndex].url.StartsWith("https://www.nicovideo.jp") || jsonList[loadingIndex].url.StartsWith("https://sp.nicovideo.jp")){
                            pInfo.Arguments = "-o \"" +movie.filePath + "\" --external-downloader aria2c --external-downloader-args \"-c -x 5 -k 2M\" " + jsonList[loadingIndex].url;

                        }else{
                            pInfo.Arguments = "-o \"" +movie.filePath + "\" " + jsonList[loadingIndex].url;
                        }
                        pInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                        pInfo.UseShellExecute = false; 
                        Debug.Log(pInfo.Arguments);
                        

                        Debug.Log("youtube-dl開始");
                        inactiveObject.transform.Find("StatusText").GetComponent<Text>().text = "動画取得中";
                        inactiveObject.transform.Find("TitleText").GetComponent<Text>().text = "NEXT:" + movie.title;
                        inactiveObject.transform.Find("InfoText").GetComponent<Text>().text = movie.username　!= "" ? movie.username : "お名前未入力";

                        int retryCount =0;

                        while(!IsFileExist(movie.filePath) && retryCount < 5){
                            Windows.Process p = Windows.Process.Start(pInfo);

                            while(!p.HasExited){
                                yield return new WaitForSeconds(1);
                            }
                            retryCount++;
                        }

                        if(movie.fileType == FileType.sound){
                            pInfo = new Windows.ProcessStartInfo();
                            string ffmpegPath = Directory.GetCurrentDirectory() + "/ffmpeg/ffmpeg.exe";
                            pInfo.FileName = ffmpegPath;
                            pInfo.Arguments = "-i \"" +movie.filePath + "\" \"" + movie.filePath + ".ogg\"";
                            pInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                            pInfo.UseShellExecute = false;
                            movie.filePath  += ".ogg";

                            Windows.Process p = Windows.Process.Start(pInfo);

                                while(!p.HasExited){
                                    yield return new WaitForSeconds(1);
                                }
                        }


                        Debug.Log("youtube-dl終了");
                        inactiveObject.transform.Find("StatusText").GetComponent<Text>().text = "待機中";
                        inactiveObject.transform.Find("TitleText").GetComponent<Text>().text = "";
                        inactiveObject.transform.Find("InfoText").GetComponent<Text>().text = "";

                        loadingIndex++;
                        movieList.Add(movie);
                        if(IsFileExist(movie.filePath)){
                            addMoviePanel.transform.Find("TitleText").GetComponent<Text>().text = "動画登録完了:" + movie.title;
                            addMoviePanel.transform.Find("InfoText").GetComponent<Text>().text = movie.username;
                            StartCoroutine(ShowAddMoviePanel());
                            PrepareAutoSkip();
                        }else{
                            addMoviePanel.transform.Find("TitleText").GetComponent<Text>().text = "動画登録失敗:" + movie.title;
                            addMoviePanel.transform.Find("InfoText").GetComponent<Text>().text = movie.username;
                            StartCoroutine(ShowAddMoviePanel());
                        }
                    }
                }
                else
                {

                }
            }
            yield return new WaitForSeconds(IsNextMovieExists() ? 30 : 5);
        }
    }
    
    private async Task<YouTubeVideo> GetVideoInforationAsync (string uri)
    {
        try {
            var youTube = YouTube.Default;
            var video = await youTube.GetVideoAsync(uri);
            Debug.Log("動画情報を取得しました。");
            return video;
        } catch(Exception e) {
            Debug.Log("動画情報取得時にエラーが発生しました。:" + e);
            return null;
        }
    }

    private async Task<byte[]> DownLoadMovieFromYoutubeAsync (YouTubeVideo y)
    {
        try {
            if(!y.IsEncrypted) {
                Debug.Log("動画の再生準備中です。少しお待ちください。");
                byte[] bytes = await y.GetBytesAsync();

                Debug.Log("完了しました！");
                return bytes;
            } else {
                Debug.Log("再生できない動画です。");
                return null;
            }
        } catch(Exception e) {
            Debug.Log("動画再生準備時にエラーが発生しました。:" + e);
            return null;
        }
    }

    private bool IsFileExist (string path)
    {
        if(File.Exists(path)) {
            Debug.Log("ファイルが存在します。");
            return true;
        }

        return false;
    }

    public void setRoomId(string roomId){
        this.roomId = roomId;
    }

    public void setMasterId(string masterId){
        this.masterId = masterId;
    }

    public void startGetJSONCorutine(){
        StartCoroutine(JSONGetCoroutine());
    }

    public static DirectoryInfo SafeCreateDirectory( string path )
    {
        if ( Directory.Exists( path ) )
        {
            return null;
        }
        return Directory.CreateDirectory( path );
    }

    private IEnumerator ShowTitlePanel(){


        float startTime = elapsedTime;
        Vector2 pivot = titlePanel.GetComponent<RectTransform>().pivot;
        while(elapsedTime - startTime < 1){
            pivot.y = 1 + (elapsedTime - startTime ) ; 
            titlePanel.GetComponent<RectTransform>().pivot = pivot;
            yield return null;
        }
        pivot.y = 2;
        titlePanel.GetComponent<RectTransform>().pivot = pivot;

        yield return new WaitForSeconds(3); 

        startTime = elapsedTime;
        while(elapsedTime - startTime < 1){
            pivot.y = 2 - (elapsedTime - startTime ); 
            titlePanel.GetComponent<RectTransform>().pivot = pivot;
            yield return null;
        }
            
        pivot.y = 1;
        titlePanel.GetComponent<RectTransform>().pivot = pivot;

        yield return null;
    }
    
    private IEnumerator ShowAddMoviePanel(){

        float startTime = elapsedTime;
        Vector2 pivot = addMoviePanel.GetComponent<RectTransform>().pivot;
        while(elapsedTime - startTime < 1){
            pivot.y = 1 - (elapsedTime - startTime ) ; 
            addMoviePanel.GetComponent<RectTransform>().pivot = pivot;
            yield return null;
        }
        pivot.y = 0;
        addMoviePanel.GetComponent<RectTransform>().pivot = pivot;

        yield return new WaitForSeconds(3); 

        startTime = elapsedTime;
        while(elapsedTime - startTime < 1){
            pivot.y = (elapsedTime - startTime ); 
            addMoviePanel.GetComponent<RectTransform>().pivot = pivot;
            yield return null;
        }
            
        pivot.y = 1;
        addMoviePanel.GetComponent<RectTransform>().pivot = pivot;

        yield return null;
    }

    public bool IsMoviePlaying(){
        return currentStatus == CurrentStatus.playing;
    }

    public bool IsNextMovieExists(){
        return movieList.Count > currentIndex;
    }

    public void SkipMovie(){
        if(IsNextMovieExists()){
            currentStatus = CurrentStatus.loading;

            if(!IsFileExist(movieList[currentIndex].filePath)){
                currentIndex++;
                return;
            }
            videoPlayer.Stop();
            audioSource.Stop();
            audioSource.GetComponent<RawImage>().enabled = false;


            switch (movieList[currentIndex].fileType)
            {
                case FileType.movie:
                    videoPlayer.url = movieList[currentIndex].filePath;
                    videoPlayer.isLooping =true;
                    videoPlayer.Prepare();
                    break;
                case FileType.sound:
                    StartCoroutine(LoadSoundAndPlay());
                    break;
                default:
                    break;
            }

            titlePanel.transform.Find("TitleText").GetComponent<Text>().text = movieList[currentIndex].title;
            string nextTitle = movieList.Count <= (currentIndex + 1) ? "なし" : movieList[currentIndex + 1].title;
            titlePanel.transform.Find("InfoText").GetComponent<Text>().text = "次の動画:" + nextTitle + " 残りスタック:" + (movieList.Count - currentIndex - 1);
        }
    }

    public IEnumerator SendIndexInfo(){

        WWWForm form = new WWWForm();
        form.AddField("room_id",roomId);
        form.AddField("master_id",masterId);
        form.AddField("index",currentIndex);

        UnityWebRequest request = UnityWebRequest.Post("https://lntk.info/youtube-list/api/youtubelist/setCurrentIndex", form);

        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        yield return request.SendWebRequest();
        Debug.Log(request.error + request.downloadHandler.text);
    }

    public void PrepareAutoSkip(){
        float prefSkipTime = PlayerPrefs.GetInt("autoSkipSecond",360);
        if(!IsNextMovieExists() || PlayerPrefs.GetInt("autoSkipEnabled",0) == 0 || currentStatus != CurrentStatus.playing){
            skipTime = (float)Double.MaxValue;
            length = endTime - startTime;
            return;
        }
        if(endTime - startTime + 10 < prefSkipTime || endTime - elapsedTime < 10){
            skipTime = (float)Double.MaxValue;
            length = endTime - startTime;
            return;
        }
        skipTime = startTime + prefSkipTime;
        length = prefSkipTime;
    }

}