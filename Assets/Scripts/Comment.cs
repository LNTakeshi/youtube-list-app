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

public class Comment : MonoBehaviour
{


    public float startTime { get; private set; }
    public NicoCommentController controller { get; private set; }
    private float elapsedTime = 0;
    private float endTime = 4;
    private bool isMoveText;
    private void Update () {
        elapsedTime += Time.deltaTime;
        float currentTimePercent = elapsedTime / endTime;
        if(isMoveText && currentTimePercent <= 1){
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(currentTimePercent, 1);
            rectTransform.anchorMin = new Vector2(1 - currentTimePercent, 1);
            rectTransform.anchorMax = new Vector2(1 - currentTimePercent, 1);
        }
        if(elapsedTime > endTime){
            RemoveSelf();
        }
    }
    private async void Start () {

    }

    public void Initialize(CommentData commentData, NicoCommentController controller)
    {
        GetComponent<Text>().text = commentData.comment;
        GetComponent<Text>().color = ToColor((int)commentData.colorType);
        int row = controller.ReserveRow(commentData.commentType);
        this.controller = controller;
        this.isMoveText = commentData.commentType == CommentType.Normal;

        RectTransform rectTransform = GetComponent<RectTransform>();
        switch (commentData.commentType)
        {
            case CommentType.Normal:
                rectTransform.pivot = new Vector2(0,1);
                rectTransform.anchorMin = new Vector2(1,1);
                rectTransform.anchorMax = new Vector2(1,1);
                rectTransform.anchoredPosition = new Vector3(0, -50 + -45 * row, 90);
                rectTransform.localScale = new Vector3(1,1,1);
            break;
            case CommentType.Down:
                this.endTime = 2.5f;
                rectTransform.pivot = new Vector2(0.5f, 0);
                rectTransform.anchorMin = new Vector2(0.5f, 0);
                rectTransform.anchorMax = new Vector2(0.5f, 0);
                rectTransform.anchoredPosition = new Vector3(0, 45 * row, 90);
                rectTransform.localScale = new Vector3(1,1,1);
            break;
            case CommentType.Up:
                this.endTime = 2.5f;
                rectTransform.pivot = new Vector2(0.5f, 1);
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.anchoredPosition = new Vector3(0, -50 + -45 * row, 90);
                rectTransform.localScale = new Vector3(1,1,1);
            break;
        }


        // Debug.Log("comment:" + GetComponent<Text>().text);
    }

    private void RemoveSelf(){
        // Debug.Log("removeComment:" + GetComponent<Text>().text);

        controller.RemoveComment(gameObject);
        Destroy(gameObject);
    }

    public Color32 ToColor(int HexVal)
    {
        byte R = (byte)((HexVal >> 16) & 0xFF);
        byte G = (byte)((HexVal >> 8) & 0xFF);
        byte B = (byte)((HexVal) & 0xFF);
        return new Color32(R, G, B, 255);
    }
}