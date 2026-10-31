using UnityEngine;
namespace ChainboundRaid
{
    [RequireComponent(typeof(RaidChain), typeof(AudioSource))]
    public sealed class RaidChainFeedback : MonoBehaviour
    {
        public AudioClip tensionSound;
        AudioSource source;
        RaidChain chain;
        bool ownsClip;
        void Awake()
        {
            chain = GetComponent<RaidChain>(); source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true;
            if (tensionSound == null)
            {
                ownsClip = true; tensionSound = AudioClip.Create("Chain tension", 2205, 1, 22050, false);
                var samples = new float[2205];
                for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * Mathf.PI * 2f * 160f / 22050f) * .3f;
                tensionSound.SetData(samples, 0);
            }
            source.clip = tensionSound;
        }
        void Update()
        {
            float tension = chain.IsSpawned ? chain.Tension.Value : 0f;
            source.volume = tension * .12f; source.pitch = 1f + tension * .4f;
            if (tension > .05f && !source.isPlaying) source.Play();
            else if (tension <= .05f && source.isPlaying) source.Stop();
        }
        void OnDestroy() { if (ownsClip) Destroy(tensionSound); }
    }
}
