using System.Collections.Generic;
using UnityEngine;

namespace Aib.Audio
{
    /// <summary>Plays bundled clips from Resources/Audio (l_*.mp3 letters, w_*.mp3 words).</summary>
    public class AudioService : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        AudioSource _source;

        void Awake() => _source = gameObject.AddComponent<AudioSource>();

        AudioClip Get(string id)
        {
            if (!_cache.TryGetValue(id, out var clip))
                _cache[id] = clip = Resources.Load<AudioClip>("Audio/" + id);
            return clip;
        }

        public bool Has(string id) => Get(id) != null;

        public void Play(string id)
        {
            var clip = Get(id);
            if (clip == null) return;
            _source.Stop();
            _source.PlayOneShot(clip);
        }
    }
}
