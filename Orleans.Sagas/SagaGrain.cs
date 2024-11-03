using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Orleans.Placement;
using Orleans.Providers;

namespace Orleans.Sagas
{
    [StorageProvider(ProviderName = "SagasStorage")]
    [PreferLocalPlacement]
    public sealed class SagaGrain : Grain<SagaState>, ISagaGrain
    {
        private static readonly string ReminderName = nameof(SagaGrain);

        private readonly IGrainContextAccessor _grainContextAccessor;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SagaGrain> _logger;
        private bool _isActive;
        private IGrainReminder _grainReminder;
        private IErrorTranslator _errorTranslator;

        public SagaGrain(IGrainContextAccessor grainContextAccessor, IServiceProvider serviceProvider, ILogger<SagaGrain> logger)
        {
            this._grainContextAccessor = grainContextAccessor;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ReadStateAsync()
        {
            try
            {
                await base.ReadStateAsync();
            }
            catch (Exception ex) when (ex is SerializationException || ex is InvalidCastException)
            {
                _logger.LogError(1, ex, "Failed to read state");
                await HandleReadingStateError();
            }
        }

        private async Task HandleReadingStateError()
        {
            try
            {
                 await UnRegisterReminderAsync();
                _isActive = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(1, ex, "Failed to handle reading state error");
            }
        }

        public async Task RequestAbort()
        {
            _logger.LogWarning(0, $"Saga {this} received an abort request.");

            // register abort request in separate grain in-case storage is mutating.
            await GetSagaCancellationGrain().RequestAbort();

            await ResumeAsync();
        }

        public async Task Execute(IEnumerable<ActivityDefinition> activities, ISagaPropertyBag sagaProperties, IErrorTranslator exceptionTranslator)
        {
            _errorTranslator = exceptionTranslator ?? new DefaultErrorTranslator();

            if (State.Status == SagaStatus.NotStarted)
            {
                State.Activities = activities.ToList();
                State.Properties = sagaProperties is null
                    ? new Dictionary<string, string>()
                    : ((SagaPropertyBag)sagaProperties).ContextProperties;
                State.Status = SagaStatus.Executing;
                await WriteStateAsync();
                await RegisterReminderAsync();
            }

            await ResumeAsync();
        }

        public Task<SagaStatus> GetStatus()
        {
            return Task.FromResult(State.Status);
        }

        public async Task<string> GetSagaError()
        {
            if (!State.Properties.ContainsKey(SagaPropertyBagKeys.ActivityErrorPropertyKey))
            {
                await Task.CompletedTask;
                return null;
            }

            return State.Properties[SagaPropertyBagKeys.ActivityErrorPropertyKey];
        }

        public Task<bool> HasCompleted()
        {
            return Task.FromResult(
                State.Status == SagaStatus.Aborted ||
                State.Status == SagaStatus.Compensated ||
                State.Status == SagaStatus.Executed
            );
        }

        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            await TryToInitGrainReminderAsync();
        
            await ResumeAsync();
        }

        public Task ResumeAsync()
        {
            if (!_isActive)
            {
                ResumeNoWaitAsync().Ignore();
            }
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            return this.GetPrimaryKey().ToString();
        }

        private ISagaCancellationGrain GetSagaCancellationGrain()
        {
            return GrainFactory.GetGrain<ISagaCancellationGrain>(this.GetPrimaryKey());
        }

        private async Task RegisterReminderAsync()
        {
            var reminderTime = TimeSpan.FromMinutes(1);
            _grainReminder = await this.RegisterOrUpdateReminder(ReminderName, reminderTime, reminderTime);
        }
        
        private async Task UnRegisterReminderAsync()
        {
            await TryToInitGrainReminderAsync();
        
            if (_grainReminder == null)
            {
                return;                
            }
        
            try
            {
                await this.UnregisterReminder(_grainReminder);
                _grainReminder = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(1, ex, "Failed to unregister the reminder");
            }
        }

        private async Task ResumeNoWaitAsync()
        {
            _isActive = true;

            try
            {
                if (State.NumCompletedActivities > 0)
                {
                    await CheckForAbortAsync();
                }

                while (State.Status == SagaStatus.Executing ||
                       State.Status == SagaStatus.Compensating)
                {
                    switch (State.Status)
                    {
                        case SagaStatus.Executing:
                            await ResumeExecuting();
                            break;
                        case SagaStatus.Compensating:
                            await ResumeCompensating();
                            break;
                    }
                }

                switch (State.Status)
                {
                    case SagaStatus.NotStarted:
                        ResumeNotStarted();
                        break;
                    case SagaStatus.Executed:
                    case SagaStatus.Compensated:
                    case SagaStatus.Aborted:
                        ResumeCompleted();
                        break;
                }

                await UnRegisterReminderAsync();
            }
            finally
            {
                _isActive = false;
            }
        }

        private void ResumeNotStarted()
        {
            _logger.LogError(0, $"Saga {this} is attempting to resume but was never started.");
        }

        private IActivity GetActivity(ActivityDefinition definition)
        {
            return (IActivity)_serviceProvider.GetService(definition.Type);
        }

        private async Task ResumeExecuting()
        {
            while (State.NumCompletedActivities < State.Activities.Count)
            {
                var definition = State.Activities[State.NumCompletedActivities];
                var currentActivity = GetActivity(definition);

                try
                {
                    _logger.LogDebug($"Executing activity #{State.NumCompletedActivities} '{currentActivity.GetType().Name}'...");
                    var context = CreateActivityRuntimeContext(definition);
                    await currentActivity.Execute(context);
                    _logger.LogDebug($"...activity #{State.NumCompletedActivities} '{currentActivity.GetType().Name}' complete.");
                    State.NumCompletedActivities++;
                    AddPropertiesToState(context);
                    await WriteStateAsync();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(0, "Activity '" + currentActivity.GetType().Name + "' in saga '" + GetType().Name + "' failed with " + e.GetType().Name);
                    State.CompensationIndex = State.NumCompletedActivities;
                    State.Status = SagaStatus.Compensating;
                    AddActivityError(e);
                    await WriteStateAsync();

                    return;
                }

                // To ensure running first activity 
                if (await CheckForAbortAsync())
                {
                    return;
                }
            }

            if (await CheckForAbortAsync())
            {
                return;
            }

            State.Status = SagaStatus.Executed;
            await WriteStateAsync();
        }

        private async Task<bool> CheckForAbortAsync()
        {
            if (await GetSagaCancellationGrain().HasAbortBeenRequested())
            {
                if (!State.HasBeenAborted)
                {
                    State.HasBeenAborted = true;
                    State.Status = SagaStatus.Compensating;
                    State.CompensationIndex = State.NumCompletedActivities - 1;
                    await WriteStateAsync();
                }

                return true;
            }

            return false;
        }

        private async Task ResumeCompensating()
        {
            while (State.CompensationIndex >= 0)
            {
                var definition = State.Activities[State.CompensationIndex];
                var currentActivity = GetActivity(definition);

                try
                {
                    _logger.LogDebug(0, $"Compensating for activity #{State.CompensationIndex} '{currentActivity.GetType().Name}'...");
                    var context = CreateActivityRuntimeContext(definition);
                    await currentActivity.Compensate(context);
                    _logger.LogDebug(0, $"...activity #{State.CompensationIndex} '{currentActivity.GetType().Name}' compensation complete.");
                    State.CompensationIndex--;
                    AddPropertiesToState(context);
                    await WriteStateAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(0, "Activity '" + currentActivity.GetType().Name + "' in saga '" + GetType().Name + "' failed while compensating with " + ex.GetType().Name, ex);
                    await Task.Delay(5000);
                    // TODO: handle compensation failure with expoential backoff.
                    // TODO: maybe eventual accept failure in a CompensationFailed state?
                }
            }

            State.Status = State.HasBeenAborted
                ? SagaStatus.Aborted
                : SagaStatus.Compensated;
            await WriteStateAsync();
        }

        private void AddPropertiesToState(ActivityContext context)
        {
            var propertyBag = (SagaPropertyBag)context.SagaProperties;
            foreach (var property in propertyBag.ContextProperties)
            {
                State.Properties[property.Key] = property.Value;
            }
        }

        private ActivityContext CreateActivityRuntimeContext(ActivityDefinition definition)
        {
            var propertyBag = (SagaPropertyBag)definition.Properties;
            IEnumerable<KeyValuePair<string, string>> properties = State.Properties;

            if (propertyBag != null)
            {
                properties = properties.Concat(propertyBag.ContextProperties);
            }

            return new ActivityContext(
                this.GetPrimaryKey(),
                GrainFactory,
                _grainContextAccessor,
                properties.ToDictionary(x => x.Key, y => y.Value)
            );
        }

        private void ResumeCompleted()
        {
            _logger.LogInformation($"Saga {this} has completed with status '{State.Status}'.");
        }

        private void AddActivityError(Exception exception)
        {
            try
            {
                State.Properties[SagaPropertyBagKeys.ActivityErrorPropertyKey] = _errorTranslator?.Translate(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to tranlsate exception.");
            }
        }

        private async Task TryToInitGrainReminderAsync()
        {
            if (_grainReminder != null)
            {
                return;
            }
        
            _grainReminder = await this.GetReminder(ReminderName);
        }
        
        public async Task<object> GetResult(string key)
        {
            State.Properties.Remove(key, out string value);
            return value;
        }

        public async Task Dispose()
        {
            await GetSagaCancellationGrain().Dispose();
            DeactivateOnIdle();
        }
    }
}
