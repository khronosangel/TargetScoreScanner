using SystemPoint = System.Drawing.Point;

namespace TargetScoreScanner.Services;

/// <summary>
/// Calculates a circular ring-based score (1–10) for each individual bullet hole
/// found in a set of discrepancy pixels.
///
/// Pixels are first clustered by proximity — all pixels within
/// <see cref="ClusterRadius"/> of an existing cluster centroid are merged into
/// that cluster.  Each cluster represents one bullet hole.
///
/// The target is divided into 10 concentric rings of equal radial width:
///   Ring 10 (bull's-eye) → centre → score 10
///   Ring  1 (outermost)  → edge   → score  1
/// </summary>
public class ScoringService
{
    /// <summary>
    /// Maximum pixel distance from a cluster centroid for a pixel to be
    /// considered part of that cluster.  Increase for larger images / holes.
    /// </summary>
    public int ClusterRadius { get; set; } = 40;

    /// <summary>
    /// Clusters <paramref name="hits"/> into individual bullet holes and scores each one.
    /// </summary>
    /// <param name="hits">Discrepancy pixels from <see cref="ImageComparisonService"/>.</param>
    /// <param name="imageWidth">Full pixel width of the reference image.</param>
    /// <param name="imageHeight">Full pixel height of the reference image.</param>
    /// <returns>A <see cref="MultiHitResult"/> containing every hit and the total score.</returns>
    public MultiHitResult Calculate(SystemPoint[] hits, int imageWidth, int imageHeight)
    {
        if (hits.Length == 0 || imageWidth <= 0 || imageHeight <= 0)
            return new MultiHitResult([], 0, Math.Min(imageWidth, imageHeight) / 2.0);

        double maxRadius = Math.Min(imageWidth, imageHeight) / 2.0;
        double ox = imageWidth  / 2.0;
        double oy = imageHeight / 2.0;

        // ── 1. Cluster pixels ───────────────────────────────────────────────
        // Each cluster is represented by the running list of its member points.
        var clusters = new List<List<SystemPoint>>();

        foreach (var pt in hits)
        {
            // Find the nearest existing cluster centroid within ClusterRadius
            int bestIndex = -1;
            double bestDist = double.MaxValue;

            for (int i = 0; i < clusters.Count; i++)
            {
                double cx = clusters[i].Average(p => (double)p.X);
                double cy = clusters[i].Average(p => (double)p.Y);
                double d  = Math.Sqrt(Math.Pow(pt.X - cx, 2) + Math.Pow(pt.Y - cy, 2));
                if (d < ClusterRadius && d < bestDist)
                {
                    bestDist  = d;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
                clusters[bestIndex].Add(pt);
            else
                clusters.Add([pt]);
        }

        // ── 2. Score each cluster ───────────────────────────────────────────
        var hitScores = new List<HitScore>(clusters.Count);

        foreach (var cluster in clusters)
        {
            double cx = cluster.Average(p => (double)p.X);
            double cy = cluster.Average(p => (double)p.Y);

            double distance = Math.Sqrt(Math.Pow(cx - ox, 2) + Math.Pow(cy - oy, 2));
            double norm     = Math.Clamp(distance / maxRadius, 0.0, 1.0);

            int band  = Math.Clamp((int)(norm * 10), 0, 9);
            int score = 10 - band;

            hitScores.Add(new HitScore(
                Score:              score,
                HitX:               (int)Math.Round(cx),
                HitY:               (int)Math.Round(cy),
                Distance:           Math.Round(distance, 1),
                NormalisedDistance: Math.Round(norm, 4)));
        }

        // Sort hits left-to-right for a consistent display order
        hitScores.Sort((a, b) => a.HitX.CompareTo(b.HitX));

        int total = hitScores.Sum(h => h.Score);

        return new MultiHitResult(hitScores, total, Math.Round(maxRadius, 1));
    }
}

// ── Records ─────────────────────────────────────────────────────────────────

/// <summary>Score for a single detected bullet hole (cluster centroid).</summary>
/// <param name="Score">1–10; higher is closer to centre.</param>
/// <param name="HitX">Centroid X in image pixels.</param>
/// <param name="HitY">Centroid Y in image pixels.</param>
/// <param name="Distance">Pixel distance from image centre to this centroid.</param>
/// <param name="NormalisedDistance">Distance as a fraction of the scoring radius (0–1).</param>
public record HitScore(
    int    Score,
    int    HitX,
    int    HitY,
    double Distance,
    double NormalisedDistance);

/// <summary>All hits detected in one comparison run.</summary>
/// <param name="Hits">Per-bullet-hole scores, sorted left-to-right.</param>
/// <param name="TotalScore">Sum of all individual scores.</param>
/// <param name="MaxRadius">Scoring radius used (half the shorter image dimension).</param>
public record MultiHitResult(
    IReadOnlyList<HitScore> Hits,
    int    TotalScore,
    double MaxRadius);
