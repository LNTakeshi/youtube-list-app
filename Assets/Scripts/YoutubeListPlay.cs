using System.Text;
using Windows = System.Diagnostics;
using System.Text.RegularExpressions;
using System; //NonSerializedなど
using System.IO; //入出力
using UnityEngine.Video; //動画再生
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Networking;
using MiniJSON;
using System.Linq;
using AngleSharp.Html.Parser;
using AngleSharp.Common;
using AngleSharp.Dom;
using System.Threading.Tasks;


public class YoutubeListPlay : MonoBehaviour
{
    private enum FileType
    {
        sound,
        movie,
        spotify,
        deleted,
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
        public bool isEndCut;
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
    [SerializeField]
    private ButtonController buttonController;
    [SerializeField]
    private Spotify spotify;

    private float titlePanelStartTime;
    [SerializeField]
    private Text versionText;
    private string logFilePath = "log";

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
        }else if(audioSource.gameObject.activeSelf == true && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.sound){
            Vector3 vector3 = infoPanel.transform.Find("SeekBar").transform.localScale;
            var time =  elapsedTime - startTime;
            vector3.x = (float)(time / length);
            infoPanel.transform.Find("SeekBar").transform.localScale = vector3;
            infoPanel.transform.Find("InfoText").GetComponent<Text>().text = "stack:" + (movieList.Count - currentIndex) + " " + Math.Floor(time / 60).ToString("00") + ":" + Math.Floor(time % 60).ToString("00") + "/" + Math.Floor(length / 60).ToString("00") + ":" + Math.Floor(length % 60).ToString("00");
        }else if(audioSource.gameObject.activeSelf == true && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.spotify){
            Vector3 vector3 = infoPanel.transform.Find("SeekBar").transform.localScale;
            var time =  elapsedTime - startTime;
            vector3.x = (float)(time / length);
            infoPanel.transform.Find("SeekBar").transform.localScale = vector3;
            infoPanel.transform.Find("InfoText").GetComponent<Text>().text = "stack:" + (movieList.Count - currentIndex) + " " + Math.Floor(time / 60).ToString("00") + ":" + Math.Floor(time % 60).ToString("00") + "/" + Math.Floor(length / 60).ToString("00") + ":" + Math.Floor(length % 60).ToString("00");
        }

        if(currentStatus == CurrentStatus.playing && currentIndex != 0 &&
            new List<FileType>(){FileType.spotify, FileType.sound}.Contains(movieList[currentIndex - 1].fileType) &&
            elapsedTime > endTime){
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
            videoPlayer.errorReceived += StopMoviePlayByError;

            RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.ARGB32);
            videoPlayer.GetComponent<VideoPlayer>().targetTexture = renderTexture;
            videoPlayer.GetComponent<RawImage>().texture = renderTexture;
            audioSource.gameObject.SetActive(true);
            Application.logMessageReceived += HandleLog;
            versionText.text = "version:"+Application.version;

            DateTime thresholdDate = DateTime.Now.AddDays(-7);

            // 対象ディレクトリ内のすべての.logファイルを取得
            string[] logFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.log");
            foreach (string logFile in logFiles)
            {
                try
                {
                    // ファイルの作成日時を取得
                    DateTime creationTime = File.GetCreationTime(logFile);

                    // 作成日時が閾値より古い場合に削除
                    if (creationTime < thresholdDate)
                    {
                        File.Delete(logFile);
                        Debug.Log("deleted:" + logFile);
                    }
                }
                catch (Exception ex)
                {
                    // エラー処理
                    Debug.Log($"エラーが発生しました: {logFile} - {ex.Message}");
                }
            }
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

    private void StartNextMovie(VideoPlayer vp){
        Debug.Log("startNext");
        // StartCoroutine(ShowTitlePanel());
        infoPanel.transform.Find("TitleText").GetComponent<Text>().text = movieList[currentIndex].title;
        infoPanel.transform.Find("UsernameText").GetComponent<Text>().text = movieList[currentIndex].username;
        try{
            videoPlayer.Play();
            startTime = elapsedTime;
            endTime = elapsedTime + (float)videoPlayer.length;
            length = (float)videoPlayer.length;
            currentStatus = CurrentStatus.playing;
        }catch(Exception e){
            Debug.LogError(e.Message);
            Debug.LogError(e.StackTrace);
            sendError("Cannnot Play Video", e.Message + e.StackTrace);

            addMoviePanel.transform.Find("TitleText").GetComponent<Text>().text = "動画再生失敗(致命的エラー):" + movieList[currentIndex].title;
            addMoviePanel.transform.Find("InfoText").GetComponent<Text>().text = movieList[currentIndex].username;
            StartCoroutine(ShowAddMoviePanel());
        }
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
        nicoCommentController.stopComment();
    }

    private void StopMoviePlayByError(VideoPlayer vp, string error){
        if (currentStatus == CurrentStatus.loading) {
            currentIndex++;
        }
        currentStatus = CurrentStatus.waiting;
        videoPlayer.Stop();
        nicoCommentController.stopComment();
        StartCoroutine(sendError("VideoPlayerError", error));
    }

    private IEnumerator LoadSoundAndPlay(){
        Debug.Log("audioStart");
        currentStatus = CurrentStatus.loading;
        UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(movieList[currentIndex].filePath,AudioType.OGGVORBIS);
        yield return request.SendWebRequest();
        audioClip = DownloadHandlerAudioClip.GetContent(request);
        audioSource.clip = audioClip;
        audioSource.GetComponent<RawImage>().texture = null;
        var image = audioSource.GetComponent<RawImage>();
        image.rectTransform.offsetMax = new Vector2(590,310);
        image.rectTransform.offsetMin = new Vector2(-590,-310);
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
                            jsonInfo.isEndCut = (bool)jsonObj["isEndCut"];
                            jsonList.Add(jsonInfo);
                        }
                    }
                }
                while(request.responseCode == 200 && jsonList.Count > loadingIndex && jsonList[loadingIndex].isSkip){
                    Movie movie = new Movie();
                    movie.title = jsonList[loadingIndex].title;
                    movie.username = jsonList[loadingIndex].username;
                    movie.startTimePadding = jsonList[loadingIndex].startTime;
                    movie.fileType = FileType.deleted;
                    movieList.Add(movie);
                    loadingIndex++;
                }
                if(request.responseCode == 200 && jsonList.Count > loadingIndex && !jsonList[loadingIndex].isSkip){
                    if(jsonList.Count > loadingIndex){
                        SafeCreateDirectory(Application.temporaryCachePath + "/Movies/");
                        var isNico = jsonList[loadingIndex].url.StartsWith("https://www.nicovideo.jp") || jsonList[loadingIndex].url.StartsWith("https://sp.nicovideo.jp");

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
                        string ffmpegPath = Directory.GetCurrentDirectory() + "/ffmpeg/ffmpeg.exe";
                        Debug.Log(movie.filePath);

                        Windows.ProcessStartInfo pInfo = new Windows.ProcessStartInfo();
                        pInfo.FileName = youtubeDlPath;
                        var needVideoEncode = false;
                        if (movie.fileType == FileType.movie && !isNico) {
                            pInfo.Arguments += "--format \"bv*[ext=mp4][vcodec~='^(avc|h264)']+ba/bv*[ext=mp4][vcodec~='^(avc|h264)']/bv*[ext=mp4]+ba/bv*[ext=mp4]\" -S \"ext,proto\" --audio-multistreams --remux-video mp4 ";
                            pInfo.Arguments += "--ffmpeg-location \"" + ffmpegPath + "\" ";
                        }
                        if(movie.fileType == FileType.movie && !isNico  &&(jsonList[loadingIndex].startTime > 0 || jsonList[loadingIndex].isEndCut)){

                            var startTime = 0;
                            var endTime = 0;
                            if(jsonList[loadingIndex].startTime > 0){
                                startTime = jsonList[loadingIndex].startTime;
                                if (jsonList[loadingIndex].length < 310){
                                    needVideoEncode = true;
                                }
                            }
                            if(jsonList[loadingIndex].isEndCut){
                                endTime = jsonList[loadingIndex].endTime;
                            }

                            var startTimeStr = "*" + (startTime / 60).ToString("00") + ":" + (startTime % 60).ToString("00");
                            var endTimeStr = (endTime / 60).ToString("00") + ":" + (endTime % 60).ToString("00");
                            if (endTime == 0){
                                endTimeStr = "inf";
                            }
                            pInfo.Arguments += "--download-sections \"" + startTimeStr + "-" + endTimeStr + "\" ";

                        }
                        if(isNico){
                            // pInfo.Arguments += "-o \"" +movie.filePath + "\" " + jsonList[loadingIndex].url;
                            pInfo.Arguments += "--ffmpeg-location \"" + ffmpegPath + "\" -o \"" +movie.filePath + "\" --external-downloader aria2c --external-downloader-args \"-c -x 5 -k 2M\" " + jsonList[loadingIndex].url;
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
                            string threadKey = null;
                            string parameters = null;
                            try{
                                IHtmlCollection<IElement> elements = htmlDocument.GetElementsByName("server-response");
                                IElement element = elements[0];
                                Dictionary<string, object> commentJson = Json.Deserialize(element.GetAttribute("content")) as Dictionary<string, object>;

                                var data = commentJson.First(key => key.Key == "data").Value as Dictionary<string, object>;
                                var response = data.First(key => key.Key == "response").Value as Dictionary<string, object>;
                                var comment = response.First(key => key.Key == "comment").Value as Dictionary<string, object>;
                                var nvComment = comment.First(key => key.Key == "nvComment").Value as Dictionary<string, object>;
                                threadKey = nvComment["threadKey"] as string;
                                parameters = Json.Serialize(nvComment["params"]);
                            }catch (Exception e){
                                Debug.Log(e);
                            }
                            Debug.Log("threadKey:" + threadKey);

                            if(threadKey != null){
                                int commentCount = 100;
                                if(jsonList[loadingIndex].length >= 60) commentCount = 200;
                                if(jsonList[loadingIndex].length >= 240) commentCount = 400;
                                if(jsonList[loadingIndex].length >= 300) commentCount = 1000;
                                Debug.Log("length:" + length + "count:" + commentCount );
                                String form = $@"{{
    ""threadKey"": ""{threadKey}"",
    ""params"": {parameters},
    ""additionals"": {{}}
}}";
                                commentRequest =  new UnityWebRequest("https://public.nvcomment.nicovideo.jp/v1/threads");
                                commentRequest.method = "POST";
                                commentRequest.SetRequestHeader("x-client-os-type", "others");
                                commentRequest.SetRequestHeader("X-Frontend-Id", "6");
                                commentRequest.SetRequestHeader("X-Frontend-Version", "0");
                                commentRequest.uploadHandler = new UploadHandlerRaw( Encoding.UTF8.GetBytes(form));
                                commentRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                                commentRequest.uploadHandler.contentType = "application/json; encoding='utf-8'";
                                Debug.Log(form);
                                yield return commentRequest.SendWebRequest();
                                Debug.Log( commentRequest.error + "body:" + commentRequest.downloadHandler.text + " threadKey:" + threadKey + " form:" + form);
                                nicoCommentController.setComment(loadingIndex, commentRequest.downloadHandler.text, jsonList[loadingIndex].startTime);
                            }else{
                                StartCoroutine(sendError("DEBUG", "NICONICO_THREAD_ID_NOT_FOUND"));
                            }
                        }else{
                            pInfo.Arguments += "-o \"" +movie.filePath + "\" " + jsonList[loadingIndex].url;
                        }
                        pInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                        pInfo.UseShellExecute = false;
                        pInfo.RedirectStandardOutput = true; // 標準出力をリダイレクト
                        pInfo.RedirectStandardError = true; // 標準エラー出力をリダイレクト

                        Debug.Log(pInfo.Arguments);


                        Debug.Log("youtube-dl開始");
                        inactiveObject.transform.Find("StatusText").GetComponent<Text>().text = "動画取得中";
                        inactiveObject.transform.Find("TitleText").GetComponent<Text>().text = "NEXT:" + movie.title;
                        inactiveObject.transform.Find("InfoText").GetComponent<Text>().text = movie.username != "" ? movie.username : "お名前未入力";

                        int retryCount =0;

                        if (movie.fileType != FileType.spotify) {
                            while( !IsFileExist(movie.filePath) && retryCount < 5){
                                using(Windows.Process p = Windows.Process.Start(pInfo))
                                using(Task<string> outputTask = p.StandardOutput.ReadToEndAsync())
                                using(Task<string> errorTask = p.StandardError.ReadToEndAsync())
                                {
                                    while(!p.HasExited){
                                        yield return new WaitForSeconds(1);
                                    }
                                                                    // ログに追記
                                    AppendLog(logFilePath, $"[標準出力] {outputTask.Result}");
                                    AppendLog(logFilePath, $"[エラー出力] {errorTask.Result}");
                                }
                                retryCount++;
                            }
                        }

                        if(movie.fileType == FileType.sound){
                            pInfo = new Windows.ProcessStartInfo();

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
                            pInfo.FileName = ffmpegPath;
                            pInfo.Arguments = "-i \"" +movie.filePath + "\" -af \"loudnorm=I=-14:TP=-3:LRA=4\" -max_muxing_queue_size 9999 -c:a aac ";
                            if(needVideoEncode){
                                Debug.Log("動画のエンコードを行います");
                                pInfo.Arguments += " -profile:v baseline -c:v h264_nvenc ";
                            } else {
                                pInfo.Arguments += " -c:v copy ";
                            }
                            if(isNico){
                                if(jsonList[loadingIndex].startTime > 0){
                                    pInfo.Arguments += " -ss " + jsonList[loadingIndex].startTime;
                                }
                                if(jsonList[loadingIndex].isEndCut){
                                    pInfo.Arguments += " -to " + jsonList[loadingIndex].endTime;
                                }
                            }
                            pInfo.Arguments += " \"" + movie.filePath + ".conv.mp4\"";
                            pInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                            pInfo.UseShellExecute = false;
                            pInfo.RedirectStandardOutput = true; // 標準出力をリダイレクト
                            pInfo.RedirectStandardError = true; // 標準エラー出力をリダイレクト

                            Debug.Log(pInfo.Arguments);

                            using(Windows.Process p = Windows.Process.Start(pInfo))
                            using(Task<string> outputTask = p.StandardOutput.ReadToEndAsync())
                            using(Task<string> errorTask = p.StandardError.ReadToEndAsync())
                            {
                                while(!p.HasExited){
                                    yield return new WaitForSeconds(1);
                                }
                                AppendLog(logFilePath, $"[標準出力] {outputTask.Result}");
                                AppendLog(logFilePath, $"[エラー出力] {errorTask.Result}");
                            }

                            yield return new WaitForSeconds(1);

                            try{
                                // File.Delete(movie.filePath);
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

    static void AppendLog(string filePath, string message)
    {

        using (StreamWriter writer = new StreamWriter($"{filePath}{DateTime.Now:yyyy_MM_dd}.log", append: true))
        {
            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }
    }

}