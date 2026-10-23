using UnityEngine;
using UnityEngine.UI;

/// <summary>Sprite未設定時にも空白の四角を出さない、金の線画エンブレム。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class QuestEmblemGraphic : MaskableGraphic
{
    public enum Symbol { Compass, Chest, Portrait, Star, Swords, Gem, Lock }
    public Symbol symbol;
    public float lineWidth = 3f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float s = Mathf.Min(r.width, r.height);
        if (s <= 0f) return;
        if (symbol == Symbol.Chest)
        {
            Path(vh, r, new[] { new Vector2(.16f,.30f), new Vector2(.16f,.59f), new Vector2(.25f,.76f), new Vector2(.75f,.76f), new Vector2(.84f,.59f), new Vector2(.84f,.30f), new Vector2(.16f,.30f) });
            Line(vh,r,new Vector2(.16f,.55f),new Vector2(.84f,.55f));
            Line(vh,r,new Vector2(.3f,.30f),new Vector2(.3f,.74f));
            Line(vh,r,new Vector2(.7f,.30f),new Vector2(.7f,.74f));
            Path(vh,r,new[] {new Vector2(.46f,.61f),new Vector2(.54f,.61f),new Vector2(.54f,.47f),new Vector2(.46f,.47f),new Vector2(.46f,.61f)});
            Diamond(vh,r,new Vector2(.5f,.92f),.045f);
        }
        else if (symbol == Symbol.Portrait)
        {
            Circle(vh,r,new Vector2(.5f,.5f),.44f);
            Circle(vh,r,new Vector2(.5f,.64f),.12f);
            Path(vh,r,new[] {new Vector2(.23f,.25f),new Vector2(.3f,.43f),new Vector2(.41f,.49f),new Vector2(.5f,.38f),new Vector2(.59f,.49f),new Vector2(.7f,.43f),new Vector2(.77f,.25f)});
            Diamond(vh,r,new Vector2(.5f,.22f),.035f);
        }
        else if (symbol == Symbol.Swords)
        {
            // フォントに ⚔ が無い端末でも出せるよう、交差した剣（細身の刃・鍔・柄・柄頭）を線画で描く。
            // 刃を2本線で描いて先端をとがらせ、ただの「×」に見えないようにする。
            for (int side = 0; side < 2; side++)
            {
                Vector2 P(float x, float y) => new Vector2(side == 0 ? x : 1f - x, y);
                Vector2 n = side == 0 ? new Vector2(-.7071f, .7071f) : new Vector2(.7071f, .7071f);
                Vector2 guard = P(.30f, .30f), tip = P(.92f, .92f);
                const float blade = .065f;
                Line(vh,r,guard + n*blade, tip);                          // 刃（両側の線が先端で合う）
                Line(vh,r,guard - n*blade, tip);
                Line(vh,r,guard + n*.15f, guard - n*.15f);                // 鍔
                Line(vh,r,guard, P(.15f,.15f));                           // 柄
                Diamond(vh,r,P(.12f,.12f),.04f);                          // 柄頭
            }
        }
        else if (symbol == Symbol.Lock)
        {
            // 🔒 未解放ステージの鍵：本体の箱・上のつる（半円）・鍵穴
            Path(vh,r,new[] {new Vector2(.20f,.08f),new Vector2(.80f,.08f),new Vector2(.80f,.50f),new Vector2(.20f,.50f),new Vector2(.20f,.08f)});
            var shackle = new Vector2[17];
            for (int i = 0; i < shackle.Length; i++)
            {
                float a = Mathf.PI * i / (shackle.Length - 1);
                shackle[i] = new Vector2(.5f + Mathf.Cos(a) * .25f, .50f + Mathf.Sin(a) * .30f);
            }
            Path(vh,r,shackle);
            Circle(vh,r,new Vector2(.5f,.33f),.06f);
            Line(vh,r,new Vector2(.5f,.27f),new Vector2(.5f,.17f));
        }
        else if (symbol == Symbol.Gem)
        {
            // 💎 ジェム（絵文字が無い端末でも出せるよう線画で描く）：上面・肩・下へ尖るカット
            Path(vh,r,new[] {new Vector2(.30f,.80f),new Vector2(.70f,.80f),new Vector2(.92f,.58f),new Vector2(.50f,.10f),new Vector2(.08f,.58f),new Vector2(.30f,.80f)});
            Line(vh,r,new Vector2(.08f,.58f),new Vector2(.92f,.58f));
            Path(vh,r,new[] {new Vector2(.30f,.80f),new Vector2(.40f,.58f),new Vector2(.50f,.80f),new Vector2(.60f,.58f),new Vector2(.70f,.80f)});
            Line(vh,r,new Vector2(.40f,.58f),new Vector2(.50f,.10f));
            Line(vh,r,new Vector2(.60f,.58f),new Vector2(.50f,.10f));
        }
        else
        {
            if (symbol == Symbol.Compass) Circle(vh,r,new Vector2(.5f,.5f),.38f);
            Path(vh,r,new[] {new Vector2(.5f,.97f),new Vector2(.59f,.59f),new Vector2(.97f,.5f),new Vector2(.59f,.41f),new Vector2(.5f,.03f),new Vector2(.41f,.41f),new Vector2(.03f,.5f),new Vector2(.41f,.59f),new Vector2(.5f,.97f)});
            Diamond(vh,r,new Vector2(.5f,.5f),.07f);
        }
    }

    private void Diamond(VertexHelper vh, Rect r, Vector2 c, float size)
    {
        Path(vh,r,new[] {c+Vector2.up*size,c+Vector2.right*size,c+Vector2.down*size,c+Vector2.left*size,c+Vector2.up*size});
    }
    private void Circle(VertexHelper vh, Rect r, Vector2 center, float radius)
    {
        for (int i=0;i<48;i++)
        {
            float a=i*Mathf.PI*2f/48f, b=(i+1)*Mathf.PI*2f/48f;
            Line(vh,r,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);
        }
    }
    private void Path(VertexHelper vh, Rect r, Vector2[] points)
    {
        for(int i=1;i<points.Length;i++) Line(vh,r,points[i-1],points[i]);
    }
    private void Line(VertexHelper vh, Rect r, Vector2 a, Vector2 b)
    {
        float size = Mathf.Min(r.width,r.height);
        a = r.center + (a-Vector2.one*.5f)*size;
        b = r.center + (b-Vector2.one*.5f)*size;
        Vector2 direction=(b-a).normalized;
        Vector2 n=new Vector2(-direction.y,direction.x)*lineWidth*.5f;
        int index=vh.currentVertCount;
        vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);
        vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
        vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);
    }
}
