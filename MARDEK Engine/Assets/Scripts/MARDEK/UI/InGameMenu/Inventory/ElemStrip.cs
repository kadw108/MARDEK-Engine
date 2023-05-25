using System.Collections.Generic;
using UnityEngine;

public class ElemStrip : MonoBehaviour
{
    [SerializeField] List<HasElement> elemStripImages;

    public void SetElement(MARDEK.Stats.Element element)
    {
        foreach(HasElement obj in elemStripImages)
        {
            if (obj.Element == element)
            {
                obj.gameObject.SetActive(true);
            }
            else
            {
                obj.gameObject.SetActive(false);
            }
        }
    }
}
