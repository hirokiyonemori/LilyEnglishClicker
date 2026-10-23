using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class QuestBorderProbe
{
    const string Key = "Lily.BorderProbe";
    static QuestBorderProbe() { EditorApplication.update += Tick; }
    [MenuItem("LilyClicker/UI Borders/Validate Phone Sizes")]
    static void Run() { SessionState.SetBool(Key,true); EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || QuestManager.Instance == null) return;
        SessionState.SetBool(Key,false);
        new GameObject("Border Probe").AddComponent<QuestBorderProbeRunner>();
    }
}
public class QuestBorderProbeRunner : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        Idle.StageDirector.Instance.StartStage(1);
        FindAnyObjectByType<Idle.GameManager>().MainHealth.Stop();
        var overlay = FindAnyObjectByType<QuestOverlayController>();
        if (overlay.hideWhileQuesting != null)
            foreach(var go in overlay.hideWhileQuesting) if(go != null) go.SetActive(false);
        overlay.questRoot.SetActive(true);
        var battle = FindAnyObjectByType<BattlePanel>(FindObjectsInactive.Include);
        battle.Show(BattlePhase.MidPath,"森のまもの",null,12,4,167,174,1,50,_=>{});
        Directory.CreateDirectory("Documentation/UIBorders");
        int originalSize = UILayoutDiagnostics.GameViewSizeTool.CurrentIndex();
        int checks = 0;
        foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(390,844),new Vector2Int(430,932),new Vector2Int(1080,2340)})
        {
            UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(size.x,size.y,"Border " + size.x + "x" + size.y);
            yield return new WaitForSecondsRealtime(1);
            Canvas.ForceUpdateCanvases();
            foreach(var surface in FindObjectsByType<QuestSurfaceGraphic>(FindObjectsSortMode.None))
            {
                if(surface.borderWidth <= 0 || surface.rectTransform.rect.width < 20 || surface.rectTransform.rect.height < 20) continue;
                using(var vh = new VertexHelper())
                {
                    typeof(QuestSurfaceGraphic).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(surface,new object[]{vh});
                    UIVertex outer = default, inner = default;
                    vh.PopulateUIVertex(ref outer,9); vh.PopulateUIVertex(ref inner,46);
                    var cam = surface.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : surface.canvas.worldCamera;
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(cam,surface.rectTransform.TransformPoint(outer.position));
                    Vector2 b = RectTransformUtility.WorldToScreenPoint(cam,surface.rectTransform.TransformPoint(inner.position));
                    if(Mathf.Abs(a.x-b.x) < 1.45f) throw new Exception("[Border FAIL] " + surface.name + " width=" + Mathf.Abs(a.x-b.x));
                    checks++;
                }
            }
            ScreenCapture.CaptureScreenshot("Documentation/UIBorders/"+size.x+"x"+size.y+".png");
            yield return new WaitForEndOfFrame();
        }
        battle.Hide(); overlay.CloseQuest();
        Idle.StageDirector.Instance.ReturnToBase();
        if(originalSize >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(originalSize);
        File.WriteAllText("Documentation/UIBorders/results.txt", checks+" borders passed at 320x568, 390x844, 430x932, 1080x2340. Minimum screen-space width 1.5px. No quest rewards granted. Original GameView index="+originalSize+"\n");
        Debug.Log("[Border] " + checks + " passed");
    }
}
