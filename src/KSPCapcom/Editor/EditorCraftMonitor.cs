using System;
using UnityEngine;

namespace KSPCapcom.Editor
{
    /// <summary>
    /// Singleton MonoBehaviour that monitors craft changes in the VAB/SPH editor.
    /// Uses debouncing to prevent excessive snapshot rebuilds during rapid editing.
    /// </summary>
    public class EditorCraftMonitor : MonoBehaviour
    {
        private static EditorCraftMonitor _instance;

        /// <summary>
        /// Singleton instance. Only valid in editor scenes.
        /// </summary>
        public static EditorCraftMonitor Instance => _instance;

        /// <summary>
        /// The most recent craft snapshot. May be Empty if no craft exists.
        /// </summary>
        public EditorCraftSnapshot CurrentSnapshot { get; private set; } = EditorCraftSnapshot.Empty;

        /// <summary>
        /// Event fired when a new snapshot is ready after debouncing.
        /// </summary>
        public event Action<EditorCraftSnapshot> OnSnapshotReady;

        /// <summary>
        /// Time in seconds to wait after the last modification before rebuilding the snapshot.
        /// </summary>
        private const float DEBOUNCE_SECONDS = 0.5f;

        private bool _isDirty;
        private float _lastModificationTime;

        // Initial capture state tracking
        private bool _initialCaptureDone;
        private float _startTime;
        private const float INITIAL_CAPTURE_TIMEOUT = 2.0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                CapcomCore.Log("EditorCraftMonitor: Duplicate instance, destroying self");
                Destroy(this);
                return;
            }

            _instance = this;
            CapcomCore.Log("EditorCraftMonitor: Awake");
        }

        private void Start()
        {
            // Subscribe to editor events
            GameEvents.onEditorShipModified.Add(OnShipModified);
            GameEvents.onEditorPartEvent.Add(OnPartEvent);

            // Initialize state for initial capture retry
            _startTime = Time.time;
            _initialCaptureDone = false;

            CapcomCore.Log("EditorCraftMonitor: Started and subscribed to GameEvents, waiting for craft to become available");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            GameEvents.onEditorShipModified.Remove(OnShipModified);
            GameEvents.onEditorPartEvent.Remove(OnPartEvent);

            if (_instance == this)
            {
                _instance = null;
            }

            CapcomCore.Log("EditorCraftMonitor: Destroyed and unsubscribed from GameEvents");
        }

        private void Update()
        {
            // Initial capture retry logic (waits for craft to become available)
            if (!_initialCaptureDone)
            {
                if (Time.time - _startTime < INITIAL_CAPTURE_TIMEOUT)
                {
                    // Check if craft is available
                    if (EditorLogic.fetch?.ship?.Parts?.Count > 0)
                    {
                        _initialCaptureDone = true;
                        CaptureSnapshotNow();
                        CapcomCore.Log("EditorCraftMonitor: Initial craft detected and captured");
                    }
                }
                else
                {
                    // Timeout expired - editor started with no craft
                    _initialCaptureDone = true;
                    CapcomCore.Log("EditorCraftMonitor: Initial capture timeout (no craft loaded)");
                }
            }

            // Check if we need to rebuild the snapshot (debounce elapsed)
            if (_isDirty && Time.time - _lastModificationTime >= DEBOUNCE_SECONDS)
            {
                _isDirty = false;
                CaptureSnapshotNow();
            }
        }

        /// <summary>
        /// Called when the ship is modified in the editor.
        /// </summary>
        private void OnShipModified(ShipConstruct ship)
        {
            MarkDirty();
        }

        /// <summary>
        /// Called for various part events in the editor (attach, detach, etc.)
        /// </summary>
        private void OnPartEvent(ConstructionEventType eventType, Part part)
        {
            // Only mark dirty for events that change the craft structure
            switch (eventType)
            {
                case ConstructionEventType.PartAttached:
                case ConstructionEventType.PartDetached:
                case ConstructionEventType.PartCreated:
                case ConstructionEventType.PartDeleted:
                case ConstructionEventType.PartDropped:
                case ConstructionEventType.PartPicked:
                case ConstructionEventType.PartRotated:
                case ConstructionEventType.PartOffset:
                    MarkDirty();
                    break;
            }
        }

        /// <summary>
        /// Mark the snapshot as needing a rebuild and reset the debounce timer.
        /// </summary>
        private void MarkDirty()
        {
            _isDirty = true;
            _lastModificationTime = Time.time;
        }

        /// <summary>
        /// Capture a snapshot immediately without waiting for debounce.
        /// </summary>
        private void CaptureSnapshotNow()
        {
            try
            {
                ShipConstruct ship = null;

                // Get the current ship from EditorLogic
                if (EditorLogic.fetch != null)
                {
                    ship = EditorLogic.fetch.ship;
                }

                CurrentSnapshot = EditorCraftSnapshot.Capture(ship);

                if (!CurrentSnapshot.IsEmpty)
                {
                    CapcomCore.Log($"EditorCraftMonitor: Captured snapshot for '{CurrentSnapshot.CraftName}' " +
                                   $"({CurrentSnapshot.TotalPartCount} parts, {CurrentSnapshot.Engines.Count} engines)");
                }

                // Fire the event
                OnSnapshotReady?.Invoke(CurrentSnapshot);
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"EditorCraftMonitor: Failed to capture snapshot: {ex.Message}");
                CurrentSnapshot = EditorCraftSnapshot.Empty;
            }
        }

        /// <summary>
        /// Force an immediate snapshot rebuild, bypassing the debounce timer.
        /// Useful for when you need the latest state right away.
        /// </summary>
        public void ForceRefresh()
        {
            _isDirty = false;
            CaptureSnapshotNow();
        }
    }
}
