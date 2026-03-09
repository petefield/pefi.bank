using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain;
using Pefi.Bank.Infrastructure;
using Pefi.Bank.Infrastructure.EventStore;

namespace Pefi.Bank.Functions.Sagas;

public abstract class SagaExecutorBase<T>(ILogger logger) : ISagaExecutor where T : Aggregate
{
    protected abstract HashSet<string> SagaEvents { get; }

    public bool CanHandle(string eventType) => SagaEvents.Contains(eventType);

    public abstract Task HandleBase(DomainEvent @event, EventDocument document);

    public async Task HandleAsync(DomainEvent @event, EventDocument document)
    {
        using var activity = DiagnosticConfig.Source.StartActivity("Saga.Execute");
        activity?.SetTag("pefi.saga.step", document.EventType);
        activity?.SetTag("pefi.stream_id", document.StreamId);
        logger.LogInformation("Saga event {EventType} for stream {StreamId}", document.EventType, document.StreamId);

        await HandleBase(@event, document);

        DiagnosticConfig.SagasStepsExecuted.Add(1,
           new KeyValuePair<string, object?>("pefi.event_type", document.EventType),
           new KeyValuePair<string, object?>("pefi.handler", GetType().Name));

    }

    public abstract Task<T> GetSaga(Guid id);

    public abstract Task SaveSaga(T saga);

    public abstract void MarkSagaFailed(T saga, string reason);

    private readonly string TypeName = typeof(T).Name;

    protected async Task ExecuteStep(
        Guid eventId,
        string stepName,
        Func<T, Task> execute,
        Func<T, Task>? compensate = null,
        bool markFailedOnError = true)
    {
        var stopwatch = Stopwatch.GetTimestamp();

        var item = await GetSaga(eventId);

        try
        {
            await execute(item);
            logger.LogInformation("Saga: {Type} {SagaId} Step [{StepName}] completed",
               TypeName, eventId, stepName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Saga: {Type} {SagaId} Step [{StepName}] failed: {ErrorMessage} , markFailedOnError={MarkFailedOnError}",
                TypeName, eventId, stepName, ex.Message, markFailedOnError);

            if (compensate is not null)
            {
                logger.LogInformation("Saga: {Type} {SagaId} Step [{StepName}] compensating",
                    TypeName, eventId, stepName);
                try
                {
                    await compensate(item);
                    await SaveSaga(item);
                }
                catch (Exception compensateEx)
                {
                    logger.LogCritical(compensateEx,
                        "Saga: {Type} {SagaId} Step [{StepName}] COMPENSATION FAILED", TypeName, eventId, stepName);
                    return;
                }
            }

            if (markFailedOnError)
                MarkSagaFailed(item, $"Step [{stepName}] failed: {ex.Message}");

            DiagnosticConfig.SagasFailed.Add(1);

        }
        finally
        {
                        await SaveSaga(item);

            var elapsedMs = Stopwatch.GetElapsedTime(stopwatch).TotalMilliseconds;
            DiagnosticConfig.SagaStepDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("pefi.saga.step", stepName));
        }
    }
}
