using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VideoControllerPanel : MonoBehaviour
{
    private float elapsedTime;

    [SerializeField]
    private GameObject controlPanel;

    private enum panelStatusEnum
    {
        closed = 0,
        opening = 1,
        opened = 2,
        closing = 3
    }
    private panelStatusEnum panelStaus = panelStatusEnum.closed;

    private float closeTime;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;

        if(Input.GetMouseButton(0) ){
            closeTime = elapsedTime + 6;
            if(panelStaus == panelStatusEnum.closed){
                StartCoroutine(ShowControlPanel());
            }
        }
    }

    private IEnumerator ShowControlPanel(){
        panelStaus = panelStatusEnum.opening;

        float startTime = elapsedTime;
        Vector2 pivot = controlPanel.GetComponent<RectTransform>().pivot;
        while(elapsedTime - startTime < 0.5){
            pivot.x = (elapsedTime - startTime ) * 2;
            controlPanel.GetComponent<RectTransform>().pivot = pivot;
            yield return null;
        }
        pivot.x = 1;
        controlPanel.GetComponent<RectTransform>().pivot = pivot;
        panelStaus = panelStatusEnum.opened;


        while(elapsedTime < closeTime){
            yield return null;
        }

        panelStaus = panelStatusEnum.closing;

        startTime = elapsedTime;
        while(elapsedTime - startTime < 0.5){
            pivot.x = 1 - (elapsedTime - startTime ) * 2;
            controlPanel.GetComponent<RectTransform>().pivot = pivot;
            yield return null;
        }

        pivot.x= 0;
        controlPanel.GetComponent<RectTransform>().pivot = pivot;

        panelStaus = panelStatusEnum.closed;


        yield return null;
    }
}
