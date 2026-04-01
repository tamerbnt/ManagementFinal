using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Management.Presentation.Converters
{
    /// <summary>
    /// Premium Converter that turns a specific color (default: White) into Transparency.
    /// Used to make editorial line-art PNGs blend into colored backgrounds without "boxes".
    /// </summary>
    public class ColorToAlphaConverter : IValueConverter
    {
        public Color TargetColor { get; set; } = Colors.White;
        public int Threshold { get; set; } = 20; // Tolerance for "near-white"

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not BitmapSource source) return value;

            try
            {
                // Core Optimization: Convert to Bgr32 for direct buffer access
                var formatConverted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                int width = formatConverted.PixelWidth;
                int height = formatConverted.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];

                formatConverted.CopyPixels(pixels, stride, 0);

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];

                    // Check if pixel matches TargetColor within threshold
                    if (Math.Abs(r - TargetColor.R) < Threshold &&
                        Math.Abs(g - TargetColor.G) < Threshold &&
                        Math.Abs(b - TargetColor.B) < Threshold)
                    {
                        pixels[i + 3] = 0; // Set Alpha to 0 (Transparent)
                    }
                }

                // Create the processed bitmap
                var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY,
                    PixelFormats.Bgra32, null, pixels, stride);
                
                result.Freeze(); // Performance: Allow cross-thread usage
                return result;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[ColorToAlpha] Failed to process image transparency. Falling back to original.");
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
