using System.Collections;
using System.Collections.Generic;
using PurrNet.Prediction;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(LineRenderer))]
    public class WormRenderer : MonoBehaviour
    {
        [Header("Worm Visual Settings")]
        public Material wormMaterial;
        public GameObject wormMeshObject;
        public int tubeResolution = 8;
        public int smoothingSubdivisions = 3;
        public int capSubdivisions = 4;
        public AnimationCurve radiusCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.8f);
        public Mesh wormMesh;

        [Header("Smoothing Settings")]
        [Range(0f, 1f)]
        public float smoothingStrength = 0.5f;
        public bool useAdaptiveSubdivision = true;
        [Range(0.01f, 0.5f)]
        public float minSegmentDistance = 0.05f;

        private LineRenderer lineRenderer;
        public LineRenderer LineRenderer => lineRenderer;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private readonly List<Vector3> visualPositions = new List<Vector3>();
        private bool warnedMissingGraphics;

        private global::Player.Player player;

        void Start()
        {
            player = GetComponent<global::Player.Player>();
            SetupWormRenderer();
            StartCoroutine(DelayedInitialUpdate());
        }

        public void Restart() => Start();

        IEnumerator DelayedInitialUpdate()
        {
            yield return new WaitForEndOfFrame();
            GenerateTubeMesh();
        }

        void SetupWormRenderer()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.material = wormMaterial;
            lineRenderer.startWidth = GameParameters.WormBodyWidth;
            lineRenderer.endWidth = GameParameters.WormBodyWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.enabled = false;
            
            if (meshFilter != null && meshRenderer != null && wormMesh != null)
                return;

            
            if (wormMeshObject == null)
            {
                Transform existing = transform.Find("WormMesh");
                if (existing != null)
                {
                    wormMeshObject = existing.gameObject;
                }
                else
                {
                    wormMeshObject = new GameObject("WormMesh");
                    wormMeshObject.transform.SetParent(transform, false);
                }
            }

            
            if (!wormMeshObject.TryGetComponent(out meshFilter))
                meshFilter = wormMeshObject.AddComponent<MeshFilter>();

            if (!wormMeshObject.TryGetComponent(out meshRenderer))
                meshRenderer = wormMeshObject.AddComponent<MeshRenderer>();

            
            if (wormMesh == null)
                wormMesh = meshFilter.sharedMesh;

            if (wormMesh == null)
                wormMesh = new Mesh { name = "WormTube" };

            wormMesh.MarkDynamic();
            meshFilter.sharedMesh = wormMesh;
            
            if (wormMaterial != null)
                meshRenderer.sharedMaterial = wormMaterial;
        }

        void LateUpdate() => GenerateTubeMesh();

        void GenerateTubeMesh()
        {
            if (meshFilter == null) return;
            
            var vertices  = new List<Vector3>();
            var uvs       = new List<Vector2>();
            var triangles = new List<int>();

            var originalPositions = GetVisualPositions();
    
            if (originalPositions.Count < 2) return;

            List<Vector3> positions  = GenerateSmoothCurve(originalPositions);
            List<Vector3> directions = CalculateSmoothedDirections(positions);

            // Build per-ring V values based on world-space arc length so the texture
            // stretches smoothly as the worm moves rather than snapping each frame.
            float[] arcV = ComputeArcLengthV(positions);

            // Head cap writes rings that the tube body stitches onto.
            GenerateHeadCap(positions, directions, vertices, uvs, triangles, arcV);
            // Tube body stitches from the last ring written by the head cap.
            GenerateTubeBody(positions, directions, vertices, uvs, triangles, arcV);
            // Tail cap stitches from the last ring written by the tube body.
            GenerateTailCap(positions, directions, vertices, uvs, triangles, arcV);

            // Vertices were built in world space; the mesh object has its own transform,
            // so convert to its local space. This handles position, rotation, scale and parents.
            Transform meshT = meshFilter.transform;
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = meshT.InverseTransformPoint(vertices[i]);

            wormMesh.Clear();
            wormMesh.vertices  = vertices.ToArray();
            wormMesh.uv        = uvs.ToArray();
            wormMesh.triangles = triangles.ToArray();
            wormMesh.RecalculateNormals();
            wormMesh.RecalculateBounds();
        }

        // Returns a normalised V [0,1] for each curve position based on cumulative
        // world-space distance, so equal-length segments always get equal UV space.
        float[] ComputeArcLengthV(List<Vector3> positions)
        {
            float[] dist = new float[positions.Count];
            for (int i = 1; i < positions.Count; i++)
                dist[i] = dist[i - 1] + Vector3.Distance(positions[i], positions[i - 1]);

            float total = dist[positions.Count - 1];
            float[] v   = new float[positions.Count];
            for (int i = 0; i < positions.Count; i++)
                v[i] = total > 0f ? dist[i] / total : 0f;

            return v;
        }

        // ─── Curve generation ────────────────────────────────────────────────

        List<Vector3> GenerateSmoothCurve(List<Vector3> original)
        {
            var result = new List<Vector3>();

            for (int i = 0; i < original.Count - 1; i++)
            {
                Vector3 p0 = i == 0                  ? original[0]                  : original[i - 1];
                Vector3 p1 = original[i];
                Vector3 p2 = original[i + 1];
                Vector3 p3 = i == original.Count - 2 ? original[original.Count - 1] : original[i + 2];

                int pointsPerSegment = useAdaptiveSubdivision
                    ? Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / minSegmentDistance))
                    : smoothingSubdivisions + 1;

                for (int sub = 0; sub < pointsPerSegment; sub++)
                {
                    float t = (float)sub / pointsPerSegment;
                    Vector3 spline = CatmullRomSpline(p0, p1, p2, p3, t);

                    if (smoothingStrength > 0f && i > 0 && sub > 0)
                        spline = Vector3.Lerp(Vector3.Lerp(p1, p2, t), spline, smoothingStrength);

                    result.Add(spline);
                }
            }

            result.Add(original[original.Count - 1]);
            return result;
        }

        List<Vector3> CalculateSmoothedDirections(List<Vector3> positions)
        {
            
            if (positions.Count < 2)
                return new List<Vector3> { Vector3.forward };
            
            var directions = new List<Vector3>();

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 dir;

                if (i == 0)
                {
                    dir = (positions[1] - positions[0]).normalized;
                }
                else if (i == positions.Count - 1)
                {
                    dir = (positions[i] - positions[i - 1]).normalized;
                }
                else
                {
                    Vector3 backward = (positions[i]     - positions[i - 1]).normalized;
                    Vector3 forward  = (positions[i + 1] - positions[i]).normalized;
                    float backDist   = Vector3.Distance(positions[i], positions[i - 1]);
                    float fwdDist    = Vector3.Distance(positions[i + 1], positions[i]);
                    float total      = backDist + fwdDist;

                    dir = (total > 0.001f)
                        ? ((backward * backDist + forward * fwdDist) / total).normalized
                        : ((backward + forward) * 0.5f).normalized;
                }

                directions.Add(dir);
            }

            return directions;
        }

        // ─── Mesh building ────────────────────────────────────────────────────

        // Each ring has (tubeResolution + 1) vertices: j=0..tubeResolution-1 are the real
        // vertices, and j=tubeResolution duplicates j=0 at u=1.0 so the texture seam has a
        // proper start and end rather than a hard u=1→0 jump on a shared vertex.
        int RingStride => tubeResolution + 1;

        void AddRing(List<Vector3> vertices, List<Vector2> uvs,
            Vector3 center, Vector3 right, Vector3 up, float radius, float v)
        {
            for (int j = 0; j <= tubeResolution; j++)
            {
                float a = (float)j / tubeResolution * 2f * Mathf.PI;

                // Mirror U: goes 0→1 for the first half, 1→0 for the second half.
                // Tiles the texture twice around the circumference (once per side)
                // with seams at top and bottom rather than a single seam on one side.
                float u = 1f - Mathf.Abs(1f - (float)j / (tubeResolution / 2));

                vertices.Add(center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
                uvs.Add(new Vector2(u, v));
            }
        }

        // Head cap: iterates from the tip inward (small radius → full radius).
        // The last ring it writes sits at positions[0], which GenerateTubeBody stitches onto.
        void GenerateHeadCap(List<Vector3> positions, List<Vector3> directions,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float[] arcV)
        {
            Vector3 center     = positions[0];
            Vector3 headDir    = directions[0];
            float   baseRadius = GameParameters.WormBodyWidth * radiusCurve.Evaluate(0f);
            Vector3 right      = GetStableRight(headDir);
            Vector3 up         = Vector3.Cross(right, headDir).normalized;

            // Cap arc length: the hemisphere has the same radius as the tube start,
            // so its surface length is (PI/2 * radius). Normalise against total length.
            float totalArcLength = TotalArcLength(positions);
            float capArcLength   = (Mathf.PI * 0.5f * baseRadius);
            float capVSpan       = totalArcLength > 0f ? capArcLength / (totalArcLength + capArcLength * 2f) : 0f;

            int prevRingStart = -1;

            for (int i = 0; i <= capSubdivisions; i++)
            {
                float t      = (float)i / capSubdivisions;
                float angle  = t * Mathf.PI * 0.5f;
                float radius = baseRadius * Mathf.Sin(angle);
                Vector3 offset = headDir * (-Mathf.Cos(angle) * baseRadius);

                // V runs from 0 at the tip to capVSpan at the tube junction.
                float v = Mathf.Lerp(0f, capVSpan, t);

                int ringStart = vertices.Count;
                AddRing(vertices, uvs, center + offset, right, up, radius, v);

                if (i > 0)
                    AddRingTriangles(prevRingStart, ringStart, triangles);

                prevRingStart = ringStart;
            }
        }

        // Tail cap: reads the last tube ring and iterates outward (full radius → tip).
        void GenerateTailCap(List<Vector3> positions, List<Vector3> directions,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float[] arcV)
        {
            int     last       = positions.Count - 1;
            Vector3 center     = positions[last];
            Vector3 tailDir    = directions[last];
            float   baseRadius = GameParameters.WormBodyWidth * radiusCurve.Evaluate(1f);
            Vector3 right      = GetStableRight(tailDir);
            Vector3 up         = Vector3.Cross(right, tailDir).normalized;

            float totalArcLength = TotalArcLength(positions);
            float capArcLength   = (Mathf.PI * 0.5f * baseRadius);
            float capVSpan       = totalArcLength > 0f ? capArcLength / (totalArcLength + capArcLength * 2f) : 0f;

            int prevRingStart = vertices.Count - RingStride;

            for (int i = capSubdivisions - 1; i >= 0; i--)
            {
                float t      = (float)i / capSubdivisions;
                float angle  = t * Mathf.PI * 0.5f;
                float radius = baseRadius * Mathf.Sin(angle);
                Vector3 offset = tailDir * (Mathf.Cos(angle) * baseRadius);

                // V runs from (1 - capVSpan) at the tube junction to 1 at the tip.
                float v = Mathf.Lerp(1f, 1f - capVSpan, t);

                int ringStart = vertices.Count;
                AddRing(vertices, uvs, center + offset, right, up, radius, v);

                AddRingTriangles(prevRingStart, ringStart, triangles);
                prevRingStart = ringStart;
            }
        }

        void GenerateTubeBody(List<Vector3> positions, List<Vector3> directions,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float[] arcV)
        {
            Vector3 prevRight = GetStableRight(directions[0]);

            float totalArcLength = TotalArcLength(positions);
            float capArcLength   = (Mathf.PI * 0.5f * GameParameters.WormBodyWidth);
            float capVSpan       = totalArcLength > 0f ? capArcLength / (totalArcLength + capArcLength * 2f) : 0f;

            for (int i = 0; i < positions.Count; i++)
            {
                float radius = GameParameters.WormBodyWidth * radiusCurve.Evaluate(arcV[i]);

                Vector3 right = GetStableRight(directions[i], prevRight);
                Vector3 up    = Vector3.Cross(right, directions[i]).normalized;
                prevRight     = right;

                // Remap arc V into the middle region [capVSpan, 1-capVSpan].
                float v = Mathf.Lerp(capVSpan, 1f - capVSpan, arcV[i]);

                int baseIndex = vertices.Count;
                AddRing(vertices, uvs, positions[i], right, up, radius, v);

                if (i > 0)
                    AddRingTriangles(baseIndex - RingStride, baseIndex, triangles);
            }
        }

        // RingStride replaces the old `resolution` param — stride is always tubeResolution+1.
        // No modulo needed: the seam vertex at j=tubeResolution connects to j=tubeResolution
        // on the adjacent ring, both sharing u=1, so wrapping is handled by UVs not topology.
        void AddRingTriangles(int prevRing, int currentRing, List<int> triangles)
        {
            for (int j = 0; j < tubeResolution; j++)
            {
                int next = j + 1;   // no modulo — seam vertex is the explicit +1 duplicate

                triangles.Add(prevRing    + j);
                triangles.Add(currentRing + j);
                triangles.Add(prevRing    + next);

                triangles.Add(prevRing    + next);
                triangles.Add(currentRing + j);
                triangles.Add(currentRing + next);
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        float TotalArcLength(List<Vector3> positions)
        {
            float len = 0f;
            for (int i = 1; i < positions.Count; i++)
                len += Vector3.Distance(positions[i], positions[i - 1]);
            return len;
        }

        Vector3 GetStableRight(Vector3 forward, Vector3 previousRight = default)
        {
            if (previousRight == default || Vector3.Dot(forward, previousRight) > 0.99f)
            {
                Vector3 upGuide = Mathf.Abs(forward.y) > 0.99f ? Vector3.forward : Vector3.up;
                return Vector3.Cross(forward, upGuide).normalized;
            }

            return (previousRight - forward * Vector3.Dot(forward, previousRight)).normalized;
        }

        Vector3 CatmullRomSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        // ─── Line renderer (debug toggle) ─────────────────────────────────────

        public void SetMaterial(Material newMaterial)
        {
            if (newMaterial == null) return;

            wormMaterial = newMaterial;

            if (meshRenderer != null)
                meshRenderer.material = newMaterial;

            if (lineRenderer != null)
                lineRenderer.material = newMaterial;
        }

        public void DisableRendering()
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            if (lineRenderer != null) lineRenderer.enabled = false;
        }
        
        public void EnableRendering()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
            UpdateLineRenderer();
        }

        void UpdateLineRenderer()
        {
            var positions = GetVisualPositions();
            lineRenderer.positionCount = positions.Count;
            lineRenderer.SetPositions(positions.ToArray());
        }
        
        private Transform VisualOf(Transform physical)
        {
            if (physical == null) return null;

            var pt = physical.GetComponent<PredictedTransform>();
            if (pt != null && pt.graphics != null) return pt.graphics;

            if (!warnedMissingGraphics)
            {
                warnedMissingGraphics = true;
                Debug.LogWarning($"WormRenderer: no PredictedTransform graphics on {physical.name}, using the stepped physics transform", physical);
            }
            return physical;
        }

        private List<Vector3> GetVisualPositions()
        {
            visualPositions.Clear();
            visualPositions.Add(VisualOf(player.wormHead).position);

            var segments = player.wormBodySegments.list;
            for (int i = 0; i < segments.Count; i++)
                visualPositions.Add(VisualOf(segments[i]).position);

            return visualPositions;
        }
    }
}