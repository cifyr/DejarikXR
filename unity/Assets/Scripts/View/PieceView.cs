using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Visual + animation controller for one game piece. Lives under the board root in board-local units.
    // Hierarchy: this (faces + scaled by PieceScale) -> inner (lunge/settle offsets) -> creature holder.
    public class PieceView : MonoBehaviour
    {
        const float RotSpeedDeg = 400f;     // deg/s a piece turns to a new facing
        const float WalkSpeed = 1.6f;       // board units/s
        const float LungeMs = 280f;

        public string PieceId { get; private set; }
        public PieceType Type { get; private set; }
        public Player Owner { get; private set; }
        public int Space { get; private set; }
        public bool IsWalking { get; private set; }

        [SerializeField] float facingOffsetDeg = 0f; // flip 180 on-device if models face outward

        CreatureInstance _c;
        Transform _inner;
        readonly Queue<Vector3> _path = new Queue<Vector3>();
        Vector3? _segTarget;
        float _desiredYaw;
        bool _turning;
        float _lungeT0 = -1f, _lungeDist;
        bool _selected;
        string _current;

        public async System.Threading.Tasks.Task Init(GamePiece p)
        {
            PieceId = p.Id; Type = p.Type; Owner = p.Owner; Space = p.Space;
            _inner = new GameObject("inner").transform;
            _inner.SetParent(transform, false);
            _c = await CreatureFactory.Load(p.Type, p.Owner, _inner);
            transform.localScale = Vector3.one * BoardLayout.PieceScale;
            transform.localPosition = BoardLayout.Pos3D(p.Space);
            _desiredYaw = YawToCenter(p.Space);
            transform.localRotation = Quaternion.Euler(0f, _desiredYaw, 0f);
            PlayLoop(_c.Idle, 0.35f);
            StartCoroutine(Ground());
        }

        // CreatureFactory grounds the bind pose, but the idle animation shifts the feet, so a single frame is
        // unreliable: a swung limb/staff reads as the lowest point and shoves the whole creature off the board.
        // Sample the bounds' min.y over ~1s and ground to the 25th percentile — the height the feet sit at
        // when planted, ignoring transient deep dips (a low-reaching limb) and transient lifts (a bob).
        IEnumerator Ground()
        {
            if (_c == null) yield break;
            var rs = _c.Holder.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) yield break;
            float planeY = transform.position.y;   // where the feet should rest (PieceView origin = cell top)
            var samples = new List<float>(64);
            float t0 = Time.time;
            while (Time.time - t0 < 1.0f)
            {
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                samples.Add(b.min.y);
                yield return null;
            }
            if (samples.Count == 0) yield break;
            samples.Sort();
            float planted = samples[Mathf.Clamp(samples.Count / 4, 0, samples.Count - 1)]; // 25th percentile
            float scaleY = _inner.lossyScale.y;
            if (Mathf.Abs(scaleY) < 1e-5f) yield break;
            var lp = _c.Holder.transform.localPosition;
            float endY = lp.y + (planeY - planted) / scaleY;
            float lt = 0f;
            while (lt < 0.25f) // ease to avoid a pop
            {
                lt += Time.deltaTime;
                lp = _c.Holder.transform.localPosition;
                lp.y = Mathf.Lerp(lp.y, endY, lt / 0.25f);
                _c.Holder.transform.localPosition = lp;
                yield return null;
            }
        }

        public void SnapTo(int space)
        {
            Space = space;
            transform.localPosition = BoardLayout.Pos3D(space);
            _desiredYaw = YawToCenter(space);
            transform.localRotation = Quaternion.Euler(0f, _desiredYaw, 0f);
        }

        // Walk through the board path (board spaces, inclusive of start) to its final cell.
        public void WalkAlong(int[] path, int finalSpace)
        {
            Space = finalSpace;
            _path.Clear();
            if (path != null && path.Length >= 2)
                for (int i = 1; i < path.Length; i++) _path.Enqueue(BoardLayout.Pos3D(path[i]));
            else
                _path.Enqueue(BoardLayout.Pos3D(finalSpace));
            IsWalking = true;
        }

        public void FaceSpace(int space) => _desiredYaw = YawToward(transform.localPosition, BoardLayout.Pos3D(space));
        public void FaceCenter() => _desiredYaw = YawToCenter(Space);

        public void PlayIdle() => PlayLoop(_c.Idle, 0.3f);
        public void PlayVictory() => PlayLoop(string.IsNullOrEmpty(_c.Victory) ? _c.Idle : _c.Victory, 0.3f);

        public void PlayAttack(bool finisher)
        {
            string clip = finisher && !string.IsNullOrEmpty(_c.Finish) ? _c.Finish : _c.Attack;
            PlayOnceThenIdle(clip, 0.08f);
            Lunge(0.12f);
        }

        public void PlayHit()
        {
            PlayOnceThenIdle(_c.Hit, 0.12f);
            Lunge(-0.14f);
        }

        public void SetSelected(bool v) => _selected = v;

        // Play the death clip, dissolve like a failing hologram, then destroy. Returns the dissolve duration.
        public void PlayDeathAndDissolve(PieceType? byType, float durationMs)
        {
            string clip = _c.DeathBy(byType);
            PlayOnce(clip, 0.15f);
            StartCoroutine(Dissolve(durationMs / 1000f));
        }

        void Lunge(float dist) { _lungeT0 = Time.time; _lungeDist = dist; }

        void Update()
        {
            if (_c == null) return;
            LockRoot();

            // Next path segment: face it, then walk.
            if (!_turning && _segTarget == null && _path.Count > 0)
            {
                _segTarget = _path.Peek();
                _desiredYaw = YawToward(transform.localPosition, _segTarget.Value);
                _turning = true;
            }

            // Rotate toward desired yaw.
            var target = Quaternion.Euler(0f, _desiredYaw, 0f);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, RotSpeedDeg * Time.deltaTime);

            if (_turning && Quaternion.Angle(transform.localRotation, target) < 7f)
            {
                _turning = false;
                PlayLoop(_c.Walk, 0.15f);
            }

            if (!_turning && _segTarget != null)
            {
                Vector3 cur = transform.localPosition;
                Vector3 to = _segTarget.Value;
                float step = WalkSpeed * Time.deltaTime;
                float d = Vector3.Distance(cur, to);
                if (d <= step || d < 0.02f)
                {
                    transform.localPosition = to;
                    _path.Dequeue();
                    _segTarget = null;
                    if (_path.Count == 0)
                    {
                        IsWalking = false;
                        _desiredYaw = YawToCenter(Space);
                        PlayLoop(_c.Idle, 0.25f);
                    }
                }
                else
                {
                    transform.localPosition = Vector3.MoveTowards(cur, to, step);
                }
            }

            // Lunge offset on the inner transform (forward in local +Z).
            float offZ = 0f;
            if (_lungeT0 >= 0f)
            {
                float e = (Time.time - _lungeT0) * 1000f;
                if (e >= LungeMs) _lungeT0 = -1f;
                else offZ = _lungeDist * Mathf.Sin(e / LungeMs * Mathf.PI);
            }
            if (_inner != null) _inner.localPosition = new Vector3(0f, 0f, offZ);

            // Selection glow pulse + gentle scale (drives the hologram shader's _Glow).
            float pulse = _selected ? 0.6f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 0f;
            foreach (var m in _c.Materials) m.SetFloat("_Glow", 1f + pulse);
            float scaleMul = _selected ? 1f + 0.03f + 0.015f * Mathf.Sin(Time.time * 6f) : 1f;
            transform.localScale = Vector3.one * (BoardLayout.PieceScale * scaleMul);
        }

        void LockRoot()
        {
            if (_c.RootBone == null) return;
            _c.RootBone.localPosition = _c.RootBaseLocalPos;
            _c.RootBone.localRotation = _c.RootBaseLocalRot;
        }

        void PlayLoop(string clip, float fade)
        {
            if (string.IsNullOrEmpty(clip) || _c.Anim == null) return;
            var st = _c.Anim[clip];
            if (st != null) st.wrapMode = WrapMode.Loop;
            if (_current == clip && _c.Anim.IsPlaying(clip)) return;
            _c.Anim.CrossFade(clip, fade);
            _current = clip;
        }

        void PlayOnce(string clip, float fade)
        {
            if (string.IsNullOrEmpty(clip) || _c.Anim == null) return;
            var st = _c.Anim[clip];
            if (st != null) st.wrapMode = WrapMode.Once;
            _c.Anim.CrossFade(clip, fade);
            _current = clip;
        }

        void PlayOnceThenIdle(string clip, float fade)
        {
            if (string.IsNullOrEmpty(clip) || _c.Anim == null) return;
            PlayOnce(clip, fade);
            float len = _c.Anim[clip] != null ? _c.Anim[clip].length : 0.6f;
            StartCoroutine(BackToIdle(len));
        }

        IEnumerator BackToIdle(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayLoop(_c.Idle, 0.3f);
        }

        IEnumerator Dissolve(float dur)
        {
            float t0 = Time.time;
            while (Time.time - t0 < dur)
            {
                float t = (Time.time - t0) / dur;
                float amp = 0.04f * t;
                if (_inner != null)
                    _inner.localPosition = new Vector3((Random.value - 0.5f) * amp, (Random.value - 0.5f) * amp * 1.5f, (Random.value - 0.5f) * amp);
                float flicker = Random.value > 0.35f ? 1f : 0.25f;
                float opacity = (1f - t) * flicker;
                foreach (var m in _c.Materials)
                {
                    m.SetFloat("_Alpha", opacity);
                    m.SetFloat("_Glow", 1f + 2.5f * t);
                }
                transform.localScale = Vector3.one * (BoardLayout.PieceScale * (1f - 0.25f * t));
                yield return null;
            }
            Destroy(gameObject);
        }

        // Corrects for the imported models' intrinsic forward axis. On-device, 180 faced them away from
        // center, so 0 is correct: models' +Z is their front, facing the board center at rest (like the web).
        public static float FacingOffsetDeg = 0f;

        static float YawToCenter(int space)
        {
            if (space == Board.Center) return FacingOffsetDeg;
            Vector3 p = BoardLayout.Pos3D(space);
            return Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg + FacingOffsetDeg;
        }

        static float YawToward(Vector3 fromLocal, Vector3 toLocal)
        {
            float dx = toLocal.x - fromLocal.x, dz = toLocal.z - fromLocal.z;
            if (Mathf.Abs(dx) < 1e-5f && Mathf.Abs(dz) < 1e-5f) return FacingOffsetDeg;
            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg + FacingOffsetDeg;
        }
    }
}
