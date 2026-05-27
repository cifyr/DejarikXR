using System.Collections;
using UnityEngine;

namespace Dejarik.View
{
    // A floating glow tile spawned over a candidate cell during the bot's telegraph. The chosen cell's marker
    // pulses brightly; the rejected ones jitter and fade out so the eye is led to the single chosen square.
    public class TelegraphMarker : MonoBehaviour
    {
        Material _mat;
        Vector3 _baseLocalPos;
        Vector3 _baseScale;
        bool _chosen;
        bool _busy;

        public void Init(bool chosen)
        {
            _mat = GetComponent<Renderer>().material;
            _baseLocalPos = transform.localPosition;
            _baseScale = transform.localScale;
            _chosen = chosen;
        }

        void Update()
        {
            if (_busy || _mat == null) return;
            float p = _chosen
                ? 0.8f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 5.5f))
                : 0.4f + 0.2f * Mathf.Sin(Time.time * 3f);
            _mat.SetFloat("_Glow", 1f + p);
        }

        // Jitter, shrink, and fade to nothing, then self-destruct.
        public IEnumerator JitterOut(float dur)
        {
            _busy = true;
            float t0 = Time.time;
            while (Time.time - t0 < dur)
            {
                float t = (Time.time - t0) / dur;
                float amp = 0.35f * t;
                transform.localPosition = _baseLocalPos + new Vector3(
                    (Random.value - 0.5f) * amp, (Random.value - 0.5f) * amp * 0.5f, (Random.value - 0.5f) * amp);
                transform.localScale = _baseScale * (1f - 0.7f * t);
                if (_mat != null) _mat.SetFloat("_Alpha", 0.85f * (1f - t));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
