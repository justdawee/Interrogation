using Interrogation.Character;
using Interrogation.Game;
using Interrogation.Llm;
using Interrogation.UI;
using UnityEngine;

namespace Interrogation.Audio
{
    /// <summary>
    /// A gyanúsított hangja beszédszintézis nélkül: a válasz elején érzelemhez illő nem szavas reakció,
    /// majd a felirat alatt halk, érthetetlen mormolás.
    /// </summary>
    public sealed class SuspectVoiceAudio : MonoBehaviour
    {
        [SerializeField] private AudioLibrary library;
        [SerializeField] private SoundPlayer player;
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private SuspectMood mood;
        [SerializeField] private SuspectLipSync lipSync;
        [Tooltip("A fej csontja — innen szól a hang.")]
        [SerializeField] private Transform head;

        [Header("Reakciók")]
        [Tooltip("Változatlan érzelemnél ekkora eséllyel szól reakció (érzelem-váltáskor mindig).")]
        [SerializeField] [Range(0f, 1f)] private float repeatReactionChance = 0.4f;

        [Header("Mormolás")]
        [SerializeField] private float mumbleFadeSpeed = 4f;

        private AudioSource _mumble;
        private float _mumbleGain;
        private float _reactionEnds;
        private SuspectEmotion _lastEmotion = SuspectEmotion.Calm;

        private void Awake()
        {
            var go = new GameObject("Mumble");
            go.transform.SetParent(transform, false);
            _mumble = go.AddComponent<AudioSource>();
            _mumble.loop = true;
            _mumble.playOnAwake = false;
            _mumble.spatialBlend = 0.6f;
            _mumble.dopplerLevel = 0f;
            _mumble.rolloffMode = AudioRolloffMode.Linear;
            _mumble.minDistance = 1.5f;
            _mumble.maxDistance = 12f;
            _mumble.clip = library != null ? library.mumbleLoop : null;
        }

        private void OnEnable()
        {
            if (manager == null) return;
            manager.TurnCompleted += OnTurnCompleted;
            manager.ScriptedLineSpoken += OnScriptedLine;
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.TurnCompleted -= OnTurnCompleted;
            manager.ScriptedLineSpoken -= OnScriptedLine;
        }

        private void OnTurnCompleted(TurnResult turn)
        {
            if (turn.IsSupersededByConfession || turn.Reply == null) return;
            React(turn.Reply.Emotion, force: false);
        }

        private void OnScriptedLine(string line, SuspectEmotion emotion)
        {
            if (emotion == SuspectEmotion.Breaking) React(emotion, force: true);
        }

        private void React(SuspectEmotion emotion, bool force)
        {
            var changed = emotion != _lastEmotion;
            _lastEmotion = emotion;
            var always = force || changed || emotion == SuspectEmotion.Hostile || emotion == SuspectEmotion.Breaking;
            if (!always && Random.value > repeatReactionChance) return;
            if (library == null || player == null) return;

            var length = player.Play(library.Voice(emotion), GameSettings.Volume(SoundChannel.Voice), HeadPosition);
            _reactionEnds = Time.time + length * 0.8f; // a mormolás a reakció végén kezdődik
        }

        private void Update()
        {
            if (_mumble.clip == null || library == null) return;
            var speaking = lipSync != null && lipSync.IsSpeaking && Time.time >= _reactionEnds;
            var target = speaking ? 1f : 0f;
            _mumbleGain = Mathf.MoveTowards(_mumbleGain, target, mumbleFadeSpeed * Time.deltaTime);

            _mumble.transform.position = HeadPosition;
            _mumble.volume = _mumbleGain * GameSettings.Volume(SoundChannel.Voice);
            if (_mumbleGain > 0.001f && !_mumble.isPlaying)
            {
                _mumble.pitch = Pitch(mood != null ? mood.Emotion : SuspectEmotion.Calm) + Random.Range(-0.03f, 0.03f);
                _mumble.Play();
                _mumble.time = Random.Range(0f, _mumble.clip.length * 0.8f); // ne mindig ugyanaz a szótag
            }
            else if (_mumbleGain <= 0.001f && _mumble.isPlaying) _mumble.Stop();
        }

        private Vector3 HeadPosition => head != null ? head.position : transform.position;

        // A feszültség emeli, a düh és a megtörés mélyíti.
        private static float Pitch(SuspectEmotion emotion)
        {
            switch (emotion)
            {
                case SuspectEmotion.Nervous: return 1.07f;
                case SuspectEmotion.Defensive: return 0.99f;
                case SuspectEmotion.Hostile: return 0.93f;
                case SuspectEmotion.Breaking: return 0.88f;
                default: return 1f;
            }
        }
    }
}
