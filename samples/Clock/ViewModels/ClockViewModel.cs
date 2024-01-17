﻿namespace Clock.ViewModels;

using Models;

// ReSharper disable once ClassNeverInstantiated.Global
internal class ClockViewModel : ViewModel, IClockViewModel, IDisposable, IObserver<Tick>
{
    private readonly ILog<ClockViewModel> _log;
    private readonly IClock _clock;
    private readonly IDisposable _timerToken;

    public ClockViewModel(
        ILog<ClockViewModel> log,
        IClock clock,
        ITimer timer)
    {
        _log = log;
        _clock = clock;
        _timerToken = timer.Subscribe(this);
    }

    public string Time => _clock.Now.ToString("T");

    public string Date => _clock.Now.ToString("d");

    void IObserver<Tick>.OnNext(Tick value)
    {
        OnPropertyChanged(nameof(Time));
        OnPropertyChanged(nameof(Date));
        _log.Info($"{Date} {Time}");
    }

    void IObserver<Tick>.OnError(Exception error) { }

    void IObserver<Tick>.OnCompleted() { }

    void IDisposable.Dispose() => _timerToken.Dispose();
}