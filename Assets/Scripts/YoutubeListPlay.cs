using System.Text;
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
using System.Linq;


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
        public bool isSkip;
        public int length;
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
    public Boolean isNicoLoginSuccess { get; private set;} = false;

    private void Update () {
        elapsedTime += Time.deltaTime;
        if(currentStatus == CurrentStatus.waiting && IsNextMovieExists()) {
            Debug.Log("ファイル再生準備開始");
            currentStatus = CurrentStatus.loading;

            if(!IsFileExist(movieList[currentIndex].filePath) || IsNextMovieSkipped()){
                Debug.Log("ファイルなし");
                titlePanel.transform.Find("TitleText").GetComponent<Text>().text = (IsNextMovieSkipped() ? "スキップされました：" : "再生失敗のためスキップされました：") +movieList[currentIndex].title;
                titlePanel.transform.Find("InfoText").GetComponent<Text>().text = movieList[currentIndex].username;
                StartCoroutine(ShowTitlePanel());
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
        }else if(audioSource.gameObject.activeSelf == true && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.sound && audioClip != null && !Double.IsNaN(audioSource.time / length)){
            Vector3 vector3 = infoPanel.transform.Find("SeekBar").transform.localScale;
            vector3.x = (float)(audioSource.time / length);
            infoPanel.transform.Find("SeekBar").transform.localScale = vector3;
            infoPanel.transform.Find("InfoText").GetComponent<Text>().text = "stack:" + (movieList.Count - currentIndex) + " " + Math.Floor(audioSource.time / 60).ToString("00") + ":" + Math.Floor(audioSource.time % 60).ToString("00") + "/" + Math.Floor(length / 60).ToString("00") + ":" + Math.Floor(length % 60).ToString("00");
        }

        if(currentStatus == CurrentStatus.playing && currentIndex != 0 && movieList[currentIndex - 1].fileType == FileType.sound && elapsedTime > endTime){
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
                        string[] length = (jsonObj["length"] as string).Split(':');
                        if(length.Length == 2) jsonInfo.length = Int32.Parse(length[0]) * 60 + Int32.Parse(length[1]);
                        jsonInfo.isSkip = (bool)jsonObj["deleted"] ;
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
                        DateTime date = DateTime.Now;
                        
                        movie.filePath = Application.temporaryCachePath + "/Movies/" + date.ToString("yyyyMMddHHmmss") + fileExtension;
                        string youtubeDlPath = Directory.GetCurrentDirectory() + "/youtube-dl.exe";
                        Debug.Log(movie.filePath);

                        Windows.ProcessStartInfo pInfo = new Windows.ProcessStartInfo();
                        pInfo.FileName = youtubeDlPath;
                        if(jsonList[loadingIndex].url.StartsWith("https://www.nicovideo.jp") || jsonList[loadingIndex].url.StartsWith("https://sp.nicovideo.jp")){
                            pInfo.Arguments = "-o \"" +movie.filePath + "\" --external-downloader aria2c --external-downloader-args \"-c -x 5 -k 2M\" " + jsonList[loadingIndex].url;
                            if(isNicoLoginSuccess){
                                inactiveObject.transform.Find("StatusText").GetComponent<Text>().text = "コメント取得中";
                                inactiveObject.transform.Find("TitleText").GetComponent<Text>().text = "NEXT:" + movie.title;
                                inactiveObject.transform.Find("InfoText").GetComponent<Text>().text = movie.username　!= "" ? movie.username : "お名前未入力";
                                var url =  jsonList[loadingIndex].url.Split('/').Last();
                                Debug.Log(url);
                                UnityWebRequest commentRequest = UnityWebRequest.Get("http://flapi.nicovideo.jp/api/getflv/" + url);
                                commentRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                                yield return commentRequest.SendWebRequest();
                                String threadId = null;
                                String commentServer = null;
                                var param = GetParams(commentRequest.downloadHandler.text);
                                param.TryGetValue("thread_id", out threadId);
                                param.TryGetValue("ms", out commentServer);
                                Debug.Log( commentRequest.error +" body:" + commentRequest.downloadHandler.text + " thread_id:" + threadId + "ms:" + commentServer);
                                if(threadId != null){
                                    int commentCount = 100;
                                    if(jsonList[loadingIndex].length >= 60) commentCount = 200;
                                    if(jsonList[loadingIndex].length >= 240) commentCount = 400;
                                    if(jsonList[loadingIndex].length >= 300) commentCount = 1000; 
                                    Debug.Log("length:" + length + "count:" + commentCount );
                                    String form = "<thread res_from=\"-" + commentCount + "\" version=\"20061206\" scores=\"1\" thread=\"" + threadId + "\" />";
                                    commentRequest =  new UnityWebRequest(commentServer);
                                    commentRequest.method = "POST";
                                    commentRequest.uploadHandler = new UploadHandlerRaw( Encoding.UTF8.GetBytes(form));
                                    commentRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                                    commentRequest.uploadHandler.contentType = "text/xml; encoding='utf-8'";
                                    yield return commentRequest.SendWebRequest();
                                    Debug.Log( commentRequest.error + "body:" + commentRequest.downloadHandler.text + " thread_id:" + threadId + "ms:" + commentServer + " form:" + form);
                                    nicoCommentController.setComment(loadingIndex, commentRequest.downloadHandler.text);
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
            yield return new WaitForSeconds(5);
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
            currentStatus = CurrentStatus.waiting;
            StopPlaying();
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

    }

    static Dictionary<string, string> GetParams(string uri)
    {
        var matches = Regex.Matches(uri, @"(([^&=]+)=([^&=#]*))", RegexOptions.Compiled);
        return matches.Cast<Match>().ToDictionary(
            m => Uri.UnescapeDataString(m.Groups[2].Value),
            m => Uri.UnescapeDataString(m.Groups[3].Value)
        );
    }

    public void SetNicoCommentEnabled(){
        this.isNicoLoginSuccess = true;
    }

}