using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Markus.Models;
using Markus.Services;
using Markus.ViewModels;

namespace Markus.Views;

internal sealed partial class OutlinePanel : UserControl
{
    private const double DragThreshold = 5.0;

    private Point? _pressPoint;
    private OutlineNode? _pressNode;
    private OutlineNode? _dragging;

    public OutlinePanel()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public event EventHandler<OutlineNodeSelectedEventArgs>? NodeSelected;

    private void OutlineTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            return;
        }
        if (e.AddedItems[0] is not OutlineNode node)
        {
            return;
        }
        NodeSelected?.Invoke(this, new OutlineNodeSelectedEventArgs(node));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }
            var item = FindTreeViewItemUnder(e.Source);
            if (item?.DataContext is not OutlineNode node)
            {
                _pressNode = null;
                return;
            }
            _pressNode = node;
            _pressPoint = e.GetPosition(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Markus drag press: {ex.Message}");
            _pressNode = null;
            _pressPoint = null;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        try
        {
            if (_dragging is not null)
            {
                MoveGhost(e.GetPosition(this));
                UpdateDropTargetVisual(e);
                return;
            }
            if (_pressNode is null || _pressPoint is null)
            {
                return;
            }
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _pressNode = null;
                _pressPoint = null;
                return;
            }
            var current = e.GetPosition(this);
            if (
                Math.Abs(current.X - _pressPoint.Value.X) < DragThreshold
                && Math.Abs(current.Y - _pressPoint.Value.Y) < DragThreshold
            )
            {
                return;
            }
            _dragging = _pressNode;
            Cursor = new Cursor(StandardCursorType.DragMove);
            e.Pointer.Capture(this);
            ShowGhost(_pressNode.Text, e.GetPosition(this));
            UpdateDropTargetVisual(e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Markus drag move: {ex.Message}");
            HideDropIndicator();
            HideGhost();
        }
    }

    private void ShowGhost(string text, Point at)
    {
        if (this.FindControl<TextBlock>("DragGhostText") is { } label)
        {
            label.Text = text;
        }
        if (this.FindControl<Border>("DragGhost") is { } ghost)
        {
            ghost.IsVisible = true;
            PositionGhost(ghost, at);
        }
    }

    private void MoveGhost(Point at)
    {
        if (this.FindControl<Border>("DragGhost") is { } ghost)
        {
            PositionGhost(ghost, at);
        }
    }

    private static void PositionGhost(Border ghost, Point at)
    {
        // Offset slightly down/right so the cursor's hit point still aligns
        // with the drop target row, not buried under the ghost itself.
        Canvas.SetLeft(ghost, at.X + 14);
        Canvas.SetTop(ghost, at.Y + 6);
    }

    private void HideGhost()
    {
        if (this.FindControl<Border>("DragGhost") is { } ghost)
        {
            ghost.IsVisible = false;
        }
    }

    private void UpdateDropTargetVisual(PointerEventArgs e)
    {
        try
        {
            var item = FindTreeViewItemUnder(e.Source);
            if (item is null)
            {
                HideDropIndicator();
                return;
            }
            var position = ResolveDropPosition(item, e.GetPosition(item));
            PositionDropIndicator(item, position);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Markus drop visual: {ex.Message}");
            HideDropIndicator();
        }
    }

    private void PositionDropIndicator(TreeViewItem item, DropPosition position)
    {
        var topLeft = item.TranslatePoint(default, this);
        if (topLeft is null)
        {
            return;
        }
        var width = item.Bounds.Width;
        var height = item.Bounds.Height;
        HideDropIndicator();
        switch (position)
        {
            case DropPosition.Before when this.FindControl<Border>("DropBefore") is { } b:
                Canvas.SetLeft(b, topLeft.Value.X);
                Canvas.SetTop(b, topLeft.Value.Y - 1);
                b.Width = width;
                b.IsVisible = true;
                break;
            case DropPosition.After when this.FindControl<Border>("DropAfter") is { } b:
                Canvas.SetLeft(b, topLeft.Value.X);
                Canvas.SetTop(b, topLeft.Value.Y + height - 1);
                b.Width = width;
                b.IsVisible = true;
                break;
            case DropPosition.Inside when this.FindControl<Border>("DropInside") is { } b:
                Canvas.SetLeft(b, topLeft.Value.X);
                Canvas.SetTop(b, topLeft.Value.Y);
                b.Width = width;
                b.Height = height;
                b.IsVisible = true;
                break;
        }
    }

    private void HideDropIndicator()
    {
        foreach (var name in new[] { "DropBefore", "DropAfter", "DropInside" })
        {
            if (this.FindControl<Border>(name) is { } b)
            {
                b.IsVisible = false;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (_dragging is null)
            {
                return;
            }
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }
            var dragged = _dragging;
            var item = FindTreeViewItemUnder(e.Source);
            OutlineNode? target = null;
            var position = DropPosition.After;
            if (item?.DataContext is OutlineNode targetNode)
            {
                target = targetNode;
                position = ResolveDropPosition(item, e.GetPosition(item));
            }
            if (target is not null && (ReferenceEquals(target, dragged) || IsDescendant(dragged, target)))
            {
                return;
            }
            vm.MoveHeading(dragged, target, position);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Markus outline drop: {ex.Message}");
        }
        finally
        {
            try
            {
                e.Pointer.Capture(null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Markus capture release: {ex.Message}");
            }
            HideDropIndicator();
            _dragging = null;
            _pressNode = null;
            _pressPoint = null;
            Cursor = Cursor.Default;
            HideGhost();
        }
    }

    private static TreeViewItem? FindTreeViewItemUnder(object? source)
    {
        for (var cursor = source as Visual; cursor is not null; cursor = cursor.GetVisualParent())
        {
            if (cursor is TreeViewItem item)
            {
                return item;
            }
        }
        return null;
    }

    private static DropPosition ResolveDropPosition(TreeViewItem item, Point local)
    {
        var height = item.Bounds.Height;
        if (height <= 0)
        {
            return DropPosition.After;
        }
        var third = height / 3.0;
        if (local.Y < third)
        {
            return DropPosition.Before;
        }
        if (local.Y > height - third)
        {
            return DropPosition.After;
        }
        return DropPosition.Inside;
    }

    private static bool IsDescendant(OutlineNode root, OutlineNode candidate)
    {
        foreach (var child in root.Children)
        {
            if (ReferenceEquals(child, candidate) || IsDescendant(child, candidate))
            {
                return true;
            }
        }
        return false;
    }
}

internal sealed class OutlineNodeSelectedEventArgs : EventArgs
{
    public OutlineNodeSelectedEventArgs(OutlineNode node)
    {
        Node = node;
    }

    public OutlineNode Node { get; }
}
