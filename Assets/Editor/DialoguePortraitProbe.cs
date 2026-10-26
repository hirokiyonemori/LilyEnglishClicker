using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DialoguePortraitProbe
{
    const string Key="Lily.PortraitProbe";
    static DialoguePortraitProbe(){EditorApplication.update+=Tick;}
    [MenuItem("LilyClicker/Dialogue Header/Validate Centered Portraits")]
    static void Run(){SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||Idle.StageDirector.Instance==null||!Idle.StageDirector.Instance.IsReady)return;SessionState.SetBool(Key,false);new GameObject("Portrait Probe").AddComponent<DialoguePortraitProbeRunner>();}
}
public class DialoguePortraitProbeRunner:MonoBehaviour
{
    int checks;
    void Check(bool ok,string label){if(!ok)throw new Exception("[Portrait FAIL] "+label);checks++;}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        Idle.StageDirector.Instance.StartStage(1);
        FindAnyObjectByType<Idle.GameManager>().MainHealth.Stop();
        var overlay=FindAnyObjectByType<QuestOverlayController>();
        foreach(var go in overlay.hideWhileQuesting)if(go!=null)go.SetActive(false);
        overlay.questRoot.SetActive(true);
        var panel=FindAnyObjectByType<VillagerEventPanel>(FindObjectsInactive.Include);
        var content=panel.transform.Find("Content");
        var slot=(RectTransform)content.Find("NPCImageSlot");
        var dialog=(RectTransform)content.Find("DialogueWindow");
        var portrait=slot.Find("NPCImage").GetComponent<Image>();
        var speaker=dialog.Find("SpeakerName").GetComponent<TMP_Text>();
        var text=dialog.Find("DialogueText").GetComponent<TMP_Text>();
        int original=UILayoutDiagnostics.GameViewSizeTool.CurrentIndex();
        Directory.CreateDirectory("Documentation/DialoguePortraits");
        foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(390,844),new Vector2Int(430,932)})
        {
            UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(size.x,size.y,"Portrait "+size.x+"x"+size.y);
            foreach(var scenario in DefaultVillagerScenarios.Get())
            {
                panel.Show(scenario,_=>{});
                yield return new WaitForSecondsRealtime(1.2f);
                Canvas.ForceUpdateCanvases();
                Check(Mathf.Abs(slot.TransformPoint(slot.rect.center).x-dialog.TransformPoint(dialog.rect.center).x)<.1f,"center "+scenario.speakerName);
                Check(Mathf.Abs(slot.anchorMax.x-slot.anchorMin.x-.67f)<.00001f && slot.anchorMin.y==.47f && slot.anchorMax.y==.85f && slot.sizeDelta==Vector2.zero,"original dimensions");
                Check(speaker.alignment==TextAlignmentOptions.Left && text.alignment==TextAlignmentOptions.TopLeft,"text left aligned");
                Check(portrait.sprite==scenario.portrait && portrait.preserveAspect,"correct portrait and proportions");
                ScreenCapture.CaptureScreenshot("Documentation/DialoguePortraits/"+scenario.portrait.name+"-"+size.x+".png");
                yield return new WaitForEndOfFrame();
                panel.Hide();
            }
        }
        overlay.CloseQuest();Idle.StageDirector.Instance.ReturnToBase();
        if(original>=0)UILayoutDiagnostics.GameViewSizeTool.SetIndex(original);
        File.WriteAllText("Documentation/DialoguePortraits/results.txt",checks+" checks passed: four characters at 320x568,390x844,430x932. Centered portraits, original size, left-aligned dialogue, correct sprites. No rewards granted.\n");
        Debug.Log("[Portrait] "+checks+" passed");
    }
}
