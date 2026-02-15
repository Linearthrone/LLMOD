using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HouseVictoria.App.HelperClasses
{
    /// <summary>
    /// Direction for auto-hide behavior
    /// </summary>
    public enum HideDirection
    {
        Top,
        Right,
        Bottom,
        Left
    }

    /// <summary>
    /// Behavior for auto-hiding windows/trays when mouse is not over them
    /// </summary>
    public class AutoHideBehavior
    {
        private readonly FrameworkElement _element;
        private readonly DispatcherTimer _hideTimer;
        private bool _isVisible = true;
        private readonly HideDirection _direction;
        private readonly int _hideDelayMs;
        private readonly double _hiddenOffset;
        private readonly double _visibleOffset;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    UpdateVisibility();
                }
            }
        }

        public AutoHideBehavior(FrameworkElement element, int hideDelayMs = 3000, double hiddenOffset = -200, HideDirection direction = HideDirection.Top)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _hideDelayMs = hideDelayMs;
            _direction = direction;
            _hiddenOffset = hiddenOffset;

            // Store the visible offset based on direction
            _visibleOffset = direction switch
            {
                HideDirection.Top => element.Margin.Top,
                HideDirection.Right => element.Margin.Right,
                HideDirection.Bottom => element.Margin.Bottom,
                HideDirection.Left => element.Margin.Left,
                _ => 0
            };

            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_hideDelayMs)
            };
            _hideTimer.Tick += HideTimer_Tick;

            RegisterEvents();
        }

        private void RegisterEvents()
        {
            _element.MouseEnter += Element_MouseEnter;
            _element.MouseLeave += Element_MouseLeave;
        }

        private void UnregisterEvents()
        {
            _element.MouseEnter -= Element_MouseEnter;
            _element.MouseLeave -= Element_MouseLeave;
            _hideTimer.Stop();
        }

        private void Element_MouseEnter(object sender, MouseEventArgs e)
        {
            IsVisible = true;
            _hideTimer.Stop();
        }

        private void Element_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isVisible)
            {
                _hideTimer.Start();
            }
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();
            IsVisible = false;
        }

        private void UpdateVisibility()
        {
            if (_isVisible)
            {
                // Restore visible position
                Thickness newMargin = _direction switch
                {
                    HideDirection.Top => new Thickness(_element.Margin.Left, _visibleOffset, _element.Margin.Right, _element.Margin.Bottom),
                    HideDirection.Right => new Thickness(_element.Margin.Left, _element.Margin.Top, _visibleOffset, _element.Margin.Bottom),
                    HideDirection.Bottom => new Thickness(_element.Margin.Left, _element.Margin.Top, _element.Margin.Right, _visibleOffset),
                    HideDirection.Left => new Thickness(_visibleOffset, _element.Margin.Top, _element.Margin.Right, _element.Margin.Bottom),
                    _ => _element.Margin
                };
                _element.Margin = newMargin;
                _element.Opacity = 1;
            }
            else
            {
                // Hide by moving to hidden position
                Thickness newMargin = _direction switch
                {
                    HideDirection.Top => new Thickness(_element.Margin.Left, _hiddenOffset, _element.Margin.Right, _element.Margin.Bottom),
                    HideDirection.Right => new Thickness(_element.Margin.Left, _element.Margin.Top, _hiddenOffset, _element.Margin.Bottom),
                    HideDirection.Bottom => new Thickness(_element.Margin.Left, _element.Margin.Top, _element.Margin.Right, _hiddenOffset),
                    HideDirection.Left => new Thickness(_hiddenOffset, _element.Margin.Top, _element.Margin.Right, _element.Margin.Bottom),
                    _ => _element.Margin
                };
                _element.Margin = newMargin;
                _element.Opacity = 0.3;
            }
        }

        public void Dispose()
        {
            UnregisterEvents();
            _hideTimer.Stop();
        }
    }
}
