using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace NeuroAccessMaui.UI.Controls
{
    #region Enums

    /// <summary>
    /// Specifies how the image should be cropped.
    /// </summary>
    public enum CropMode
    {
        /// <summary>
        /// The image is cropped to a square.
        /// </summary>
        Square,

        /// <summary>
        /// The image is cropped to a circle.
        /// </summary>
        Circle,

        /// <summary>
        /// The image is cropped to a rectangle using the aspect ratio of <see cref="ImageCropperView.OutputMaxResolution"/>.
        /// </summary>
        Aspect
    }

    /// <summary>
    /// Specifies the output format of the cropped image.
    /// </summary>
    public enum CropOutputFormat
    {
        /// <summary>
        /// Encode the image as PNG (lossless).
        /// </summary>
        Png,

        /// <summary>
        /// Encode the image as JPEG (lossy).
        /// </summary>
        Jpeg
    }

    #endregion

    /// <summary>
    /// A custom view that displays an image with pinch-zoom, pan, and rotation support,
    /// and allows cropping based on a defined <see cref="CropMode"/>. The crop shape remains
    /// centered in the view with an optional margin.
    /// </summary>
    public class ImageCropperView : ContentView
    {
#warning Replace this with InternetContent
        /// <summary>
        /// A shared HttpClient for downloading images.
        /// </summary>
        private static readonly HttpClient sHttpClient = new HttpClient();

        #region Bindable Properties

        /// <summary>
        /// Backing store for <see cref="OutputMaxResolution"/>.
        /// </summary>
        public static readonly BindableProperty OutputMaxResolutionProperty = BindableProperty.Create(
            propertyName: nameof(OutputMaxResolution),
            returnType: typeof(Size),
            declaringType: typeof(ImageCropperView),
            defaultValue: new Size(800, 800));

        /// <summary>
        /// Gets or sets the maximum resolution for the cropped output image.
        /// </summary>
        public Size OutputMaxResolution
        {
            get => (Size)this.GetValue(OutputMaxResolutionProperty);
            set => this.SetValue(OutputMaxResolutionProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="InputMaxResolution"/>.
        /// </summary>
        public static readonly BindableProperty InputMaxResolutionProperty = BindableProperty.Create(
            propertyName: nameof(InputMaxResolution),
            returnType: typeof(Size),
            declaringType: typeof(ImageCropperView),
            defaultValue: new Size(3840, 2160));

        /// <summary>
        /// Gets or sets the maximum resolution for the input image.
        /// </summary>
        public Size InputMaxResolution
        {
            get => (Size)this.GetValue(InputMaxResolutionProperty);
            set => this.SetValue(InputMaxResolutionProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="ImageSource"/>.
        /// </summary>
        public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(
            propertyName: nameof(ImageSource),
            returnType: typeof(ImageSource),
            declaringType: typeof(ImageCropperView),
            defaultValue: null,
            propertyChanged: OnImageSourceChanged);

        /// <summary>
        /// Gets or sets the image source to display and crop.
        /// </summary>
        public ImageSource ImageSource
        {
            get => (ImageSource)this.GetValue(ImageSourceProperty);
            set => this.SetValue(ImageSourceProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="CropMode"/>.
        /// </summary>
        public static readonly BindableProperty CropModeProperty = BindableProperty.Create(
            propertyName: nameof(CropMode),
            returnType: typeof(CropMode),
            declaringType: typeof(ImageCropperView),
            defaultValue: CropMode.Square);

        /// <summary>
        /// Gets or sets the crop mode.
        /// </summary>
        public CropMode CropMode
        {
            get => (CropMode)this.GetValue(CropModeProperty);
            set => this.SetValue(CropModeProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="OutputFormat"/>.
        /// </summary>
        public static readonly BindableProperty OutputFormatProperty = BindableProperty.Create(
            propertyName: nameof(OutputFormat),
            returnType: typeof(CropOutputFormat),
            declaringType: typeof(ImageCropperView),
            defaultValue: CropOutputFormat.Png);

        /// <summary>
        /// Gets or sets the output image format.
        /// </summary>
        public CropOutputFormat OutputFormat
        {
            get => (CropOutputFormat)this.GetValue(OutputFormatProperty);
            set => this.SetValue(OutputFormatProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="JpegQuality"/>.
        /// </summary>
        public static readonly BindableProperty JpegQualityProperty = BindableProperty.Create(
            propertyName: nameof(JpegQuality),
            returnType: typeof(int),
            declaringType: typeof(ImageCropperView),
            defaultValue: 90);

        /// <summary>
        /// Gets or sets the JPEG quality when encoding as JPEG.
        /// </summary>
        public int JpegQuality
        {
            get => (int)this.GetValue(JpegQualityProperty);
            set => this.SetValue(JpegQualityProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="CropShapeFillPortion"/>.
        /// </summary>
        public static readonly BindableProperty CropShapeFillPortionProperty = BindableProperty.Create(
            propertyName: nameof(CropShapeFillPortion),
            returnType: typeof(float),
            declaringType: typeof(ImageCropperView),
            defaultValue: 0.9f);

        /// <summary>
        /// Gets or sets the fraction of the view filled by the crop shape.
        /// </summary>
        public float CropShapeFillPortion
        {
            get => (float)this.GetValue(CropShapeFillPortionProperty);
            set => this.SetValue(CropShapeFillPortionProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="RotationAngle"/>.
        /// </summary>
        public static readonly BindableProperty RotationAngleProperty = BindableProperty.Create(
            propertyName: nameof(RotationAngle),
            returnType: typeof(double),
            declaringType: typeof(ImageCropperView),
            defaultValue: 0.0,
            propertyChanged: OnRotationAngleChanged);

        /// <summary>
        /// Gets or sets the rotation angle (in degrees) for the image.
        /// </summary>
        public double RotationAngle
        {
            get => (double)this.GetValue(RotationAngleProperty);
            set => this.SetValue(RotationAngleProperty, value);
        }

        /// <summary>
        /// Backing store for <see cref="LimitToBounds"/>.
        /// </summary>
        public static readonly BindableProperty LimitToBoundsProperty = BindableProperty.Create(
        propertyName: nameof(LimitToBounds),
        returnType: typeof(bool),
        declaringType: typeof(ImageCropperView),
        defaultValue: true);

        /// <summary>
        /// When true, prevents moving or zooming the image so that it does not fully cover the crop area.
        /// </summary>
        public bool LimitToBounds
        {
            get => (bool)this.GetValue(LimitToBoundsProperty);
            set => this.SetValue(LimitToBoundsProperty, value);
        }

        #endregion

        #region Instance Fields

        /// <summary>
        /// Current scale factor for the image.
        /// </summary>
        private float scale = 1f;

        /// <summary>
        /// Current horizontal offset for the image.
        /// </summary>
        private float offsetX;

        /// <summary>
        /// Current vertical offset for the image.
        /// </summary>
        private float offsetY;

        /// <summary>
        /// Starting scale for pinch gestures.
        /// </summary>
        private float startScale = 1f;

        /// <summary>
        /// Starting horizontal offset for pinch gestures.
        /// </summary>
        private float startOffsetX;

        /// <summary>
        /// Starting vertical offset for pinch gestures.
        /// </summary>
        private float startOffsetY;

        /// <summary>
        /// The pivot used during pinch gestures (normalized to [0, 1]).
        /// </summary>
        private Point pinchPivot = new Point(0.5, 0.5);

        /// <summary>
        /// The loaded SKBitmap.
        /// </summary>
        private SKBitmap? bitmap;

        /// <summary>
        /// The SkiaSharp canvas view used for rendering.
        /// </summary>
        private readonly SKCanvasView canvasView;

        /// <summary>
        /// Indicates whether the initial positioning of the image has been set.
        /// </summary>
        private bool initialPositionSet;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageCropperView"/> class.
        /// </summary>
        public ImageCropperView()
        {
            // Create a root grid layout.
            Grid root = new Grid
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };

            // Initialize the canvas view.
            this.canvasView = new SKCanvasView
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };

            // Set up pan and pinch gesture recognizers.
            PanGestureRecognizer panGesture = new() { TouchPoints = 1 };
            panGesture.PanUpdated += this.OnPan;

            PinchGestureRecognizer pinchGesture = new();
            pinchGesture.PinchUpdated += this.OnPinch;

            this.canvasView.GestureRecognizers.Add(panGesture);
            this.canvasView.GestureRecognizers.Add(pinchGesture);

            // Subscribe to the canvas paint event.
            this.canvasView.PaintSurface += this.OnPaintSurface; // No need to unregister as the canvasView has the same lifetime

            root.Add(this.canvasView);
            this.Content = root;
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Resets the image transformations (pan, zoom, rotation) to their default values.
        /// </summary>
        public void ResetTransformations()
        {
            this.scale = 1f;
            this.offsetX = 0f;
            this.offsetY = 0f;
            this.startScale = 1f;
            this.startOffsetX = 0f;
            this.startOffsetY = 0f;
            this.canvasView.InvalidateSurface();
        }

        /// <summary>
        /// Performs cropping of the image using the current pan, zoom, and rotation transformations.
        /// The resulting cropped image is resized to <see cref="OutputMaxResolution"/>.
        /// </summary>
        /// <returns>A task that returns the cropped image as a byte array, or null if cropping fails.</returns>
        public byte[]? PerformCrop()
        {
            try
            {
                if (this.bitmap == null)
                    return null;

                int viewWidth = (int)this.canvasView.CanvasSize.Width;
                int viewHeight = (int)this.canvasView.CanvasSize.Height;
                if (viewWidth <= 0 || viewHeight <= 0)
                    return null;

                // Render the transformed image to a temporary surface.
                using SKBitmap tempSurfaceBitmap = new SKBitmap(viewWidth, viewHeight, isOpaque: false);
                using SKCanvas tempCanvas = new SKCanvas(tempSurfaceBitmap);
                tempCanvas.Clear(SKColors.Transparent);

                float centerX = viewWidth / 2f;
                float centerY = viewHeight / 2f;

                tempCanvas.Save();
                tempCanvas.Translate(this.offsetX, this.offsetY);
                tempCanvas.Scale(this.scale);
                tempCanvas.Translate(centerX, centerY);
                tempCanvas.RotateDegrees((float)this.RotationAngle);
                tempCanvas.Translate(-centerX, -centerY);

                SKRect destRect = new SKRect(0, 0, this.bitmap.Width, this.bitmap.Height);
                tempCanvas.DrawBitmap(this.bitmap, destRect);
                tempCanvas.Restore();

                // Define the crop shape.
                SKRect cropRect = this.ComputeCropShapeRect(viewWidth, viewHeight, this.CropShapeFillPortion);
                SKPath cropPath = new SKPath();
                switch (this.CropMode)
                {
                    case CropMode.Circle:
                        {
                            float radius = cropRect.Width / 2f;
                            float cx = cropRect.MidX;
                            float cy = cropRect.MidY;
                            cropPath.AddCircle(cx, cy, radius);
                            break;
                        }
                    case CropMode.Square:
                    case CropMode.Aspect:
                        cropPath.AddRect(cropRect);
                        break;
                }

                // Apply the crop mask.
                using SKBitmap maskedBitmap = new SKBitmap(viewWidth, viewHeight, isOpaque: false);
                using (SKCanvas maskedCanvas = new SKCanvas(maskedBitmap))
                {
                    maskedCanvas.Clear(SKColors.Transparent);
                    maskedCanvas.ClipPath(cropPath, SKClipOperation.Intersect, true);

                    maskedCanvas.Save();
                    maskedCanvas.Translate(this.offsetX, this.offsetY);
                    maskedCanvas.Scale(this.scale);
                    maskedCanvas.Translate(centerX, centerY);
                    maskedCanvas.RotateDegrees((float)this.RotationAngle);
                    maskedCanvas.Translate(-centerX, -centerY);
                    maskedCanvas.DrawBitmap(this.bitmap, destRect);
                    maskedCanvas.Restore();
                }

                // Extract the bounding box of the crop area.
                SKRectI boundingBox = SKRectI.Round(cropRect);
                using SKBitmap Cropped = new SKBitmap(boundingBox.Width, boundingBox.Height);
                using (SKCanvas CroppedCanvas = new SKCanvas(Cropped))
                {
                    SKRect dst = new SKRect(0, 0, boundingBox.Width, boundingBox.Height);
                    CroppedCanvas.DrawBitmap(maskedBitmap, boundingBox, dst);
                }

                // Resize the cropped bitmap if needed.
                SKBitmap FinalBitmap = ResizeBitmapIfNeeded(Cropped,
                    (int)this.OutputMaxResolution.Width,
                    (int)this.OutputMaxResolution.Height);

                // Encode the final bitmap to the specified format.
                using MemoryStream EncodingStream = new MemoryStream();
                if (this.OutputFormat == CropOutputFormat.Png)
                {
                    FinalBitmap.Encode(EncodingStream, SKEncodedImageFormat.Png, 100);
                }
                else
                {
                    int Quality = Math.Max(0, Math.Min(100, this.JpegQuality));
                    FinalBitmap.Encode(EncodingStream, SKEncodedImageFormat.Jpeg, Quality);
                }
                return EncodingStream?.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cropping: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Gesture Handlers

        /// <summary>
        /// Handles pan gestures to update the image offset.
        /// </summary>
        /// <param name="sender">The gesture recognizer.</param>
        /// <param name="e">Pan gesture event arguments.</param>
        private void OnPan(object? sender, PanUpdatedEventArgs e)
        {
            double density = DeviceDisplay.MainDisplayInfo.Density;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    this.startOffsetX = this.offsetX;
                    this.startOffsetY = this.offsetY;
                    break;
                case GestureStatus.Running:
                    this.offsetX = this.startOffsetX + (float)(e.TotalX * density);
                    this.offsetY = this.startOffsetY + (float)(e.TotalY * density);
                    if (this.LimitToBounds)
                    {
                        this.ClampTransformations();
                    }
                    this.canvasView.InvalidateSurface();
                    break;
            }
        }

        /// <summary>
        /// Handles pinch gestures to update the image scale and adjust the offset so that the pinch pivot remains stationary.
        /// </summary>
        /// <param name="sender">The gesture recognizer.</param>
        /// <param name="e">Pinch gesture event arguments.</param>
        private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
        {
            switch (e.Status)
            {
                case GestureStatus.Started:
                    // Record the current offsets and the pinch pivot.
                    this.startOffsetX = this.offsetX;
                    this.startOffsetY = this.offsetY;
                    this.pinchPivot = e.ScaleOrigin;
                    break;
                case GestureStatus.Running:
                    // Use the incremental scale factor directly.
                    float delta = (float)e.Scale; // e.Scale is the incremental factor.

                    // Update the cumulative scale.
                    this.scale *= delta;

                    // Convert the pinch pivot (relative coordinates) to actual pixels.
                    float pivotX = (float)(this.pinchPivot.X * this.canvasView.CanvasSize.Width);
                    float pivotY = (float)(this.pinchPivot.Y * this.canvasView.CanvasSize.Height);

                    // Adjust offsets to keep the pivot point stable.
                    this.offsetX = pivotX - (pivotX - this.offsetX) * delta;
                    this.offsetY = pivotY - (pivotY - this.offsetY) * delta;

                    if (this.LimitToBounds)
                    {
                        this.ClampTransformations();
                    }
                    this.canvasView.InvalidateSurface();
                    break;
                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    break;
            }
        }

        #endregion

        #region Paint & Layout

        /// <summary>
        /// Handles the painting of the canvas by drawing the transformed image and overlaying the crop mask.
        /// </summary>
        /// <param name="sender">The canvas view.</param>
        /// <param name="e">The paint surface event arguments.</param>
        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (this.bitmap == null)
                return;

            float centerX = e.Info.Width / 2f;
            float centerY = e.Info.Height / 2f;

            canvas.Save();
            canvas.Translate(this.offsetX, this.offsetY);
            canvas.Scale(this.scale);
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees((float)this.RotationAngle);
            canvas.Translate(-centerX, -centerY);
            SKRect destRect = new SKRect(0, 0, this.bitmap.Width, this.bitmap.Height);
            canvas.DrawBitmap(this.bitmap, destRect);
            canvas.Restore();

            SKRect shapeRect = this.ComputeCropShapeRect(e.Info.Width, e.Info.Height, this.CropShapeFillPortion);
            SKPath path = new SKPath();
            switch (this.CropMode)
            {
                case CropMode.Circle:
                    {
                        float radius = shapeRect.Width / 2f;
                        float cx = shapeRect.MidX;
                        float cy = shapeRect.MidY;
                        path.AddCircle(cx, cy, radius);
                        break;
                    }
                case CropMode.Square:
                case CropMode.Aspect:
                    path.AddRect(shapeRect);
                    break;
            }

            using (SKPaint overlayPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 100),
                Style = SKPaintStyle.Fill
            })
            {
                canvas.Save();
                canvas.ClipPath(path, SKClipOperation.Difference, true);
                canvas.DrawRect(new SKRect(0, 0, e.Info.Width, e.Info.Height), overlayPaint);
                canvas.Restore();
            }

            using (SKPaint outlinePaint = new SKPaint
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                IsAntialias = true
            })
            {
                canvas.DrawPath(path, outlinePaint);
            }
        }

        /// <summary>
        /// Overrides the OnSizeAllocated method to set the initial image position when the view is first laid out.
        /// </summary>
        /// <param name="width">The width of the view in device independent pixels (DIPs).</param>
        /// <param name="height">The height of the view in DIPs.</param>
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (!this.initialPositionSet && width > 0 && height > 0 && this.bitmap != null)
            {
                this.SetInitialImagePosition(width, height);
                this.initialPositionSet = true;
                this.canvasView.InvalidateSurface();
            }
        }

        /// <summary>
        /// Sets the initial image position and scale so that the image covers the crop area.
        /// </summary>
        /// <param name="width">The width of the view in DIPs.</param>
        /// <param name="height">The height of the view in DIPs.</param>
        private void SetInitialImagePosition(double width, double height)
        {
            double density = DeviceDisplay.MainDisplayInfo.Density;
            float viewWidthPx = (float)(width * density);
            float viewHeightPx = (float)(height * density);

            SKRect cropShapeRect = this.ComputeCropShapeRect(viewWidthPx, viewHeightPx, this.CropShapeFillPortion);
            float cropCenterX = cropShapeRect.MidX;
            float cropCenterY = cropShapeRect.MidY;

            float scaleX = cropShapeRect.Width / this.bitmap!.Width;
            float scaleY = cropShapeRect.Height / this.bitmap.Height;
            float newScale = MathF.Max(scaleX, scaleY);
            this.scale = newScale;

            float scaledWidth = this.bitmap.Width * newScale;
            float scaledHeight = this.bitmap.Height * newScale;

            this.offsetX = cropCenterX - (scaledWidth / 2f);
            this.offsetY = cropCenterY - (scaledHeight / 2f);
        }

        /// <summary>
        /// Computes the rectangle for the crop shape based on the container size and margin factor.
        /// </summary>
        /// <param name="containerWidth">The width of the container in pixels.</param>
        /// <param name="containerHeight">The height of the container in pixels.</param>
        /// <param name="marginFactor">The fraction of the container to be used for the crop shape.</param>
        /// <returns>An <see cref="SKRect"/> defining the crop area.</returns>
        private SKRect ComputeCropShapeRect(float containerWidth, float containerHeight, float marginFactor)
        {
            marginFactor = MathF.Max(0.0f, MathF.Min(1.0f, marginFactor));

            switch (this.CropMode)
            {
                case CropMode.Circle:
                case CropMode.Square:
                    {
                        float minDim = MathF.Min(containerWidth, containerHeight);
                        float shapeSize = minDim * marginFactor;
                        float left = (containerWidth - shapeSize) / 2f;
                        float top = (containerHeight - shapeSize) / 2f;
                        return new SKRect(left, top, left + shapeSize, top + shapeSize);
                    }
                case CropMode.Aspect:
                    {
                        float targetRatio = 1.0f;
                        if (this.OutputMaxResolution.Height > 0)
                        {
                            targetRatio = (float)this.OutputMaxResolution.Width / (float)this.OutputMaxResolution.Height;
                        }
                        float shapeWidth = containerWidth * marginFactor;
                        float shapeHeight = shapeWidth / targetRatio;
                        if (shapeHeight > containerHeight * marginFactor)
                        {
                            shapeHeight = containerHeight * marginFactor;
                            shapeWidth = shapeHeight * targetRatio;
                        }
                        float left = (containerWidth - shapeWidth) / 2f;
                        float top = (containerHeight - shapeHeight) / 2f;
                        return new SKRect(left, top, left + shapeWidth, top + shapeHeight);
                    }
            }
            return SKRect.Empty;
        }

        #endregion

        #region Bitmap Loading & Resizing

        /// <summary>
        /// Called when the <see cref="RotationAngle"/> property changes.
        /// </summary>
        /// <param name="bindable">The bindable object.</param>
        /// <param name="oldValue">The previous rotation angle.</param>
        /// <param name="newValue">The new rotation angle.</param>
        private static void OnRotationAngleChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ImageCropperView cropper)
            {
                cropper.ClampTransformations();
                cropper.canvasView.InvalidateSurface();
            }
        }

        /// <summary>
        /// Called when the <see cref="ImageSource"/> property changes.
        /// Loads and resizes the image from the new source.
        /// </summary>
        /// <param name="bindable">The bindable object.</param>
        /// <param name="oldValue">The previous image source.</param>
        /// <param name="newValue">The new image source.</param>
        private static async void OnImageSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ImageCropperView cropper && newValue is ImageSource newSource)
            {
                cropper.bitmap?.Dispose();
                cropper.bitmap = await LoadAndResizeBitmapAsync(newSource, cropper.InputMaxResolution);
                // Optionally, reset transformations:
                // cropper.ResetTransformations();

                cropper.initialPositionSet = false;

                if (cropper.Width > 0 && cropper.Height > 0)
                {
                    cropper.SetInitialImagePosition(cropper.Width, cropper.Height);
                    cropper.initialPositionSet = true;
                }
                else
                {
                    cropper.initialPositionSet = false;
                }

                cropper.canvasView.InvalidateSurface();
            }
        }

        /// <summary>
        /// Loads and resizes the bitmap from the specified image source.
        /// </summary>
        /// <param name="imageSource">The source of the image.</param>
        /// <param name="maxResolution">The maximum allowed resolution for the image.</param>
        /// <returns>A task that returns the loaded <see cref="SKBitmap"/>, or null if loading fails.</returns>
        private static async Task<SKBitmap?> LoadAndResizeBitmapAsync(ImageSource imageSource, Size maxResolution)
        {
            try
            {
                StreamImageSource? streamImageSource = null;
                switch (imageSource)
                {
                    case FileImageSource fileSource:
                        if (File.Exists(fileSource.File))
                        {
                            streamImageSource = new StreamImageSource
                            {
                                Stream = _ => Task.FromResult<Stream>(File.OpenRead(fileSource.File))
                            };
                        }
                        break;
                    case UriImageSource uriSource:
                        {
                            HttpResponseMessage response = await sHttpClient.GetAsync(uriSource.Uri);
                            if (response.IsSuccessStatusCode)
                            {
                                MemoryStream ms = new MemoryStream();
                                await response.Content.CopyToAsync(ms);
                                ms.Position = 0;
                                streamImageSource = new StreamImageSource
                                {
                                    Stream = _ => Task.FromResult<Stream>(ms)
                                };
                            }
                            break;
                        }
                    case StreamImageSource sis:
                        streamImageSource = sis;
                        break;
                }

                if (streamImageSource == null)
                    return null;

                Stream stream = await streamImageSource.Stream(CancellationToken.None).ConfigureAwait(false);
                if (stream == null)
                    return null;

                using MemoryStream managedStream = new MemoryStream();
                await stream.CopyToAsync(managedStream).ConfigureAwait(false);
                managedStream.Position = 0;

                SKBitmap loadedBitmap = SKBitmap.Decode(managedStream);
                if (loadedBitmap == null)
                    return null;

                SKBitmap resized = ResizeBitmapIfNeeded(loadedBitmap,
                    (int)maxResolution.Width,
                    (int)maxResolution.Height);

                if (resized != loadedBitmap)
                {
                    loadedBitmap.Dispose();
                }
                return resized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading bitmap: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resizes the provided bitmap if it exceeds the specified maximum dimensions.
        /// </summary>
        /// <param name="sourceBitmap">The source bitmap.</param>
        /// <param name="maxWidth">The maximum allowed width.</param>
        /// <param name="maxHeight">The maximum allowed height.</param>
        /// <returns>
        /// The original bitmap if no resizing is needed; otherwise, a new resized <see cref="SKBitmap"/>.
        /// </returns>
        private static SKBitmap ResizeBitmapIfNeeded(SKBitmap sourceBitmap, int maxWidth, int maxHeight)
        {
            if (sourceBitmap is null || maxWidth <= 0 || maxHeight <= 0)
                return new SKBitmap(1, 1);

            int width = sourceBitmap.Width;
            int height = sourceBitmap.Height;

            if (width <= maxWidth && height <= maxHeight)
            {
                return sourceBitmap;
            }

            float widthRatio = (float)maxWidth / width;
            float heightRatio = (float)maxHeight / height;
            float scale = Math.Min(widthRatio, heightRatio);

            int newWidth = (int)(width * scale);
            int newHeight = (int)(height * scale);

            SKBitmap resized = new SKBitmap(newWidth, newHeight, sourceBitmap.ColorType, sourceBitmap.AlphaType);
            using SKCanvas canvas = new SKCanvas(resized);
            using SKPaint paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true
            };

            SKRect srcRect = new SKRect(0, 0, width, height);
            SKRect destRect = new SKRect(0, 0, newWidth, newHeight);
            canvas.DrawBitmap(sourceBitmap, srcRect, destRect, paint);

            return resized;
        }

        #endregion

        /// <summary>
        /// Clamps the current offset and scale so that the transformed image fully covers the crop area.
        /// This method enforces a minimum zoom level (scale) and adjusts the translation (offset) accordingly.
        /// </summary>
        private void ClampTransformations()
        {
            if (!this.LimitToBounds || this.bitmap == null)
                return;

            SKSize canvasSize = this.canvasView.CanvasSize;
            if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
                return;

            float canvasWidth = canvasSize.Width;
            float canvasHeight = canvasSize.Height;
            // Compute the crop rectangle (in canvas coordinates).
            SKRect cropRect = this.ComputeCropShapeRect(canvasWidth, canvasHeight, this.CropShapeFillPortion);

            // -----------------------------------------------------
            // 1. Enforce a Minimum Scale (Zoom)
            // -----------------------------------------------------
            // When the image is rotated, its axis-aligned bounding box is larger than the original.
            // Compute the bounding box dimensions of the image after rotation (at scale = 1).
            float angle = (float)this.RotationAngle;
            float rad = angle * (MathF.PI / 180f);
            float absCos = MathF.Abs(MathF.Cos(rad));
            float absSin = MathF.Abs(MathF.Sin(rad));

            float rotatedWidth = this.bitmap.Width * absCos + this.bitmap.Height * absSin;
            float rotatedHeight = this.bitmap.Width * absSin + this.bitmap.Height * absCos;

            // Compute the minimal scale needed so that the rotated image’s bounding box covers the cropRect.
            float minScaleX = cropRect.Width / rotatedWidth;
            float minScaleY = cropRect.Height / rotatedHeight;
            float minScale = MathF.Max(minScaleX, minScaleY);

            // Enforce the minimum scale.
            if (this.scale < minScale)
            {
                this.scale = minScale;
            }

            // -----------------------------------------------------
            // 2. Adjust the Offset (Translation)
            // -----------------------------------------------------
            // In OnPaintSurface the image is drawn with:
            //   canvas.Translate(offsetX, offsetY);
            //   canvas.Scale(scale);
            //   canvas.Translate(centerX, centerY);
            //   canvas.RotateDegrees(RotationAngle);
            //   canvas.Translate(-centerX, -centerY);
            //
            // This is equivalent to transforming any image point p as:
            //   p' = offset + scale * (center + R*(p - center))
            // where the canvas center is:
            SKPoint center = new SKPoint(canvasWidth / 2, canvasHeight / 2);

            // Compute the transformed (but not offset-adjusted) positions of the image corners.
            float localMinX = float.MaxValue;
            float localMaxX = float.MinValue;
            float localMinY = float.MaxValue;
            float localMaxY = float.MinValue;

            // The four corners of the original image.
            SKPoint[] corners =
			[
                new SKPoint(0, 0),
                new SKPoint(this.bitmap.Width, 0),
                new SKPoint(this.bitmap.Width, this.bitmap.Height),
                new SKPoint(0, this.bitmap.Height)
            ];

            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            foreach (SKPoint p in corners)
            {
                // Compute the vector from the canvas center.
                SKPoint diff = new SKPoint(p.X - center.X, p.Y - center.Y);
                // Rotate the vector.
                SKPoint rotated = new SKPoint(diff.X * cos - diff.Y * sin,
                                              diff.X * sin + diff.Y * cos);
                // Apply scaling and add back the canvas center.
                SKPoint transformedLocal = new SKPoint(
                    this.scale * (center.X + rotated.X),
                    this.scale * (center.Y + rotated.Y)
                );

                localMinX = MathF.Min(localMinX, transformedLocal.X);
                localMaxX = MathF.Max(localMaxX, transformedLocal.X);
                localMinY = MathF.Min(localMinY, transformedLocal.Y);
                localMaxY = MathF.Max(localMaxY, transformedLocal.Y);
            }

            // The final (drawn) image bounds are the local bounds shifted by the offset.
            float imageMinX = this.offsetX + localMinX;
            float imageMaxX = this.offsetX + localMaxX;
            float imageMinY = this.offsetY + localMinY;
            float imageMaxY = this.offsetY + localMaxY;

            // Determine the adjustments needed so that the image bounds fully cover the crop area.
            float deltaX = 0;
            float deltaY = 0;

            // If the image is too far right (its left edge is right of the crop's left), shift left.
            if (imageMinX > cropRect.Left)
            {
                deltaX = cropRect.Left - imageMinX;
            }
            // Or if the image is too far left (its right edge is left of the crop's right), shift right.
            else if (imageMaxX < cropRect.Right)
            {
                deltaX = cropRect.Right - imageMaxX;
            }

            // Do the same for vertical adjustment.
            if (imageMinY > cropRect.Top)
            {
                deltaY = cropRect.Top - imageMinY;
            }
            else if (imageMaxY < cropRect.Bottom)
            {
                deltaY = cropRect.Bottom - imageMaxY;
            }

            this.offsetX += deltaX;
            this.offsetY += deltaY;
        }


    }
}
