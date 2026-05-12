using SixLabors.ImageSharp.PixelFormats;
using SystemPoint = System.Drawing.Point;
using SharpImage = SixLabors.ImageSharp.Image;

namespace TargetScoreScanner.Services;

/// <summary>
/// Compares two images pixel-by-pixel in pure C# using SixLabors.ImageSharp.
/// </summary>
public class ImageComparisonService
{
    /// <summary>
    /// Compares two images loaded from the provided streams.
    /// </summary>
    /// <param name="stream1">Stream of the first (reference) image.</param>
    /// <param name="stream2">Stream of the second (modified) image.</param>
    /// <param name="colorThreshold">
    ///     0–255. A pixel is flagged as a discrepancy when any R/G/B channel
    ///     differs by more than this value between the two images.
    /// </param>
    /// <param name="sampleStep">
    ///     Check every Nth pixel to reduce result size (1 = every pixel).
    /// </param>
    /// <returns>
    ///     Array of <see cref="SystemPoint"/> values with the (X, Y) coordinates
    ///     of every differing pixel, expressed in the natural image coordinate system.
    /// </returns>
    public SystemPoint[] Compare(
        Stream stream1,
        Stream stream2,
        int colorThreshold = 30,
        int sampleStep = 2)
    {
        using var img1 = SharpImage.Load<Rgba32>(stream1);
        using var img2 = SharpImage.Load<Rgba32>(stream2);

        // Use the overlapping region of the two images
        int width  = Math.Min(img1.Width,  img2.Width);
        int height = Math.Min(img1.Height, img2.Height);

        var discrepancies = new List<SystemPoint>();

        for (int y = 0; y < height; y += sampleStep)
        {
            for (int x = 0; x < width; x += sampleStep)
            {
                Rgba32 p1 = img1[x, y];
                Rgba32 p2 = img2[x, y];

                int diffR = Math.Abs(p1.R - p2.R);
                int diffG = Math.Abs(p1.G - p2.G);
                int diffB = Math.Abs(p1.B - p2.B);

                if (diffR > colorThreshold ||
                    diffG > colorThreshold ||
                    diffB > colorThreshold)
                {
                    discrepancies.Add(new SystemPoint(x, y));
                }
            }
        }

        return discrepancies.ToArray();
    }
}
