using UnityEngine;

namespace ChainboundRaid
{
    [RequireComponent(typeof(RaidPlayer), typeof(AudioSource))]
    public sealed class RaidCombatFeedback : MonoBehaviour
    {
        public AudioClip hitSound;
        public Material hitMaterial;
        RaidPlayer player;
        AudioSource audioSource;
        bool ownsSound;
        bool ownsMaterial;

        void Awake()
        {
            player = GetComponent<RaidPlayer>();
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = .25f;
            if (hitSound == null)
            {
                ownsSound = true;
                hitSound = AudioClip.Create("Impact cue", 1764, 1, 22050, false);
                var samples = new float[1764];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = Mathf.Sin(i * .19f) * Mathf.Exp(-i / 250f);
                hitSound.SetData(samples, 0);
            }
            if (hitMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    ownsMaterial = true;
                    hitMaterial = new Material(shader);
                    if (hitMaterial.HasProperty("_BaseColor")) hitMaterial.SetColor("_BaseColor", new Color(1f, .8f, .2f));
                    if (hitMaterial.HasProperty("_Color")) hitMaterial.SetColor("_Color", new Color(1f, .8f, .2f));
                }
            }
        }
        void OnEnable() { player.HitConfirmed += ShowHit; }
        void OnDisable() { player.HitConfirmed -= ShowHit; }
        void OnDestroy()
        {
            if (ownsSound) Destroy(hitSound);
            if (ownsMaterial) Destroy(hitMaterial);
        }
        void ShowHit(Vector3 point)
        {
            audioSource.PlayOneShot(hitSound);
            if (hitMaterial == null) return;
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Confirmed Hit";
            flash.GetComponent<Collider>().enabled = false;
            flash.transform.position = point;
            flash.transform.localScale = Vector3.one * .2f;
            flash.GetComponent<Renderer>().sharedMaterial = hitMaterial;
            flash.AddComponent<RaidHitFlash>();
        }
    }
}
