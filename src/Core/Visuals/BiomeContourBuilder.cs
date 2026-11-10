using System;
using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    // marching squares over a 1m lattice of plant biome samples. nodes sit on k + 0.5 so the straight borders on
    // whole meter lines (zone edges and zone center lines) land mid edge instead of on a node
    internal sealed class BiomeContourBuilder
    {
        private const int LatticeMargin = 2;
        private const int MaximumSubdivisionDepth = 3;

        // points inside a cell are whole 1/128m units from its south west corner. those stay exact in a float
        // anywhere in the world, so cells and sub cells that share an edge compute bit identical points
        private const int UnitsPerCell = 128;
        private const float UnitSize = 1f / UnitsPerCell;

        // bisection stops at 2 units, so 6 steps on a lattice edge pin a crossing to 1/64m
        private const int CrossingResolutionUnits = 2;

        private readonly PlantBiomeSampler _sampler;
        private readonly BiomeOverlayMeshBuilder _meshBuilder;
        private readonly BorderStripBuilder _borderStrips;

        // one cell ring, 4 corners plus up to 4 crossings, counter clockwise from south west
        private readonly Vector2[] _ringPoints = new Vector2[8];
        private readonly Heightmap.Biome[] _ringBiomes = new Heightmap.Biome[8];
        private readonly bool[] _ringIsCrossing = new bool[8];
        private readonly Vector2[] _polygonPoints = new Vector2[8];
        private readonly Heightmap.Biome[] _cornerBiomes = new Heightmap.Biome[4];
        private int _ringCount;

        private Heightmap.Biome[] _nodeBiomes = Array.Empty<Heightmap.Biome>();
        private OverlayBuildSpec _spec;
        private int _latticeSize;
        private int _latticeOriginX;
        private int _latticeOriginZ;
        private float _cellOriginX;
        private float _cellOriginZ;

        public BiomeContourBuilder(PlantBiomeSampler sampler, BiomeOverlayMeshBuilder meshBuilder, BorderStripBuilder borderStrips)
        {
            _sampler = sampler;
            _meshBuilder = meshBuilder;
            _borderStrips = borderStrips;
        }

        public void Build(in OverlayBuildSpec spec)
        {
            _spec = spec;
            PrepareLattice();
            ClassifyNodes();
            EmitCells();
        }

        private float NodeX(int column) => _latticeOriginX + column + 0.5f;

        private float NodeZ(int row) => _latticeOriginZ + row + 0.5f;

        private void PrepareLattice()
        {
            int extent = Mathf.CeilToInt(_spec.Radius) + LatticeMargin;
            _latticeOriginX = _spec.FocusCellX - extent;
            _latticeOriginZ = _spec.FocusCellZ - extent;
            _latticeSize = (2 * extent) + 1;

            int nodeCount = _latticeSize * _latticeSize;
            if (_nodeBiomes.Length < nodeCount)
            {
                _nodeBiomes = new Heightmap.Biome[nodeCount];
            }

            Array.Clear(_nodeBiomes, 0, nodeCount);
            _meshBuilder.PrepareNodeVertices(nodeCount);
        }

        // each heightmap labels its own nodes directly, no zone lookup per node. nodes never sit on a zone edge
        private void ClassifyNodes()
        {
            Vector2 focus = _spec.FocusCenter;
            float reach = _spec.Radius + 1.5f;
            float reachSquared = reach * reach;
            int lastNode = _latticeSize - 1;

            foreach (Heightmap heightmap in _sampler.Heightmaps)
            {
                Vector3 center = heightmap.transform.position;
                float halfSize = heightmap.m_width * heightmap.m_scale * 0.5f;
                int firstColumn = Mathf.Max(0, Mathf.CeilToInt(center.x - halfSize - _latticeOriginX - 0.5f));
                int lastColumn = Mathf.Min(lastNode, Mathf.FloorToInt(center.x + halfSize - _latticeOriginX - 0.5f));
                int firstRow = Mathf.Max(0, Mathf.CeilToInt(center.z - halfSize - _latticeOriginZ - 0.5f));
                int lastRow = Mathf.Min(lastNode, Mathf.FloorToInt(center.z + halfSize - _latticeOriginZ - 0.5f));

                for (int row = firstRow; row <= lastRow; row++)
                {
                    float nodeZ = NodeZ(row);
                    float offsetZ = nodeZ - focus.y;
                    for (int column = firstColumn; column <= lastColumn; column++)
                    {
                        float nodeX = NodeX(column);
                        float offsetX = nodeX - focus.x;
                        if ((offsetX * offsetX) + (offsetZ * offsetZ) > reachSquared)
                        {
                            continue;
                        }

                        _nodeBiomes[(row * _latticeSize) + column] = heightmap.GetBiome(new Vector3(nodeX, 0f, nodeZ));
                    }
                }
            }
        }

        private void EmitCells()
        {
            Vector2 focus = _spec.FocusCenter;
            float radiusSquared = _spec.Radius * _spec.Radius;
            int cellsPerSide = _latticeSize - 1;

            for (int row = 0; row < cellsPerSide; row++)
            {
                float offsetZ = NodeZ(row) + 0.5f - focus.y;
                for (int column = 0; column < cellsPerSide; column++)
                {
                    float offsetX = NodeX(column) + 0.5f - focus.x;
                    if ((offsetX * offsetX) + (offsetZ * offsetZ) > radiusSquared)
                    {
                        continue;
                    }

                    int southWest = (row * _latticeSize) + column;
                    int southEast = southWest + 1;
                    int northWest = southWest + _latticeSize;
                    int northEast = northWest + 1;
                    Heightmap.Biome southWestBiome = _nodeBiomes[southWest];
                    Heightmap.Biome southEastBiome = _nodeBiomes[southEast];
                    Heightmap.Biome northEastBiome = _nodeBiomes[northEast];
                    Heightmap.Biome northWestBiome = _nodeBiomes[northWest];

                    // no loaded heightmap means no plant answer, so nothing gets drawn there
                    if (southWestBiome == Heightmap.Biome.None ||
                        southEastBiome == Heightmap.Biome.None ||
                        northEastBiome == Heightmap.Biome.None ||
                        northWestBiome == Heightmap.Biome.None)
                    {
                        continue;
                    }

                    if (southWestBiome == southEastBiome && southWestBiome == northEastBiome && southWestBiome == northWestBiome)
                    {
                        _meshBuilder.AddUniformQuad(southWest, _latticeSize, new Vector2(NodeX(column), NodeZ(row)), southWestBiome);
                        continue;
                    }

                    _cellOriginX = NodeX(column);
                    _cellOriginZ = NodeZ(row);
                    ProcessCell(0, 0, UnitsPerCell, southWestBiome, southEastBiome, northEastBiome, northWestBiome, 0);
                }
            }
        }

        private Vector2 CellPoint(int unitX, int unitZ) =>
            new Vector2(_cellOriginX + (unitX * UnitSize), _cellOriginZ + (unitZ * UnitSize));

        private Heightmap.Biome ClassifyCellPoint(int unitX, int unitZ) =>
            _sampler.Classify(CellPoint(unitX, unitZ));

        // three or more biomes in one cell only happens at junctions, so split down until each piece has two
        private void ProcessCell(
            int westUnit,
            int southUnit,
            int sizeUnits,
            Heightmap.Biome southWest,
            Heightmap.Biome southEast,
            Heightmap.Biome northEast,
            Heightmap.Biome northWest,
            int depth)
        {
            if (southWest == southEast && southWest == northEast && southWest == northWest)
            {
                _meshBuilder.AddFillQuad(CellPoint(westUnit, southUnit), CellPoint(westUnit + sizeUnits, southUnit + sizeUnits), southWest);
                return;
            }

            if (CountDistinct(southWest, southEast, northEast, northWest) == 2)
            {
                EmitTwoBiomeCell(westUnit, southUnit, sizeUnits, southWest, southEast, northEast, northWest);
                return;
            }

            // at 12.5cm the piece keeps its two most common biomes and folds the rest into the first,
            // so the border still runs through the junction instead of stopping short of it
            if (depth >= MaximumSubdivisionDepth)
            {
                (Heightmap.Biome primary, Heightmap.Biome secondary) = GetTwoMostCommonBiomes(southWest, southEast, northEast, northWest);
                EmitTwoBiomeCell(
                    westUnit,
                    southUnit,
                    sizeUnits,
                    FoldIntoPair(southWest, primary, secondary),
                    FoldIntoPair(southEast, primary, secondary),
                    FoldIntoPair(northEast, primary, secondary),
                    FoldIntoPair(northWest, primary, secondary));
                return;
            }

            int halfUnits = sizeUnits / 2;
            int centerUnitX = westUnit + halfUnits;
            int centerUnitZ = southUnit + halfUnits;
            int eastUnit = westUnit + sizeUnits;
            int northUnit = southUnit + sizeUnits;
            Heightmap.Biome south = ClassifyCellPoint(centerUnitX, southUnit);
            Heightmap.Biome east = ClassifyCellPoint(eastUnit, centerUnitZ);
            Heightmap.Biome north = ClassifyCellPoint(centerUnitX, northUnit);
            Heightmap.Biome west = ClassifyCellPoint(westUnit, centerUnitZ);
            Heightmap.Biome center = ClassifyCellPoint(centerUnitX, centerUnitZ);

            ProcessCell(westUnit, southUnit, halfUnits, southWest, south, center, west, depth + 1);
            ProcessCell(centerUnitX, southUnit, halfUnits, south, southEast, east, center, depth + 1);
            ProcessCell(centerUnitX, centerUnitZ, halfUnits, center, east, northEast, north, depth + 1);
            ProcessCell(westUnit, centerUnitZ, halfUnits, west, center, north, northWest, depth + 1);
        }

        private void EmitTwoBiomeCell(
            int westUnit,
            int southUnit,
            int sizeUnits,
            Heightmap.Biome southWest,
            Heightmap.Biome southEast,
            Heightmap.Biome northEast,
            Heightmap.Biome northWest)
        {
            int eastUnit = westUnit + sizeUnits;
            int northUnit = southUnit + sizeUnits;
            CellCorner southWestCorner = new CellCorner(westUnit, southUnit, southWest);
            CellCorner southEastCorner = new CellCorner(eastUnit, southUnit, southEast);
            CellCorner northEastCorner = new CellCorner(eastUnit, northUnit, northEast);
            CellCorner northWestCorner = new CellCorner(westUnit, northUnit, northWest);

            _ringCount = 0;
            AddRingCorner(southWestCorner);
            AddRingCrossing(southWestCorner, southEastCorner);
            AddRingCorner(southEastCorner);
            AddRingCrossing(southEastCorner, northEastCorner);
            AddRingCorner(northEastCorner);
            AddRingCrossing(northEastCorner, northWestCorner);
            AddRingCorner(northWestCorner);
            AddRingCrossing(northWestCorner, southWestCorner);

            int firstCrossing = -1;
            int secondCrossing = -1;
            int crossingCount = 0;
            for (int ringIndex = 0; ringIndex < _ringCount; ringIndex++)
            {
                if (!_ringIsCrossing[ringIndex])
                {
                    continue;
                }

                crossingCount++;
                if (firstCrossing < 0)
                {
                    firstCrossing = ringIndex;
                }
                else if (secondCrossing < 0)
                {
                    secondCrossing = ringIndex;
                }
            }

            if (crossingCount == 2)
            {
                EmitSplitCell(firstCrossing, secondCrossing);
                return;
            }

            Heightmap.Biome otherBiome = southEast != southWest ? southEast : northEast;
            EmitSaddleCell(westUnit + (sizeUnits / 2), southUnit + (sizeUnits / 2), southWest, otherBiome);
        }

        private void AddRingCorner(CellCorner corner)
        {
            _ringPoints[_ringCount] = CellPoint(corner.UnitX, corner.UnitZ);
            _ringBiomes[_ringCount] = corner.Biome;
            _ringIsCrossing[_ringCount] = false;
            _ringCount++;
        }

        private void AddRingCrossing(CellCorner firstCorner, CellCorner secondCorner)
        {
            if (firstCorner.Biome == secondCorner.Biome)
            {
                return;
            }

            _ringPoints[_ringCount] = FindCrossing(firstCorner, secondCorner);
            _ringBiomes[_ringCount] = Heightmap.Biome.None;
            _ringIsCrossing[_ringCount] = true;
            _ringCount++;
        }

        // always bisect from the lower end. both cells on a lattice edge, and sub cells of any size on the same line,
        // then probe the same points and get bit identical crossings, the border strip builder joins by exact endpoint
        // and folded junction pieces are the exception, their folded start biome can put the crossing somewhere else
        private Vector2 FindCrossing(CellCorner firstCorner, CellCorner secondCorner)
        {
            bool isFirstLower = firstCorner.UnitX + firstCorner.UnitZ < secondCorner.UnitX + secondCorner.UnitZ;
            CellCorner start = isFirstLower ? firstCorner : secondCorner;
            CellCorner end = isFirstLower ? secondCorner : firstCorner;
            int stepX = end.UnitX > start.UnitX ? 1 : 0;
            int stepZ = 1 - stepX;

            int low = 0;
            int high = (end.UnitX - start.UnitX) + (end.UnitZ - start.UnitZ);
            while (high - low > CrossingResolutionUnits)
            {
                int middle = (low + high) / 2;
                if (ClassifyCellPoint(start.UnitX + (stepX * middle), start.UnitZ + (stepZ * middle)) == start.Biome)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            int crossing = (low + high) / 2;
            return CellPoint(start.UnitX + (stepX * crossing), start.UnitZ + (stepZ * crossing));
        }

        // ring from the first crossing to the second holds one biome, the way back round holds the other
        private void EmitSplitCell(int firstCrossing, int secondCrossing)
        {
            Heightmap.Biome firstSideBiome = _ringBiomes[firstCrossing + 1];
            Heightmap.Biome secondSideBiome = _ringBiomes[(secondCrossing + 1) % _ringCount];

            EmitRingPolygon(firstCrossing, secondCrossing, firstSideBiome);
            EmitRingPolygon(secondCrossing, firstCrossing, secondSideBiome);
            _borderStrips.AddSegment(
                _ringPoints[firstCrossing],
                _ringPoints[secondCrossing],
                firstSideBiome,
                _ringPoints[firstCrossing + 1],
                secondSideBiome);
        }

        // diagonal corners match, so the cell center decides which pair connects through the middle
        private void EmitSaddleCell(int centerUnitX, int centerUnitZ, Heightmap.Biome firstBiome, Heightmap.Biome secondBiome)
        {
            Heightmap.Biome centerBiome = ClassifyCellPoint(centerUnitX, centerUnitZ);
            if (centerBiome != firstBiome && centerBiome != secondBiome)
            {
                centerBiome = firstBiome;
            }

            int polygonCount = 0;
            for (int ringIndex = 0; ringIndex < _ringCount; ringIndex++)
            {
                if (_ringIsCrossing[ringIndex] || _ringBiomes[ringIndex] == centerBiome)
                {
                    _polygonPoints[polygonCount] = _ringPoints[ringIndex];
                    polygonCount++;
                    continue;
                }

                Vector2 previousCrossing = _ringPoints[(ringIndex + _ringCount - 1) % _ringCount];
                Vector2 nextCrossing = _ringPoints[(ringIndex + 1) % _ringCount];
                Vector2 cornerPoint = _ringPoints[ringIndex];
                Heightmap.Biome cornerBiome = _ringBiomes[ringIndex];

                _meshBuilder.AddFillTriangle(previousCrossing, cornerPoint, nextCrossing, cornerBiome);
                _borderStrips.AddSegment(previousCrossing, nextCrossing, cornerBiome, cornerPoint, centerBiome);
            }

            _meshBuilder.AddFillPolygon(_polygonPoints, polygonCount, centerBiome);
        }

        private void EmitRingPolygon(int startIndex, int endIndex, Heightmap.Biome biome)
        {
            int pointCount = 0;
            int ringIndex = startIndex;
            while (true)
            {
                _polygonPoints[pointCount] = _ringPoints[ringIndex];
                pointCount++;
                if (ringIndex == endIndex)
                {
                    break;
                }

                ringIndex = (ringIndex + 1) % _ringCount;
            }

            _meshBuilder.AddFillPolygon(_polygonPoints, pointCount, biome);
        }

        private (Heightmap.Biome Primary, Heightmap.Biome Secondary) GetTwoMostCommonBiomes(
            Heightmap.Biome southWest,
            Heightmap.Biome southEast,
            Heightmap.Biome northEast,
            Heightmap.Biome northWest)
        {
            _cornerBiomes[0] = southWest;
            _cornerBiomes[1] = southEast;
            _cornerBiomes[2] = northEast;
            _cornerBiomes[3] = northWest;

            // corners are never None here, cells with an unloaded corner never get this far
            Heightmap.Biome primary = FindMostCommonCornerBiome(Heightmap.Biome.None);
            return (primary, FindMostCommonCornerBiome(primary));
        }

        // ties go to the corner that comes first counter clockwise from south west
        private Heightmap.Biome FindMostCommonCornerBiome(Heightmap.Biome excludedBiome)
        {
            Heightmap.Biome mostCommonBiome = Heightmap.Biome.None;
            int highestCount = 0;
            for (int cornerIndex = 0; cornerIndex < _cornerBiomes.Length; cornerIndex++)
            {
                Heightmap.Biome biome = _cornerBiomes[cornerIndex];
                if (biome == excludedBiome)
                {
                    continue;
                }

                int count = 0;
                for (int otherIndex = 0; otherIndex < _cornerBiomes.Length; otherIndex++)
                {
                    if (_cornerBiomes[otherIndex] == biome)
                    {
                        count++;
                    }
                }

                if (count > highestCount)
                {
                    mostCommonBiome = biome;
                    highestCount = count;
                }
            }

            return mostCommonBiome;
        }

        private static Heightmap.Biome FoldIntoPair(Heightmap.Biome biome, Heightmap.Biome primary, Heightmap.Biome secondary) =>
            biome == secondary ? secondary : primary;

        private static int CountDistinct(Heightmap.Biome first, Heightmap.Biome second, Heightmap.Biome third, Heightmap.Biome fourth)
        {
            int distinctCount = 1;
            if (second != first)
            {
                distinctCount++;
            }

            if (third != first && third != second)
            {
                distinctCount++;
            }

            if (fourth != first && fourth != second && fourth != third)
            {
                distinctCount++;
            }

            return distinctCount;
        }

        private record struct CellCorner(int UnitX, int UnitZ, Heightmap.Biome Biome);
    }
}
