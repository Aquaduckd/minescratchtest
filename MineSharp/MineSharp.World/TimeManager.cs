namespace MineSharp.World;

/// <summary>
/// Manages world time tracking including world age and time of day.
/// </summary>
public class TimeManager
{
    /// <summary>
    /// Total ticks since world creation. Increments every server tick.
    /// </summary>
    public long WorldAge { get; private set; }

    /// <summary>
    /// Current time of day in ticks (0-24000).
    /// 0 = Midnight (6:00 AM in-game)
    /// 6000 = Noon (12:00 PM in-game)
    /// 12000 = Sunset (6:00 PM in-game)
    /// 18000 = Midnight (12:00 AM in-game)
    /// 24000 = Full cycle (wraps back to 0)
    /// </summary>
    public long TimeOfDay { get; private set; }

    /// <summary>
    /// Whether time is currently advancing.
    /// </summary>
    public bool TimeIncreasing { get; private set; }

    /// <summary>
    /// Creates a new TimeManager with default values.
    /// </summary>
    /// <param name="initialTimeOfDay">Initial time of day (default: 6000 = noon)</param>
    /// <param name="timeIncreasing">Whether time should be increasing (default: true)</param>
    public TimeManager(long initialTimeOfDay = 6000, bool timeIncreasing = true)
    {
        WorldAge = 0;
        TimeOfDay = initialTimeOfDay;
        TimeIncreasing = timeIncreasing;
    }

    /// <summary>
    /// Advances time by one tick.
    /// </summary>
    public void Tick()
    {
        // Always increment world age
        WorldAge++;
        
        // Only increment time of day if time is increasing
        if (TimeIncreasing)
        {
            TimeOfDay++;
            
            // Wrap time of day at 24000 (full day cycle)
            // TimeOfDay range is 0-23999, so when it reaches 24000, wrap to 0
            if (TimeOfDay >= 24000)
            {
                TimeOfDay = 0;
            }
        }
    }

    /// <summary>
    /// Sets the time of day.
    /// </summary>
    /// <param name="timeOfDay">Time of day in ticks (0-24000)</param>
    public void SetTimeOfDay(long timeOfDay)
    {
        // Clamp time of day to valid range
        if (timeOfDay < 0)
        {
            TimeOfDay = 0;
        }
        else if (timeOfDay > 24000)
        {
            TimeOfDay = 24000;
        }
        else
        {
            TimeOfDay = timeOfDay;
        }
    }

    /// <summary>
    /// Sets whether time is increasing.
    /// </summary>
    /// <param name="increasing">True to advance time, false to pause</param>
    public void SetTimeIncreasing(bool increasing)
    {
        TimeIncreasing = increasing;
    }
}

