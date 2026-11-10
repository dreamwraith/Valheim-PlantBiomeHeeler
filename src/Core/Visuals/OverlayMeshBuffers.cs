using System.Collections.Generic;
using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    internal sealed class OverlayMeshBuffers
    {
        private readonly List<Vector3> _vertices;
        private readonly List<Color32> _colors;
        private readonly List<Vector2> _uvs;
        private readonly List<int> _indices;

        public OverlayMeshBuffers(int vertexCapacity)
        {
            _vertices = new List<Vector3>(vertexCapacity);
            _colors = new List<Color32>(vertexCapacity);
            _uvs = new List<Vector2>();
            _indices = new List<int>(vertexCapacity * 2);
        }

        public bool HasGeometry => _indices.Count > 0;

        public void Clear()
        {
            _vertices.Clear();
            _colors.Clear();
            _uvs.Clear();
            _indices.Clear();
        }

        public int AddVertex(Vector3 position, Color32 color)
        {
            _vertices.Add(position);
            _colors.Add(color);
            return _vertices.Count - 1;
        }

        // only the hatch buffers carry uvs, they add every vertex through here
        public int AddVertex(Vector3 position, Color32 color, Vector2 uv)
        {
            _uvs.Add(uv);
            return AddVertex(position, color);
        }

        public void AddTriangle(int first, int second, int third)
        {
            _indices.Add(first);
            _indices.Add(second);
            _indices.Add(third);
        }

        public void AddQuad(int first, int second, int third, int fourth)
        {
            AddTriangle(first, second, third);
            AddTriangle(first, third, fourth);
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            if (_vertices.Count == 0)
            {
                return;
            }

            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            if (_uvs.Count > 0)
            {
                mesh.SetUVs(0, _uvs);
            }

            // SetIndices recalculates the bounds by default
            mesh.SetIndices(_indices, MeshTopology.Triangles, 0);
        }
    }
}
