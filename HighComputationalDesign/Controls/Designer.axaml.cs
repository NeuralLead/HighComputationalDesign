using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using HighComputationalDesign.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HighComputationalDesign;

public partial class Designer : UserControl, INotifyPropertyChanged
{
    //ObservableCollection<DigitalComponent> Components { get; set; } = new ObservableCollection<DigitalComponent>();
    //ObservableCollection<DigitalComponentConnections> DigitalComponentConnections { get; set; } = new ObservableCollection<DigitalComponentConnections>();

    public HCPProject Project;

    private object? _SelectedObject = new object();
    public object? SelectedObject { get => _SelectedObject; set { _SelectedObject = value; _isPanning = false; OnPropertyChanged(nameof(SelectedObject)); } }

    public Designer()
    {
        InitializeComponent();
        this.DataContext = this;
    }

    internal void AddComponent(DigitalComponent c)
    {
        var prevComponent = Project.Components.LastOrDefault();

        //var c = new DigitalComponent(this) { DigitalComponentName = $"Digital Component {Project.Components.Count}" };
        c.ComponentPosition = (prevComponent == null)
            ? new Avalonia.Point(0 + 50, 50)
            : new Avalonia.Point(prevComponent.ComponentPosition.X + prevComponent.ComponentSize.X + 50, prevComponent.ComponentPosition.Y);

        Project.Components.Add(c);
        eCanvas.Children.Add(c);
    }

    public void RemoveComponent(DigitalComponent comp)
    {
        _isPanning = false;

        // Remove from UI
        foreach (var saved in Project.SavedWirePaths.Where(x => x.DigitalComponentConnection.DigitalComponentA == comp || x.DigitalComponentConnection.DigitalComponentB == comp))
            eCanvas.Children.Remove(saved.Wire);

        // Remove from memory
        int removed = Project.SavedWirePaths.RemoveAll(x => x.DigitalComponentConnection.DigitalComponentA == comp || x.DigitalComponentConnection.DigitalComponentB == comp);

        // Remove frommemory
        Project.Components.Remove(comp);

        // Remove from UI
        eCanvas.Children.Remove(comp);
    }

    bool isInvalidPosition = false;
    bool releaseRequestAfterInvalid = false;
    readonly bool AreaExpandable = false;

    private double _zoom = 1.0;
    private Point _pan = new Point(0, 0);
    private bool _isPanning = false;
    private Point _lastPanPoint;

    private DigitalComponent? MovingComponent = null;
    private object MovingComponent_Lock = new object();

    public void SetNewMovingComponent(DigitalComponent? movingComponent)
    {
        lock (MovingComponent_Lock)
        {
            if (isInvalidPosition)
            {
                releaseRequestAfterInvalid = true;
                return;
            }
        }

        lock (MovingComponent_Lock)
        {
            releaseRequestAfterInvalid = false;
            MovingComponent = movingComponent;
        }
    }

    public Transform CanvasTransform =>
        new TransformGroup
        {
            Children = new Transforms
            {
                new ScaleTransform(_zoom, _zoom),
                new TranslateTransform(_pan.X, _pan.Y)
            }
        };

    private const double PanBorderLimit = 150; // px dal bordo dove triggerare espansione
    private const double ExpandStep = 10; // quanto espandere ogni volta

    private void UpdateTransform()
    {
        // Applichiamo zoom e pan ottico!
        eCanvas.RenderTransform = new TransformGroup
        {
            Children = new Transforms
                {
                    new ScaleTransform(_zoom, _zoom),
                    new TranslateTransform(_pan.X, _pan.Y)
                }
        };
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // pt: posizione del mouse rispetto a eCanvas
        var pt = e.GetPosition(eCanvas);

        Debug.WriteLine($"Mouse {pt.X},{pt.Y}");
        Debug.WriteLine($"Pan prima: {_pan.X},{_pan.Y}, Zoom: {_zoom}");

        double oldZoom = _zoom;
        _zoom *= Math.Pow(1.1, e.Delta.Y);

        var wx = (pt.X - _pan.X) / oldZoom;
        var wy = (pt.Y - _pan.Y) / oldZoom;

        _pan = new Point(pt.X - wx * _zoom, pt.Y - wy * _zoom);

        Debug.WriteLine($"Pan dopo: {_pan.X},{_pan.Y}, Zoom: {_zoom}");

        UpdateTransform();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = e.GetPosition(this);
        }
    }

    protected void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        lock (MovingComponent_Lock)
        {
            isInvalidPosition = false;

            if (MovingComponent != null)
            {
                //Debug.WriteLine("Moving component");
                var pos = e.GetPosition(this);

                var oldPos = MovingComponent.ComponentPosition;

                var newPos = new Point(pos.X - MovingComponent.ComponentSize.X + 25, pos.Y - 55);
                MovingComponent.ComponentPosition = newPos;

                var deltaPos = newPos - oldPos;

                foreach (var wirePathOJ in Project.SavedWirePaths)
                {
                    if (wirePathOJ.DigitalComponentConnection.DigitalComponentA == MovingComponent)
                    {
                        var startAnchor = GetPinAnchor(wirePathOJ.DigitalComponentConnection.DigitalComponentA.GetLabelOPin_OutputPins(wirePathOJ.DigitalComponentConnection.NodeA), eCanvas);
                        var endAnchor = GetPinAnchor(wirePathOJ.DigitalComponentConnection.DigitalComponentB.GetLabelOPin_InputPins(wirePathOJ.DigitalComponentConnection.NodeB), eCanvas);

                        var points = FindPath(startAnchor, endAnchor, eCanvas.Bounds.Size, false);

                        //if (wirePathOJ.Wire != null) eCanvas.Children.Remove(wirePathOJ.Wire);

                        IImmutableSolidColorBrush brush;

                        if (!points.Any())
                        {
                            isInvalidPosition = true;
                            points = new List<Point>() { startAnchor, endAnchor };
                            brush = Brushes.Red;
                        }
                        else
                        {
                            brush = Brushes.Yellow;
                        }

                        eCanvas.Children.Remove(wirePathOJ.Wire);
                        RemoveLineMenu(wirePathOJ.Wire);
                        wirePathOJ.Points = points;
                        wirePathOJ.Wire = BuildWire(wirePathOJ.Points);
                        AddLineMenu(wirePathOJ.Wire);
                        wirePathOJ.Wire.Stroke = brush;
                        eCanvas.Children.Add(wirePathOJ.Wire);
                    }
                    if (wirePathOJ.DigitalComponentConnection.DigitalComponentB == MovingComponent)
                    {
                        var startAnchor = GetPinAnchor(wirePathOJ.DigitalComponentConnection.DigitalComponentB.GetLabelOPin_InputPins(wirePathOJ.DigitalComponentConnection.NodeB), eCanvas);
                        var endAnchor = GetPinAnchor(wirePathOJ.DigitalComponentConnection.DigitalComponentA.GetLabelOPin_OutputPins(wirePathOJ.DigitalComponentConnection.NodeA), eCanvas);

                        var points = FindPath(startAnchor, endAnchor, eCanvas.Bounds.Size, false);

                        //if (wirePathOJ.Wire != null) eCanvas.Children.Remove(wirePathOJ.Wire);

                        IImmutableSolidColorBrush brush;

                        if (!points.Any())
                        {
                            isInvalidPosition = true;
                            points = new List<Point>() { startAnchor, endAnchor };
                            brush = Brushes.Red;
                        }
                        else
                        {
                            brush = Brushes.Yellow;
                        }

                        eCanvas.Children.Remove(wirePathOJ.Wire);
                        RemoveLineMenu(wirePathOJ.Wire);
                        wirePathOJ.Points = points;
                        wirePathOJ.Wire = BuildWire(wirePathOJ.Points);
                        AddLineMenu(wirePathOJ.Wire);
                        wirePathOJ.Wire.Stroke = brush;
                        eCanvas.Children.Add(wirePathOJ.Wire);
                    }
                }

                if (releaseRequestAfterInvalid && !isInvalidPosition)
                    SetNewMovingComponent(null);

                return;
            }
        }

        if (_isPanning)
        {
            var pos = e.GetPosition(this);
            var delta = pos - _lastPanPoint;
            _lastPanPoint = pos;

            _pan = new Point(_pan.X + delta.X, _pan.Y + delta.Y);
            UpdateTransform();

            if (AreaExpandable)
            {
                // Calcola le coordinate del bordo visibile nel sistema canvas
                var left = -_pan.X / _zoom;
                var top = -_pan.Y / _zoom;
                var right = left + (this.Bounds.Width / _zoom);
                var bottom = top + (this.Bounds.Height / _zoom);

                // Se arrivi vicino al bordo SX
                if (left < PanBorderLimit)
                    ExpandCanvasLeft();

                // Se arrivi vicino al bordo DX
                if (eCanvas.Width - right < PanBorderLimit)
                    ExpandCanvasRight();
            }

            // Stesso per top/bottom se vuoi
        }
    }

    protected void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _isPanning = false;
    }

    private void TranslateChildrens()
    {
        // 2. Sposta tutti i figli del canvas a dx di ExpandStep
        foreach (var child in eCanvas.Children)
        {
            if (child is Path)
            {
                var path = child as Path;
                if (path?.Data is PathGeometry)
                {
                    var pathGeometry = (path.Data as PathGeometry);

                    foreach (PathFigure fig in pathGeometry.Figures)
                    {
                        fig.StartPoint = new Point(fig.StartPoint.X + (ExpandStep * _zoom), fig.StartPoint.Y);

                        foreach (PolyLineSegment seg in fig.Segments)
                            for (int p = 0; p < seg.Points.Count; p++)
                                seg.Points[p] = new Point(seg.Points[p].X + (ExpandStep * _zoom), seg.Points[p].Y);
                    }
                }
            }
            else
            {
                var oldLeft = Canvas.GetLeft(child);
                Canvas.SetLeft(child, oldLeft + (ExpandStep * _zoom));
            }
        }
    }

    private void ExpandCanvasLeft()
    {
        Debug.WriteLine($"New Expand Step {ExpandStep}");
        Debug.WriteLine($"Old Width {eCanvas.Width}");

        // 1. Aumenta larghezza canvas
        eCanvas.Width = eCanvas.Bounds.Width + (ExpandStep * _zoom);

        Debug.WriteLine($"New Width {eCanvas.Width}");

        TranslateChildrens();

        // 3. Sposta anche il pan ottico per mantenerlo coerente
        _pan = new Point(_pan.X + (ExpandStep * _zoom), _pan.Y);

        UpdateTransform();
    }

    private void ExpandCanvasRight()
    {
        eCanvas.Width = eCanvas.Bounds.Width + (ExpandStep * _zoom);

        _pan = new Point(_pan.X + (ExpandStep * _zoom), _pan.Y);

        UpdateTransform();
    }









    //ContextMenu contextMenu;

    public ContextMenu InitRightMenu()
    {
        var remove = new MenuItem { Header = "Remove" };
        remove.Click += RemoveLine_Click;

        var contextMenu = new ContextMenu
        {
            Items =
                {
                    remove,
                    /*new MenuItem
                    {
                        Header = "SottoMenu",
                        Items =
                        {
                            new MenuItem { Header = "Sub 1" },
                            new MenuItem { Header = "Sub 2" }
                        }
                    }*/
                }
        };

        return contextMenu;
    }

    public void AddLineMenu(Control ctrl)
    {
        // Collega il menu al controllo
        ctrl.ContextMenu = InitRightMenu();

        ctrl.ContextMenu.Opened += (_, __) =>
        {
            _isPanning = false;
            ctrl.ContextMenu.PlacementTarget = ctrl;
        };
    }

    public void RemoveLineMenu(Control ctrl)
    {
        ctrl.ContextMenu = null;
    }

    private void RemoveLine_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu ctxMenu)
        {
            if (ctxMenu.PlacementTarget is Path targetCtrl)
            {
                // Qui hai il controllo che ha aperto il menu
                Debug.WriteLine($"Remove su: {targetCtrl}");

                var savedWire = Project.SavedWirePaths.FirstOrDefault(x => x.Wire == targetCtrl);

                // rimuovere dalla memoria (wire, points...)
                if (savedWire != null)
                {
                    RemoveLineMenu(savedWire.Wire);
                    Project.SavedWirePaths.Remove(savedWire);
                }

                // rimuovere la linea dal canvas
                if (targetCtrl.Parent is Panel panel)
                    panel.Children.Remove(targetCtrl);
            }
        }
    }

    public void ClearGrid()
    {
        for (int a = 0; a < eCanvas.Children.Count; a++)
        {
            if (eCanvas.Children[a] is Rectangle)
            {
                eCanvas.Children.RemoveAt(a);
                a--;
            }
        }
    }

    public void DrawGrid(bool[,] grid, int cols, int rows)
    {
        for (int x = 0; x < cols; x++)
            for (int y = 0; y < rows; y++)
                if (grid[x, y])
                {
                    var rect = new Rectangle
                    {
                        Width = GridStep - 1,
                        Height = GridStep - 1,
                        //Fill = Brushes.Red,
                        Stroke = Brushes.Red,
                        StrokeThickness = 2,
                        Opacity = 0.2
                    };
                    Canvas.SetLeft(rect, x * GridStep);
                    Canvas.SetTop(rect, y * GridStep);
                    eCanvas.Children.Add(rect);
                }
    }

    public void ClearAllLines()
    {
        for (int a = 0; a < eCanvas.Children.Count; a++)
        {
            if (eCanvas.Children[a] is Line)
            {
                eCanvas.Children.RemoveAt(a);
                a--;
            }
        }
    }


    private Control? startPin;
    private Path? tempWire;
    Point startPos;
    Point endPos;

    const int GridStep = 15;
    public record struct Node(int F, int H, int X, int Y);

    public Point GetPinAnchor(Control pin, Canvas eCanvas)
    {
        var pinCenter = pin.TranslatePoint(
            new Point(pin.Bounds.Width / 2, pin.Bounds.Height / 2),
            eCanvas) ?? new Point();

        var tag = pin.Tag?.ToString();
        var offset = 10.0;

        if (tag == "Output")
            return pinCenter + new Point(offset, 0);
        if (tag == "Input")
            return pinCenter + new Point(-offset, 0);

        return pinCenter;
    }

    public void Pin_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control pin && pin.Tag?.ToString() == "Output")
        {
            startPin = pin;
            startPos = GetPinAnchor(startPin, eCanvas);

            tempWire = new Path
            {
                Stroke = Brushes.Yellow,
                StrokeThickness = 2,
                Data = new PathGeometry()
            };

            AddLineMenu(tempWire);
            eCanvas.Children.Add(tempWire);
            e.Pointer.Capture(pin);
        }
    }

    public void Pin_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (tempWire != null && startPin != null && e.GetCurrentPoint(eCanvas).Properties.IsLeftButtonPressed)
        {
            endPos = e.GetPosition(eCanvas);

            // Calcola un percorso A* dal pin al mouse (considerando le linee esistenti come ostacoli)
            var points = FindPath(startPos, endPos, eCanvas.Bounds.Size);

            if (points.Any())
            {
                points.Insert(0, startPos);
                points.Add(endPos);
                tempWire.Stroke = Brushes.Yellow;
            }
            else
            {
                points = new List<Point> { startPos, endPos };
                tempWire.Stroke = Brushes.Red;
            }

            var fig = new PathFigure { StartPoint = points.First(), IsClosed = false };
            var seg = new PolyLineSegment();
            foreach (var p in points.Skip(1))
                seg.Points.Add(p);
            fig.Segments.Clear();
            fig.Segments.Add(seg);

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            tempWire.Data = geo;

            //Debug.WriteLine("Path points: " + string.Join(", ", points));
        }
    }

    public void Pin_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (tempWire == null || startPin == null)
            return;

        var pos = e.GetPosition(eCanvas);

        var hits = eCanvas.GetVisualsAt(pos);
        var targetPin = hits
            .OfType<Visual>()
            .Select(v => v.FindAncestorOfType<Label>())
            .FirstOrDefault(l => l?.Tag?.ToString() == "Input");

        if (targetPin != null)
        {
            var startAnchor = GetPinAnchor(startPin, eCanvas);
            var endAnchor = GetPinAnchor(targetPin, eCanvas);

            var points = FindPath(startAnchor, endAnchor, eCanvas.Bounds.Size);

            if (points.Any())
            {
                points.Insert(0, startAnchor);
                points.Add(endAnchor);

                var digitalComponentA = startPin.Parent.Parent.Parent.Parent.Parent.DataContext as DigitalComponent;
                var digitalComponentB = targetPin.Parent.Parent.Parent.Parent.Parent.DataContext as DigitalComponent;

                (targetPin.Parent.DataContext as Models.Pin).Name = (startPin.Parent.DataContext as Models.Pin).Name;

                // IMPORTANTE: Salva il percorso della nuova linea
                Project.SavedWirePaths.Add(new WirePaths()
                {
                    DigitalComponentConnection = new DigitalComponentConnections(
                        digitalComponentA,
                        (startPin.Parent.DataContext as Models.Pin).Index,
                        digitalComponentB,
                        (targetPin.Parent.DataContext as Models.Pin).Index),
                    Points = points,
                    Wire = tempWire
                });

                tempWire = BuildWire(points);
                //eCanvas.Children.Add(tempWire);
            }
            else
            {
                RemoveLineMenu(tempWire);
                eCanvas.Children.Remove(tempWire);
            }
        }
        else
        {
            RemoveLineMenu(tempWire);
            eCanvas.Children.Remove(tempWire);
        }

        ClearGrid(); // Debug

        tempWire = null;
        startPin = null;
        e.Pointer.Capture(null);
    }

    private bool[,] BuildGrid(Size canvasSize, bool debugGrid)
    {
        if (debugGrid)
            ClearGrid(); // Debug

        int cols = (int)(canvasSize.Width / GridStep);
        int rows = (int)(canvasSize.Height / GridStep);
        var grid = new bool[cols, rows];

        foreach (var comp in Project.Components)
        {
            var pos = comp.ComponentPosition;
            var size = comp.ComponentSize;

            int x1 = Math.Max(0, (int)(pos.X / GridStep));
            int y1 = Math.Max(0, (int)(pos.Y / GridStep));
            int x2 = Math.Min(cols - 1, (int)Math.Ceiling((pos.X + size.X) / GridStep) - 1);
            int y2 = Math.Min(rows - 1, (int)Math.Ceiling((pos.Y + size.Y) / GridStep) - 1);

            for (int x = x1; x <= x2; x++)
                for (int y = y1; y <= y2; y++)
                    grid[x, y] = true;
        }

        // 2. NUOVO: Marca tutte le linee salvate come ostacoli
        foreach (var wirePath in Project.SavedWirePaths)
        {
            foreach (var point in wirePath.Points)
            {
                int col = (int)(point.X / GridStep);
                int row = (int)(point.Y / GridStep);

                if (row < 0 || col < 0)
                    continue;

                if (col >= grid.GetLength(0) || row >= grid.GetLength(1))
                    continue;

                // Marca la cella e quelle adiacenti per creare una "zona buffer"
                /*for (int dx = 0; dx <= 0; dx++)
                {
                    for (int dy = 0; dy <= 0; dy++)
                    {
                        int x = col + dx;
                        int y = row + dy;
                        if (x >= 0 && x < cols && y >= 0 && y < rows)
                        {
                            grid[x, y] = true;
                        }
                    }
                }*/
                grid[col, row] = true;
            }
        }

        if (debugGrid)
            DrawGrid(grid, cols, rows); // Debug

        return grid;
    }

    private (int, int) GetFreeGridCell(bool[,] grid, Point p)
    {
        int col = (int)(p.X / GridStep);
        int row = (int)(p.Y / GridStep);

        if (row < 0 || col < 0)
            return (0, 0);

        if (col >= grid.GetLength(0) || row >= grid.GetLength(1))
            return (0, 0);

        if (!grid[col, row])
            return (col, row);

        var dirs = new (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((col, row));
        visited.Add((col, row));

        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            foreach (var d in dirs)
            {
                int nx = n.Item1 + d.Item1;
                int ny = n.Item2 + d.Item2;
                if (nx >= 0 && ny >= 0 && nx < grid.GetLength(0) && ny < grid.GetLength(1) && !visited.Contains((nx, ny)))
                {
                    if (!grid[nx, ny])
                        return (nx, ny);
                    queue.Enqueue((nx, ny));
                    visited.Add((nx, ny));
                }
            }
        }

        return (col, row);
    }

    public List<Point> FindPath(Point start, Point end, Size canvasSize, bool debugGrid = true)
    {
        var grid = BuildGrid(canvasSize, debugGrid);

        // Trova la cella libera più vicina fuori dagli ostacoli attorno a start e end
        var startGrid = GetFreeGridCell(grid, start);
        var endGrid = GetFreeGridCell(grid, end);

        // Questo punto è nel centro della cella libera
        var freeStart = new Point(startGrid.Item1 * GridStep + GridStep / 2, startGrid.Item2 * GridStep + GridStep / 2);
        var freeEnd = new Point(endGrid.Item1 * GridStep + GridStep / 2, endGrid.Item2 * GridStep + GridStep / 2);

        // Path A* SOLO da freeStart a freeEnd
        var points = FindPathNonInclusive(freeStart, freeEnd, canvasSize, grid);

        // Componi il percorso completo: dal vero pin a freeStart (segmento rettilineo), path A*, freeEnd a vero end (segmento rettilineo)
        var fullPath = new List<Point>();

        //if (points.Any()) // TODO solo per debug per tracciare linea libera
        {
            fullPath.Add(start);
            if (freeStart != start)
                fullPath.Add(freeStart);
            fullPath.AddRange(points.Where(p => p != freeStart && p != freeEnd));
            if (freeEnd != end)
                fullPath.Add(freeEnd);
            fullPath.Add(end);
        }

        return fullPath;
    }

    private List<Point> FindPathNonInclusive(Point start, Point end, Size canvasSize, bool[,] grid)
    {
        int cols = grid.GetLength(0);
        int rows = grid.GetLength(1);

        int colStart = (int)(start.X / GridStep);
        int rowStart = (int)(start.Y / GridStep);
        int colEnd = (int)(end.X / GridStep);
        int rowEnd = (int)(end.Y / GridStep);

        var open = new SortedSet<Node>(
            Comparer<Node>.Create((a, b) => {
                int cmp = a.F.CompareTo(b.F);
                if (cmp == 0) cmp = a.H.CompareTo(b.H);
                if (cmp == 0) cmp = a.X.CompareTo(b.X);
                if (cmp == 0) cmp = a.Y.CompareTo(b.Y);
                return cmp;
            }));

        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), int> { [(colStart, rowStart)] = 0 };

        int Heuristic((int X, int Y) n) => Math.Abs(n.X - colEnd) + Math.Abs(n.Y - rowEnd);
        open.Add(new Node(Heuristic((colStart, rowStart)), Heuristic((colStart, rowStart)), colStart, rowStart));

        while (open.Any())
        {
            var current = open.Min;
            open.Remove(current);
            var node = (current.X, current.Y);

            if (node == (colEnd, rowEnd))
            {
                // Path found! Ricostruiscilo.
                var path = new List<Point>();
                var n = node;
                while (cameFrom.ContainsKey(n))
                {
                    path.Add(new Point(n.Item1 * GridStep + GridStep / 2, n.Item2 * GridStep + GridStep / 2));
                    n = cameFrom[n];
                }
                // Aggiungi il punto di partenza
                path.Add(new Point(colStart * GridStep + GridStep / 2, rowStart * GridStep + GridStep / 2));
                path.Reverse();
                return path;
            }

            foreach (var dir in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var neigh = (node.Item1 + dir.Item1, node.Item2 + dir.Item2);
                if (neigh.Item1 < 0 || neigh.Item2 < 0 || neigh.Item1 >= cols || neigh.Item2 >= rows)
                    continue;
                if (grid[neigh.Item1, neigh.Item2])
                    continue;

                int tentativeG = gScore[node] + 1;
                if (!gScore.ContainsKey(neigh) || tentativeG < gScore[neigh])
                {
                    cameFrom[neigh] = node;
                    gScore[neigh] = tentativeG;
                    int h = Heuristic(neigh);
                    open.Add(new Node(tentativeG + h, h, neigh.Item1, neigh.Item2));
                }
            }
        }

        // Nessun percorso trovato!
        return new List<Point>();
    }

    public Path BuildWire(List<Point> points)
    {
        var fig = new PathFigure { StartPoint = points.First(), IsClosed = false };
        var seg = new PolyLineSegment();
        foreach (var p in points.Skip(1))
            seg.Points.Add(p);
        fig.Segments.Add(seg);

        var geo = new PathGeometry();
        geo.Figures.Add(fig);

        var path = new Path
        {
            Stroke = Brushes.Yellow,
            StrokeThickness = 2,
            Data = geo
        };

        return path;
    }


    // PropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}