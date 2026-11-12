using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Serilog;
using Serilog.Events;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI;

/// <summary>
/// The handler <see cref="AvaloniaDiagnostics.InstallAvaloniaHooks"/> subscribes to the UI thread's
/// <see cref="Dispatcher.UnhandledException"/> (<c>plans/00002</c>, decision 4): every exception a job on
/// the UI thread lets escape is handled, logged, and shown or not, and the app keeps running. A task
/// exception nobody observed is shown through the same gate and repeat table (<c>plans/00003</c>).
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b><c>Handled</c> first, and nothing escapes.</b> It is set before anything else runs, and every
/// step after it guards itself, so neither a filter, a logger nor a dialog that fails can let the
/// exception end the process, or put an exception of its own in its place.
/// </para>
/// <para>
/// ⛔ <b>Another handler's exception is left alone.</b> Avalonia calls the subscribers in order with one
/// shared args object, so <c>Handled</c> already set means a handler subscribed before this one has
/// dealt with the exception.
/// </para>
/// <para>
/// A benign exception (<see cref="AvaloniaDiagnosticsOptions.IsBenignException"/>) is logged at Debug and
/// shows nothing. Any other is logged at Error and shown in a <see cref="FatalErrorDialog"/>, <b>one
/// dialog at a time</b>: while a dialog this opened is open, a further exception is logged and not
/// shown, and the gate reopens when that dialog closes or fails to show.
/// </para>
/// <para>
/// ⚠ <b>A repeat is logged once and never shown twice.</b> An exception with the type, message and
/// throw site of one already reported is counted rather than logged. Each failure is offered one
/// dialog: at its first occurrence, or, when another dialog was open then, at its first repeat once
/// the gate has reopened. A dialog that fails to show counts as offered. One line gives the count when
/// that failure's dialog closes, and one when <see cref="AvaloniaDiagnostics.EntryPointOptions"/>'
/// <c>FlushLog</c> runs at exit. An exception that repeats on every frame would otherwise roll the log
/// past its last file, deleting the start of the session, and a dialog that came back after every close
/// could never be got rid of. A failure is one failure whichever path reports it.
/// </para>
/// <para>
/// ⚠ <b>Avalonia reports each of these exceptions a second time.</b> Its 12.1.3
/// <c>DispatcherOperation.InvokeCore</c> also puts the exception in the posted job's task, which nothing
/// reads, so the runtime raises it again, at a later garbage collection, as a task exception nobody
/// observed. Every exception the dispatcher reports is remembered, weakly, with what an aggregate
/// flattens to, whoever handles it, so that <see cref="AvaloniaDiagnostics.EntryPointOptions"/> can tell
/// that second report from a failure of its own (<see cref="WasSeen"/>). So the handler is subscribed
/// whenever <see cref="AvaloniaDiagnosticsOptions.HandleUiThreadExceptions"/> or
/// <see cref="AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions"/> is set, and handles nothing
/// unless the first is (<c>plans/00003</c>, decision 4).
/// </para>
/// <para>
/// ⛔ <b>A task exception nobody observed arrives on the finalizer thread</b>
/// (<see cref="ReportUnobserved"/>). It is counted and logged there, and its dialog is posted to the
/// dispatcher recorded at install, so the gate is taken on the UI thread, where a dialog the handler
/// opened meanwhile still holds it. <see cref="Dispatcher.UIThread"/> is never read there: before
/// Avalonia has started, it would make a dispatcher bound to the finalizer thread.
/// </para>
/// </remarks>
internal static class UiThreadExceptions
{
    private static readonly Lock _lock = new();

    // Every failure reported this session, with its repeats since its last count line, and whether it
    // has been offered its dialog.
    private static readonly Dictionary<Failure, Reported> _reported = [];

    // The dispatcher the handler is subscribed to: a task exception's dialog is posted to it, and
    // ResetForTests leaves it.
    private static Dispatcher? _installedOn;

    // Whether the handler handles what the dispatcher reports, or only remembers it. Written at install,
    // which runs on the UI thread before the handler can.
    private static volatile bool _handles;

    // A token for the dialog this opened that is still open; null while the gate is open.
    private static object? _openDialog;

    // Every exception the dispatcher has reported, whoever handled it, and what each aggregate among
    // them flattens to, held weakly: an entry lives as long as its exception, which Avalonia's unread
    // task keeps until its second report.
    private static readonly ConditionalWeakTable<Exception, object> _seen = new();

    private static readonly object SeenMarker = new();

    /// <summary>
    /// Opens the dialog for a reported exception, and calls the action it is given once that dialog
    /// closes. Throws when the dialog cannot be shown. A seam for tests: <see cref="OpenFatalErrorDialog"/>
    /// unless a test replaces it, and <see cref="ResetForTests"/> puts it back.
    /// </summary>
    internal static Action<Exception, Action> OpenDialog { get; set; } = OpenFatalErrorDialog;

    /// <summary>
    /// Subscribes the handler to <paramref name="dispatcher"/>, once however often it is called, and
    /// records it for the dialogs <see cref="ReportUnobserved"/> posts.
    /// </summary>
    /// <param name="dispatcher">The UI thread's dispatcher.</param>
    /// <param name="handle">Whether the handler handles what the dispatcher reports; when false, it only
    /// remembers it, for <see cref="WasSeen"/>.</param>
    internal static void Install(Dispatcher dispatcher, bool handle)
    {
        lock (_lock)
        {
            if (_installedOn is not null)
            {
                return;
            }

            _installedOn = dispatcher;
            _handles = handle;
        }

        dispatcher.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Whether the dispatcher has reported <paramref name="exception"/>, or an aggregate it reported
    /// flattens to it: when true, a task exception nobody observed that carries it is Avalonia's second
    /// report of an exception the UI thread let escape, whoever handled it.
    /// </summary>
    internal static bool WasSeen(Exception exception) => _seen.TryGetValue(exception, out _);

    /// <summary>
    /// Reports a task exception nobody observed that is not benign (<c>plans/00003</c>, decisions 2, 3 and
    /// 5): logs it at Error, unless its failure is a repeat, and posts its dialog to the dispatcher
    /// recorded at install. Called on the finalizer thread; it never waits for the dialog.
    /// </summary>
    /// <param name="raised">The exception as the runtime raised it, wrapped.</param>
    /// <param name="filterFailure">What the host's filter threw on it, if it threw, logged beside it.</param>
    /// <remarks>
    /// The failure counted and shown is the wrapper's one inner exception when it has exactly one, as a
    /// task that failed on one throw does; otherwise, as for a <c>Task.WhenAll</c> that several failures
    /// faulted, the wrapper itself. Keyed on the wrapper, every one would be the same failure. Before
    /// <see cref="Install"/>, and once the dispatcher has shut down, it is logged and not shown.
    /// </remarks>
    internal static void ReportUnobserved(Exception raised, Exception? filterFailure)
    {
        Exception exception = raised is AggregateException { InnerExceptions: [Exception only] } ? only : raised;
        Failure failure = Failure.Of(exception);
        Dispatcher? dispatcher;
        bool repeat;
        lock (_lock)
        {
            dispatcher = _installedOn;
            if (_reported.TryGetValue(failure, out Reported? reported))
            {
                reported.Count++;

                // Only a failure never offered its dialog is offered it at a repeat.
                if (reported.Offered)
                {
                    return;
                }

                repeat = true;
            }
            else
            {
                _reported.Add(failure, new Reported());
                repeat = false;
            }
        }

        if (!repeat)
        {
            Write(LogEventLevel.Error, raised, "A task exception nobody observed");
            if (filterFailure is not null)
            {
                Write(LogEventLevel.Warning, filterFailure,
                    "The IsBenignException filter threw on the task exception above, so that exception counts as not benign");
            }
        }

        if (dispatcher is null)
        {
            return;
        }

        try
        {
            // ⛔ Posted, never waited for: every finalizer in the process would wait for the user.
            dispatcher.Post(() => ShowPosted(failure, exception));
        }
        catch (Exception cannotPost)
        {
            Write(LogEventLevel.Error, cannotPost, "The dialog for the task exception above could not be shown");
        }
    }

    /// <summary>Writes one line for each failure that has repeated since its last count line.</summary>
    internal static void WritePendingRepeats()
    {
        List<(Failure Failure, long Count)> pending = [];
        lock (_lock)
        {
            foreach ((Failure failure, Reported reported) in _reported)
            {
                if (reported.Count > 0)
                {
                    pending.Add((failure, reported.Count));
                    reported.Count = 0;
                }
            }
        }

        foreach ((Failure failure, long count) in pending)
        {
            WriteRepeats(failure, count);
        }
    }

    /// <summary>
    /// Opens a <see cref="FatalErrorDialog"/> for <paramref name="exception"/>, titled as
    /// <see cref="AvaloniaDiagnostics.ShowFatalErrorDialog"/> titles it, and calls
    /// <paramref name="closed"/> once it closes. Unlike <see cref="FatalErrorDialog.ShowSafe"/>, it
    /// throws when the dialog cannot be shown, which is how the handler knows to reopen its gate.
    /// </summary>
    internal static void OpenFatalErrorDialog(Exception exception, Action closed)
    {
        FatalErrorDialog dialog = new(AvaloniaDiagnostics.FatalErrorDialogTitle, FatalErrorDialog.DefaultMessage, exception);
        dialog.Closed += (_, _) => closed();
        dialog.Show();
    }

    /// <summary>
    /// Leaves the dispatcher the handler was installed on, reopens the gate, forgets every failure, count
    /// and exception seen, and puts <see cref="OpenDialog"/> back. Test-only.
    /// </summary>
    /// <remarks>
    /// ⛔ It leaves the dispatcher recorded at install, never <see cref="Dispatcher.UIThread"/>: read where
    /// no UI dispatcher lives, as in a test's <c>Dispose</c>, that creates one bound to the reading thread.
    /// </remarks>
    internal static void ResetForTests()
    {
        Dispatcher? installedOn;
        lock (_lock)
        {
            installedOn = _installedOn;
            _installedOn = null;
            _handles = false;
            _openDialog = null;
            _reported.Clear();
        }

        _seen.Clear();

        if (installedOn is not null)
        {
            installedOn.UnhandledException -= OnUnhandledException;
        }

        OpenDialog = OpenFatalErrorDialog;
    }

    private static void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        bool handle = _handles && !e.Handled;
        if (handle)
        {
            e.Handled = true;
        }

        try
        {
            Remember(e.Exception);
            if (handle)
            {
                Handle(e.Exception);
            }
        }
        catch (Exception)
        {
            // ⛔ The last guard. Every step of Handle guards itself, and this catches what nobody
            // foresaw: a throw out of a dispatcher handler escapes in place of the exception it handled.
        }
    }

    // Every exception, whoever handles it, with what an aggregate flattens to, because the rule offers a
    // filter those and never the aggregate.
    private static void Remember(Exception exception)
    {
        try
        {
            _seen.AddOrUpdate(exception, SeenMarker);
            if (exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.Flatten().InnerExceptions)
                {
                    _seen.AddOrUpdate(inner, SeenMarker);
                }
            }
        }
        catch (Exception)
        {
            // ⛔ Remembering serves the second report; it must never cost the first its log line or dialog.
        }
    }

    private static void Handle(Exception exception)
    {
        bool benign = BenignExceptionRule.IsBenign(
            AvaloniaDiagnostics.BenignExceptionFilter, exception, out Exception? filterFailure);
        if (benign)
        {
            Write(LogEventLevel.Debug, exception, "A benign exception on the UI thread, ignored");
            return;
        }

        Failure failure = Failure.Of(exception);
        bool repeat;
        lock (_lock)
        {
            if (_reported.TryGetValue(failure, out Reported? reported))
            {
                reported.Count++;

                // Only a failure never offered its dialog, first seen while another dialog was open, is
                // shown at a repeat, and only once the gate has reopened.
                if (reported.Offered || _openDialog is not null)
                {
                    return;
                }

                repeat = true;
            }
            else
            {
                _reported.Add(failure, new Reported());
                repeat = false;
            }
        }

        if (!repeat)
        {
            Write(LogEventLevel.Error, exception, "An exception on the UI thread that nothing handled; the app keeps running");
            if (filterFailure is not null)
            {
                Write(LogEventLevel.Warning, filterFailure,
                    "The IsBenignException filter threw on the exception above, so that exception counts as not benign");
            }
        }

        Show(failure, exception);
    }

    // The dialog ReportUnobserved posted, run on the UI thread.
    private static void ShowPosted(Failure failure, Exception exception)
    {
        try
        {
            Show(failure, exception);
        }
        catch (Exception)
        {
            // ⛔ A throw out of a posted job would reach the dispatcher as a UI-thread failure of its own.
        }
    }

    private static void Show(Failure failure, Exception exception)
    {
        object dialog = new();
        lock (_lock)
        {
            // One dialog at a time, and one for each failure: a dialog posted for a failure that has been
            // offered one since, by either path, is not shown.
            _reported.TryGetValue(failure, out Reported? reported);
            if (_openDialog is not null || reported is { Offered: true })
            {
                return;
            }

            _openDialog = dialog;

            // Offered before it is tried: a dialog that cannot open is not tried again, and its failure
            // not logged again, on every repeat.
            if (reported is not null)
            {
                reported.Offered = true;
            }
        }

        try
        {
            OpenDialog(exception, () => Closed(dialog, failure));
        }
        catch (Exception cannotShow)
        {
            // A dialog that never opened would otherwise keep the gate shut for the rest of the session.
            lock (_lock)
            {
                if (ReferenceEquals(_openDialog, dialog))
                {
                    _openDialog = null;
                }
            }

            Write(LogEventLevel.Error, cannotShow, "The dialog for the exception above could not be shown");
        }
    }

    private static void Closed(object dialog, Failure failure)
    {
        try
        {
            long count = 0;
            lock (_lock)
            {
                if (ReferenceEquals(_openDialog, dialog))
                {
                    _openDialog = null;
                }

                if (_reported.TryGetValue(failure, out Reported? reported))
                {
                    count = reported.Count;
                    reported.Count = 0;
                }
            }

            if (count > 0)
            {
                WriteRepeats(failure, count);
            }
        }
        catch (Exception)
        {
            // Raised from the dialog's Closed event, on the UI thread: nothing may escape into Avalonia.
        }
    }

    private static void WriteRepeats(Failure failure, long count) =>
        Write(LogEventLevel.Error, null,
            "An exception repeated {RepeatCount} more times: {ExceptionType:l}: {ExceptionMessage:l}, thrown {ThrowSite:l}",
            count, failure.Type, failure.Message, failure.ThrowSite);

    private static void Write(LogEventLevel level, Exception? exception, string template, params object?[] values)
    {
        try
        {
            Log.Write(level, exception, template, values);
        }
        catch (Exception)
        {
            // ⛔ A logger that throws must neither escape the handler nor cost the user the dialog.
        }
    }

    /// <summary>What makes two exceptions one failure: their type, their message and where they were thrown.</summary>
    private readonly record struct Failure(string Type, string Message, string ThrowSite)
    {
        internal static Failure Of(Exception exception) =>
            new(exception.GetType().FullName ?? exception.GetType().Name, exception.Message, ThrowSiteOf(exception));

        // The stack trace's first frame: the method that threw, with its file and line when the symbols
        // are there. Read as text, because TargetSite and StackFrame.GetMethod need metadata that a
        // trimmed app may have removed.
        private static string ThrowSiteOf(Exception exception)
        {
            string? trace = exception.StackTrace;
            if (string.IsNullOrEmpty(trace))
            {
                return "";
            }

            int end = trace.AsSpan().IndexOfAny('\r', '\n');
            return (end < 0 ? trace : trace[..end]).Trim();
        }
    }

    /// <summary>A reported failure: its repeats since its last count line, and whether it has been offered its dialog.</summary>
    private sealed class Reported
    {
        internal long Count;

        internal bool Offered;
    }
}
