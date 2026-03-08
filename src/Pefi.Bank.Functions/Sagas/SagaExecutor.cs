using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pefi.Bank.Domain;
using Pefi.Bank.Infrastructure;
using Pefi.Bank.Infrastructure.EventStore;

namespace Pefi.Bank.Functions.Projections;

public abstract class SagaExecutor<T>(ILogger logger) : ISagaExecutor where T:Aggregate
{
    protected abstract HashSet<string> SagaEvents { get; }

    public bool CanHandle(string eventType) => SagaEvents.Contains(eventType);

    public abstract Task HandleBase(DomainEvent @event, EventDocument document);

    public async Task HandleAsync(DomainEvent @event, EventDocument document)
    {
        using var activity = DiagnosticConfig.Source.StartActivity("Saga.Execute");
        activity?.SetTag("pefi.saga.step", document.EventType);
        activity?.SetTag("pefi.stream_id", document.StreamId);
        await HandleBase(@event, document);
    }

    public abstract Task<T> GetSaga(Guid id) ;
    
    public abstract Task SaveSaga(T saga) ;

    protected async Task ExecuteStep(
        Guid eventId,
        string stepName,
        Func<T, Task > execute,
        Func<T,Task>? compensate = null,
        bool markFailedOnError = true)
    {
        var stopwatch = Stopwatch.GetTimestamp();

        var item = await GetSaga(eventId);

        try
        {
            await execute(item);
            await SaveSaga(item);
            logger.LogInformation("{Type} {TransferId} [{StepName}] completed",
               nameof(T), eventId, stepName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Type} {TransferId} [{StepName}] failed",
                nameof(T), eventId, stepName);

            if (!markFailedOnError)
                return;

            if (compensate is not null)
            {
                logger.LogInformation("{Type} {TransferId} [{StepName}] compensating",
                    nameof(T), eventId, stepName);
                try
                {
                    await compensate(item);
                    await SaveSaga(item);
                }
                catch (Exception compensateEx)
                {
                    logger.LogCritical(compensateEx,
                        "{Type} {TransferId} [{StepName}] COMPENSATION FAILED", nameof(T), eventId, stepName);
                    return;
                }
            }

            DiagnosticConfig.SagasFailed.Add(1);

        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(stopwatch).TotalMilliseconds;
            DiagnosticConfig.SagaStepDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("pefi.saga.step", stepName));
        }
    }
}
