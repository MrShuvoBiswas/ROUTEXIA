using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views;

public partial class LatencyGraphControl : UserControl
{
    public static readonly DependencyProperty RouteHistoryProperty =
        DependencyProperty.Register(
            nameof(RouteHistory),
            typeof(ObservableCollection<RouteSnapshot>),
            typeof(LatencyGraphControl),
            new PropertyMetadata(null, OnRouteHistoryChanged));

    public static readonly DependencyProperty IsConnectedProperty =
        DependencyProperty.Register(
            nameof(IsConnected),
            typeof(bool),
            typeof(LatencyGraphControl),
            new PropertyMetadata(false, OnIsConnectedChanged));

    public ObservableCollection<RouteSnapshot>? RouteHistory
    {
        get => (ObservableCollection<RouteSnapshot>?)GetValue(RouteHistoryProperty);
        set => SetValue(RouteHistoryProperty, value);
    }

    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    private NotifyCollectionChangedEventHandler? _collectionHandler;
    private bool _isRedrawQueued;

    // Cached resources to eliminate per-tick allocations & resource dictionary lookups
    private static readonly FontFamily MonoFont = new("/Resources/Fonts/#JetBrains Mono");
    private Brush? _accentBrush;
    private Brush? _warnBrush;
    private Brush? _mutedBrush;
    private Brush? _bgPanelBrush;

    // Retained visual elements per route ID to avoid creating UI elements on each tick
    private class RouteVisualGroup
    {
        public required Polyline Line { get; init; }
        public required Ellipse Dot { get; init; }
        public required Border BadgeBorder { get; init; }
        public required TextBlock BadgeText { get; init; }
    }

    private readonly Dictionary<string, RouteVisualGroup> _routeVisuals = new();
    private readonly Dictionary<string, List<RouteSnapshot>> _groupedSamples = new();
    private readonly HashSet<string> _activeKeys = new();

    public LatencyGraphControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeCollection();
        if (IsVisible && IsConnected)
        {
            QueueRedraw();
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && IsLoaded && IsConnected)
        {
            QueueRedraw();
        }
    }

    private void EnsureBrushes()
    {
        _accentBrush  ??= (Brush)Application.Current.FindResource("AccentBrush");
        _warnBrush    ??= (Brush)Application.Current.FindResource("StatusWarnBrush");
        _mutedBrush   ??= (Brush)Application.Current.FindResource("TextMutedBrush");
        _bgPanelBrush ??= (Brush)Application.Current.FindResource("BgPanelBrush");
    }

    private void SubscribeCollection()
    {
        if (RouteHistory != null && _collectionHandler == null)
        {
            _collectionHandler = (_, _) =>
            {
                if (IsVisible && IsLoaded && IsConnected)
                {
                    QueueRedraw();
                }
            };
            RouteHistory.CollectionChanged += _collectionHandler;
        }
    }

    private void UnsubscribeCollection()
    {
        if (RouteHistory != null && _collectionHandler != null)
        {
            RouteHistory.CollectionChanged -= _collectionHandler;
            _collectionHandler = null;
        }
    }

    private static void OnRouteHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LatencyGraphControl control)
        {
            if (e.OldValue is ObservableCollection<RouteSnapshot> oldCollection && control._collectionHandler != null)
            {
                oldCollection.CollectionChanged -= control._collectionHandler;
                control._collectionHandler = null;
            }

            control.SubscribeCollection();
            control.QueueRedraw();
        }
    }

    private static void OnIsConnectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LatencyGraphControl control)
        {
            control.QueueRedraw();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeCollection();
    }

    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueRedraw();
    }

    public void QueueRedraw()
    {
        // Skip redraw if control is not visible, unrendered, or already queued
        if (!IsVisible || !IsLoaded || _isRedrawQueued || GraphCanvas == null) return;

        _isRedrawQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _isRedrawQueued = false;
            RedrawGraph();
        });
    }

    public void RedrawGraph()
    {
        if (GraphCanvas == null || PlaceholderOverlay == null || !IsVisible || !IsLoaded) return;

        double width = GraphCanvas.ActualWidth;
        double height = GraphCanvas.ActualHeight;

        if (width <= 10 || height <= 10)
        {
            return;
        }

        if (!IsConnected || RouteHistory == null || RouteHistory.Count == 0)
        {
            PlaceholderOverlay.Visibility = Visibility.Visible;
            GridLinesContainer.Visibility = Visibility.Collapsed;
            foreach (var kvp in _routeVisuals)
            {
                kvp.Value.Line.Visibility = Visibility.Collapsed;
                kvp.Value.Dot.Visibility = Visibility.Collapsed;
                kvp.Value.BadgeBorder.Visibility = Visibility.Collapsed;
            }
            return;
        }

        PlaceholderOverlay.Visibility = Visibility.Collapsed;
        GridLinesContainer.Visibility = Visibility.Visible;
        EnsureBrushes();

        const double maxPingScale = 180.0;
        const int maxSamplesWindow = 60;

        // Group snapshots into reusable collections without LINQ allocations
        foreach (var list in _groupedSamples.Values)
        {
            list.Clear();
        }
        _activeKeys.Clear();

        var history = RouteHistory;
        int totalHistory = history.Count;
        for (int i = 0; i < totalHistory; i++)
        {
            var snap = history[i];
            if (!_groupedSamples.TryGetValue(snap.RelayId, out var list))
            {
                list = new List<RouteSnapshot>(maxSamplesWindow);
                _groupedSamples[snap.RelayId] = list;
            }
            list.Add(snap);
        }

        foreach (var kvp in _groupedSamples)
        {
            string relayKey = kvp.Key;
            var allSamples = kvp.Value;
            if (allSamples.Count < 2) continue;

            _activeKeys.Add(relayKey);

            // Sliding window: slice last maxSamplesWindow without reallocation
            int sampleCount = allSamples.Count;
            int startOffset = Math.Max(0, sampleCount - maxSamplesWindow);
            int windowCount = sampleCount - startOffset;

            var lastSample = allSamples[sampleCount - 1];
            bool isPrimary = lastSample.IsActivePrimary;
            Brush strokeBrush = isPrimary ? _accentBrush! : (relayKey.Contains("in", StringComparison.OrdinalIgnoreCase) ? _warnBrush! : _mutedBrush!);
            double strokeThickness = isPrimary ? 2.5 : 1.5;

            // Get or create retained visual elements for this route
            if (!_routeVisuals.TryGetValue(relayKey, out var visualGroup))
            {
                var polyline = new Polyline
                {
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    IsHitTestVisible = false
                };

                var dot = new Ellipse { IsHitTestVisible = false };

                var labelText = new TextBlock
                {
                    FontFamily = MonoFont,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };

                var labelBorder = new Border
                {
                    Background = _bgPanelBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Child = labelText,
                    IsHitTestVisible = false
                };

                visualGroup = new RouteVisualGroup
                {
                    Line = polyline,
                    Dot = dot,
                    BadgeBorder = labelBorder,
                    BadgeText = labelText
                };

                _routeVisuals[relayKey] = visualGroup;
                GraphCanvas.Children.Add(polyline);
                GraphCanvas.Children.Add(dot);
                GraphCanvas.Children.Add(labelBorder);
            }

            visualGroup.Line.Visibility = Visibility.Visible;
            visualGroup.Dot.Visibility = Visibility.Visible;
            visualGroup.BadgeBorder.Visibility = Visibility.Visible;

            visualGroup.Line.Stroke = strokeBrush;
            visualGroup.Line.StrokeThickness = strokeThickness;
            visualGroup.Line.Opacity = isPrimary ? 1.0 : 0.75;

            // Update PointCollection in-place without reallocating
            var points = visualGroup.Line.Points;
            double stepX = windowCount > 1 ? width / (maxSamplesWindow - 1) : width;
            int startIndex = maxSamplesWindow - windowCount;

            while (points.Count > windowCount)
            {
                points.RemoveAt(points.Count - 1);
            }
            while (points.Count < windowCount)
            {
                points.Add(new Point(0, 0));
            }

            for (int i = 0; i < windowCount; i++)
            {
                var s = allSamples[startOffset + i];
                double x = (startIndex + i) * stepX;
                double clampedPing = Math.Clamp(s.PingMs, 0, maxPingScale);
                double y = height - (clampedPing / maxPingScale * height);
                points[i] = new Point(x, y);
            }

            // Update endpoint indicator dot position
            var lastPt = points[windowCount - 1];
            double dotSize = isPrimary ? 8 : 6;
            visualGroup.Dot.Width = dotSize;
            visualGroup.Dot.Height = dotSize;
            visualGroup.Dot.Fill = strokeBrush;
            Canvas.SetLeft(visualGroup.Dot, lastPt.X - (dotSize / 2.0));
            Canvas.SetTop(visualGroup.Dot, lastPt.Y - (dotSize / 2.0));

            // Update ping badge text and position
            visualGroup.BadgeBorder.BorderBrush = strokeBrush;
            visualGroup.BadgeText.Text = $"{lastSample.PingMs:F0}ms";
            visualGroup.BadgeText.Foreground = strokeBrush;

            double labelLeft = Math.Min(lastPt.X - 35, width - 42);
            double labelTop = Math.Clamp(lastPt.Y - 18, 2, height - 20);
            Canvas.SetLeft(visualGroup.BadgeBorder, Math.Max(4, labelLeft));
            Canvas.SetTop(visualGroup.BadgeBorder, labelTop);
        }

        // Hide visuals for inactive routes
        foreach (var kvp in _routeVisuals)
        {
            if (!_activeKeys.Contains(kvp.Key))
            {
                kvp.Value.Line.Visibility = Visibility.Collapsed;
                kvp.Value.Dot.Visibility = Visibility.Collapsed;
                kvp.Value.BadgeBorder.Visibility = Visibility.Collapsed;
            }
        }
    }
}
