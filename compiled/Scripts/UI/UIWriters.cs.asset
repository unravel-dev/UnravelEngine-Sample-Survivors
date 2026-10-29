using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Shared rule of the cached style writers below: per-frame code calls Write every frame, and the element is
    /// touched only when the value changed (a style write costs a style update and often a layout).
    /// </summary>
    internal static class UIWrite
    {
        /// <summary>A number is rewritten when it moved by more than this, or when it reached exactly 0 or 1.</summary>
        public const float EPSILON = 0.0005f;

        /// <summary>Whether <paramref name="value"/> differs enough from <paramref name="written"/> to write it.</summary>
        public static bool HasChanged(float value, float written)
        {
            if (value == written)
            {
                return false;
            }
            bool isEnd = value == 0.0f || value == 1.0f;
            return isEnd || float.IsNaN(written) || MathF.Abs(value - written) > EPSILON;
        }
    }

    /// <summary>Writes a progress bar's value only when it changes (see <see cref="UIWrite"/>).</summary>
    internal struct ProgressWriter
    {
        private UIProgress bar;
        private float written;

        /// <summary>Targets a bar of a freshly loaded document; the next <see cref="Write"/> always writes.</summary>
        public void Bind(UIProgress target)
        {
            bar = target;
            written = float.NaN;
        }

        public void Write(float value)
        {
            if (ReferenceEquals(bar, null) || !UIWrite.HasChanged(value, written))
            {
                return;
            }
            written = value;
            bar.value = value;
        }
    }

    /// <summary>Writes an element's opacity only when it changes (see <see cref="UIWrite"/>).</summary>
    internal struct OpacityWriter
    {
        private UIElement element;
        private float written;

        /// <summary>Targets an element of a freshly loaded document; the next <see cref="Write"/> always writes.</summary>
        public void Bind(UIElement target)
        {
            element = target;
            written = float.NaN;
        }

        public void Write(float value)
        {
            if (ReferenceEquals(element, null) || !UIWrite.HasChanged(value, written))
            {
                return;
            }
            written = value;
            element.style.opacity = value;
        }
    }

    /// <summary>
    /// Shows or hides a pooled element through its visibility (it keeps its place in the layout) only when that
    /// changes. Pooled elements start hidden: their RCSS rules say visibility: hidden.
    /// </summary>
    internal struct VisibilityWriter
    {
        private UIElement element;
        private bool isVisible;

        public bool visible => isVisible;

        /// <summary>Targets an element of a freshly loaded document, hidden by its style sheet.</summary>
        public void Bind(UIElement target)
        {
            element = target;
            isVisible = false;
        }

        /// <summary>Returns whether the visibility changed.</summary>
        public bool Write(bool show)
        {
            if (show == isVisible || ReferenceEquals(element, null))
            {
                return false;
            }
            isVisible = show;
            element.style["visibility"] = show ? "visible" : "hidden";
            return true;
        }
    }
}
