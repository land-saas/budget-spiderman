using System.Collections.Generic;
using UnityEngine;
/// Bounded, deterministic chunk streaming. Existing authored town stays intact.
public sealed class WoodlandEndlessTerrain : MonoBehaviour {
 public Transform player;
 public Material groundMaterial,anchorMaterial,treeMaterial;
 public int seed=731, radius=2;
 public float chunkSize=32;
 readonly Dictionary<Vector2Int,GameObject> chunks=new();
 Mesh treeMesh;
 public int ActiveChunkCount=>chunks.Count;
 void Start(){Refresh();}
 void Update(){Refresh();}
 public void Refresh(){
 if(!player)return;var center=new Vector2Int(Mathf.FloorToInt(player.position.x/chunkSize),Mathf.FloorToInt(player.position.z/chunkSize));
 var remove=new List<Vector2Int>();foreach(var pair in chunks)if(Mathf.Abs(pair.Key.x-center.x)>radius||Mathf.Abs(pair.Key.y-center.y)>radius)remove.Add(pair.Key);
 foreach(var key in remove){var obj=chunks[key];obj.SetActive(false);var filter=obj.GetComponent<MeshFilter>();if(filter&&filter.sharedMesh)DestroyObject(filter.sharedMesh);DestroyObject(obj);chunks.Remove(key);}
 for(int x=center.x-radius;x<=center.x+radius;x++)for(int z=center.y-radius;z<=center.y+radius;z++){var key=new Vector2Int(x,z);if(!chunks.ContainsKey(key))chunks.Add(key,Build(key));}
 }
 public float Height(float x,float z){float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))-65)/20));return -.65f+blend*(Mathf.PerlinNoise((x+seed)*.018f,(z+seed)*.018f)*2.1f);}
 GameObject Build(Vector2Int key){
 var chunk=new GameObject("Chunk "+key.x+", "+key.y);chunk.transform.SetParent(transform,false);
 const int n=16;var vertices=new Vector3[(n+1)*(n+1)];var uv=new Vector2[vertices.Length];var indices=new int[n*n*6];
 for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){float wx=(key.x+x/(float)n)*chunkSize,wz=(key.y+z/(float)n)*chunkSize;vertices[z*(n+1)+x]=new Vector3(wx,Height(wx,wz),wz);uv[z*(n+1)+x]=new Vector2(wx/4,wz/4);}
 int at=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++){int a=z*(n+1)+x;indices[at++]=a;indices[at++]=a+n+1;indices[at++]=a+1;indices[at++]=a+1;indices[at++]=a+n+1;indices[at++]=a+n+2;}
 var mesh=new Mesh{name="Streamed terrain"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();chunk.AddComponent<MeshFilter>().sharedMesh=mesh;chunk.AddComponent<MeshRenderer>().sharedMaterial=groundMaterial;chunk.AddComponent<MeshCollider>().sharedMesh=mesh;
 // Leave the authored central map's anchors and vegetation unchanged.
 for(int z=0;z<2;z++)for(int x=0;x<2;x++){float wx=key.x*chunkSize+8+x*16,wz=key.y*chunkSize+8+z*16;if(Mathf.Abs(wx)<22&&Mathf.Abs(wz)<18)continue;CreateAnchor(chunk.transform,new Vector3(wx,Height(wx,wz)+8+2*Mathf.PerlinNoise(wx*.07f+seed,wz*.07f),wz));}
 var rng=new System.Random(unchecked(seed+key.x*73856093^key.y*19349663));
 for(int i=0;i<10;i++){float x=key.x*chunkSize+(float)rng.NextDouble()*chunkSize,z=key.y*chunkSize+(float)rng.NextDouble()*chunkSize;if(Mathf.Abs(x)<25&&Mathf.Abs(z)<22)continue;var tree=new GameObject("Backdrop pine (no collider)");tree.transform.SetParent(chunk.transform,false);tree.transform.position=new Vector3(x,Height(x,z),z);float size=3+(float)rng.NextDouble()*3;tree.transform.localScale=Vector3.one*size;tree.AddComponent<MeshFilter>().sharedMesh=GetTreeMesh();tree.AddComponent<MeshRenderer>().sharedMaterial=treeMaterial;tree.AddComponent<ForestRoadtrip25D.ForestBillboard>();}
 return chunk;
 }
 Mesh GetTreeMesh(){if(treeMesh)return treeMesh;treeMesh=new Mesh{name="Stepped pixel pine"};var v=new List<Vector3>();var t=new List<int>();for(int row=0;row<7;row++){float y=row/7f,w=(7-row)*.065f;int a=v.Count;v.Add(new Vector3(-w,y,0));v.Add(new Vector3(w,y,0));v.Add(new Vector3(-w,y+1f/7,0));v.Add(new Vector3(w,y+1f/7,0));t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}treeMesh.SetVertices(v);treeMesh.SetTriangles(t,0);treeMesh.RecalculateNormals();return treeMesh;}
 void CreateAnchor(Transform parent,Vector3 position){var obj=new GameObject("Generated sky anchor");obj.transform.SetParent(parent,false);obj.transform.position=position;obj.AddComponent<WoodlandSkyAnchor>();var c=obj.AddComponent<SphereCollider>();c.radius=.8f;c.isTrigger=true;var l=obj.AddComponent<LineRenderer>();l.sharedMaterial=anchorMaterial;l.useWorldSpace=false;l.loop=true;l.positionCount=12;l.startWidth=l.endWidth=.11f;for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;l.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.48f);}}
 static void DestroyObject(Object obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
 void OnDestroy(){foreach(var pair in chunks)if(pair.Value){var f=pair.Value.GetComponent<MeshFilter>();if(f&&f.sharedMesh)DestroyObject(f.sharedMesh);}if(treeMesh)DestroyObject(treeMesh);}
}
