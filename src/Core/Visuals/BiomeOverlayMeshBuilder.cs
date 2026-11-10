using System;
using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    // turns the contour pieces into the fill, hatch, and border meshes
    internal sealed class BiomeOverlayMeshBuilder
    {
        private readonly PlantBiomeSampler _sampler = new PlantBiomeSampler();
        private readonly OverlayVertexFactory _vertexFactory;
        private readonly BiomeContourBuilder _contourBuilder;
        private readonly BorderStripBuilder _borderStrips = new BorderStripBuilder();
        private readonly OverlayMeshBuffers _fillBuffers = new OverlayMeshBuffers(16384);
        private readonly OverlayMeshBuffers _hatchBuffers = new OverlayMeshBuffers(16384);
        private readonly OverlayMeshBuffers _borderBuffers = new OverlayMeshBuffers(4096);

        private int[] _nodeVertices = Array.Empty<int>();
        private int[] _nodeVertexStamps = Array.Empty<int>();
        private int _stamp;
        private bool _hasHatchLines;

        public BiomeOverlayMeshBuilder()
        {
            _vertexFactory = new OverlayVertexFactory(new TerrainHeightSampler(_sampler));
            _contourBuilder = new BiomeContourBuilder(_sampler, this, _borderStrips);
        }

        public bool HasFillGeometry => _fillBuffers.HasGeometry;
        public bool HasHatchGeometry => _hatchBuffers.HasGeometry;
        public bool HasBorderGeometry => _borderBuffers.HasGeometry;

        public void Rebuild(Mesh fillMesh, Mesh hatchMesh, Mesh borderMesh, in OverlayBuildSpec spec)
        {
            _hasHatchLines = spec.EnableHatchLines;
            _fillBuffers.Clear();
            _hatchBuffers.Clear();
            _borderBuffers.Clear();
            _borderStrips.Clear();

            _sampler.Prepare(spec.FocusCenter, spec.Radius);
            _vertexFactory.Prepare(in spec);
            _contourBuilder.Build(in spec);
            _borderStrips.Emit(_borderBuffers, _vertexFactory, spec.BorderWidth);
            _sampler.Clear();

            _fillBuffers.ApplyTo(fillMesh);
            _hatchBuffers.ApplyTo(hatchMesh);
            _borderBuffers.ApplyTo(borderMesh);
        }

        // stamps mark node vertices this rebuild already added, so nothing needs clearing between rebuilds
        public void PrepareNodeVertices(int nodeCount)
        {
            if (_nodeVertexStamps.Length < nodeCount)
            {
                _nodeVertices = new int[nodeCount];
                _nodeVertexStamps = new int[nodeCount];
                _stamp = 0;
            }

            if (_stamp == int.MaxValue)
            {
                Array.Clear(_nodeVertexStamps, 0, _nodeVertexStamps.Length);
                _stamp = 0;
            }

            _stamp++;
        }

        // uniform cells share node vertices. a node has one biome so the shared color is always right
        public void AddUniformQuad(int southWestNode, int rowStride, Vector2 southWestPoint, Heightmap.Biome biome)
        {
            int northWestNode = southWestNode + rowStride;
            AddFillQuadIndices(
                GetNodeVertex(southWestNode, southWestPoint, biome),
                GetNodeVertex(southWestNode + 1, southWestPoint + new Vector2(1f, 0f), biome),
                GetNodeVertex(northWestNode + 1, southWestPoint + new Vector2(1f, 1f), biome),
                GetNodeVertex(northWestNode, southWestPoint + new Vector2(0f, 1f), biome));
        }

        // contour pieces are convex, so a fan from the first point covers them
        public void AddFillPolygon(Vector2[] points, int pointCount, Heightmap.Biome biome)
        {
            int firstVertex = AddFillVertex(points[0], biome);
            for (int pointIndex = 1; pointIndex < pointCount; pointIndex++)
            {
                AddFillVertex(points[pointIndex], biome);
            }

            for (int pointIndex = 1; pointIndex < pointCount - 1; pointIndex++)
            {
                AddFillTriangleIndices(firstVertex, firstVertex + pointIndex, firstVertex + pointIndex + 1);
            }
        }

        public void AddFillTriangle(Vector2 firstPoint, Vector2 secondPoint, Vector2 thirdPoint, Heightmap.Biome biome) =>
            AddFillTriangleIndices(
                AddFillVertex(firstPoint, biome),
                AddFillVertex(secondPoint, biome),
                AddFillVertex(thirdPoint, biome));

        public void AddFillQuad(Vector2 southWestPoint, Vector2 northEastPoint, Heightmap.Biome biome) =>
            AddFillQuadIndices(
                AddFillVertex(southWestPoint, biome),
                AddFillVertex(new Vector2(northEastPoint.x, southWestPoint.y), biome),
                AddFillVertex(northEastPoint, biome),
                AddFillVertex(new Vector2(southWestPoint.x, northEastPoint.y), biome));

        private int GetNodeVertex(int nodeIndex, Vector2 point, Heightmap.Biome biome)
        {
            if (_nodeVertexStamps[nodeIndex] == _stamp)
            {
                return _nodeVertices[nodeIndex];
            }

            int vertexIndex = AddFillVertex(point, biome);
            _nodeVertexStamps[nodeIndex] = _stamp;
            _nodeVertices[nodeIndex] = vertexIndex;
            return vertexIndex;
        }

        // the hatch mesh is the fill mesh again in border colors. every fill vertex and index goes into both
        // buffers in the same order, so one vertex index is valid in either
        private int AddFillVertex(Vector2 point, Heightmap.Biome biome)
        {
            Vector3 position = _vertexFactory.GetPosition(point);
            if (_hasHatchLines)
            {
                _hatchBuffers.AddVertex(position, _vertexFactory.GetBorderColor(biome, point), _vertexFactory.GetHatchUv(point));
            }

            return _fillBuffers.AddVertex(position, _vertexFactory.GetFillColor(biome, point));
        }

        private void AddFillTriangleIndices(int first, int second, int third)
        {
            _fillBuffers.AddTriangle(first, second, third);
            if (_hasHatchLines)
            {
                _hatchBuffers.AddTriangle(first, second, third);
            }
        }

        private void AddFillQuadIndices(int first, int second, int third, int fourth)
        {
            AddFillTriangleIndices(first, second, third);
            AddFillTriangleIndices(first, third, fourth);
        }
    }
}
