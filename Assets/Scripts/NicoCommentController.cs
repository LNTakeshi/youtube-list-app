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
using System.Xml.Linq;


public class NicoCommentController : MonoBehaviour
{
    [SerializeField]
    private GameObject commentLayer;
    private Dictionary<int ,List<CommentData>> commentDatas = new Dictionary<int, List<CommentData>>();
    private List<GameObject> commentObjList = new List<GameObject>();
    private float[] normalCommentTime = new float[15];
    private float[] shitaCommentTime = new float[15];
    private float[] ueCommentTime = new float[15];

    private Boolean isReady = false;
    private Boolean isPlaying = false;
    private float elapsedTime = 0;
    private float startTime;
    private int playedVpos;
    private int currentIndex;
    private NGType currentNGType = NGType.None;

    private void Update () {
        elapsedTime += Time.deltaTime;
        if(isPlaying){
            int currentVpos = (int) ((elapsedTime - startTime) * 100);
            commentDatas[currentIndex].Where(c=>
                c.score > ((int)currentNGType)
                &&
                (
                    (c.commentType == CommentType.Normal && c.vpos > playedVpos + 0.5f && c.vpos <= currentVpos + 0.5f)
                    ||    (c.commentType != CommentType.Normal && c.vpos > playedVpos && c.vpos <= currentVpos )
                )
            )
            .ToList()
            .ForEach(c=>{
                GameObject commentObj = (GameObject)Resources.Load ("CommentText");
                GameObject obj = Instantiate(commentObj, commentLayer.transform.position, Quaternion.identity);
                obj.transform.SetParent(commentLayer.transform);
                obj.GetComponent<Comment>().Initialize(c, this);
                commentObjList.Add(obj);
            });
            playedVpos = currentVpos;
        }
    }
    private async void Start () {

    }

    public void setComment(int index,String json, int startTime){
        commentDatas[index] = new List<CommentData>();
        try{
            //var j = Regex.Unescape(json);
            var jsonObj =  Json.Deserialize(json) as IList;

            //データの中身すべてを取得
            // var rows = table.Elements("chat");

            //取り出し
            foreach (Dictionary<string, object> row in jsonObj)
            {
                if(!row.ContainsKey("chat")){
                    continue;
                }
                var chat =(IDictionary) row["chat"];
                int vpos = (int)(long)chat["vpos"];
                vpos -= startTime * 100;
                if(vpos < 0){
                    continue;
                }
                commentDatas[index].Add(new CommentData((string)chat["content"], vpos, (string)chat["mail"], 0));
            }
            //ソート
            commentDatas[index].Sort((a,b)=> a.vpos - b.vpos);
        }catch (Exception e){
            Debug.LogError(e.Message);
            Debug.LogError(e.StackTrace);
        }
        if(commentDatas.Count != 0){
            isReady = true;
        }
    }

    public void startIfReady(int index){
        if(commentDatas.ContainsKey(index)){
            currentIndex = index;
            isReady = false;
            isPlaying = true;
            this.startTime = elapsedTime;
            playedVpos = 0;
        }
    }

    public void RemoveComment(GameObject comment){
        commentObjList.Remove(comment);
    }

    public Int32 ReserveRow(CommentType type){
        float[] rows = normalCommentTime;
        switch (type)
        {
            case CommentType.Normal:
                rows = normalCommentTime;
                break;
            case CommentType.Down:
                rows = shitaCommentTime;
                break;
            case CommentType.Up:
                rows = ueCommentTime;
                break;
        }
        int row = rows.Select((t, i)=> new {t, i}).Where(t=>t.t < elapsedTime).Select(t=> t.i).DefaultIfEmpty(-1).First();
        if(row == -1){
            row = rows.Select((t, i)=> new {t, i}).OrderBy(t=>t.t).Select(t=> t.i).First();
        }
        // Debug.Log("selected:" + String.Join(",",rows.Where(t=>t < elapsedTime)));
        rows[row] = elapsedTime + (type == CommentType.Normal ? 2f : 2.5f);
        // Debug.Log("row:" + row + " all:" + String.Join(",",rows) + " currentTime:" + elapsedTime);
        return row;
    }

    public void setNGLevel(NGType NGType){
        this.currentNGType = NGType;
    }

    public void stopComment(){
        this.isPlaying = false;
    }

}
public class CommentData{
    public string comment;
    public int vpos;
    public string mail;
    public int score;
    public ColorType colorType;
    public CommentType commentType;
    public CommentData(string comment, int vpos, string mail, int score){
        this.comment = comment;
        this.vpos = vpos;
        this.mail = mail;
        this.score = score;
        List<string> commands = mail?.Split(' ').ToList() ?? new List<string>();
        this.commentType = CommentType.Normal;
        this.colorType = ColorType.White;
        commands.ForEach(c=>{
            CommentType? comType = COMMENT_TYPES.FirstOrDefault(type => type.Value == c).Key;
            if(comType != null) this.commentType = (CommentType) comType;
            ColorType? cType = COLOR_TYPES.FirstOrDefault(type => type.Value.Contains(c)).Key;
            if(cType != null) this.colorType = (ColorType) cType;
        });
    }

    static public Dictionary<CommentType?, string> COMMENT_TYPES = new Dictionary<CommentType?, string>()
    {
        {CommentType.Up, "ue"},
        {CommentType.Down, "shita"},
        {CommentType.Normal, "naka"}
    };

    static public Dictionary<ColorType?, List<String>> COLOR_TYPES = new Dictionary<ColorType?, List<string>>()
    {
        {ColorType.White, new List<String>(){"white"}},
        {ColorType.Red, new List<String>(){"red"}},
        {ColorType.Pink, new List<String>(){"pink"}},
        {ColorType.Orange, new List<String>(){"orange"}},
        {ColorType.Yellow, new List<String>(){"yellow"}},
        {ColorType.Green, new List<String>(){"green"}},
        {ColorType.Cyan, new List<String>(){"cyan"}},
        {ColorType.Blue, new List<String>(){"blue"}},
        {ColorType.Purple, new List<String>(){"purple"}},
        {ColorType.Black, new List<String>(){"black"}},
        {ColorType.White2, new List<String>(){"white2", "niconicowhite"}},
        {ColorType.Red2, new List<String>(){"red2", "truered"}},
        {ColorType.Pink2, new List<String>(){"pink2"}},
        {ColorType.Orange2, new List<String>(){"orange2", "passionorange"}},
        {ColorType.Yellow2, new List<String>(){"yellow2", "madyellow"}},
        {ColorType.Green2, new List<String>(){"green2", "elementalgreen"}},
        {ColorType.Cyan2, new List<String>(){"cyan2"}},
        {ColorType.Blue2, new List<String>(){"blue2", "marineblue"}},
        {ColorType.Purple2, new List<String>(){"purple2", "nobleviolet"}},
        {ColorType.Black2, new List<String>(){"black2"}},


    };

    static public Dictionary<NGType, string> NG_RADIO_BUTTONS = new Dictionary<NGType, string>()
    {
        {NGType.None, "FilterNoneButton"},
        {NGType.Low, "FilterLowButton"},
        {NGType.Normal, "FilterNormalButton"},
        {NGType.High, "FilterHighButton"},
        {NGType.Max, "FilterMaxButton"},
        {NGType.Zero, "FilterZeroButton"},
    };
};

public enum ColorType: int
{
    White = 0xFFFFFF,
    Red = 0xFF0000,
    Pink = 0xFF8080,
    Orange = 0xFFC000,
    Yellow = 0xFFFF00,
    Green = 0x00FF00,
    Cyan = 0x00FFFF,
    Blue = 0x0000FF,
    Purple = 0xC000FF,
    Black = 0x000000,
    White2 = 0xCCCC99,
    Red2 = 0xCC0033,
    Pink2 = 0xFF33CC,
    Orange2 = 0xFF6600,
    Yellow2 = 0x999900,
    Green2 = 0x00CC66,
    Cyan2 = 0x00CCCC,
    Blue2 = 0x3399FF,
    Purple2 = 0x6633CC,
    Black2 = 0x666666

}

public enum CommentType{
    Up,
    Down,
    Normal
}

public enum NGType: int{
    None = -99999,
    Low = -10000,
    Normal = -5000,
    High = -1000,
    Max = -1,
    Zero = 99999
}