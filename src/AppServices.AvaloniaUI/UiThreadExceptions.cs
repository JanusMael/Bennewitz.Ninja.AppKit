using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Serilog;
using Serilog.Events;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI;

/// <summary>
/// The handler <see cref="AvaloniaDiagnostics.InstallAvaloniaHooks"/> subscribes to the UI thread's
/// <see cref="Dispatcher.UnhandledException"/> (<c>plans/00002</c>, decision 4): every exception a job on
/// the UI thread lets escape is handled, logged, and shown or not, and the app keeps running.
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
/// could never be got rid of.
/// </para>
/// </remarks>
internal static class UiThreadExceptions
{
    private static readonly Lock _lock = new();

    // Every failure reported this session, with its repeats since its last count line, and whether it
    // has been offered its dialog.
    private static readonly Dictionary<Failure, Reported> _reported = [];

    // The dispatcher the handler is subscribed to, recorded so that ResetForTests can leave it.
    private static Dispatcher? _installedOn;

    // A token for the dialog this opened that is still open; null while the gate is open.
    private static object? _openDialog;

    /// <summary>
    /// Opens the dialog for a reported exception, and calls the action it is given once that dialog
    /// closes. Throws when the dialog cannot be shown. A seam for tests: <see cref="OpenFatalErrorDialog"/>
    /// unless a test replaces it, and <see cref="ResetForTests"/> puts it back.
    /// </summary>
    internal static Action<Exception, Action> OpenDialog { get; set; } = OpenFatalErrorDialog;

    /// <summary>Subscribes the handler to <paramref name="dispatcher"/>, once however often it is called.</summary>
    internal static void Install(Dispatcher dispatcher)
    {
        lock (_lock)
        {
            if (_installedOn is not null)
            {
                return;
            }

            _installedOn = dispatcher;
        }

        dispatcher.UnhandledException += OnUnhandledException;
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
    /// Leaves the dispatcher the handler was installed on, reopens the gate, forgets every failure and
    /// count, and puts <see cref="OpenDialog"/> back. Test-only.
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
            _openDialog = null;
            _reported.Clear();
        }

        if (installedOn is not null)
        {
            installedOn.UnhandledException -= OnUnhandledException;
        }

        OpenDialog = OpenFatalErrorDialog;
    }

    private static void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        e.Handled = true;
        try
        {
            Handle(e.Exception);
        }
        catch (Exception)
        {
            // ⛔ The last guard. Every step of Handle guards itself, and this catches what nobody
            // foresaw: a throw out of a dispatcher handler escapes in place of the exception it handled.
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

    private static void Show(Failure failure, Exception exception)
    {
        object dialog = new();
        lock (_lock)
        {
            if (_openDialog is not null)
            {
                return;
            }

            _openDialog = dialog;

            // Offered before it is tried: a dialog that cannot open is not tried again, and its failure
            // not logged again, on every repeat.
            if (_reported.TryGetValue(failure, out Reported? reported))
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
            "A UI-thread exception repeated {RepeatCount} more times: {ExceptionType:l}: {ExceptionMessage:l}, thrown {ThrowSite:l}",
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
