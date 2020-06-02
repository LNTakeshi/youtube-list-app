using System.Text;
using Windows = System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net.Mime;
using System.Dynamic;
using System; //NonSerializedなど
using System.IO; //入出力
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
using System.Linq;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;


public class YoutubeListPlay : MonoBehaviour
{
    private enum FileType
    {
        sound,
        movie,
        spotify,
    }

    private class Movie{
        public string title;
        public string filePath;
        public string username;
        public FileType fileType;
        public int startTimePadding;
    }

    private class JsonInfo{
        public string username;
        public string title;
        public string url;
        public bool isSkip;
        public int length;
        public int startTime;
        public int endTime;
    }

    private string roomId = "";

    private string masterId = "";


    [SerializeField]
    private VideoPlayer videoPlayer;

    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private GameObject soundPlayerBackground;
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
    [SerializeField]
    private NicoCommentController nicoCommentController;

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
    private DateTime lastUpdateDate = DateTime.MinValue;
    public Boolean isNicoLoginSuccess { get; private set;} = false;
    [SerializeField]
    private ButtonController buttonController;
    [SerializeField]
    private Spotify spotify;

    private float titlePanelStartTime;
    [SerializeField]
    private Text versionText;

    private void Update () {
        elapsedTime += Time.deltaTime;
        if(currentStatus == CurrentStatus.waiting && IsNextMovieExists()) {
            Debug.Log("ファイル再生準備開始");
            currentStatus = CurrentStatus.loading;

            if((movieList[currentIndex].fileType != FileType.spotify && spotify.isEnable) && (!IsFileExist(movieList[currentIndex].filePath)) || IsNextMovieSkipped()){
                Debug.Log("ファイルなし");
                StartCoroutine(ShowTitlePanel((IsNextMovieSkipped() ? "スキップされました：" : "再生失敗のためスキップされました：") +movieList[currentIndex].title, movieList[currentIndex].username));
                currentIndex++;
                StopPlaying();
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
                    audioSource.GetComponent<RawImage>().enabled = true;
                    infoPanel.SetActive(true);
                    Debug.Log("音声");
                    StartCoroutine(LoadSoundAndPlay());
                    break;
                case FileType.spotify:
                    soundPlayerBackground.SetActive(true);
                    audioSource.GetComponent<RawImage>().enabled = true;
                    infoPanel.SetActive(true);
                    Debug.Log("Spotify");
                    StartCoroutine(LoadSpotifyAndPlay());
                    break;
                default:
                    break;
            }

        }

        if(currentStatus == CurrentStatus.waiting && (videoPlayerObject.activeSelf == true || audioSource.GetComponent<RawImage>().enabled == true)){
            videoPlayerObject.SetActive(false);
            infoPanel.SetActive(false);
            audioSource.GetComponent<RawImage>().enabled = false;
        }

        if(videoPlayerObject.activeSelf == true && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.movie && !Double.IsNaN(videoPlayer.time / length)){
            Vector3 vector3 = infoPanel.transform.Find("SeekBar").transform.localScale;
            vector3.x = (float)(videoPlayer.time / length);
            infoPanel.transform.Find("SeekBar").transform.localScale = vector3;
            infoPanel.transform.Find("InfoText").GetComponent<Text>().text = "stack:" + (movieList.Count - currentIndex) + " " + Math.Floor(videoPlayer.time / 60).ToString("00") + ":" + Math.Floor(videoPlayer.time % 60).ToString("00") + "/" + Math.Floor(length / 60).ToString("00") + ":" + Math.Floor(length % 60).ToString("00");
        }else if(audioSource.gameObject.activeSelf == true && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.spotify){
            Vector3 vector3 = infoPanel.transform.Find("SeekBar").transform.localScale;
            var time =  elapsedTime - startTime;
            vector3.x = (float)(time / length);
            infoPanel.transform.Find("SeekBar").transform.localScale = vector3;
            infoPanel.transform.Find("InfoText").GetComponent<Text>().text = "stack:" + (movieList.Count - currentIndex) + " " + Math.Floor(time / 60).ToString("00") + ":" + Math.Floor(time % 60).ToString("00") + "/" + Math.Floor(length / 60).ToString("00") + ":" + Math.Floor(length % 60).ToString("00");
        }

        if(currentStatus == CurrentStatus.playing && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.spotify && elapsedTime > endTime){
            Debug.Log("音声による再生停止");
            StopPlaying();

        }

        if(elapsedTime > skipTime){
            Debug.Log("オートスキップ");
            StopPlaying();
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
            audioSource.gameObject.SetActive(true);
            Application.logMessageReceived += HandleLog;
            versionText.text = "version:"+Application.version;
     }

    void HandleLog (string condition, string stackTrace, LogType type)
	{
		if(type == LogType.Exception || type == LogType.Error || type == LogType.Warning){
            StartCoroutine(sendError(condition, stackTrace));
        }
	}

    public IEnumerator sendError(string condition, string stackTrace){
            WWWForm form = new WWWForm();
            form.AddField("addVersion", Application.version);
            form.AddField("sendTime", DateTime.Now.ToLongDateString());
            form.AddField("condition",condition);
            form.AddField("stackTrace",stackTrace);
            form.AddField("roomId", roomId);
            form.AddField("currentIndex", currentIndex);
            if(currentIndex < movieList.Count) form.AddField("currentTitle", movieList[currentIndex].title);

            UnityWebRequest request = UnityWebRequest.Post("https://lntk.info/youtube-list/api/youtubelist/sendError", form);

            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
            Debug.Log(condition +":"+stackTrace);

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
        try{
            nicoCommentController.startIfReady(currentIndex);
        }catch(Exception e){
            Debug.LogError(e.Message);
            Debug.LogError(e.StackTrace);
        }
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
        currentStatus = CurrentStatus.loading;
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

    private IEnumerator LoadSpotifyAndPlay(){
        Debug.Log("audioStart");
        currentStatus = CurrentStatus.loading;
        infoPanel.transform.Find("TitleText").GetComponent<Text>().text = movieList[currentIndex].title;
        infoPanel.transform.Find("UsernameText").GetComponent<Text>().text = movieList[currentIndex].username;
        length =  jsonList[currentIndex].length + 3;
        endTime = elapsedTime + length + 3;
        startTime = elapsedTime;
        var t = new Texture2D(1,1);
        t.SetPixel(0,0,Color.black);
        SetAlbumTexture(t);
        yield return spotify.StartPlaying(jsonList[currentIndex].url, jsonList[currentIndex].startTime);
        currentIndex++;
        currentStatus = CurrentStatus.playing;
        audioSource.GetComponent<RawImage>().enabled = true;
        PrepareAutoSkip();
    }

    public void SetAlbumTexture(Texture2D  texture){
        audioSource.GetComponent<RawImage>().texture = texture;
        var image = audioSource.GetComponent<RawImage>();
        image.FixAspect();
    }

    private IEnumerator JSONGetCoroutine(){
        while(true){
            Debug.Log(roomId);
            UnityWebRequest request = new UnityWebRequest("https://lntk.info/youtube-list/api/youtubelist/getList?room_id=" + roomId + "&lastUpdateDate=" +lastUpdateDate.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ssZ"), "GET");
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
                    var info = jsonDic["info"] as Dictionary<string, object>;
                    if ((Boolean)info["needUpdate"] == true){
                        lastUpdateDate = DateTime.Now;
                        List<object> json = jsonDic["data"] as List<object>;
                        jsonList = new List<JsonInfo>();
                        foreach (Dictionary<string, object> jsonObj in json){
                            JsonInfo jsonInfo = new JsonInfo();
                            jsonInfo.username = jsonObj["username"] as string;
                            jsonInfo.title = jsonObj["title"] as string;
                            jsonInfo.url = jsonObj["url"] as string;
                            string[] length = (jsonObj["length"] as string).Split(':');
                            jsonInfo.length = Int32.Parse(length[0]) *60*60 +  Int32.Parse(length[1]) * 60 + Int32.Parse(length[2]);
                            jsonInfo.startTime = (int)((long)jsonObj["start"]);
                            jsonInfo.endTime = (int)((long)jsonObj["end"]);
                            jsonInfo.isSkip = (bool)jsonObj["deleted"];
                            jsonList.Add(jsonInfo);
                        }
                    }

                    if(jsonList.Count > loadingIndex){
                        SafeCreateDirectory(Application.temporaryCachePath + "/Movies/");

                        Movie movie = new Movie();
                        movie.title = jsonList[loadingIndex].title;
                        movie.username = jsonList[loadingIndex].username;
                        movie.startTimePadding = jsonList[loadingIndex].startTime;

                        string fileExtension = "";
                        if(jsonList[loadingIndex].url.StartsWith("https://soundcloud.com")){
                            movie.fileType = FileType.sound;
                            fileExtension = ".mp3";
                        }else if (jsonList[loadingIndex].url.StartsWith("https://open.spotify.com/")){
                            movie.fileType = FileType.spotify;
                            fileExtension = ".mp3";
                        }else {
                            movie.fileType = FileType.movie;
                            fileExtension = ".mp4";
                        }
                        DateTime date = DateTime.Now;

                        movie.filePath = Application.temporaryCachePath + "/Movies/" + date.ToString("yyyyMMddHHmmss") + fileExtension;
                        string youtubeDlPath = Directory.GetCurrentDirectory() + "/youtube-dl.exe";
                        Debug.Log(movie.filePath);

                        Windows.ProcessStartInfo pInfo = new Windows.ProcessStartInfo();
                        pInfo.FileName = youtubeDlPath;
                        if(jsonList[loadingIndex].url.StartsWith("https://www.nicovideo.jp") || jsonList[loadingIndex].url.StartsWith("https://sp.nicovideo.jp")){
                            pInfo.Arguments = "-o \"" +movie.filePath + "\" --external-downloader aria2c --external-downloader-args \"-c -x 5 -k 2M\" " + jsonList[loadingIndex].url;
                            if(isNicoLoginSuccess){
                                pInfo.Arguments += " -u " + PlayerPrefs.GetString("nicoMail") + " -p " + PlayerPrefs.GetString("nicoPassword");
                                inactiveObject.transform.Find("StatusText").GetComponent<Text>().text = "コメント取得中";
                                inactiveObject.transform.Find("TitleText").GetComponent<Text>().text = "NEXT:" + movie.title;
                                inactiveObject.transform.Find("InfoText").GetComponent<Text>().text = movie.username != "" ? movie.username : "お名前未入力";
                                var url =  jsonList[loadingIndex].url.Split('/').Last();
                                Debug.Log(url);
                                UnityWebRequest commentRequest = UnityWebRequest.Get("https://www.nicovideo.jp/watch/" + url);
                                commentRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                                yield return commentRequest.SendWebRequest();
                                var parser = new HtmlParser();
                                var htmlDocument = parser.ParseDocument(commentRequest.downloadHandler.text);
                                Dictionary<string, object> commentJson = Json.Deserialize(htmlDocument.GetElementById("js-initial-watch-data").GetAttribute("data-api-data").ToString()) as Dictionary<string, object>;

                                int threadId = 0;
                                String userKey = null;
                                try{
                                    var comment = commentJson.First(key => key.Key == "comment").Value as Dictionary<string, object>;
                                    userKey = (comment.First(key => key.Key == "keys").Value as Dictionary<string, object>).First(key => key.Key == "userKey").Value as String;
                                    var layers = comment.First(key => key.Key == "layers").Value as IList;
                                    var layer = layers[0] as Dictionary<string, object>;
                                    var threadIds = layer.First(key => key.Key == "threadIds").Value as IList;
                                    var thread = (IDictionary) threadIds[0];
                                    threadId = (int)(long)thread["id"];
                                }catch (Exception e){
                                    Debug.Log(e);
                                }
                                Debug.Log("threadId:" + threadId);

                                if(threadId != 0){
                                    int commentCount = 100;
                                    if(jsonList[loadingIndex].length >= 60) commentCount = 200;
                                    if(jsonList[loadingIndex].length >= 240) commentCount = 400;
                                    if(jsonList[loadingIndex].length >= 300) commentCount = 1000;
                                    Debug.Log("length:" + length + "count:" + commentCount );
                                    String form = $@"[
    {{
        ""thread"": {{
            ""thread"": ""{threadId}"",
            ""version"": ""20090904"",
            ""language"": 0,
            ""with_global"": 1,
            ""scores"": 1,
            ""nicoru"": 3
        }}
    }},
    {{
        ""thread_leaves"": {{
            ""thread"": ""{threadId}"",
            ""language"": 0,
            ""content"": ""0-30:100,{commentCount},nicoru:100"",
            ""scores"": 1,
            ""nicoru"": 3
        }}
    }}
]";
                                    commentRequest =  new UnityWebRequest("https://nvcomment.nicovideo.jp/legacy/api.json/");
                                    commentRequest.method = "POST";
                                    commentRequest.uploadHandler = new UploadHandlerRaw( Encoding.UTF8.GetBytes(form));
                                    commentRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                                    commentRequest.uploadHandler.contentType = "application/json; encoding='utf-8'";
                                    yield return commentRequest.SendWebRequest();
                                    Debug.Log( commentRequest.error + "body:" + commentRequest.downloadHandler.text + " thread_id:" + threadId + " form:" + form);
                                    nicoCommentController.setComment(loadingIndex, commentRequest.downloadHandler.text, jsonList[loadingIndex].startTime);
                                }else{
                                    StartCoroutine(sendError("DEBUG", "NICONICO_THREAD_ID_NOT_FOUND"));
                                }
                            }
                        }else{
                            pInfo.Arguments = "-o \"" +movie.filePath + "\" " + jsonList[loadingIndex].url;
                        }
                        pInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                        pInfo.UseShellExecute = false;
                        Debug.Log(pInfo.Arguments);


                        Debug.Log("youtube-dl開始");
                        inactiveObject.transform.Find("StatusText").GetComponent<Text>().text = "動画取得中";
                        inactiveObject.transform.Find("TitleText").GetComponent<Text>().text = "NEXT:" + movie.title;
                        inactiveObject.transform.Find("InfoText").GetComponent<Text>().text = movie.username != "" ? movie.username : "お名前未入力";

                        int retryCount =0;

                        if (movie.fileType != FileType.spotify) {
                            while( !IsFileExist(movie.filePath) && retryCount < 5){
                                Windows.Process p = Windows.Process.Start(pInfo);

                                while(!p.HasExited){
                                    yield return new WaitForSeconds(1);
                                }
                                retryCount++;
                            }
                        }

                        if(movie.fileType == FileType.sound){
                            pInfo = new Windows.ProcessStartInfo();
                            string ffmpegPath = Directory.GetCurrentDirectory() + "/ffmpeg/ffmpeg.exe";
                            pInfo.FileName = ffmpegPath;
                            pInfo.Arguments = "-i \"" +movie.filePath + "\" -af \"loudnorm=I=-14:TP=-3:LRA=4\" \"" + movie.filePath + ".ogg\"";
                            pInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                            pInfo.UseShellExecute = false;
                            movie.filePath  += ".ogg";

                            Windows.Process p = Windows.Process.Start(pInfo);

                            while(!p.HasExited){
                                yield return new WaitForSeconds(1);
                            }
                        }else{
                            pInfo = new Windows.ProcessStartInfo();
                            string ffmpegPath = Directory.GetCurrentDirectory() + "/ffmpeg/ffmpeg.exe";
                            pInfo.FileName = ffmpegPath;
                            pInfo.Arguments = "-i \"" +movie.filePath + "\" -af \"loudnorm=I=-14:TP=-3:LRA=4\" -c:v copy -c:a aac";
                            if(jsonList[loadingIndex].startTime > 0){
                                 pInfo.Arguments += " -ss " + jsonList[loadingIndex].startTime;
                            }
                            if(jsonList[loadingIndex].endTime > 0){
                                 pInfo.Arguments += " -to " + jsonList[loadingIndex].endTime;
                            }
                            pInfo.Arguments += " \"" + movie.filePath + ".conv.mp4\"";
                            pInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                            pInfo.UseShellExecute = false;


                            Windows.Process p = Windows.Process.Start(pInfo);

                                while(!p.HasExited){
                                    yield return new WaitForSeconds(1);
                                }
                            try{
                                File.Delete(movie.filePath);
                            }catch (Exception e){
                                Debug.LogError(e.Message);
                                Debug.LogError(e.StackTrace);
                            }
                            movie.filePath  += ".conv.mp4";
                        }


                        Debug.Log("youtube-dl終了");
                        inactiveObject.transform.Find("StatusText").GetComponent<Text>().text = "待機中";
                        inactiveObject.transform.Find("TitleText").GetComponent<Text>().text = "";
                        inactiveObject.transform.Find("InfoText").GetComponent<Text>().text = "";

                        loadingIndex++;
                        movieList.Add(movie);
                        if(IsFileExist(movie.filePath) || movie.fileType == FileType.spotify){
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
            yield return new WaitForSeconds(5);
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

    private bool IsNextMovieSkipped ()
    {
        if(jsonList[currentIndex].isSkip) {
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

    public IEnumerator ShowTitlePanel(string title , string info){
        titlePanel.transform.Find("TitleText").GetComponent<Text>().text = title;
        titlePanel.transform.Find("InfoText").GetComponent<Text>().text = info;

        float startTime = elapsedTime;
        titlePanelStartTime = startTime;
        Vector2 pivot = titlePanel.GetComponent<RectTransform>().pivot;
        while(elapsedTime - startTime < 1){
            if(titlePanelStartTime != startTime){
                yield break;
            }
            pivot.y = 1 + (elapsedTime - startTime ) ;
            titlePanel.GetComponent<RectTransform>().pivot = pivot;
            yield return null;
        }
        pivot.y = 2;
        titlePanel.GetComponent<RectTransform>().pivot = pivot;

        yield return new WaitForSeconds(3);

        var startTime2 = elapsedTime;
        while(elapsedTime - startTime2 < 1){
            if(titlePanelStartTime != startTime){
                yield break;
            }
            pivot.y = 2 - (elapsedTime - startTime2 );
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
            currentStatus = CurrentStatus.waiting;
            StopPlaying();
        }
    }

    public IEnumerator SendIndexInfo(){

        WWWForm form = new WWWForm();
        form.AddField("room_id",roomId);
        form.AddField("master_id",masterId);
        form.AddField("index",currentIndex);

        UnityWebRequest request = UnityWebRequest.Post("https://api-vfmwtii73q-an.a.run.app/youtube-list/api/youtubelist/setCurrentIndex", form);

        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        yield return request.SendWebRequest();
        Debug.Log(request.error + request.downloadHandler.text + "request:" + System.Text.Encoding.UTF8.GetString(form.data));
    }

    public void PrepareAutoSkip(){
        float prefSkipTime = PlayerPrefs.GetInt("autoSkipSecond",360);
        int prefStack = PlayerPrefs.GetInt("autoSkipStack",1);
        if(movieList.Count - currentIndex < prefStack || PlayerPrefs.GetInt("autoSkipEnabled",0) == 0 || currentStatus != CurrentStatus.playing){
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

        if(skipTime - elapsedTime < 5){
            skipTime = elapsedTime + 5;
            length = skipTime - startTime;
        }
    }

    public void StopPlaying(){
        currentStatus = CurrentStatus.waiting;
        nicoCommentController.stopComment();
        videoPlayer.Stop();
        audioSource.Stop();
        audioSource.GetComponent<RawImage>().enabled = false;
        soundPlayerBackground.SetActive(false);
        infoPanel.SetActive(false);
        StartCoroutine(spotify.Stop());
    }

    static Dictionary<string, string> GetParams(string uri)
    {
        var matches = Regex.Matches(uri, @"(([^&=]+)=([^&=#]*))", RegexOptions.Compiled);
        return matches.Cast<Match>().ToDictionary(
            m => Uri.UnescapeDataString(m.Groups[2].Value),
            m => Uri.UnescapeDataString(m.Groups[3].Value)
        );
    }

    public void SetNicoCommentEnabled(int nicoId){
        this.isNicoLoginSuccess = true;
    }

}