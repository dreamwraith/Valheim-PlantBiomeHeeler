using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    // chains the per cell border segments into polylines and draws a mitered strip on each side, each in its own
    // biome color. chaining is what keeps corners solid, separate quads leave notches on every turn
    internal sealed class BorderStripBuilder
    {
        private const float MinimumMiterDot = 0.35f;
        private const float MinimumSegmentLengthSquared = 1e-8f;

        private readonly List<BorderSegment> _segments = new List<BorderSegment>(1024);
        private readonly Dictionary<Vector2, EndpointLinks> _links = new Dictionary<Vector2, EndpointLinks>(2048);
        private readonly List<Vector2> _chainPoints = new List<Vector2>(256);
        private bool[] _isSegmentUsed = Array.Empty<bool>();

        public void Clear()
        {
            _segments.Clear();
            _links.Clear();
        }

        public void AddSegment(Vector2 start, Vector2 end, Heightmap.Biome firstBiome, Vector2 pointOnFirstSide, Heightmap.Biome secondBiome)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude < MinimumSegmentLengthSquared)
            {
                return;
            }

            Vector2 leftNormal = new Vector2(-direction.y, direction.x);
            bool isFirstOnLeft = Vector2.Dot(leftNormal, pointOnFirstSide - start) > 0f;
            BorderSegment segment = isFirstOnLeft
                ? new BorderSegment(start, end, firstBiome, secondBiome)
                : new BorderSegment(start, end, secondBiome, firstBiome);

            int segmentIndex = _segments.Count;
            _segments.Add(segment);
            AddLink(start, segmentIndex);
            AddLink(end, segmentIndex);
        }

        public void Emit(OverlayMeshBuffers buffers, OverlayVertexFactory vertexFactory, float borderWidth)
        {
            int segmentCount = _segments.Count;
            if (_isSegmentUsed.Length < segmentCount)
            {
                _isSegmentUsed = new bool[Mathf.NextPowerOfTwo(segmentCount)];
            }

            Array.Clear(_isSegmentUsed, 0, segmentCount);

            for (int seedIndex = 0; seedIndex < segmentCount; seedIndex++)
            {
                if (_isSegmentUsed[seedIndex])
                {
                    continue;
                }

                (int headIndex, BorderSegment head) = FindChainHead(seedIndex);
                CollectChain(headIndex, head);
                EmitChain(buffers, vertexFactory, borderWidth, head.LeftBiome, head.RightBiome);
            }
        }

        private void AddLink(Vector2 point, int segmentIndex)
        {
            if (!_links.TryGetValue(point, out EndpointLinks links))
            {
                _links[point] = new EndpointLinks(segmentIndex, -1, 1);
                return;
            }

            // a third segment makes this a junction, the count alone stops chains here
            _links[point] = links.Count == 1
                ? links with { SecondSegment = segmentIndex, Count = 2 }
                : links with { Count = links.Count + 1 };
        }

        private bool TryGetOtherSegment(Vector2 point, int fromSegment, out int otherSegment)
        {
            if (_links.TryGetValue(point, out EndpointLinks links) && links.Count == 2)
            {
                otherSegment = links.FirstSegment == fromSegment ? links.SecondSegment : links.FirstSegment;
                return true;
            }

            otherSegment = -1;
            return false;
        }

        private (int Index, BorderSegment Segment) FindChainHead(int seedIndex)
        {
            int headIndex = seedIndex;
            BorderSegment head = _segments[seedIndex];

            for (int step = 0; step < _segments.Count; step++)
            {
                if (!TryGetOtherSegment(head.Start, headIndex, out int previousIndex) ||
                    previousIndex == seedIndex ||
                    _isSegmentUsed[previousIndex])
                {
                    break;
                }

                BorderSegment previous = OrientToEndAt(_segments[previousIndex], head.Start);
                if (previous.LeftBiome != head.LeftBiome || previous.RightBiome != head.RightBiome)
                {
                    break;
                }

                headIndex = previousIndex;
                head = previous;
            }

            return (headIndex, head);
        }

        private void CollectChain(int headIndex, BorderSegment head)
        {
            _chainPoints.Clear();
            _chainPoints.Add(head.Start);
            _chainPoints.Add(head.End);
            _isSegmentUsed[headIndex] = true;

            int currentIndex = headIndex;
            BorderSegment current = head;
            while (TryGetOtherSegment(current.End, currentIndex, out int nextIndex) && !_isSegmentUsed[nextIndex])
            {
                BorderSegment next = OrientToStartAt(_segments[nextIndex], current.End);
                if (next.LeftBiome != current.LeftBiome || next.RightBiome != current.RightBiome)
                {
                    break;
                }

                _chainPoints.Add(next.End);
                _isSegmentUsed[nextIndex] = true;
                currentIndex = nextIndex;
                current = next;
            }
        }

        private void EmitChain(
            OverlayMeshBuffers buffers,
            OverlayVertexFactory vertexFactory,
            float borderWidth,
            Heightmap.Biome leftBiome,
            Heightmap.Biome rightBiome)
        {
            int pointCount = _chainPoints.Count;
            bool isClosed = pointCount > 3 && _chainPoints[0].Equals(_chainPoints[pointCount - 1]);

            int previousLeftInner = -1;
            int previousLeftOuter = -1;
            int previousRightInner = -1;
            int previousRightOuter = -1;

            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Vector2 point = _chainPoints[pointIndex];
                Vector2 offset = GetMiterNormal(pointIndex, isClosed) * borderWidth;
                Vector2 leftOuterPoint = point + offset;
                Vector2 rightOuterPoint = point - offset;
                Vector3 innerPosition = vertexFactory.GetPosition(point);

                int leftInner = buffers.AddVertex(innerPosition, vertexFactory.GetBorderColor(leftBiome, point));
                int leftOuter = buffers.AddVertex(vertexFactory.GetPosition(leftOuterPoint), vertexFactory.GetBorderColor(leftBiome, leftOuterPoint));
                int rightInner = buffers.AddVertex(innerPosition, vertexFactory.GetBorderColor(rightBiome, point));
                int rightOuter = buffers.AddVertex(vertexFactory.GetPosition(rightOuterPoint), vertexFactory.GetBorderColor(rightBiome, rightOuterPoint));

                if (pointIndex > 0)
                {
                    buffers.AddQuad(previousLeftInner, previousLeftOuter, leftOuter, leftInner);
                    buffers.AddQuad(previousRightInner, previousRightOuter, rightOuter, rightInner);
                }

                previousLeftInner = leftInner;
                previousLeftOuter = leftOuter;
                previousRightInner = rightInner;
                previousRightOuter = rightOuter;
            }
        }

        // miter so both strips keep their full width through a turn, clamped so sharp corners dont spike out
        private Vector2 GetMiterNormal(int pointIndex, bool isClosed)
        {
            int lastIndex = _chainPoints.Count - 1;
            if (!isClosed && pointIndex == 0)
            {
                return GetLeftNormal(_chainPoints[1] - _chainPoints[0]);
            }

            if (!isClosed && pointIndex == lastIndex)
            {
                return GetLeftNormal(_chainPoints[lastIndex] - _chainPoints[lastIndex - 1]);
            }

            // closed chains repeat the first point at the end, so neighbors wrap past that duplicate
            Vector2 point = _chainPoints[pointIndex];
            Vector2 previousPoint = pointIndex == 0 ? _chainPoints[lastIndex - 1] : _chainPoints[pointIndex - 1];
            Vector2 nextPoint = pointIndex == lastIndex ? _chainPoints[1] : _chainPoints[pointIndex + 1];

            Vector2 outgoingNormal = GetLeftNormal(nextPoint - point);
            Vector2 normalSum = GetLeftNormal(point - previousPoint) + outgoingNormal;
            if (normalSum.sqrMagnitude < 1e-6f)
            {
                return outgoingNormal;
            }

            Vector2 miterDirection = normalSum.normalized;
            float miterDot = Mathf.Max(Vector2.Dot(miterDirection, outgoingNormal), MinimumMiterDot);
            return miterDirection / miterDot;
        }

        private static Vector2 GetLeftNormal(Vector2 direction) =>
            new Vector2(-direction.y, direction.x).normalized;

        private static BorderSegment OrientToStartAt(BorderSegment segment, Vector2 point) =>
            segment.Start.Equals(point) ? segment : Reverse(segment);

        private static BorderSegment OrientToEndAt(BorderSegment segment, Vector2 point) =>
            segment.End.Equals(point) ? segment : Reverse(segment);

        private static BorderSegment Reverse(BorderSegment segment) =>
            new BorderSegment(segment.End, segment.Start, segment.RightBiome, segment.LeftBiome);

        private record struct BorderSegment(Vector2 Start, Vector2 End, Heightmap.Biome LeftBiome, Heightmap.Biome RightBiome);

        private record struct EndpointLinks(int FirstSegment, int SecondSegment, int Count);
    }
}
