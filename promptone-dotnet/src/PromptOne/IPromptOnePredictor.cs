namespace PromptOne;

/// <summary>
/// Anything that can answer a <see cref="QuestionSet"/> about a state in one call: a single
/// <see cref="PromptOneEngine"/>, or a <see cref="PromptOneRouter"/> that picks one first. Mirrors Python's
/// duck-typed <c>predict</c>/<c>system_one</c> (<c>shortlist._call_predict</c>), so
/// <see cref="PromptOneShortlist.Predict"/> works over either.
/// </summary>
public interface IPromptOnePredictor
{
    /// <summary>Answer every question in <paramref name="questions"/> about <paramref name="state"/>.</summary>
    PromptOneResult Predict(object? state, QuestionSet questions);
}

/// <summary>
/// A predictor that also takes the per-call <see cref="PromptOnePredictOptions.Lang"/>, <see cref="PromptOnePredictOptions.MaxLen"/>
/// and <see cref="PromptOnePredictOptions.HeadMaxLen"/>: a <see cref="PromptOneEngine"/>. A <see cref="PromptOneRouter"/> forwards them
/// to the checkpoint it routed to; one that does not implement this is called without them (and refused when a token
/// budget was asked for, as Python refuses a keyword the agent does not accept).
/// </summary>
public interface IPromptOneCallPredictor : IPromptOnePredictor
{
    /// <summary>
    /// <see cref="IPromptOnePredictor.Predict"/> with the per-call language and token budgets of
    /// <paramref name="options"/>. <see cref="PromptOnePredictOptions.MinConfidence"/> is the caller's to apply.
    /// </summary>
    PromptOneResult Predict(object? state, QuestionSet questions, PromptOnePredictOptions? options);
}

/// <summary>
/// Whether a predictor carries per-language temperatures (Python <c>getattr(agent, "lang_temperatures", None)</c> being
/// non-empty). A <see cref="PromptOneRouter"/> only splits a batch by language for a checkpoint that does.
/// </summary>
public interface IPromptOneLangTemperatures
{
    /// <summary>Whether <see cref="PromptOneOptions.LangTemperatures"/> named at least one language.</summary>
    bool HasLangTemperatures { get; }
}
