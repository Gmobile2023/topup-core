using System;
using System.Collections.ObjectModel;

namespace Topup.Shared.Helpers;

/// <summary>
///     Represents a datetime helper
/// </summary>
public class DateTimeHelper : IDateTimeHelper
{
    #region Ctor

    #endregion

    #region Fields

    #endregion

    #region Methods

    /// <summary>
    ///     Retrieves a System.TimeZoneInfo object from the registry based on its identifier.
    /// </summary>
    /// <param name="id">The time zone identifier, which corresponds to the System.TimeZoneInfo.Id property.</param>
    /// <returns>A System.TimeZoneInfo object whose identifier is the value of the id parameter.</returns>
    public virtual TimeZoneInfo FindTimeZoneById(string id)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(id);
    }

    /// <summary>
    ///     Returns a sorted collection of all the time zones
    /// </summary>
    /// <returns>A read-only collection of System.TimeZoneInfo objects.</returns>
    public virtual ReadOnlyCollection<TimeZoneInfo> GetSystemTimeZones()
    {
        return TimeZoneInfo.GetSystemTimeZones();
    }

    /// <summary>
    ///     Converts the date and time to current user date and time
    /// </summary>
    /// <param name="dt">The date and time (represents local system time or UTC time) to convert.</param>
    /// <returns>A DateTime value that represents time that corresponds to the dateTime parameter in customer time zone.</returns>
    public virtual DateTime ConvertToUserTime(DateTime dt)
    {
        return ConvertToUserTime(dt, dt.Kind);
    }

    /// <summary>
    ///     Converts the date and time to current user date and time
    /// </summary>
    /// <param name="dt">The date and time (represents local system time or UTC time) to convert.</param>
    /// <param name="sourceDateTimeKind">The source datetimekind</param>
    /// <returns>A DateTime value that represents time that corresponds to the dateTime parameter in customer time zone.</returns>
    public virtual DateTime ConvertToUserTime(DateTime dt, DateTimeKind sourceDateTimeKind)
    {
        dt = DateTime.SpecifyKind(dt, sourceDateTimeKind);
        if (sourceDateTimeKind == DateTimeKind.Local && TimeZoneInfo.Local.IsInvalidTime(dt))
            return dt;

        var currentUserTimeZoneInfo = CurrentTimeZone();
        return TimeZoneInfo.ConvertTime(dt, currentUserTimeZoneInfo);
    }

    /// <summary>
    ///     Converts the date and time to current user date and time
    /// </summary>
    /// <param name="dt">The date and time to convert.</param>
    /// <param name="sourceTimeZone">The time zone of dateTime.</param>
    /// <returns>A DateTime value that represents time that corresponds to the dateTime parameter in customer time zone.</returns>
    public virtual DateTime ConvertToUserTime(DateTime dt, TimeZoneInfo sourceTimeZone)
    {
        var currentUserTimeZoneInfo = CurrentTimeZone();
        return ConvertToUserTime(dt, sourceTimeZone, currentUserTimeZoneInfo);
    }

    /// <summary>
    ///     Converts the date and time to current user date and time
    /// </summary>
    /// <param name="dt">The date and time to convert.</param>
    /// <param name="sourceTimeZone">The time zone of dateTime.</param>
    /// <param name="destinationTimeZone">The time zone to convert dateTime to.</param>
    /// <returns>A DateTime value that represents time that corresponds to the dateTime parameter in customer time zone.</returns>
    public virtual DateTime ConvertToUserTime(DateTime dt, TimeZoneInfo sourceTimeZone,
        TimeZoneInfo destinationTimeZone)
    {
        if (sourceTimeZone.IsInvalidTime(dt))
            return dt;

        return TimeZoneInfo.ConvertTime(dt, sourceTimeZone, destinationTimeZone);
    }

    /// <summary>
    ///     Converts the date and time to Coordinated Universal Time (UTC)
    /// </summary>
    /// <param name="dt">The date and time (represents local system time or UTC time) to convert.</param>
    /// <returns>
    ///     A DateTime value that represents the Coordinated Universal Time (UTC) that corresponds to the dateTime
    ///     parameter. The DateTime value's Kind property is always set to DateTimeKind.Utc.
    /// </returns>
    public virtual DateTime ConvertToUtcTime(DateTime dt)
    {
        return ConvertToUtcTime(dt, dt.Kind);
    }

    /// <summary>
    ///     Converts the date and time to Coordinated Universal Time (UTC)
    /// </summary>
    /// <param name="dt">The date and time (represents local system time or UTC time) to convert.</param>
    /// <param name="sourceDateTimeKind">The source datetimekind</param>
    /// <returns>
    ///     A DateTime value that represents the Coordinated Universal Time (UTC) that corresponds to the dateTime
    ///     parameter. The DateTime value's Kind property is always set to DateTimeKind.Utc.
    /// </returns>
    public virtual DateTime ConvertToUtcTime(DateTime dt, DateTimeKind sourceDateTimeKind)
    {
        dt = DateTime.SpecifyKind(dt, sourceDateTimeKind);
        if (sourceDateTimeKind == DateTimeKind.Local && TimeZoneInfo.Local.IsInvalidTime(dt))
            return dt;

        return TimeZoneInfo.ConvertTimeToUtc(dt);
    }

    /// <summary>
    ///     Converts the date and time to Coordinated Universal Time (UTC)
    /// </summary>
    /// <param name="dt">The date and time to convert.</param>
    /// <param name="sourceTimeZone">The time zone of dateTime.</param>
    /// <returns>
    ///     A DateTime value that represents the Coordinated Universal Time (UTC) that corresponds to the dateTime
    ///     parameter. The DateTime value's Kind property is always set to DateTimeKind.Utc.
    /// </returns>
    public virtual DateTime ConvertToUtcTime(DateTime dt, TimeZoneInfo sourceTimeZone)
    {
        if (sourceTimeZone.IsInvalidTime(dt))
            //could not convert
            return dt;

        return TimeZoneInfo.ConvertTimeToUtc(dt, sourceTimeZone);
    }

    public virtual TimeZoneInfo CurrentTimeZone()
    {
        return TimeZoneInfo.Local;
    }

    #endregion
}