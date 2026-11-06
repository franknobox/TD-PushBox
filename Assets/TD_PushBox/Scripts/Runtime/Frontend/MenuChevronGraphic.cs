using UnityEngine;
using UnityEngine.UI;

namespace TD.PushBox.Frontend
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuChevronGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();Rect r=GetPixelAdjustedRect();float x=r.center.x,y=r.center.y,w=r.width*0.28f,h=r.height*0.30f,t=2;
            AddLine(helper,new Vector2(x-w,y+h),new Vector2(x+w,y),t);
            AddLine(helper,new Vector2(x+w,y),new Vector2(x-w,y-h),t);
        }
        private void AddLine(VertexHelper helper,Vector2 a,Vector2 b,float thickness)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness/2;int index=helper.currentVertCount;
            helper.AddVert(a+n,color,Vector2.zero);helper.AddVert(b+n,color,Vector2.zero);
            helper.AddVert(b-n,color,Vector2.zero);helper.AddVert(a-n,color,Vector2.zero);
            helper.AddTriangle(index,index+1,index+2);helper.AddTriangle(index,index+2,index+3);
        }
    }
}
