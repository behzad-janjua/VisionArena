using System;
using System.Text;
using KiForge.Shared;
using UnityEngine;

namespace KiForge.UI
{
    /// <summary>
    /// Plays Deepgram TTS audio for each commentator narration line.
    /// The backend encodes WAV (linear16) as base64 and attaches it to the
    /// AGENT_RESPONSE payload. This component decodes it and plays via AudioSource.
    /// </summary>
    public sealed class CommentatorAudioPlayer : MonoBehaviour
    {
        private KiForgeEventBus eventBus;
        private AudioSource audioSource;

        public void Setup(KiForgeEventBus bus)
        {
            eventBus = bus;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
            eventBus.AgentResponseReceived += OnAgentResponse;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
                eventBus.AgentResponseReceived -= OnAgentResponse;
        }

        private void OnAgentResponse(AgentResponseEvent response)
        {
            if (string.IsNullOrEmpty(response.audioB64)) return;
            try
            {
                var wav = Convert.FromBase64String(response.audioB64);
                var clip = WavToClip(wav, "commentary");
                if (clip != null)
                    audioSource.PlayOneShot(clip);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commentator] Audio decode failed: {e.Message}");
            }
        }

        /// <summary>
        /// Parses a WAV (RIFF linear16) byte array into an AudioClip.
        /// Scans for the 'data' chunk rather than assuming a fixed header size,
        /// so it handles both plain and extended fmt chunks.
        /// </summary>
        private static AudioClip WavToClip(byte[] wav, string name)
        {
            if (wav.Length < 44) return null;

            int channels     = wav[22] | (wav[23] << 8);
            int sampleRate   = wav[24] | (wav[25] << 8) | (wav[26] << 16) | (wav[27] << 24);
            int bitsPerSample = wav[34] | (wav[35] << 8);

            if (bitsPerSample != 16)
            {
                Debug.LogWarning($"[Commentator] Unsupported bits-per-sample: {bitsPerSample}");
                return null;
            }

            // Scan chunk list starting after the WAVE identifier (byte 12)
            int pos = 12;
            int dataOffset = -1;
            int dataSize   = 0;
            while (pos + 8 <= wav.Length)
            {
                var chunkId = Encoding.ASCII.GetString(wav, pos, 4);
                int chunkSize = wav[pos + 4] | (wav[pos + 5] << 8) | (wav[pos + 6] << 16) | (wav[pos + 7] << 24);
                if (chunkId == "data")
                {
                    dataOffset = pos + 8;
                    dataSize   = chunkSize;
                    break;
                }
                pos += 8 + chunkSize;
            }

            if (dataOffset < 0 || dataOffset + dataSize > wav.Length)
            {
                Debug.LogWarning("[Commentator] WAV data chunk not found or truncated");
                return null;
            }

            int sampleCount = dataSize / 2; // 16-bit = 2 bytes per sample
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(wav[dataOffset + i * 2] | (wav[dataOffset + i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }

            var clip = AudioClip.Create(name, sampleCount / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
