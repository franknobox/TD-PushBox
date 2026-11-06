using System;
using System.Collections.Generic;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    internal sealed class VoxelModelBuilder
    {
        private readonly List<Vector3> vertices = new(), normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int>[] triangles = { new(), new(), new(), new(), new() };

        private void Surface(int columns, int rows, int material, Func<float,float,Vector3> point,
            Func<float,float,Vector3> normal, Vector3 center, Quaternion rotation)
        {
            int start = vertices.Count;
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns, v = y / (float)rows;
                vertices.Add(center + rotation * point(u,v)); normals.Add((rotation * normal(u,v)).normalized); uvs.Add(new Vector2(u,v));
            }
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                int a = start + y * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
                Triangle(a,b,c,material); Triangle(b,d,c,material);
            }
        }
        private void Triangle(int a, int b, int c, int material)
        {
            Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (cross.sqrMagnitude < 0.0000000001f) return;
            if (Vector3.Dot(cross, normals[a] + normals[b] + normals[c]) < 0) (b,c) = (c,b);
            triangles[material].Add(a); triangles[material].Add(b); triangles[material].Add(c);
        }
        public void Box(Vector3 center, Vector3 size, int material, Vector3 euler = default)
        {
            Vector3 half = size * 0.5f;
            foreach (Vector3 face in new[] { Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back })
            {
                Vector3 a = Mathf.Abs(face.y) > 0 ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(face,a);
                Surface(1,1,material,(u,v)=>Vector3.Scale(face+a*(u*2-1)+b*(v*2-1),half),
                    (u,v)=>face,center,Quaternion.Euler(euler));
            }
        }
        public void SlopedLayer(float bottomLow, float bottomHigh, float topLow, float topHigh, int material, float yaw)
        {
            Vector3[] points =
            {
                new(-0.54f,bottomLow,-0.54f), new(0.54f,bottomLow,-0.54f),
                new(0.54f,bottomHigh,0.54f), new(-0.54f,bottomHigh,0.54f),
                new(-0.54f,topLow,-0.54f), new(0.54f,topLow,-0.54f),
                new(0.54f,topHigh,0.54f), new(-0.54f,topHigh,0.54f)
            };
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Length; i++) { points[i] = rotation * points[i]; center += points[i] / 8; }
            int[][] faces = { new[]{0,1,2,3}, new[]{4,7,6,5}, new[]{0,4,5,1}, new[]{3,2,6,7}, new[]{0,3,7,4}, new[]{1,5,6,2} };
            foreach (int[] face in faces)
            {
                Vector3 a = points[face[0]], b = points[face[1]], c = points[face[2]], d = points[face[3]];
                if (Vector3.Dot(Vector3.Cross(b-a,c-a), (a+b+c+d)/4-center) < 0) (b,d) = (d,b);
                FlatTriangle(a,b,c,material); FlatTriangle(a,c,d,material);
            }
        }

        private void FlatTriangle(Vector3 a, Vector3 b, Vector3 c, int material)
        {
            int start=vertices.Count;
            Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{normal,normal,normal});
            uvs.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.right});
            Triangle(start,start+1,start+2,material);
        }
        private void Pyramid(Vector3 center, float width, float depth, float height, int material)
        {
            Vector3[] corners={center+new Vector3(-width/2,0,-depth/2),center+new Vector3(width/2,0,-depth/2),
                center+new Vector3(width/2,0,depth/2),center+new Vector3(-width/2,0,depth/2)};
            for(int i=0;i<4;i++) FlatTriangle(corners[i],center+Vector3.up*height,corners[(i+1)%4],material);
            FlatTriangle(corners[0],corners[1],corners[2],material);
            FlatTriangle(corners[0],corners[2],corners[3],material);
        }
        private void Bar(Vector3 from, Vector3 to, float width, int material)
        {
            Box((from+to)*0.5f,new Vector3(width,Vector3.Distance(from,to),width),material,
                Quaternion.FromToRotation(Vector3.up,to-from).eulerAngles);
        }
        public void Ring(Vector3 center, float radius, float thickness, int material, Vector3 euler = default)
        {
            Surface(28,8,material,(u,v)=>
            {
                float a=u*Mathf.PI*2,b=v*Mathf.PI*2,r=radius+thickness*Mathf.Cos(b);
                return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(b)*thickness,Mathf.Sin(a)*r);
            },(u,v)=>new Vector3(Mathf.Cos(u*Mathf.PI*2)*Mathf.Cos(v*Mathf.PI*2),Mathf.Sin(v*Mathf.PI*2),Mathf.Sin(u*Mathf.PI*2)*Mathf.Cos(v*Mathf.PI*2)),center,Quaternion.Euler(euler));
        }
        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name, subMeshCount = triangles.Length };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uvs);
            for(int i=0;i<triangles.Length;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateBounds(); return mesh;
        }

        public static Mesh Hero(string role,bool fighter=false)
        {
            if(role!="Warrior"&&role!="Thief"&&role!="Mage"&&role!="Shieldbearer") throw new ArgumentException("未知的角色类型。",nameof(role));
            var b = new VoxelModelBuilder();
            bool warrior=role=="Warrior",thief=role=="Thief",shieldbearer=role=="Shieldbearer";
            float heightScale=warrior?1.12f:shieldbearer?1.08f:1f,widthScale=warrior?1.16f:shieldbearer?1.10f:1f;
            void Body(Vector3 center,Vector3 size,int material) => b.Box(
                Vector3.Scale(center,new Vector3(widthScale,heightScale,widthScale)),
                Vector3.Scale(size,new Vector3(widthScale,heightScale,widthScale)),material);
            foreach(float side in new[]{-1f,1f})
                Body(new Vector3(side*0.13f,0.14f,0),new Vector3(0.22f,0.28f,0.28f),3);
            Body(new Vector3(0,0.6f,0),new Vector3(0.6f,0.66f,0.38f),1);
            Body(new Vector3(0,1.15f,0),new Vector3(0.44f,0.44f,0.42f),2);
            Body(new Vector3(0,1.40f,0.01f),new Vector3(0.48f,0.10f,0.45f),3);
            if(shieldbearer)
            {
                Body(new Vector3(0.38f,0.66f,0),new Vector3(0.16f,0.50f,0.28f),1);
                Body(new Vector3(0.38f,0.38f,0),new Vector3(0.16f,0.16f,0.24f),2);
                Body(new Vector3(-0.32f,0.70f,-0.12f),new Vector3(0.16f,0.20f,0.26f),2);
                Body(new Vector3(0,1.39f,-0.232f),new Vector3(0.46f,0.08f,0.035f),0);
                b.Box(new Vector3(-0.25f,0.72f,-0.32f),new Vector3(0.64f,1.12f,0.14f),0);
                b.Box(new Vector3(-0.25f,0.72f,-0.405f),new Vector3(0.54f,0.99f,0.035f),4);
                b.Box(new Vector3(-0.25f,0.72f,-0.433f),new Vector3(0.065f,0.83f,0.025f),0);
                b.Box(new Vector3(-0.25f,0.72f,-0.455f),new Vector3(0.15f,0.18f,0.035f),3);
            }
            else if(warrior)
            {
                if(fighter)
                {
                    foreach(float side in new[]{-1f,1f})
                    {
                        Body(new Vector3(side*0.40f,0.69f,0),new Vector3(0.23f,0.48f,0.33f),1);
                        Body(new Vector3(side*0.40f,0.40f,-0.015f),new Vector3(0.24f,0.22f,0.34f),2);
                    }
                }
                else
                {
                Vector3 guard=new(0.45f,0.62f,-0.02f);
                Quaternion rotation=Quaternion.Euler(0,0,-16);
                b.Box(guard+rotation*Vector3.up*0.35f,new Vector3(0.09f,0.65f,0.065f),0,rotation.eulerAngles);
                b.Box(guard,new Vector3(0.24f,0.07f,0.085f),4,rotation.eulerAngles);
                b.Box(guard-rotation*Vector3.up*0.08f,new Vector3(0.06f,0.13f,0.07f),3,rotation.eulerAngles);
                }
            }
            else if(thief)
            {
                // A clipped, angular hook hangs at the waist with its free tip facing down.
                b.Box(new Vector3(0.34f,0.79f,-0.025f),new Vector3(0.09f,0.07f,0.10f),3);
                b.Box(new Vector3(0.37f,0.74f,-0.025f),new Vector3(0.065f,0.15f,0.08f),3);
                b.Bar(new Vector3(0.37f,0.68f,-0.025f),new Vector3(0.37f,0.53f,-0.025f),0.06f,0);
                b.Bar(new Vector3(0.37f,0.53f,-0.025f),new Vector3(0.44f,0.59f,-0.025f),0.06f,0);
                b.Bar(new Vector3(0.44f,0.59f,-0.025f),new Vector3(0.53f,0.53f,-0.025f),0.06f,0);
                b.Bar(new Vector3(0.53f,0.53f,-0.025f),new Vector3(0.53f,0.39f,-0.025f),0.06f,0);
            }
            else
            {
                b.Box(new Vector3(0,1.475f,0.01f),new Vector3(0.60f,0.05f,0.54f),1);
                b.Pyramid(new Vector3(0,1.50f,0.01f),0.43f,0.41f,0.34f,1);
                b.Box(new Vector3(0.41f,0.84f,-0.025f),new Vector3(0.065f,1.24f,0.065f),3);
                b.Box(new Vector3(0.41f,1.49f,-0.025f),new Vector3(0.15f,0.15f,0.15f),4);
            }
            return b.Mesh(role);
        }

        public static VoxelModelBuilder Sentry() => LegacyEnemy(true);

        public static VoxelModelBuilder LegacyEnemy(bool ranged)
        {
            var b = new VoxelModelBuilder();
            // Exact block dimensions and colors from the original SRPG enemy.
            b.Box(new Vector3(0,0.16f,0),new Vector3(0.58f,0.28f,0.40f),3);
            b.Box(new Vector3(0,0.52f,0),new Vector3(0.62f,0.58f,0.48f),1);
            b.Box(new Vector3(0,0.94f,-0.02f),new Vector3(0.50f,0.40f,0.46f),2);
            foreach (float side in new[]{-1f,1f})
                b.Box(new Vector3(side*0.35f,0.98f,0),new Vector3(0.28f,0.12f,0.18f),2,new Vector3(0,0,-side*18));
            if (!ranged)
                b.Box(new Vector3(0.46f,0.58f,0),new Vector3(0.14f,0.74f,0.16f),0,new Vector3(0,0,-25));
            else
            {
                b.Box(new Vector3(0.19f,0.63f,-0.29f),new Vector3(0.18f,0.14f,0.36f),3);
                b.Box(new Vector3(0.19f,0.65f,-0.57f),new Vector3(0.075f,0.075f,0.48f),4);
                b.Box(new Vector3(0.19f,0.53f,-0.30f),new Vector3(0.08f,0.18f,0.10f),3);
                b.Box(new Vector3(-0.08f,0.62f,-0.32f),new Vector3(0.40f,0.10f,0.12f),1);
            }
            return b;
        }

        public static Mesh Crystal()
        {
            var b=new VoxelModelBuilder();
            void Chunk(Vector3 origin,float width,float depth,float height,float yaw,int shade)
            {
                var rotation=Quaternion.Euler(0,yaw,0);var rings=new Vector3[3,6];
                float[] radii={0.82f,1,0.78f},levels={0,0.52f,0.83f};
                for(int ring=0;ring<3;ring++) for(int side=0;side<6;side++)
                {
                    float angle=side*Mathf.PI/3;
                    rings[ring,side]=origin+rotation*new Vector3(Mathf.Cos(angle)*width*0.5f*radii[ring],levels[ring]*height,Mathf.Sin(angle)*depth*0.5f*radii[ring]);
                }
                Vector3 center=origin+Vector3.up*height*0.45f;
                void Face(Vector3 a,Vector3 c,Vector3 d,int material)
                {if(Vector3.Dot(Vector3.Cross(c-a,d-a),(a+c+d)/3-center)<0) (c,d)=(d,c);b.FlatTriangle(a,c,d,material);}
                for(int ring=0;ring<2;ring++) for(int side=0;side<6;side++)
                {
                    int next=(side+1)%6,material=(side+shade)%3;
                    Face(rings[ring,side],rings[ring,next],rings[ring+1,next],material);
                    Face(rings[ring,side],rings[ring+1,next],rings[ring+1,side],material);
                }
                var tip=origin+rotation*new Vector3(width*0.08f,height,depth*0.05f);
                for(int side=0;side<6;side++) Face(rings[2,side],rings[2,(side+1)%6],tip,side%2==0?2:3);
            }
            Chunk(new Vector3(-0.05f,0.05f,0.03f),0.68f,0.60f,0.97f,12,0);
            Chunk(new Vector3(0.28f,0.02f,-0.08f),0.40f,0.45f,0.67f,-16,1);
            Chunk(new Vector3(-0.29f,0.01f,-0.15f),0.36f,0.35f,0.51f,-24,0);
            Chunk(new Vector3(0.05f,0,-0.27f),0.43f,0.40f,0.46f,20,1);
            Chunk(new Vector3(0.21f,0.04f,0.22f),0.35f,0.35f,0.77f,5,0);
            return b.Mesh("Object_Crystal");
        }

        public static Mesh Ground()
        {
            var b=new VoxelModelBuilder();
            b.Box(new Vector3(0,-0.28f,0),new Vector3(1.08f,0.56f,1.08f),0);
            return b.Mesh("Ground Tile");
        }
    }
}
