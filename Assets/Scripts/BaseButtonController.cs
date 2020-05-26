using UnityEngine;
using System.Collections;

public class BaseButtonController : MonoBehaviour {

    public BaseButtonController button;

    public void OnClick()
    {
        if (button == null)
        {
            throw new System.Exception("Button instance is null!!");
        }
        // 自身のオブジェクト名を渡す
        button.OnClick(this.gameObject);
    }

    protected virtual void OnClick(GameObject gameObject)
    {
        // 呼ばれることはない
        Debug.Log("Base Button");
    }

}