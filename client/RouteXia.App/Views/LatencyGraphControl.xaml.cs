using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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

    public LatencyGraphControl()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private static void OnRouteHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LatencyGraphControl control)
        {
            if (e.OldValue is ObservableCollection<RouteSnapshot> oldCollection && control._collectionHandler != null)
            {
                oldCollection.CollectionChanged -= control._collectionHandler;
            }

            if (e.NewValue is ObservableCollection<RouteSnapshot> newCollection)
            {
                control._collectionHandler = (_, _) => control.Dispatcher.Invoke(control.RedrawGraph);
                newCollection.CollectionChanged += control._collectionHandler;
            }

            control.RedrawGraph();
        }
    }

    private static void OnIsConnectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LatencyGraphControl control)
        {
            control.RedrawGraph();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (RouteHistory != null && _collectionHandler != null)
        {
            RouteHistory.CollectionChanged -= _collectionHandler;
            _collectionHandler = null;
        }
    }

    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawGraph();
    }

    public void RedrawGraph()
    {
        if (GraphCanvas == null || PlaceholderOverlay == null) return;

        double width = GraphCanvas.ActualWidth;
        double height = GraphCanvas.ActualHeight;

        if (width <= 10 || height <= 10)
        {
            return;
        }

        if (!IsConnected || RouteHistory == null || RouteHistory.Count == 0)
        {
            PlaceholderOverlay.Visibility = Visibility.Visible;
            GraphCanvas.Children.Clear();
            return;
        }

        PlaceholderOverlay.Visibility = Visibility.Collapsed;
        GraphCanvas.Children.Clear();

        var routes = RouteHistory.GroupBy(s => s.RelayId).ToList();
        if (routes.Count == 0) return;

        const double maxPingScale = 180.0;
        const int maxSamplesWindow = 60;

        Brush accentBrush = (Brush)Application.Current.FindResource("AccentBrush");
        Brush warnBrush   = (Brush)Application.Current.FindResource("StatusWarnBrush");
        Brush mutedBrush  = (Brush)Application.Current.FindResource("TextMutedBrush");

        foreach (var group in routes)
        {
            var samples = group.TakeLast(maxSamplesWindow).ToList();
            if (samples.Count < 2) continue;

            bool isPrimary = samples.Last().IsActivePrimary;
            Brush strokeBrush = isPrimary ? accentBrush : (group.Key.Contains("in") ? warnBrush : mutedBrush);
            double strokeThickness = isPrimary ? 2.5 : 1.5;

            var points = new PointCollection();
            double stepX = samples.Count > 1 ? width / (maxSamplesWindow - 1) : width;
            int startIndex = maxSamplesWindow - samples.Count;

            for (int i = 0; i < samples.Count; i++)
            {
                double x = (startIndex + i) * stepX;
                double clampedPing = Math.Clamp(samples[i].PingMs, 0, maxPingScale);
                double y = height - (clampedPing / maxPingScale * height);
                points.Add(new Point(x, y));
            }

            var polyline = new Polyline
            {
                Points = points,
                Stroke = strokeBrush,
                StrokeThickness = strokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = isPrimary ? 1.0 : 0.75
            };

            GraphCanvas.Children.Add(polyline);

            // Latest point indicator dot
            if (points.Count > 0)
            {
                var lastPt = points.Last();
                var dot = new Ellipse
                {
                    Width = isPrimary ? 8 : 6,
                    Height = isPrimary ? 8 : 6,
                    Fill = strokeBrush
                };
                Canvas.SetLeft(dot, lastPt.X - (dot.Width / 2.0));
                Canvas.SetTop(dot, lastPt.Y - (dot.Height / 2.0));
                GraphCanvas.Children.Add(dot);

                // Ping label badge at the end of the line
                var labelBorder = new Border
                {
                    Background = (Brush)Application.Current.FindResource("BgPanelBrush"),
                    BorderBrush = strokeBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1)
                };
                var labelText = new TextBlock
                {
                    Text = $"{samples.Last().PingMs:F0}ms",
                    FontFamily = new FontFamily("/Resources/Fonts/#JetBrains Mono"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = strokeBrush
                };
                labelBorder.Child = labelText;

                double labelLeft = Math.Min(lastPt.X - 35, width - 42);
                double labelTop = Math.Clamp(lastPt.Y - 18, 2, height - 20);

                Canvas.SetLeft(labelBorder, Math.Max(4, labelLeft));
                Canvas.SetTop(labelBorder, labelTop);
                GraphCanvas.Children.Add(labelBorder);
            }
        }
    }
}
