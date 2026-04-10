using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 공격 예고 인디케이터 컴포넌트.
    ///
    /// [방향 기준 통일]
    ///   모든 메시는 x축 기준 각도(Atan2 / Cos·Sin 순서)를 사용한다.
    ///   forward = (Cos(rad), Sin(rad)) — 부채꼴·사각형 동일.
    ///   MeleeAttackAction 판정도 동일한 기준을 사용한다.
    /// </summary>
    public class AttackIndicator : MonoBehaviour
    {
        private class IndicatorSet
        {
            public GameObject root;
            public MeshFilter fillFilter;
            public MeshFilter outlineFilter;
            public MeshFilter scanFilter;
        }

        private const int   SECTOR_SEGMENTS   = 32;
        private const float OUTLINE_THICKNESS = 0.06f;
        private const float FILL_ALPHA        = 0.18f;
        private const float OUTLINE_ALPHA     = 0.85f;
        private static readonly Color BASE_COLOR = new Color(0.88f, 0.29f, 0.29f);

        private List<AttackIndicatorEntry> _entries;
        private List<IndicatorSet>         _sets;
        private bool                       _isPlaying;
        private float                      _windupDuration;
        private float                      _windupTimer;
        private bool                       _lockDirection;
        private Func<Vector2>              _getForward;
        private Vector2                    _lockedForward;

        public void Play(
            List<AttackIndicatorEntry> entries,
            float                      duration,
            bool                       lockDirection,
            Func<Vector2>              getForward)
        {
            Stop();
            if (entries == null || entries.Count == 0) return;

            _entries        = entries;
            _windupDuration = duration;
            _windupTimer    = 0f;
            _lockDirection  = lockDirection;
            _getForward     = getForward;
            _lockedForward  = getForward?.Invoke() ?? Vector2.up;
            _isPlaying      = true;

            _sets = new List<IndicatorSet>();
            foreach (var entry in entries)
                _sets.Add(CreateIndicatorSet());

            UpdateMeshes(0f);
        }

        public void Stop()
        {
            _isPlaying = false;
            if (_sets == null) return;
            foreach (var set in _sets)
                if (set?.root != null) Destroy(set.root);
            _sets = null;
        }

        private void Update()
        {
            if (!_isPlaying || _sets == null) return;
            _windupTimer += Time.deltaTime;
            UpdateMeshes(Mathf.Clamp01(_windupTimer / _windupDuration));
        }

        private void UpdateMeshes(float t)
        {
            Vector2 baseForward = _lockDirection
                ? _lockedForward
                : (_getForward?.Invoke() ?? _lockedForward);

            // x축 기준 각도 (Atan2 = (y, x) 순서)
            float baseAngleDeg = Mathf.Atan2(baseForward.y, baseForward.x) * Mathf.Rad2Deg;

            for (int i = 0; i < _sets.Count; i++)
            {
                var entry        = _entries[i];
                var set          = _sets[i];
                // angleOffset 적용 (x축 기준 각도에 더하기)
                float angleDeg   = baseAngleDeg + entry.angleOffset;

                switch (entry.shape)
                {
                    case AttackIndicatorShape.Sector:
                        BuildSectorMeshes(set, entry.sectorData, angleDeg, t);
                        break;
                    case AttackIndicatorShape.Rectangle:
                        BuildRectMeshes(set, entry.rectData, angleDeg, t);
                        break;
                }
            }
        }

        // ── 부채꼴 ────────────────────────────────────────────────

        private void BuildSectorMeshes(IndicatorSet set, SectorIndicatorData data, float angleDeg, float t)
        {
            float halfAngle  = data.angle * 0.5f;
            float scanRadius = data.range * t;

            set.fillFilter.mesh    = BuildSectorFillMesh(data.range, halfAngle, angleDeg);
            set.outlineFilter.mesh = BuildSectorOutlineMesh(data.range, halfAngle, angleDeg);
            set.scanFilter.mesh    = scanRadius < 0.01f
                ? new Mesh()
                : BuildArcMesh(scanRadius, halfAngle, angleDeg);
        }

        private Mesh BuildSectorFillMesh(float radius, float halfAngleDeg, float angleDeg)
        {
            var mesh  = new Mesh();
            var verts = new Vector3[SECTOR_SEGMENTS + 2];
            var tris  = new int[SECTOR_SEGMENTS * 3];
            verts[0]  = Vector3.zero;

            for (int i = 0; i <= SECTOR_SEGMENTS; i++)
            {
                float a   = (angleDeg - halfAngleDeg + (halfAngleDeg * 2f / SECTOR_SEGMENTS) * i) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            for (int i = 0; i < SECTOR_SEGMENTS; i++)
            {
                tris[i * 3]     = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private Mesh BuildSectorOutlineMesh(float radius, float halfAngleDeg, float angleDeg)
        {
            var mesh  = new Mesh();
            var verts = new List<Vector3>();
            var tris  = new List<int>();

            AddArcStrip(verts, tris, radius, halfAngleDeg, angleDeg);

            float aL = (angleDeg - halfAngleDeg) * Mathf.Deg2Rad;
            float aR = (angleDeg + halfAngleDeg) * Mathf.Deg2Rad;
            AddLineStrip(verts, tris, Vector3.zero,
                new Vector3(Mathf.Cos(aL) * radius, Mathf.Sin(aL) * radius, 0f));
            AddLineStrip(verts, tris, Vector3.zero,
                new Vector3(Mathf.Cos(aR) * radius, Mathf.Sin(aR) * radius, 0f));

            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        private Mesh BuildArcMesh(float radius, float halfAngleDeg, float angleDeg)
        {
            var mesh  = new Mesh();
            var verts = new List<Vector3>();
            var tris  = new List<int>();
            AddArcStrip(verts, tris, radius, halfAngleDeg, angleDeg);
            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        // ── 사각형 ────────────────────────────────────────────────
        // forward = (Cos(rad), Sin(rad)) — 부채꼴과 동일한 x축 기준

        private void BuildRectMeshes(IndicatorSet set, RectIndicatorData data, float angleDeg, float t)
        {
            float scanDist = data.range * t;

            set.fillFilter.mesh    = BuildRectFillMesh(data.range, data.width, angleDeg);
            set.outlineFilter.mesh = BuildRectOutlineMesh(data.range, data.width, angleDeg);
            set.scanFilter.mesh    = BuildRectScanMesh(scanDist, data.width, angleDeg);
        }

        private Vector3[] GetRectCorners(float range, float width, float angleDeg)
        {
            float rad     = angleDeg * Mathf.Deg2Rad;
            // x축 기준: forward = (Cos, Sin), right = (−Sin, Cos) 의 수직
            var   forward = new Vector3( Mathf.Cos(rad),  Mathf.Sin(rad), 0f);
            var   right   = new Vector3(-Mathf.Sin(rad),  Mathf.Cos(rad), 0f);
            float hw      = width * 0.5f;
            return new[]
            {
                (Vector3)Vector3.zero - right * hw,
                (Vector3)Vector3.zero + right * hw,
                forward * range + right * hw,
                forward * range - right * hw,
            };
        }

        private Mesh BuildRectFillMesh(float range, float width, float angleDeg)
        {
            var c    = GetRectCorners(range, width, angleDeg);
            var mesh = new Mesh();
            mesh.vertices  = new[] { c[0], c[1], c[2], c[3] };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private Mesh BuildRectOutlineMesh(float range, float width, float angleDeg)
        {
            var mesh  = new Mesh();
            var verts = new List<Vector3>();
            var tris  = new List<int>();
            var c     = GetRectCorners(range, width, angleDeg);
            AddLineStrip(verts, tris, c[0], c[1]);
            AddLineStrip(verts, tris, c[1], c[2]);
            AddLineStrip(verts, tris, c[2], c[3]);
            AddLineStrip(verts, tris, c[3], c[0]);
            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        private Mesh BuildRectScanMesh(float dist, float width, float angleDeg)
        {
            if (dist < 0.01f) return new Mesh();
            float rad     = angleDeg * Mathf.Deg2Rad;
            // x축 기준: forward = (Cos, Sin)
            var   forward = new Vector3( Mathf.Cos(rad),  Mathf.Sin(rad), 0f);
            var   right   = new Vector3(-Mathf.Sin(rad),  Mathf.Cos(rad), 0f);
            var   center  = forward * dist;
            var   p0      = center - right * (width * 0.5f);
            var   p1      = center + right * (width * 0.5f);
            var   mesh    = new Mesh();
            var   verts   = new List<Vector3>();
            var   tris    = new List<int>();
            AddLineStrip(verts, tris, p0, p1);
            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        // ── 헬퍼 ──────────────────────────────────────────────────

        private void AddArcStrip(List<Vector3> verts, List<int> tris,
                                  float radius, float halfAngleDeg, float angleDeg)
        {
            int baseIdx = verts.Count;
            for (int i = 0; i <= SECTOR_SEGMENTS; i++)
            {
                float a   = (angleDeg - halfAngleDeg + (halfAngleDeg * 2f / SECTOR_SEGMENTS) * i) * Mathf.Deg2Rad;
                var   dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(dir * (radius - OUTLINE_THICKNESS * 0.5f));
                verts.Add(dir * (radius + OUTLINE_THICKNESS * 0.5f));
            }
            for (int i = 0; i < SECTOR_SEGMENTS; i++)
            {
                int b = baseIdx + i * 2;
                tris.AddRange(new[] { b, b+1, b+2, b+1, b+3, b+2 });
            }
        }

        private void AddLineStrip(List<Vector3> verts, List<int> tris, Vector3 from, Vector3 to)
        {
            var   dir  = (to - from).normalized;
            var   perp = new Vector3(-dir.y, dir.x, 0f) * (OUTLINE_THICKNESS * 0.5f);
            int   b    = verts.Count;
            verts.Add(from - perp);
            verts.Add(from + perp);
            verts.Add(to   + perp);
            verts.Add(to   - perp);
            tris.AddRange(new[] { b, b+1, b+2, b, b+2, b+3 });
        }

        // ── IndicatorSet 생성 ─────────────────────────────────────

        private IndicatorSet CreateIndicatorSet()
        {
            var set  = new IndicatorSet();
            set.root = new GameObject("AttackIndicator");
            set.root.transform.SetParent(transform, false);
            set.root.transform.localPosition = Vector3.zero;

            // 부모 스케일(bossScale 등) 상속 취소 → 메시 좌표가 월드 단위와 일치하도록
            float invScale = transform.lossyScale.x > 0f ? 1f / transform.lossyScale.x : 1f;
            set.root.transform.localScale = new Vector3(invScale, invScale, 1f);

            set.fillFilter    = CreateMeshChild(set.root, "Fill",    -1,
                new Color(BASE_COLOR.r, BASE_COLOR.g, BASE_COLOR.b, FILL_ALPHA));
            set.outlineFilter = CreateMeshChild(set.root, "Outline",  0,
                new Color(BASE_COLOR.r, BASE_COLOR.g, BASE_COLOR.b, OUTLINE_ALPHA));
            set.scanFilter    = CreateMeshChild(set.root, "Scan",     1,
                new Color(1f, 0.9f, 0.5f, 1f));

            return set;
        }

        private MeshFilter CreateMeshChild(GameObject parent, string childName, int sortingOrder, Color color)
        {
            var go  = new GameObject(childName);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = Vector3.zero;
            var mf  = go.AddComponent<MeshFilter>();
            var mr  = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color       = color;
            mr.material     = mat;
            mr.sortingOrder = sortingOrder;
            return mf;
        }
    }
}
