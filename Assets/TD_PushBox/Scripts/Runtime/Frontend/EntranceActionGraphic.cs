using UnityEngine;
using UnityEngine.UI;

namespace TD.PushBox.Frontend
{
    public enum EntranceIcon { Door,Glasses }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EntranceActionGraphic : MaskableGraphic
    {
        public EntranceIcon icon;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();
            Vector2 P(float x,float y)=>new Vector2(rect.x+x*rect.width,rect.y+y*rect.height);
            void Line(float x1,float y1,float x2,float y2)
            {
                Vector2 a=P(x1,y1),b=P(x2,y2),normal=new Vector2(-(b-a).y,(b-a).x).normalized*Mathf.Min(rect.width,rect.height)*0.038f;
                int start=mesh.currentVertCount;
                mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);
                mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
            void Box(float x,float y,float w,float h)
            {Line(x,y,x+w,y);Line(x+w,y,x+w,y+h);Line(x+w,y+h,x,y+h);Line(x,y+h,x,y);}
            if(icon==EntranceIcon.Door)
            {
                Line(0.22f,0.12f,0.22f,0.88f);Line(0.22f,0.88f,0.76f,0.88f);Line(0.76f,0.88f,0.76f,0.12f);
                Line(0.22f,0.88f,0.54f,0.73f);Line(0.54f,0.73f,0.54f,0.08f);Line(0.54f,0.08f,0.22f,0.12f);
                Line(0.43f,0.43f,0.44f,0.43f);Line(0.14f,0.1f,0.86f,0.1f);
            }
            else
            {
                Box(0.1f,0.27f,0.32f,0.39f);Box(0.58f,0.27f,0.32f,0.39f);
                Line(0.42f,0.55f,0.58f,0.55f);Line(0.1f,0.66f,0.02f,0.8f);Line(0.9f,0.66f,0.98f,0.8f);
            }
        }
    }
}
