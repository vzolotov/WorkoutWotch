namespace WorkoutWotch.Models
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Kent.Boogaart.HelperTrinity.Extensions;
    using WorkoutWotch.Services.Contracts.Logger;
    using WorkoutWotch.Services.Contracts.Speech;
    using WorkoutWotch.Models.Actions;
    using WorkoutWotch.Models.Events;

	public sealed class Exercise
	{
        private readonly ILogger logger;
        private readonly ISpeechService speechService;
        private readonly string name;
        private readonly int setCount;
        private readonly int repetitionCount;
        private readonly IImmutableList<MatcherWithAction> matchersWithActions;
        private readonly TimeSpan duration;

        public Exercise(ILoggerService loggerService, ISpeechService speechService, string name, int setCount, int repetitionCount, IEnumerable<MatcherWithAction> matchersWithActions)
        {
            loggerService.AssertNotNull("loggerService");
            speechService.AssertNotNull("speechService");
            name.AssertNotNull("name");
            matchersWithActions.AssertNotNull("matchersWithActions");

            if (setCount < 0)
            {
                throw new ArgumentException("setCount cannot be less than zero.", "setCount");
            }

            if (repetitionCount < 0)
            {
                throw new ArgumentException("repetitionCount cannot be less than zero.", "repetitionCount");
            }

            this.logger = loggerService.GetLogger(this.GetType());
            this.speechService = speechService;
            this.name = name;
            this.setCount = setCount;
            this.repetitionCount = repetitionCount;
            this.matchersWithActions = matchersWithActions.ToImmutableList();

            using (var dummyExecutionContext = new ExecutionContext())
            {
                this.duration = this
                    .GetEventsWithActions(dummyExecutionContext)
                    .SelectMany(x => x.Actions)
                    .Select(x => x.Duration)
                    .DefaultIfEmpty()
                    .Aggregate((running, next) => running + next);
            }
        }

        public string Name
        {
            get { return this.name; }
        }

        public int SetCount
        {
            get { return this.setCount; }
        }

        public int RepetitionCount
        {
            get { return this.repetitionCount; }
        }

        public TimeSpan Duration
        {
            get { return this.duration; }
        }

        public async Task ExecuteAsync(ExecutionContext context)
        {
            context.AssertNotNull("context");

            foreach (var eventWithActions in this.GetEventsWithActions(context))
            {
                foreach (var action in eventWithActions.Actions)
                {
                    if (context.SkipAhead > TimeSpan.Zero && context.SkipAhead >= action.Duration)
                    {
                        this.logger.Debug("Skipping action {0} for event {1} because its duration ({2}) is less than the remaining skip ahead ({3}).", action, eventWithActions.Event, action.Duration, context.SkipAhead);
                        context.AddProgress(action.Duration);
                        continue;
                    }

                    this.logger.Debug("Executing action {0} for event {1}.", action, eventWithActions.Event);
                    await action.ExecuteAsync(context).ContinueOnAnyContext();
                }
            }
        }

        private IEnumerable<IEvent> GetEvents(ExecutionContext executionContext)
        {
            executionContext.SetCurrentExercise(this);

            yield return new BeforeExerciseEvent(executionContext, this);

            for (var setNumber = 1; setNumber <= this.SetCount; ++setNumber)
            {
                executionContext.SetCurrentSet(setNumber);

                yield return new BeforeSetEvent(executionContext, setNumber);

                for (var repetitionNumber = 1; repetitionNumber <= this.RepetitionCount; ++repetitionNumber)
                {
                    executionContext.SetCurrentRepetition(repetitionNumber);

                    yield return new BeforeRepetitionEvent(executionContext, repetitionNumber);
                    yield return new DuringRepetitionEvent(executionContext, repetitionNumber);
                    yield return new AfterRepetitionEvent(executionContext, repetitionNumber);
                }

                yield return new AfterSetEvent(executionContext, setNumber);
            }

            yield return new AfterExerciseEvent(executionContext, this);
        }

        private IEnumerable<EventWithActions> GetEventsWithActions(ExecutionContext executionContext)
        {
            return this
                .GetEvents(executionContext)
                .Select(x => new EventWithActions(x, this.GetActionsForEvent(x)));
        }

        private IEnumerable<IAction> GetActionsForEvent(IEvent @event)
        {
            BeforeExerciseEvent beforeExerciseEvent;

            if ((beforeExerciseEvent = @event as BeforeExerciseEvent) != null)
            {
                yield return new SayAction(this.speechService, beforeExerciseEvent.Exercise.Name);
            }

            foreach (var matcherWithAction in this.matchersWithActions)
            {
                if (!matcherWithAction.Matcher.Matches(@event))
                {
                    continue;
                }

                yield return matcherWithAction.Action;
            }
        }

        private struct EventWithActions
        {
            private readonly IEvent @event;
            private readonly IImmutableList<IAction> actions;

            public EventWithActions(IEvent @event, IEnumerable<IAction> actions)
            {
                this.@event = @event;
                this.actions = actions.ToImmutableList();
            }

            public IEvent Event
            {
                get { return this.@event; }
            }

            public IImmutableList<IAction> Actions
            {
                get { return this.actions; }
            }
        }
	}
}