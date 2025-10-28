using System.Diagnostics.Metrics;
using AIChat.Safety.Contracts;

namespace AIChat.Safety.Telemetry;

/// <summary>
/// Provides metrics for safety evaluation operations.
/// </summary>
public class SafetyMetrics
{
    private static readonly Meter Meter = new("AIChat.Safety", "1.0.0");

    // Counters
    private static readonly Counter<long> EvaluationsTotal = Meter.CreateCounter<long>(
   "safety.evaluations.total",
        description: "Total number of safety evaluations performed");

    private static readonly Counter<long> ViolationsTotal = Meter.CreateCounter<long>(
        "safety.violations.total",
        description: "Total number of safety violations detected");

    private static readonly Counter<long> ErrorsTotal = Meter.CreateCounter<long>(
       "safety.errors.total",
     description: "Total number of safety evaluation errors");

  // Histograms
    private static readonly Histogram<double> EvaluationDuration = Meter.CreateHistogram<double>(
        "safety.evaluation.duration",
        unit: "ms",
        description: "Duration of safety evaluations in milliseconds");

    private static readonly Histogram<int> RiskScoreDistribution = Meter.CreateHistogram<int>(
      "safety.risk_score.distribution",
        description: "Distribution of risk scores");

    // Gauges (via ObservableGauge)
    private static long _activeEvaluations = 0;
 
    private static readonly ObservableGauge<long> ActiveEvaluations = Meter.CreateObservableGauge<long>(
        "safety.evaluations.active",
    () => _activeEvaluations,
        description: "Number of currently active safety evaluations");

    /// <summary>
    /// Records a safety evaluation.
    /// </summary>
    /// <param name="provider">The safety provider name.</param>
    /// <param name="evaluationType">The type of evaluation (input/output/streaming).</param>
    /// <param name="isSafe">Whether the content was safe.</param>
    /// <param name="durationMs">Duration of the evaluation in milliseconds.</param>
    /// <param name="riskScore">The calculated risk score.</param>
    /// <param name="violatedCategories">List of violated categories, if any.</param>
    public static void RecordEvaluation(
        string provider,
        string evaluationType,
        bool isSafe,
  double durationMs,
        int riskScore,
        IEnumerable<HarmCategory>? violatedCategories = null)
    {
        var tags = new KeyValuePair<string, object?>[]
    {
       new("provider", provider),
   new("evaluation_type", evaluationType),
            new("is_safe", isSafe)
        };

        EvaluationsTotal.Add(1, tags);
        EvaluationDuration.Record(durationMs, tags);
        RiskScoreDistribution.Record(riskScore, tags);

  if (!isSafe && violatedCategories != null)
        {
    foreach (var category in violatedCategories)
            {
 var violationTags = new KeyValuePair<string, object?>[]
{
     new("provider", provider),
     new("evaluation_type", evaluationType),
        new("category", category.ToString())
            };
                
ViolationsTotal.Add(1, violationTags);
      }
        }
    }

    /// <summary>
  /// Records a safety evaluation error.
    /// </summary>
    /// <param name="provider">The safety provider name.</param>
    /// <param name="evaluationType">The type of evaluation.</param>
    /// <param name="errorType">The type of error that occurred.</param>
  public static void RecordError(string provider, string evaluationType, string errorType)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
    new("provider", provider),
    new("evaluation_type", evaluationType),
  new("error_type", errorType)
        };

     ErrorsTotal.Add(1, tags);
    }

    /// <summary>
    /// Increments the active evaluations counter.
    /// </summary>
    public static void IncrementActiveEvaluations()
    {
        Interlocked.Increment(ref _activeEvaluations);
    }

    /// <summary>
/// Decrements the active evaluations counter.
    /// </summary>
    public static void DecrementActiveEvaluations()
    {
        Interlocked.Decrement(ref _activeEvaluations);
    }

    /// <summary>
    /// Records metrics from a SafetyEvaluationResult.
    /// </summary>
    /// <param name="result">The safety evaluation result.</param>
    /// <param name="evaluationType">The type of evaluation.</param>
    public static void RecordFromResult(SafetyEvaluationResult result, string evaluationType)
    {
  var provider = result.Metadata?.Provider ?? "Unknown";
        var durationMs = result.Metadata?.ProcessingTimeMs ?? 0;
        var violatedCategories = result.DetectedCategories.Select(c => c.Category);

        RecordEvaluation(
       provider,
       evaluationType,
        result.IsSafe,
      durationMs,
   result.RiskScore,
            violatedCategories);
    }
}
