using System;
using System.Diagnostics;

namespace StayLit
{
    public enum SessionMode { Idle, Timed, Always }
    public enum SessionResult { None, Stopped, Completed, Failed, DisplayOff }

    public interface IClock
    {
        long Milliseconds { get; }
        DateTime UtcNow { get; }
    }
    public interface IPowerController
    {
        void Acquire();
        void Release();
        void TurnOffDisplay();
    }

    public sealed class MonotonicClock : IClock
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();
        public long Milliseconds { get { return watch.ElapsedMilliseconds; } }
        public DateTime UtcNow { get { return DateTime.UtcNow; } }
    }

    // All methods run on the window's UI thread. Windows execution-state
    // requests belong to the thread that acquired them.
    public sealed class Session : IDisposable
    {
        private readonly IPowerController power;
        private readonly IClock clock;
        private long deadline;
        private bool leaseHeld;
        private long alwaysStartTick;
        private long recordedAlwaysMilliseconds;

        public Session(IPowerController power, IClock clock)
        {
            this.power = power;
            this.clock = clock;
        }

        public SessionMode Mode { get; private set; }
        public SessionResult Result { get; private set; }
        public long DurationMilliseconds { get; private set; }
        public bool IsActive { get { return Mode != SessionMode.Idle; } }
        public bool NeedsRelease { get { return leaseHeld; } }
        public DateTime? AlwaysStartedUtc { get; private set; }
        public DateTime? AlwaysEndedUtc { get; private set; }
        public bool HasAlwaysRecord { get { return AlwaysStartedUtc.HasValue; } }
        public long AlwaysElapsedMilliseconds
        {
            get { return Mode == SessionMode.Always ? Math.Max(0, clock.Milliseconds - alwaysStartTick) : recordedAlwaysMilliseconds; }
        }
        public DateTime RecordSnapshotUtc { get { return clock.UtcNow; } }
        public long RemainingMilliseconds
        {
            get { return Mode == SessionMode.Timed ? Math.Max(0, deadline - clock.Milliseconds) : 0; }
        }

        public void StartTimed(TimeSpan duration)
        {
            long milliseconds = checked((long)duration.TotalMilliseconds);
            if (milliseconds < 1000 || milliseconds > (long)TimeSpan.FromHours(100).TotalMilliseconds)
                throw new ArgumentOutOfRangeException("duration", "请选择 1 秒至 100 小时的亮屏时长。");
            long nextDeadline = checked(clock.Milliseconds + milliseconds);
            power.Acquire();
            FinishAlwaysRecord();
            leaseHeld = true;
            deadline = nextDeadline;
            DurationMilliseconds = milliseconds;
            Mode = SessionMode.Timed;
            Result = SessionResult.None;
        }

        public void StartAlways()
        {
            power.Acquire();
            leaseHeld = true;
            alwaysStartTick = clock.Milliseconds;
            recordedAlwaysMilliseconds = 0;
            AlwaysStartedUtc = clock.UtcNow;
            AlwaysEndedUtc = null;
            Mode = SessionMode.Always;
            DurationMilliseconds = 0;
            Result = SessionResult.None;
        }

        public void Stop()
        {
            ReleaseLease();
            FinishAlwaysRecord();
            Mode = SessionMode.Idle;
            Result = SessionResult.Stopped;
        }

        public void Tick()
        {
            if (Mode != SessionMode.Timed || clock.Milliseconds < deadline) return;
            FinishAndTurnOff(SessionResult.Completed);
        }

        public void TurnOffNow()
        {
            FinishAndTurnOff(SessionResult.DisplayOff);
        }

        private void FinishAndTurnOff(SessionResult result)
        {
            // Transition first so the screen-off command is never repeated,
            // including at the old deadline after a manual screen-off command.
            FinishAlwaysRecord();
            Mode = SessionMode.Idle;
            Result = result;
            try
            {
                ReleaseLease();
                power.TurnOffDisplay();
            }
            catch
            {
                Result = SessionResult.Failed;
                throw;
            }
        }

        public void RefreshPowerRequest()
        {
            Tick();
            if (IsActive) power.Acquire();
        }

        private void ReleaseLease()
        {
            if (!leaseHeld) return;
            power.Release();
            leaseHeld = false;
        }

        private void FinishAlwaysRecord()
        {
            if (Mode != SessionMode.Always) return;
            recordedAlwaysMilliseconds = AlwaysElapsedMilliseconds;
            AlwaysEndedUtc = clock.UtcNow;
        }

        public void RestoreAlwaysRecord(long milliseconds, DateTime startedUtc, DateTime endedUtc)
        {
            if (IsActive) throw new InvalidOperationException("不能在亮屏任务运行时恢复记录。");
            if (milliseconds < 0 || milliseconds > (long)TimeSpan.FromDays(3650).TotalMilliseconds)
                throw new ArgumentOutOfRangeException("milliseconds");
            recordedAlwaysMilliseconds = milliseconds;
            AlwaysStartedUtc = startedUtc.ToUniversalTime();
            AlwaysEndedUtc = endedUtc.ToUniversalTime();
        }

        public void Dispose()
        {
            FinishAlwaysRecord();
            Mode = SessionMode.Idle;
            ReleaseLease();
        }
    }
}
