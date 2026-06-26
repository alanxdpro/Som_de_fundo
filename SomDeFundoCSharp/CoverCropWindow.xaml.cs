using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SomDeFundoCSharp;

public partial class CoverCropWindow : Window
{
    private const double PreviewSize = 320;

    private readonly string _sourcePath;
    private readonly string _targetDirectory;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private double _baseScale;
    private double _zoom = 1;
    private double _offsetX;
    private double _offsetY;
    private bool _isDragging;
    private Point _lastDragPoint;

    public string? SavedCoverPath { get; private set; }

    public CoverCropWindow(string sourcePath, string targetDirectory)
    {
        InitializeComponent();
        _sourcePath = sourcePath;
        _targetDirectory = targetDirectory;

        CoverPreview preview = CoverImageService.LoadCropPreview(sourcePath);
        _sourceWidth = preview.PixelWidth;
        _sourceHeight = preview.PixelHeight;
        PreviewImage.Source = preview.Source;

        ResetView();
    }

    private void ResetView()
    {
        _zoom = 1;
        ZoomSlider.Value = 1;
        _baseScale = Math.Max(PreviewSize / _sourceWidth, PreviewSize / _sourceHeight);
        CenterImage();
        UpdateImagePlacement();
    }

    private void CenterImage()
    {
        double scale = CurrentScale;
        _offsetX = (PreviewSize - (_sourceWidth * scale)) / 2;
        _offsetY = (PreviewSize - (_sourceHeight * scale)) / 2;
        ClampOffsets();
    }

    private double CurrentScale => _baseScale * _zoom;

    private void UpdateImagePlacement()
    {
        double scale = CurrentScale;
        PreviewImage.Width = _sourceWidth * scale;
        PreviewImage.Height = _sourceHeight * scale;
        Canvas.SetLeft(PreviewImage, _offsetX);
        Canvas.SetTop(PreviewImage, _offsetY);
        if (ZoomText is not null)
        {
            ZoomText.Text = $"{(int)Math.Round(_zoom * 100)}%";
        }
    }

    private void ClampOffsets()
    {
        double width = _sourceWidth * CurrentScale;
        double height = _sourceHeight * CurrentScale;

        _offsetX = width <= PreviewSize
            ? (PreviewSize - width) / 2
            : Math.Clamp(_offsetX, PreviewSize - width, 0);

        _offsetY = height <= PreviewSize
            ? (PreviewSize - height) / 2
            : Math.Clamp(_offsetY, PreviewSize - height, 0);
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreviewImage is null || ZoomText is null || _sourceWidth <= 0 || _sourceHeight <= 0)
        {
            return;
        }

        double oldScale = CurrentScale;
        Point center = new(PreviewSize / 2, PreviewSize / 2);
        double sourceCenterX = (center.X - _offsetX) / oldScale;
        double sourceCenterY = (center.Y - _offsetY) / oldScale;

        _zoom = Math.Clamp(e.NewValue, 1, 4);
        double newScale = CurrentScale;
        _offsetX = center.X - (sourceCenterX * newScale);
        _offsetY = center.Y - (sourceCenterY * newScale);
        ClampOffsets();
        UpdateImagePlacement();
    }

    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastDragPoint = e.GetPosition(PreviewCanvas);
        PreviewCanvas.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        Point currentPoint = e.GetPosition(PreviewCanvas);
        Vector delta = currentPoint - _lastDragPoint;
        _offsetX += delta.X;
        _offsetY += delta.Y;
        _lastDragPoint = currentPoint;
        ClampOffsets();
        UpdateImagePlacement();
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        PreviewCanvas.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private void PreviewCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double nextZoom = ZoomSlider.Value + (e.Delta > 0 ? 0.1 : -0.1);
        ZoomSlider.Value = Math.Clamp(nextZoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetView();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double scale = CurrentScale;
            double sourceSize = PreviewSize / scale;
            double sourceX = -_offsetX / scale;
            double sourceY = -_offsetY / scale;
            SavedCoverPath = CoverImageService.CropResizeAndSave(
                new CoverCropRequest(_sourcePath, sourceX, sourceY, sourceSize),
                _targetDirectory);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ajustar capa", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
