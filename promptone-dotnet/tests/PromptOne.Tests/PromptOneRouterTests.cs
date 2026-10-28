using System.Text.Json;

namespace PromptOne.Tests;

/// <summary>
/// Tier 1: routing decisions and checkpoint lifecycle, which needs no artifacts. The internal
/// engine-factory constructor stands in fake predictors for real <see cref="PromptOneEngine"/>
/// instances, so LRU/attach/preload/unload/eviction behaviour is exercised without ONNX weights.
/// Named cases mirror <c>tests/test_router.py</c>'s routing sections (script/is_english coverage
/// lives in <see cref="LanguageDetectionTests"/>); <see cref="EveryRouteProbeEntry"/> consumes
/// every entry of <c>golden/routing/route_probe.json</c>.
/// </summary>
public sealed class PromptOneRouterTests
{
    private sealed class FakePredictor(string name) : IPromptOnePredictor
    {
        public string Name { get; } = name;
        public int Calls;

        public PromptOneResult Predict(object? state, QuestionSet questions)
        {
            Interlocked.Increment(ref Calls);
            return new PromptOneResult(Name, [], new Dictionary<string, Answer>(StringComparer.Ordinal), new Usage(0, 0));
        }
    }

    private static PromptOneRouter NewFakeRouter(PromptOneRouterOptions? options = null) =>
        new(options, ck => new FakePredictor(CheckpointName(ck)));

    private static string CheckpointName(PromptOneCheckpoint c) => PromptOneOptions.CheckpointSubfolder(c);

    // ── workflow signatures ───────────────────────────────────────────────────

    public static readonly Dictionary<string, string[]> TypedDecisionWorkflowIds = new(StringComparer.Ordinal)
    {
        ["agent_trace_observability"] = ["action", "needs_review", "outcome", "risk", "urgency"],
        ["customer_service"] = ["action", "category", "churn_risk", "needs_human", "urgency"],
        ["invoice_processing"] = ["discrepancy_severity", "disposition", "duplicate", "matches_order", "urgency"],
        ["security_incidents"] = ["credential_compromise", "disposition", "severity", "true_positive", "urgency"],
    };

    [Theory]
    [MemberData(nameof(WorkflowCases))]
    public void MatchTypedDecisionsWorkflowMatchesExactIdSets(string workflow, string[] ids)
    {
        var questions = new QuestionSet();
        foreach (var id in ids) questions[id] = Question.Noul("x");
        Assert.Equal(workflow, PromptOneRouter.MatchTypedDecisionsWorkflow(questions));
    }

    public static IEnumerable<object[]> WorkflowCases() =>
        TypedDecisionWorkflowIds.Select(kv => new object[] { kv.Key, kv.Value });

    [Fact]
    public void WorkflowPartialOverlapMatchesNothing()
    {
        var qs = new QuestionSet { ["urgency"] = Question.Noul("x"), ["category"] = Question.Noul("x") };
        Assert.Null(PromptOneRouter.MatchTypedDecisionsWorkflow(qs));
    }

    [Fact]
    public void WorkflowSupersetMatchesNothing()
    {
        var qs = new QuestionSet();
        foreach (var id in TypedDecisionWorkflowIds["customer_service"]) qs[id] = Question.Noul("x");
        qs["extra"] = Question.Noul("x");
        Assert.Null(PromptOneRouter.MatchTypedDecisionsWorkflow(qs));
    }

    [Fact]
    public void WorkflowEmptyMatchesNothing() => Assert.Null(PromptOneRouter.MatchTypedDecisionsWorkflow(null));

    // ── name normalisation ────────────────────────────────────────────────────

    [Theory]
    [InlineData("en", PromptOneCheckpoint.English)]
    [InlineData("promptone", PromptOneCheckpoint.English)]
    [InlineData("multi", PromptOneCheckpoint.Multilingual)]
    [InlineData("ML", PromptOneCheckpoint.Multilingual)]
    [InlineData("typed", PromptOneCheckpoint.TypedDecisions)]
    [InlineData("typed_decisions", PromptOneCheckpoint.TypedDecisions)]
    [InlineData("English", PromptOneCheckpoint.English)]
    // "zameer1/promptone".split("/")[-1] is literally the alias "promptone", already covered above.
    public void NormaliseNameMatchesPython(string alias, PromptOneCheckpoint expected) =>
        Assert.Equal(expected, PromptOneRouter.NormaliseName(alias));

    [Fact]
    public void NormaliseNameUnknownThrows() =>
        Assert.Throws<ArgumentException>(() => PromptOneRouter.NormaliseName("nope"));

    // ── routing decisions ─────────────────────────────────────────────────────

    private static readonly QuestionSet GenericQuestions = new()
    {
        ["dept"] = Question.Choice("Which team?", ("billing", null), ("tech", null)),
    };

    private static QuestionSet TypedDecisionsQuestions() =>
        new(TypedDecisionWorkflowIds["customer_service"].Select(
            id => new KeyValuePair<string, Question>(id, Question.Noul("x"))));

    public sealed record RouteCase(string Label, object? State, string? Model, string? Task, string? Lang, PromptOneCheckpoint Expected);

    [Theory]
    [MemberData(nameof(RouteCases))]
    public void RouteMatchesPython(RouteCase c)
    {
        using var router = NewFakeRouter();
        var decision = router.Route(c.State, GenericQuestions, c.Model, c.Task, c.Lang);
        Assert.True(decision.Model == c.Expected, $"{c.Label}: got {decision.Model}, want {c.Expected}");
    }

    public static TheoryData<RouteCase> RouteCases() =>
    [
        new RouteCase("english text", new Dictionary<string, object?> { ["body"] = "I was charged twice, please refund." }, null, null, null, PromptOneCheckpoint.English),
        new RouteCase("armenian text", new Dictionary<string, object?> { ["body"] = "Հայերեն" }, null, null, null, PromptOneCheckpoint.Multilingual),
        new RouteCase("armenian explicit override", new Dictionary<string, object?> { ["body"] = "Հայերեն" }, "english", null, null, PromptOneCheckpoint.English),
        new RouteCase("hindi text", new Dictionary<string, object?> { ["body"] = "मुझसे दो बार शुल्क लिया गया" }, null, null, null, PromptOneCheckpoint.Multilingual),
        new RouteCase("explicit model", new Dictionary<string, object?> { ["body"] = "anything" }, "multilingual", null, null, PromptOneCheckpoint.Multilingual),
        new RouteCase("explicit model overrides script", new Dictionary<string, object?> { ["body"] = "मुझसे दो बार" }, "english", null, null, PromptOneCheckpoint.English),
        new RouteCase("explicit task", new Dictionary<string, object?> { ["body"] = "x" }, null, "typed_decisions", null, PromptOneCheckpoint.TypedDecisions),
        new RouteCase("explicit lang en", new Dictionary<string, object?> { ["body"] = "मुझसे दो बार" }, null, null, "en", PromptOneCheckpoint.English),
        new RouteCase("explicit lang de", new Dictionary<string, object?> { ["body"] = "hello there" }, null, null, "de", PromptOneCheckpoint.Multilingual),
        new RouteCase("empty state", new Dictionary<string, object?>(), null, null, null, PromptOneCheckpoint.Multilingual),
        new RouteCase("none state", null, null, null, null, PromptOneCheckpoint.Multilingual),
    ];

    [Fact]
    public void TypedDecisionsWorkflowIsNotUsedWhenAutoTaskDetectionIsOff()
    {
        using var router = NewFakeRouter();
        var decision = router.Route(new Dictionary<string, object?> { ["body"] = "I was charged twice" }, TypedDecisionsQuestions());
        Assert.Equal(PromptOneCheckpoint.English, decision.Model);
        Assert.Equal("customer_service", decision.Workflow); // reported even though it played no part
    }

    [Fact]
    public void AutoTaskDetectionIsOptIn()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { AutoTaskDetection = true });
        Assert.Equal(PromptOneCheckpoint.TypedDecisions,
            router.Route(new Dictionary<string, object?> { ["body"] = "I was charged twice" }, TypedDecisionsQuestions()).Model);
        Assert.Equal(PromptOneCheckpoint.English,
            router.Route(new Dictionary<string, object?> { ["body"] = "I was charged twice" }, GenericQuestions).Model);
        // explicit model still beats an auto-detected workflow
        Assert.Equal(PromptOneCheckpoint.Multilingual,
            router.Route("x", TypedDecisionsQuestions(), model: "multilingual").Model);
    }

    [Fact]
    public void DecisionPayloadShape()
    {
        using var router = NewFakeRouter();
        var d = router.Route(new Dictionary<string, object?> { ["body"] = "मुझसे दो बार शुल्क लिया गया" }, GenericQuestions);
        Assert.True(d.Reason.Length > 0);
        Assert.Equal("devanagari", d.Detection!.Script);
        Assert.Equal(PromptOneCheckpoint.Multilingual, d.Model);
    }

    [Theory]
    [InlineData(PromptOneCheckpoint.English)]
    [InlineData(PromptOneCheckpoint.Multilingual)]
    public void CustomDefaultOverride(PromptOneCheckpoint checkpoint)
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { Default = checkpoint });
        Assert.Equal(checkpoint, router.Route("12345", GenericQuestions).Model);
        Assert.Equal(checkpoint, router.Route("Quero cancelar", GenericQuestions).Model);
        Assert.Equal(PromptOneCheckpoint.English, router.Route("I was charged twice", GenericQuestions).Model);
    }

    [Theory]
    [InlineData("Gătește-mi o rețetă de sarmale de post pentru mâine.")]
    [InlineData("Exportă APK-ul pentru Android și pune-l pe Drive ca să-l instalez.")]
    [InlineData("Klient został obciążony dwukrotnie i chce zwrot pieniędzy za fakturę")]
    [InlineData("Müşteriden iki kez ücret alındı ve para iadesi istiyor lütfen yardım")]
    public void UnknownLatinLanguageRoutesToMultilingual(string text)
    {
        using var router = NewFakeRouter();
        Assert.Equal(PromptOneCheckpoint.Multilingual, router.Route(text).Model);
    }

    [Fact]
    public void UndecidedReasonMentionsLettersNotAGuessedLanguage()
    {
        using var router = NewFakeRouter();
        var reason = router.Route("Müşteriden iki kez ücret alındı ve para iadesi istiyor").Reason;
        Assert.Contains("not identified", reason);
    }

    // ── LRU bookkeeping ───────────────────────────────────────────────────────

    [Fact]
    public void Cap1KeepsNewest()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 1 });
        router.Load("english");
        router.Load("multilingual");
        Assert.Equal([PromptOneCheckpoint.Multilingual], router.Loaded);
    }

    [Fact]
    public void DefaultKeepsTwoCheckpointsLikePython()
    {
        // Python's Router(max_loaded=2) since #180: alternating two languages must not reload.
        Assert.Equal(2, new PromptOneRouterOptions().MaxLoaded);
        using var router = NewFakeRouter(new PromptOneRouterOptions());
        router.Load("english");
        router.Load("multilingual");
        Assert.Equal(2, router.Loaded.Count);
        Assert.Contains(PromptOneCheckpoint.English, router.Loaded);
        Assert.Contains(PromptOneCheckpoint.Multilingual, router.Loaded);
    }

    [Fact]
    public void Cap2EvictsOldest()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 2 });
        router.Load("english");
        router.Load("multilingual");
        router.Load("typed-decisions");
        Assert.Equal([PromptOneCheckpoint.Multilingual, PromptOneCheckpoint.TypedDecisions], router.Loaded);
    }

    [Fact]
    public void TouchProtectsFromEviction()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 2 });
        router.Load("english");
        router.Load("multilingual");
        router.Load("english"); // touch english
        router.Load("typed-decisions");
        Assert.Equal([PromptOneCheckpoint.English, PromptOneCheckpoint.TypedDecisions], [.. router.Loaded.OrderBy(c => c)]);
    }

    [Fact]
    public void UnloadOneAndAll()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 3 });
        router.Load("english");
        router.Load("multilingual");
        router.Unload("english");
        Assert.DoesNotContain(PromptOneCheckpoint.English, router.Loaded);
        router.Unload();
        Assert.Empty(router.Loaded);
    }

    // ── preload ───────────────────────────────────────────────────────────────

    [Fact]
    public void PreloadAllThreeStayResident()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 1 });
        router.Preload();
        Assert.Equal(
            new[] { PromptOneCheckpoint.English, PromptOneCheckpoint.Multilingual, PromptOneCheckpoint.TypedDecisions }.OrderBy(c => c),
            router.Loaded.OrderBy(c => c));
    }

    [Fact]
    public void PreloadRaisesMaxLoaded()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 1 });
        router.Preload(); // all three checkpoints
        Assert.Equal(3, router.Loaded.Count);
        // re-loading an already-resident checkpoint must not evict anything
        router.Load("english");
        Assert.Equal(3, router.Loaded.Count);
    }

    [Fact]
    public void PreloadSubsetStaysResidentAndTouchDoesNotEvict()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 1 });
        router.Preload(["english", "multilingual"]);
        router.Load("english"); // routing to an already-resident checkpoint must not evict anything
        Assert.Equal(
            new[] { PromptOneCheckpoint.English, PromptOneCheckpoint.Multilingual }.OrderBy(c => c),
            router.Loaded.OrderBy(c => c));
    }

    // ── attach ────────────────────────────────────────────────────────────────

    [Fact]
    public void AttachRegistersAndSurvivesALaterLoad()
    {
        // MaxLoaded=2 from construction, matching Python's `ra.max_loaded = max(ra.max_loaded, 2)`
        // before the second load: this port raises capacity only via PromptOneRouterOptions or an
        // Attach/Preload side effect, never through a bare mutable field.
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 2 });
        var sentinel = new FakePredictor("already-built");
        router.Attach("english", sentinel);
        Assert.Contains(PromptOneCheckpoint.English, router.Loaded);
        Assert.True(router.Loaded.Count >= 1); // max_loaded raised to hold it

        router.Load("multilingual");
        Assert.Equal(
            new[] { PromptOneCheckpoint.English, PromptOneCheckpoint.Multilingual }.OrderBy(c => c),
            router.Loaded.OrderBy(c => c));
        Assert.Same(sentinel, router.Load("english"));
    }

    [Fact]
    public void AttachAcceptsAliases()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 1 });
        router.Attach("en", new FakePredictor("x"));
        Assert.Contains(PromptOneCheckpoint.English, router.Loaded);
    }

    [Fact]
    public void AttachedPredictorIsNeverDisposedByTheRouter()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 1 });
        var disposable = new DisposableFake();
        router.Attach("english", disposable);
        router.Load("multilingual"); // evicts the attached english slot (max_loaded=1)
        router.Unload();
        Assert.False(disposable.Disposed);
    }

    private sealed class DisposableFake : IPromptOnePredictor, IDisposable
    {
        public bool Disposed { get; private set; }
        public PromptOneResult Predict(object? state, QuestionSet questions) =>
            new("fake", [], new Dictionary<string, Answer>(StringComparer.Ordinal), new Usage(0, 0));
        public void Dispose() => Disposed = true;
    }

    // ── eviction safety: a retired engine is disposed only after its in-flight Predict returns ──

    private sealed class SlowDisposable(ManualResetEventSlim releaseGate, SemaphoreSlim inPredict) : IPromptOnePredictor, IDisposable
    {
        public bool Disposed { get; private set; }

        public PromptOneResult Predict(object? state, QuestionSet questions)
        {
            inPredict.Release();
            releaseGate.Wait(TimeSpan.FromSeconds(10));
            Assert.False(Disposed); // never disposed while a predict call is still inside it
            return new PromptOneResult("fake", [], new Dictionary<string, Answer>(StringComparer.Ordinal), new Usage(0, 0));
        }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task RetiredEngineIsDisposedOnlyAfterItsInFlightPredictReturns()
    {
        using var releaseGate = new ManualResetEventSlim(false);
        using var inPredict = new SemaphoreSlim(0);
        SlowDisposable? englishInstance = null;

        using var router = new PromptOneRouter(new PromptOneRouterOptions { MaxLoaded = 1 }, ck =>
        {
            if (ck == PromptOneCheckpoint.English) return englishInstance = new SlowDisposable(releaseGate, inPredict);
            return new FakePredictor(CheckpointName(ck));
        });

        var task = Task.Run(() => router.Predict("x", GenericQuestions, model: "english"));
        await inPredict.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // With MaxLoaded=1, loading multilingual evicts (retires) the english slot while its
        // Predict call above is still blocked inside SlowDisposable.Predict.
        router.Load("multilingual");
        Assert.False(englishInstance!.Disposed);

        releaseGate.Set();
        await task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(englishInstance.Disposed);
    }

    // ── thread safety (Python issue #95) ─────────────────────────────────────

    [Fact]
    public void EightConcurrentLoadsOfTheSameCheckpointBuildItOnce()
    {
        var built = 0;
        using var router = new PromptOneRouter(null, ck =>
        {
            Thread.Sleep(50); // widen the check-then-build window
            Interlocked.Increment(ref built);
            return new FakePredictor(CheckpointName(ck));
        });

        var results = new IPromptOnePredictor[8];
        Parallel.For(0, 8, i => results[i] = router.Load("english"));

        Assert.Equal(1, built);
        Assert.Single(results.Distinct());
        Assert.Equal([PromptOneCheckpoint.English], router.Loaded);
    }

    [Fact]
    public void ConcurrentHotPathLoadsOfAnAlreadyCachedModelStayConsistent()
    {
        using var router = NewFakeRouter(new PromptOneRouterOptions { MaxLoaded = 3 });
        router.Load("english"); // warm the cache

        Parallel.For(0, 20, _ => router.Load("english"));

        Assert.Equal([PromptOneCheckpoint.English], router.Loaded);
    }

    // ── Predict populates Routing ─────────────────────────────────────────────

    [Fact]
    public void PredictPopulatesRoutingOnTheResult()
    {
        using var router = NewFakeRouter();
        var result = router.Predict(new Dictionary<string, object?> { ["body"] = "Hola, necesito ayuda con mi factura por favor" }, GenericQuestions);
        Assert.NotNull(result.Routing);
        Assert.Equal(PromptOneCheckpoint.Multilingual, result.Routing!.Model);
        Assert.Equal("multilingual", result.Model); // the fake predictor names itself after the checkpoint
    }

    [Fact]
    public void DisposedRouterThrowsOnFurtherUse()
    {
        var router = NewFakeRouter();
        router.Dispose();
        Assert.Throws<ObjectDisposedException>(() => router.Load("english"));
    }

    // ── every recorded probe entry ────────────────────────────────────────────

    public static IEnumerable<object[]> RouteProbeCases()
    {
        using var doc = RoutingGoldenData.Load("route_probe.json");
        return [.. doc.RootElement.EnumerateArray().Select(e => new object[] { e.GetProperty("label").GetString()! })];
    }

    private static JsonElement FindRouteProbeEntry(string label)
    {
        using var doc = RoutingGoldenData.Load("route_probe.json");
        foreach (var e in doc.RootElement.EnumerateArray())
            if (e.GetProperty("label").GetString() == label)
                return JsonDocument.Parse(e.GetRawText()).RootElement;
        throw new InvalidOperationException($"no route_probe.json entry labelled '{label}'");
    }

    [Theory]
    [MemberData(nameof(RouteProbeCases))]
    public void EveryRouteProbeEntry(string label)
    {
        var e = FindRouteProbeEntry(label);
        var routeKwargs = e.GetProperty("route_kwargs");
        var routerKwargs = e.GetProperty("router_kwargs");

        var state = RoutingGoldenData.ToClr(routeKwargs.GetProperty("state"));
        QuestionSet? questions = null;
        if (routeKwargs.TryGetProperty("questions", out var q) && q.ValueKind != JsonValueKind.Null)
            questions = GoldenData.BuildQuestions(q);

        var model = OptString(routeKwargs, "model");
        var task = OptString(routeKwargs, "task");
        var lang = OptString(routeKwargs, "lang");
        var guess = OptString(routeKwargs, "lang_guess");
        var langGuess = guess is null ? null : LangGuess.FromCode(guess);

        var installed = OptString(routerKwargs, "lang_guess");
        var options = new PromptOneRouterOptions { LangGuess = installed is null ? null : LangGuess.FromCode(installed) };
        if (routerKwargs.TryGetProperty("auto_task_detection", out var atd)) options.AutoTaskDetection = atd.GetBoolean();
        if (routerKwargs.TryGetProperty("default", out var def)) options.Default = PromptOneRouter.NormaliseName(def.GetString()!);
        // "models" and "standalone_repos" affect only the repo string, which RouteDecision does
        // not carry in this port (see golden/routing/meta.json's notes), so they are irrelevant
        // to the model selection under test here and are intentionally not applied.

        using var router = NewFakeRouter(options);
        var decision = router.Route(state, questions, model, task, lang, langGuess);

        var want = e.GetProperty("decision");
        Assert.Equal(want.GetProperty("model").GetString(), CheckpointName(decision.Model));
        Assert.Equal(want.GetProperty("reason").GetString(), decision.Reason);

        var wantWorkflow = want.GetProperty("workflow");
        Assert.Equal(wantWorkflow.ValueKind == JsonValueKind.Null ? null : wantWorkflow.GetString(), decision.Workflow);

        var wantDetection = want.GetProperty("detection");
        if (wantDetection.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(decision.Detection);
        }
        else
        {
            Assert.NotNull(decision.Detection);
            var got = decision.Detection!;
            Assert.Equal(wantDetection.GetProperty("script").GetString(), got.Script);
            Assert.Equal(wantDetection.GetProperty("language").ValueKind == JsonValueKind.Null
                ? null : wantDetection.GetProperty("language").GetString(), got.Language);
            Assert.Equal(wantDetection.GetProperty("is_english").GetBoolean(), got.IsEnglish);
            Assert.Equal(wantDetection.GetProperty("language_undecided").GetBoolean(), got.LanguageUndecided);
            Assert.Equal(wantDetection.GetProperty("diacritic_rate").GetDouble(), got.DiacriticRate, 1e-9);
            Assert.Equal(wantDetection.GetProperty("non_latin_fraction").GetDouble(), got.NonLatinFraction, 1e-9);
            Assert.Equal(wantDetection.GetProperty("mixed_segment").ValueKind == JsonValueKind.Null
                ? null : wantDetection.GetProperty("mixed_segment").GetString(), got.MixedSegment);
        }
    }

    private static string? OptString(JsonElement obj, string prop) =>
        obj.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
}
