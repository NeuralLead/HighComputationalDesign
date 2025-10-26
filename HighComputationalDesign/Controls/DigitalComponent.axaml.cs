using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CustomComponents;
using HighComputationalDesign.Models;
using HighComputationalDesign.Utils;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HighComputationalDesign;

[JsonConverter(typeof(JsonDigitalComponentDeserializer))]
public partial class DigitalComponent : UserControl, IDigitalComponent, INotifyPropertyChanged
{
    [JsonSave]
    [BrowsableAttribute(false)]
    public string Typized { get => this.GetType().FullName; }

    [JsonSave]
    public DigitalComponentInfo DigitalComponentInfo { get; set; } = new DigitalComponentInfo();

    public Point ComponentPosition { get => DigitalComponentInfo.ComponentPosition.Point; set { DigitalComponentInfo.ComponentPosition.Point = value; OnPropertyChanged(nameof(ComponentPosition)); UpdatePosition(); } }
    public Point ComponentSize { get => new Point(Bounds.Width, Bounds.Height); set { Width = value.X; Height = value.Y; } }

    /*private string _DigitalComponentName = "New digital component";
    public string DigitalComponentName { get => _DigitalComponentName; set { _DigitalComponentName = value; OnPropertyChanged(nameof(DigitalComponentName)); } }
    public string LogicSourceFile { get; set; } = string.Empty;
    public ObservableCollection<Models.Pin> PinsIn { get; set; } = new ObservableCollection<Models.Pin>();
    public ObservableCollection<Models.Pin> PinsOut { get; set; } = new ObservableCollection<Models.Pin>();*/

    private Designer _DesignerWindow;
    public Designer DesignerWindow {get => _DesignerWindow; set { _DesignerWindow = value; OnDesignerWindowSet?.Invoke(); } }
    protected event Action OnDesignerWindowSet;

    public SpiceSharp.Components.Component SpiceComponent { get; protected set; }
    
    public virtual object? GetPropertiesObject { get => DigitalComponentInfo; }

    public DigitalComponent()
    {
        Init();
    }

    public DigitalComponent(Designer designerWindow, string componentName)
    {
        DigitalComponentInfo.DigitalComponentName = componentName;
        this.DesignerWindow = designerWindow;

        Init();
    }

    private void Init()
    {
        InitializeComponent();

        this.DataContext = this;

        btnMove.AddHandler(
            InputElement.PointerPressedEvent,
            btnMove_Pressed,
            handledEventsToo: true
        );

        btnMove.AddHandler(
            InputElement.PointerReleasedEvent,
            btnMove_Release,
            handledEventsToo: true
        );
    }

    private bool _areInputPinsEditable = true;
    public bool AreInputPinsEditable
    {
        get => _areInputPinsEditable;
        set
        {
            _areInputPinsEditable = value;
            OnPropertyChanged(nameof(AreInputPinsEditable));
        }
    }
    
    private bool _areOutputPinsEditable = true;
    public bool AreOutputPinsEditable
    {
        get => _areOutputPinsEditable;
        set
        {
            _areOutputPinsEditable = value;
            OnPropertyChanged(nameof(AreOutputPinsEditable));
        }
    }

    private bool _IsCodeEditable = true;
    public bool IsCodeEditable
    {
        get => _IsCodeEditable;
        set
        {
            _IsCodeEditable = value;
            OnPropertyChanged(nameof(IsCodeEditable));
        }
    }

    public void UpdatePosition()
    {
        Canvas.SetLeft(this, ComponentPosition.X);
        Canvas.SetTop(this, ComponentPosition.Y);
    }

    public virtual void UpdateSpiceComponent()
    {
        if (SpiceComponent != null)
            (SpiceComponent as ThresholdComponent).OnChangedOutputPinState -= DigitalComponent_OnChangedOutputPinState;

        SpiceComponent = new ThresholdComponent(DigitalComponentInfo.DigitalComponentName, DigitalComponentInfo.PinsIn.Select(x => x.Name).ToArray(), DigitalComponentInfo.PinsOut.Select(x => x.Name).ToArray(), "0", DigitalComponentInfo.LogicSourceFile);

        (SpiceComponent as ThresholdComponent).OnChangedOutputPinState += DigitalComponent_OnChangedOutputPinState;
    }

    private void DigitalComponent_OnChangedOutputPinState(int x, bool s)
    {
        IImmutableSolidColorBrush brush = !s ? Brushes.Blue : Brushes.Red;

        Dispatcher.UIThread.Post(() =>
        {
            Label? labelO = GetLabelOPin_OutputPins(x);
            labelO.Foreground = brush;
            labelO.Background = brush;
        });

        Dispatcher.UIThread.Post(() =>
        {
            foreach (var a in DesignerWindow.Project.SavedWirePaths.Where(e => e.DigitalComponentConnection.DigitalComponentA == this && e.DigitalComponentConnection.NodeA == x))
                a.Wire.Stroke = brush;
        });
    }

    private void btnAddInputPin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DigitalComponentInfo.PinsIn.Add(new Models.Pin($"In{DigitalComponentInfo.PinsIn.Count}", DigitalComponentInfo.PinsIn.Count));
    }

    private void btnAddOutputPin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DigitalComponentInfo.PinsOut.Add(new Models.Pin($"Out{DigitalComponentInfo.PinsOut.Count}", DigitalComponentInfo.PinsOut.Count));
    }

    private void btnRemovePinIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Models.Pin? pinName = (sender as Button)?.DataContext as Models.Pin;

        foreach (var p in DesignerWindow.Project.SavedWirePaths)
        {
            if (p.DigitalComponentConnection.DigitalComponentB == this)
            {
                if (p.DigitalComponentConnection.NodeB > pinName.Index)
                    pinName.Index--;
            }
        }

        foreach (var t in DesignerWindow.Project.SavedWirePaths.Where(y => y.DigitalComponentConnection.DigitalComponentB == this && y.DigitalComponentConnection.NodeB == pinName.Index))
            DesignerWindow.eCanvas.Children.Remove(t.Wire);
        DesignerWindow.Project.SavedWirePaths.RemoveAll(y => y.DigitalComponentConnection.DigitalComponentB == this && y.DigitalComponentConnection.NodeB == pinName.Index);

        if ((sender as Button)?.Tag as string == "RemoveFromInputs")
            DigitalComponentInfo.PinsIn.RemoveAt(pinName.Index);
        else
            DigitalComponentInfo.PinsOut.RemoveAt(pinName.Index);
    }

    private void btnMove_Pressed(object? sender, PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            DesignerWindow.SetNewMovingComponent(this);
    }

    private void btnMove_Release(object? sender, PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        //if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            DesignerWindow.SetNewMovingComponent(null);
    }

    private void MenuItemRemove_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DesignerWindow.RemoveComponent(this);
    }

    private void MenuItemEditProperties_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DesignerWindow.SelectedObject = GetPropertiesObject;
    }

    private void Pin_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        DesignerWindow.Pin_PointerPressed(sender, e);
    }

    private void Pin_PointerMoved(object? sender, PointerEventArgs e)
    {
        DesignerWindow.Pin_PointerMoved(sender, e);
    }

    private void Pin_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        DesignerWindow.Pin_PointerReleased(sender, e);
    }

    private void ImageCode_Tapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DigitalComponentInfo.LogicSourceFile))
            DigitalComponentInfo.LogicSourceFile = DigitalComponentInfo.DigitalComponentName.Replace(" ", "_") + ".cs";

        var sourceCodeFile = DigitalComponentInfo.LogicSourceFile;

        if (!File.Exists(sourceCodeFile))
        {
            //File.Create(sourceCodeFile);
            File.WriteAllText(sourceCodeFile, @"// File Created with HighComputationalDesign in #date#

using CustomComponents;

static class ScriptState
{
    public static void OnUpdate(ThresholdBiasingBehavior Self)
    {

        bool v = Self.GetInputPin(0);
        Self.SetOutputPin(0, !v);
    }
}

".Replace("#date#", DateTime.Now.Year.ToString()));
        }

        Process.Start(new ProcessStartInfo(sourceCodeFile) { UseShellExecute = true });
    }

    internal Label? GetLabelOPin_InputPins(int pinIndex)
    {
        // Cerca tra gli Input
        if (pinIndex >= 0)
        {
            var container = UIItemsPinsIn.ItemContainerGenerator.ContainerFromIndex(pinIndex) as ContentPresenter;
            if (container != null)
            {
                // Cerco la Label col Tag="Input"
                var label = container.FindDescendantOfType<Label>();
                if (label != null && label.Tag?.ToString() == "Input")
                    return label;
            }
        }

        return null;
    }

    internal Label? GetLabelOPin_OutputPins(int pinIndex)
    {
        // Cerca tra gli Output
        if (pinIndex >= 0)
        {
            var container = UIItemsPinsOut.ItemContainerGenerator.ContainerFromIndex(pinIndex) as ContentPresenter;

            if (container != null)
            {
                // Cerco la Label col Tag="Output"
                var label = container.FindDescendantOfType<Label>();
                if (label != null && label.Tag?.ToString() == "Output")
                    return label;
            }
        }

        return null;
    }

    internal TextBox? GetInputTextPin(int pinIndex)
    {
        // Cerca tra gli Output
        if (pinIndex >= 0)
        {
            var container = UIItemsPinsOut.ItemContainerGenerator.ContainerFromIndex(pinIndex) as ContentPresenter;

            if (container != null)
            {
                // Cerco la Label col Tag="Output"
                var label = container.FindDescendantOfType<TextBox>();
                if (label != null)
                    return label;
            }
        }

        return null;
    }


    // PropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}