using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>Tap presentation only. A bounded pool keeps rapid tapping inexpensive.</summary>
    public sealed class MainAttackVfx : MonoBehaviour
    {
        const int Capacity = 6;
        readonly TapSpellGraphic[] effects = new TapSpellGraphic[Capacity];
        static readonly string[] Sounds = { "se_tap_fire", "se_tap_ice", "se_tap_thunder" };
        public string AttackSound => LastStyle >= 0 ? Sounds[LastStyle] : SfxDirector.Attack;
        int next, style;
        public int LastStyle { get; private set; } = -1;
        public int ActiveCount { get { int count=0; foreach(var fx in effects) if(fx != null && fx.gameObject.activeSelf) count++; return count; } }

        public void Play(EnemyController enemy)
        {
            var canvas = GetComponentInParent<Canvas>();
            var player = transform as RectTransform;
            var target = enemy != null ? enemy.AttackVisual : null;
            if (canvas == null || player == null || target == null) return;
            var fx = effects[next];
            if (fx == null)
            {
                var go = new GameObject("TapSpell", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(canvas.transform, false);
                fx = go.AddComponent<TapSpellGraphic>();
                fx.raycastTarget = false;
                var rt = fx.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                effects[next] = fx;
            }
            next = (next + 1) % Capacity;
            LastStyle = style;
            // Sample positions now: killing the enemy must not leave a dangling target.
            Vector3 start = player.TransformPoint(new Vector3(player.rect.xMax * .65f, player.rect.center.y, 0));
            Vector3 end = target.TransformPoint(target.rect.center);
            fx.Launch(start, end, style);
            style = (style + 1) % 3;
        }
        public void Clear() { foreach(var fx in effects) if(fx != null) fx.gameObject.SetActive(false); }
        void OnDisable() => Clear();
        void OnDestroy() { foreach(var fx in effects) if(fx != null) Destroy(fx.gameObject); }
    }

    /// <summary>Flame streak, ice crystal, lightning bolt; no textures or gameplay state.</summary>
    public sealed class TapSpellGraphic : MaskableGraphic
    {
        Vector2 start, end;
        int style;
        float elapsed;
        const float Duration = .32f;
        static readonly Color[] Palette = { new Color(1f,.42f,.12f), new Color(.3f,.9f,1f), new Color(1f,.91f,.3f) };
        public void Launch(Vector3 worldStart, Vector3 worldEnd, int element)
        {
            start = rectTransform.InverseTransformPoint(worldStart);
            end = rectTransform.InverseTransformPoint(worldEnd);
            style = element; elapsed = 0;
            gameObject.SetActive(true); transform.SetAsLastSibling(); SetVerticesDirty();
        }
        void Update()
        {
            elapsed += Time.deltaTime;
            if(elapsed >= Duration) { gameObject.SetActive(false); return; }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float travel = Mathf.Clamp01(elapsed/.085f);
            float fade = 1f-Mathf.InverseLerp(.12f, Duration, elapsed);
            Color tint = Palette[style]; tint.a = fade;
            Vector2 head = Vector2.Lerp(start,end,travel);
            Vector2 direction = (end-start).normalized;
            Vector2 normal = new Vector2(-direction.y,direction.x);
            if(style == 2)
            {
                Vector2 previous=start;
                for(int i=1;i<=8;i++)
                {
                    var point=Vector2.Lerp(start,head,i/8f)+normal*(i==8?0:(i%2==0?-18:18));
                    Line(vh,previous,point,12,new Color(tint.r,tint.g,tint.b,fade*.25f));
                    Line(vh,previous,point,4,tint); previous=point;
                }
            }
            else
            {
                Line(vh,Vector2.Lerp(start,head,.5f),head,20,new Color(tint.r,tint.g,tint.b,fade*.2f));
                Line(vh,Vector2.Lerp(start,head,.65f),head,6,tint);
                float radius=style==0?18:23;
                Diamond(vh,head,direction*radius,normal*(style==0?12:16),tint);
                Diamond(vh,head,direction*9,normal*6,new Color(1,1,1,fade));
            }
            if(travel < 1) return;
            float burst=Mathf.InverseLerp(.085f,Duration,elapsed);
            int rays=style==1?6:8;
            for(int i=0;i<rays;i++)
            {
                float angle=i*Mathf.PI*2/rays;
                Vector2 ray=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float radius=14+burst*60;
                Line(vh,end+ray*radius,end+ray*(radius+14),style==1?4:6,tint);
                if(style==1) Diamond(vh,end+ray*radius,ray*9,new Vector2(-ray.y,ray.x)*5,tint);
            }
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            Vector2 d=b-a; if(d.sqrMagnitude<.001f)return;
            Vector2 n=new Vector2(-d.y,d.x).normalized*width*.5f;
            Quad(vh,a-n,a+n,b+n,b-n,c);
        }
        static void Diamond(VertexHelper vh,Vector2 p,Vector2 a,Vector2 b,Color c) => Quad(vh,p-a,p-b,p+a,p+b,c);
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int n=vh.currentVertCount;
            vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
