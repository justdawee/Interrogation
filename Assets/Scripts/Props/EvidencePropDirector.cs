using System;
using System.Collections;
using System.Collections.Generic;
using Interrogation.Game;
using Interrogation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Interrogation.Props
{
    /// <summary>
    /// A bizonyíték-szembesítés tárgy-rendezője: a csatolt bizonyíték elindítja a jelenetét, a tárgy a
    /// válasz végéig kint marad, majd minden visszaáll. A jelenetek egymás után futnak.
    /// </summary>
    public sealed class EvidencePropDirector : MonoBehaviour
    {
        [SerializeField] private InterrogationManager manager;
        [SerializeField] private QuestionBar question;
        [SerializeField] private SubtitleView subtitle;
        [SerializeField] private CaseFilePanel caseFile;
        [SerializeField] private PropStage stage;
        [SerializeField] private EvidencePropScene[] scenes = new EvidencePropScene[0];
        [Tooltip("Ennyit vár a felirat vége után, mielőtt a tárgyak visszaállnak (mp).")]
        [SerializeField] private float holdAfterLine = 1.2f;
        [SerializeField] private float clickDistance = 3f;

        private readonly Queue<IEnumerator> _queue = new Queue<IEnumerator>();
        private bool _running;
        private bool _awaitingReply;
        private EvidencePropScene _current;
        private bool _locked, _lockApplied;

        /// <summary>A tárgy ilyenkor a detektív-kamerához igazodik, ezért a kamera a detektív-nézetbe tér vissza.</summary>
        public bool IsPresenting { get; private set; }

        private void OnEnable()
        {
            if (question != null)
            {
                question.EvidenceChanged += OnEvidenceChanged;
                question.Submitted += OnSubmitted;
            }
            if (manager != null) manager.TurnCompleted += OnTurnCompleted;
        }

        private void OnDisable()
        {
            if (question != null)
            {
                question.EvidenceChanged -= OnEvidenceChanged;
                question.Submitted -= OnSubmitted;
            }
            if (manager != null) manager.TurnCompleted -= OnTurnCompleted;
        }

        /// <summary>
        /// Csak ha a kihallgatás elindult, a gyanúsított nem gondolkodik és nem beszél, nincs függő
        /// szembesítés és az előző tárgy-animáció lefutott, így a szembesítések nem torlódhatnak fel.
        /// </summary>
        public bool CanSelect =>
            question != null && question.IsInteractable
            && !_running && _queue.Count == 0 && !_awaitingReply
            && (subtitle == null || !subtitle.IsTyping)
            && (manager == null || !manager.IsOver)
            && Time.timeScale > 0f; // szünet alatt nem

        private void Update()
        {
            SyncSelectionLock();

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (!CanSelect || caseFile == null) return;

            var scene = PickAt(mouse.position.ReadValue());
            if (scene != null) caseFile.ChooseById(scene.EvidenceFactId);
        }

        /// <summary>UI fölött null.</summary>
        public EvidencePropScene PickAt(Vector2 screenPoint)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;
            var cam = stage != null ? stage.ViewCamera : Camera.main;
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(screenPoint);
            return Physics.Raycast(ray, out var hit, clickDistance)
                ? hit.collider.GetComponentInParent<EvidencePropScene>()
                : null;
        }

        public EvidencePropScene SceneFor(string evidenceId) => Find(evidenceId);

        private void SyncSelectionLock()
        {
            var locked = !CanSelect;
            if (_lockApplied && locked == _locked) return;
            _lockApplied = true;
            _locked = locked;
            caseFile?.SetSelectionLocked(locked);
            question?.SetEvidenceLocked(locked);
        }

        private void OnEvidenceChanged(string evidenceId) => Enqueue(SwitchTo(evidenceId));

        private void OnSubmitted(string text, string evidenceId)
        {
            if (!string.IsNullOrEmpty(evidenceId)) _awaitingReply = true;
            SyncSelectionLock();
        }

        private void OnTurnCompleted(TurnResult turn)
        {
            if (!_awaitingReply) return;
            _awaitingReply = false;
            Enqueue(ReturnAfterLine());
        }

        private IEnumerator SwitchTo(string evidenceId)
        {
            if (_current != null && !string.Equals(_current.EvidenceFactId, evidenceId, StringComparison.OrdinalIgnoreCase))
            {
                yield return _current.Restore(stage);
                _current = null;
            }
            var next = Find(evidenceId);
            if (next == null || next == _current) yield break;
            _current = next;
            IsPresenting = true;
            try { yield return next.Present(stage); }
            finally { IsPresenting = false; }
        }

        private IEnumerator ReturnAfterLine()
        {
            yield return null; // a HUD ebben a körben indítja a feliratot
            while (subtitle != null && subtitle.IsTyping) yield return null;
            yield return new WaitForSeconds(holdAfterLine);
            if (_current == null) yield break;
            yield return _current.Restore(stage);
            _current = null;
        }

        private EvidencePropScene Find(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId)) return null;
            foreach (var s in scenes)
                if (s != null && string.Equals(s.EvidenceFactId, evidenceId, StringComparison.OrdinalIgnoreCase))
                    return s;
            return null;
        }

        private void Enqueue(IEnumerator routine)
        {
            _queue.Enqueue(routine);
            if (!_running && isActiveAndEnabled) StartCoroutine(Pump());
            SyncSelectionLock(); // azonnal, hogy ugyanabban a képkockában se jöhessen második választás
        }

        private IEnumerator Pump()
        {
            _running = true;
            while (_queue.Count > 0) yield return _queue.Dequeue();
            _running = false;
        }
    }
}
