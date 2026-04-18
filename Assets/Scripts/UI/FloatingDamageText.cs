using System.Collections;
using TMPro;
using UnityEngine;

namespace PokemonAdventure.UI
{
    // ==========================================================================
    // Floating Damage Text
    // A single pooled floating number that rises and fades out over its lifetime.
    // Managed by FloatingDamageManager — do not call Play() directly.
    // Attach to a World-Space Canvas child with a TextMeshPro component.
    // ==========================================================================

    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingDamageText : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float _duration     = 1.1f;
        [SerializeField] private float _riseSpeed    = 1.8f;
        [SerializeField] private float _spreadRadius = 0.3f; // Random horizontal spread

        [Header("Font Sizes")]
        [SerializeField] private float _normalSize = 2.2f;
        [SerializeField] private float _critSize   = 3.0f; // Super effective

        private TextMeshPro _tmp;
        private Coroutine   _anim;

        private static readonly Color ColorNormal    = new(1.00f, 1.00f, 1.00f, 1f);
        private static readonly Color ColorSuper     = new(1.00f, 0.80f, 0.10f, 1f); // yellow
        private static readonly Color ColorDoubleSuper = new(1.00f, 0.40f, 0.10f, 1f); // orange
        private static readonly Color ColorWeak      = new(0.65f, 0.65f, 0.65f, 1f); // gray
        private static readonly Color ColorHeal      = new(0.25f, 0.95f, 0.35f, 1f); // green
        private static readonly Color ColorArmor     = new(0.40f, 0.70f, 1.00f, 1f); // blue

        private void Awake() => _tmp = GetComponent<TextMeshPro>();

        // ── Public API ────────────────────────────────────────────────────────

        public void ShowDamage(float hpDamage, float armorDamage,
            Combat.EffectivenessCategory effectiveness, Vector3 worldPos)
        {
            transform.position = worldPos + new Vector3(
                Random.Range(-_spreadRadius, _spreadRadius), 0f, 0f);

            bool isSuper = effectiveness == Combat.EffectivenessCategory.SuperEffective
                        || effectiveness == Combat.EffectivenessCategory.DoubleSuper;
            bool isWeak  = effectiveness == Combat.EffectivenessCategory.HalfEffective
                        || effectiveness == Combat.EffectivenessCategory.QuarterEffective
                        || effectiveness == Combat.EffectivenessCategory.Immune;

            // Show HP damage primarily; show armor if no HP damage
            float display = hpDamage > 0 ? hpDamage : armorDamage;
            _tmp.text     = Mathf.CeilToInt(display).ToString();
            _tmp.fontSize = isSuper ? _critSize : _normalSize;
            _tmp.color    = isSuper ? (effectiveness == Combat.EffectivenessCategory.DoubleSuper
                                        ? ColorDoubleSuper : ColorSuper)
                          : isWeak  ? ColorWeak
                          : armorDamage > 0 && hpDamage <= 0 ? ColorArmor
                          : ColorNormal;

            StartAnim();
        }

        public void ShowHeal(float amount, Vector3 worldPos)
        {
            transform.position = worldPos + new Vector3(
                Random.Range(-_spreadRadius, _spreadRadius), 0f, 0f);

            _tmp.text     = $"+{Mathf.CeilToInt(amount)}";
            _tmp.fontSize = _normalSize;
            _tmp.color    = ColorHeal;

            StartAnim();
        }

        // ── Animation ─────────────────────────────────────────────────────────

        private void StartAnim()
        {
            if (_anim != null) StopCoroutine(_anim);
            gameObject.SetActive(true);
            _anim = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float elapsed = 0f;
            var   startColor = _tmp.color;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                transform.position += Vector3.up * _riseSpeed * Time.deltaTime;

                float t = elapsed / _duration;
                // Fade out in last third
                float alpha = t < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.65f) / 0.35f);
                _tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                // Face camera every frame
                if (Camera.main != null)
                    transform.rotation = Camera.main.transform.rotation;

                yield return null;
            }

            gameObject.SetActive(false);
            _anim = null;
        }
    }
}
