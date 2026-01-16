using System;
using KSP.UI.Screens;
using UnityEngine;

namespace KSPCapcom
{
    /// <summary>
    /// Manages the stock ApplicationLauncher toolbar button.
    /// Handles registration, state management, and cleanup across scene changes.
    /// </summary>
    public class ToolbarButton
    {
        private ApplicationLauncherButton _button;
        private readonly Action _onToggle;
        private bool _isOn;
        private Texture2D _iconTexture;

        // Scenes where the button should be visible
        private const ApplicationLauncher.AppScenes VISIBLE_SCENES =
            ApplicationLauncher.AppScenes.FLIGHT |
            ApplicationLauncher.AppScenes.VAB |
            ApplicationLauncher.AppScenes.SPH;

        /// <summary>
        /// Creates a new toolbar button handler.
        /// </summary>
        /// <param name="onToggle">Callback when button is clicked.</param>
        public ToolbarButton(Action onToggle)
        {
            _onToggle = onToggle ?? throw new ArgumentNullException(nameof(onToggle));
            _isOn = false;

            // Create a simple colored texture for the button icon
            _iconTexture = CreateIconTexture();

            // Subscribe to launcher ready event
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIApplicationLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIApplicationLauncherDestroyed);

            // If the launcher is already ready (e.g., hot reload), add immediately
            if (ApplicationLauncher.Ready)
            {
                AddButton();
            }

            CapcomCore.Log("ToolbarButton initialized");
        }

        /// <summary>
        /// Clean up the button and event subscriptions.
        /// </summary>
        public void Destroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIApplicationLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnGUIApplicationLauncherDestroyed);

            RemoveButton();

            if (_iconTexture != null)
            {
                UnityEngine.Object.Destroy(_iconTexture);
                _iconTexture = null;
            }

            CapcomCore.Log("ToolbarButton destroyed");
        }

        /// <summary>
        /// Set the button state programmatically (for syncing with panel state).
        /// </summary>
        public void SetState(bool isOn)
        {
            _isOn = isOn;
            if (_button != null)
            {
                if (isOn)
                {
                    _button.SetTrue(makeCall: false);
                }
                else
                {
                    _button.SetFalse(makeCall: false);
                }
            }
        }

        private void OnGUIApplicationLauncherReady()
        {
            CapcomCore.Log("ApplicationLauncher ready");
            AddButton();
        }

        private void OnGUIApplicationLauncherDestroyed()
        {
            CapcomCore.Log("ApplicationLauncher destroyed");
            RemoveButton();
        }

        private void AddButton()
        {
            if (_button != null)
            {
                CapcomCore.Log("Button already exists, skipping add");
                return;
            }

            if (!ApplicationLauncher.Ready)
            {
                CapcomCore.LogWarning("ApplicationLauncher not ready, cannot add button");
                return;
            }

            _button = ApplicationLauncher.Instance.AddModApplication(
                onTrue: OnButtonTrue,
                onFalse: OnButtonFalse,
                onHover: null,
                onHoverOut: null,
                onEnable: null,
                onDisable: null,
                visibleInScenes: VISIBLE_SCENES,
                texture: _iconTexture
            );

            CapcomCore.Log("Toolbar button added");
        }

        private void RemoveButton()
        {
            if (_button == null)
            {
                return;
            }

            ApplicationLauncher.Instance?.RemoveModApplication(_button);
            _button = null;
            CapcomCore.Log("Toolbar button removed");
        }

        private void OnButtonTrue()
        {
            _isOn = true;
            _onToggle?.Invoke();
        }

        private void OnButtonFalse()
        {
            _isOn = false;
            _onToggle?.Invoke();
        }

        /// <summary>
        /// Creates a simple icon texture for the button.
        /// In production, this would load from GameData/KSPCapcom/Icons/
        /// </summary>
        private Texture2D CreateIconTexture()
        {
            const int size = 38; // Standard KSP toolbar icon size
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);

            // Create a simple "speech bubble" style icon
            var backgroundColor = new Color(0.2f, 0.3f, 0.4f, 1f);
            var foregroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            var accentColor = new Color(0.4f, 0.7f, 1f, 1f);

            // Fill with background
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, backgroundColor);
                }
            }

            // Draw a simple rounded rectangle (chat bubble body)
            int margin = 4;
            int bubbleHeight = size - margin * 2 - 6;
            int bubbleWidth = size - margin * 2;

            for (int y = margin + 6; y < margin + 6 + bubbleHeight; y++)
            {
                for (int x = margin; x < margin + bubbleWidth; x++)
                {
                    // Round corners slightly
                    int cornerDist = 3;
                    bool isCorner = (x < margin + cornerDist && y < margin + 6 + cornerDist) ||
                                   (x < margin + cornerDist && y > margin + 6 + bubbleHeight - cornerDist) ||
                                   (x > margin + bubbleWidth - cornerDist && y < margin + 6 + cornerDist) ||
                                   (x > margin + bubbleWidth - cornerDist && y > margin + 6 + bubbleHeight - cornerDist);

                    if (!isCorner)
                    {
                        texture.SetPixel(x, y, foregroundColor);
                    }
                }
            }

            // Draw small "tail" for speech bubble
            for (int i = 0; i < 4; i++)
            {
                texture.SetPixel(margin + 6 + i, margin + 5 - i, foregroundColor);
                texture.SetPixel(margin + 7 + i, margin + 5 - i, foregroundColor);
            }

            // Draw "text lines" inside bubble
            int lineY = margin + 12;
            for (int line = 0; line < 3; line++)
            {
                int lineWidth = (line == 2) ? 12 : 20;
                for (int x = margin + 4; x < margin + 4 + lineWidth; x++)
                {
                    texture.SetPixel(x, lineY + line * 5, accentColor);
                    texture.SetPixel(x, lineY + line * 5 + 1, accentColor);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
