using System.Diagnostics;
using System.Drawing;

namespace AutoContinue;

internal enum SchedulerState
{
    Stopped,
    Running,
    Paused
}

internal enum MouseClickType
{
    Single,
    Double
}

internal readonly record struct SchedulerStatus(SchedulerState State, TimeSpan Remaining, int? RunsRemaining);

internal sealed class AutoContinueScheduler : IDisposable
{
    private static readonly TimeSpan TickPeriod = TimeSpan.FromMilliseconds(200);
    private readonly object _sync = new();
    private readonly System.Threading.Timer _timer;
    private readonly Action<Point, MouseClickType> _click;
    private SchedulerState _state;
    private Point _point;
    private MouseClickType _clickType;
    private TimeSpan _interval;
    private TimeSpan _pausedRemaining;
    private int? _runsRemaining;
    private long _deadline;
    private bool _disposed;

    public event Action<SchedulerStatus>? StatusChanged;
    public event Action<Exception>? ErrorOccurred;

    public AutoContinueScheduler(Action<Point, MouseClickType> click)
    {
        _click = click;
        _timer = new System.Threading.Timer(Tick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start(TimeSpan interval, Point point, MouseClickType clickType, int? runCount)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (runCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runCount));
        }

        SchedulerStatus status;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _interval = interval;
            _point = point;
            _clickType = clickType;
            _runsRemaining = runCount;
            _deadline = Stopwatch.GetTimestamp() + ToStopwatchTicks(interval);
            _state = SchedulerState.Running;
            _timer.Change(TickPeriod, TickPeriod);
            status = GetStatus();
        }

        StatusChanged?.Invoke(status);
    }

    public void Stop()
    {
        SchedulerStatus status;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _state = SchedulerState.Stopped;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            status = GetStatus();
        }

        StatusChanged?.Invoke(status);
    }

    public void TogglePause()
    {
        SchedulerStatus status;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_state == SchedulerState.Running)
            {
                _pausedRemaining = RemainingUntil(_deadline);
                _state = SchedulerState.Paused;
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
            else if (_state == SchedulerState.Paused)
            {
                _deadline = Stopwatch.GetTimestamp() + ToStopwatchTicks(_pausedRemaining);
                _state = SchedulerState.Running;
                _timer.Change(TickPeriod, TickPeriod);
            }
            else
            {
                return;
            }

            status = GetStatus();
        }

        StatusChanged?.Invoke(status);
    }

    public SchedulerStatus GetCurrentStatus()
    {
        lock (_sync)
        {
            return GetStatus();
        }
    }

    private void Tick(object? state)
    {
        SchedulerStatus status;
        Exception? error = null;
        lock (_sync)
        {
            if (_disposed || _state != SchedulerState.Running)
            {
                return;
            }

            if (Stopwatch.GetTimestamp() >= _deadline)
            {
                try
                {
                    _click(_point, _clickType);
                    if (_runsRemaining is int runsRemaining)
                    {
                        _runsRemaining = runsRemaining - 1;
                    }

                    if (_runsRemaining == 0)
                    {
                        _state = SchedulerState.Stopped;
                        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        _deadline = Stopwatch.GetTimestamp() + ToStopwatchTicks(_interval);
                    }
                }
                catch (Exception exception)
                {
                    error = exception;
                    _state = SchedulerState.Stopped;
                    _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }

            status = GetStatus();
        }

        StatusChanged?.Invoke(status);
        if (error is not null)
        {
            ErrorOccurred?.Invoke(error);
        }
    }

    private SchedulerStatus GetStatus()
    {
        var remaining = _state switch
        {
            SchedulerState.Running => RemainingUntil(_deadline),
            SchedulerState.Paused => _pausedRemaining,
            _ => TimeSpan.Zero
        };
        return new SchedulerStatus(_state, remaining, _state == SchedulerState.Stopped ? null : _runsRemaining);
    }

    private static TimeSpan RemainingUntil(long deadline)
    {
        var ticksRemaining = Math.Max(0, deadline - Stopwatch.GetTimestamp());
        return TimeSpan.FromSeconds(ticksRemaining / (double)Stopwatch.Frequency);
    }

    private static long ToStopwatchTicks(TimeSpan duration) =>
        checked((long)(duration.TotalSeconds * Stopwatch.Frequency));

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _state = SchedulerState.Stopped;
            _timer.Dispose();
        }
    }
}
