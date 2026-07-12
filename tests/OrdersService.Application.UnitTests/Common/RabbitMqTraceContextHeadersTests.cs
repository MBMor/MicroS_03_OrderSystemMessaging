using System.Diagnostics;
using System.Text;
using Observability.Shared.Messaging;

namespace OrdersService.Application.UnitTests.Common;

public sealed class RabbitMqTraceContextHeadersTests
{
    private const string TestActivitySourceName = "OrderSystem.Tests.TraceContext";

    private static readonly ActivitySource ActivitySource = new(TestActivitySourceName);

    [Fact]
    public void InjectCurrent_And_Extract_PreservesTraceContext()
    {
        using var listener = CreateActivityListener();

        using var activity = ActivitySource.StartActivity(
            "test.publish",
            ActivityKind.Producer);

        Assert.NotNull(activity);

        var headers = new Dictionary<string, object?>();

        RabbitMqTraceContextHeaders.InjectCurrent(headers);

        Assert.True(headers.ContainsKey(RabbitMqTraceContextHeaders.TraceParentHeaderName));

        var extractedContext = RabbitMqTraceContextHeaders.Extract(headers);

        Assert.Equal(
            activity.TraceId,
            extractedContext.ActivityContext.TraceId);

        Assert.Equal(
            activity.SpanId,
            extractedContext.ActivityContext.SpanId);
    }

    [Fact]
    public void CaptureCurrent_SetTraceContext_And_Extract_PreservesTraceContext()
    {
        using var listener = CreateActivityListener();

        using var activity = ActivitySource.StartActivity(
            "test.outbox.capture",
            ActivityKind.Internal);

        Assert.NotNull(activity);

        var snapshot = RabbitMqTraceContextHeaders.CaptureCurrent();

        Assert.True(snapshot.HasTraceParent);
        Assert.NotNull(snapshot.TraceParent);

        var headers = new Dictionary<string, object?>();

        RabbitMqTraceContextHeaders.SetTraceContext(
            headers,
            snapshot.TraceParent,
            snapshot.TraceState);

        var extractedContext = RabbitMqTraceContextHeaders.Extract(headers);

        Assert.Equal(
            activity.TraceId,
            extractedContext.ActivityContext.TraceId);

        Assert.Equal(
            activity.SpanId,
            extractedContext.ActivityContext.SpanId);
    }

    [Fact]
    public void Extract_ReadsTraceParent_FromByteArrayHeader()
    {
        using var listener = CreateActivityListener();

        using var activity = ActivitySource.StartActivity(
            "test.byte-array-header",
            ActivityKind.Consumer);

        Assert.NotNull(activity);

        var traceParent = activity.Id;

        Assert.False(string.IsNullOrWhiteSpace(traceParent));

        var headers = new Dictionary<string, object?>
        {
            [RabbitMqTraceContextHeaders.TraceParentHeaderName] =
                Encoding.UTF8.GetBytes(traceParent)
        };

        var extractedContext = RabbitMqTraceContextHeaders.Extract(headers);

        Assert.Equal(
            activity.TraceId,
            extractedContext.ActivityContext.TraceId);

        Assert.Equal(
            activity.SpanId,
            extractedContext.ActivityContext.SpanId);
    }

    [Fact]
    public void Extract_ReturnsDefaultContext_WhenHeadersAreMissing()
    {
        var extractedContext = RabbitMqTraceContextHeaders.Extract(
            headers: null);

        Assert.Equal(
            default,
            extractedContext.ActivityContext);
    }

    private static ActivityListener CreateActivityListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TestActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }
}