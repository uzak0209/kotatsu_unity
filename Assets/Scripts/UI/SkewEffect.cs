using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class SkewEffect : BaseMeshEffect
{
    [Tooltip("X軸方向の傾き具合")]
    public float skewX = 0.5f;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        UIVertex vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            // Y座標の値を利用して、X座標をずらす（平行四辺形にする）
            vertex.position.x += vertex.position.y * skewX;
            vh.SetUIVertex(vertex, i);
        }
    }
}