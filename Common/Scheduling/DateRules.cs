﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NodaTime;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Helper class used to provide better syntax when defining date rules
    /// </summary>
    public class DateRules
    {
        private readonly DateTimeZone _timeZone;
        private readonly SecurityManager _securities;

        /// <summary>
        /// Initializes a new instance of the <see cref="DateRules"/> helper class
        /// </summary>
        /// <param name="securities">The security manager</param>
        /// <param name="timeZone">The algorithm's default time zone</param>
        public DateRules(SecurityManager securities, DateTimeZone timeZone)
        {
            _timeZone = timeZone;
            _securities = securities;
        }

        /// <summary>
        /// Specifies an event should fire only on the specified day
        /// </summary>
        /// <param name="year">The year</param>
        /// <param name="month">The month</param>
        /// <param name="day">The day</param>
        /// <returns></returns>
        public IDateRule On(int year, int month, int day)
        {
            // make sure they're date objects
            var dates = new[] {new DateTime(year, month, day)};
            return new FuncDateRule(string.Join(",", dates.Select(x => x.ToShortDateString())), (start, end) => dates);
        }

        /// <summary>
        /// Specifies an event should fire only on the specified days
        /// </summary>
        /// <param name="dates">The dates the event should fire</param>
        public IDateRule On(params DateTime[] dates)
        {
            // make sure they're date objects
            dates = dates.Select(x => x.Date).ToArray();
            return new FuncDateRule(string.Join(",", dates.Select(x => x.ToShortDateString())), (start, end) => dates);
        }

        /// <summary>
        /// Specifies an event should only fire today in the algorithm's time zone
        /// using _securities.UtcTime instead of 'start' since ScheduleManager backs it up a day
        /// </summary>
        public IDateRule Today => new FuncDateRule("TodayOnly",
            (start, e) => new[] {_securities.UtcTime.ConvertFromUtc(_timeZone).Date}
        );

        /// <summary>
        /// Specifies an event should only fire tomorrow in the algorithm's time zone
        /// using _securities.UtcTime instead of 'start' since ScheduleManager backs it up a day
        /// </summary>
        public IDateRule Tomorrow => new FuncDateRule("TomorrowOnly",
            (start, e) => new[] {_securities.UtcTime.ConvertFromUtc(_timeZone).Date.AddDays(1)}
        );

        /// <summary>
        /// Specifies an event should fire on each of the specified days of week
        /// </summary>
        /// <param name="day">The day the event should fire</param>
        /// <returns>A date rule that fires on every specified day of week</returns>
        public IDateRule Every(DayOfWeek day) => Every(new[] { day });

        /// <summary>
        /// Specifies an event should fire on each of the specified days of week
        /// </summary>
        /// <param name="days">The days the event should fire</param>
        /// <returns>A date rule that fires on every specified day of week</returns>
        public IDateRule Every(params DayOfWeek[] days)
        {
            var hash = days.ToHashSet();
            return new FuncDateRule(string.Join(",", days), (start, end) => Time.EachDay(start, end).Where(date => hash.Contains(date.DayOfWeek)));
        }

        /// <summary>
        /// Specifies an event should fire every day
        /// </summary>
        /// <returns>A date rule that fires every day</returns>
        public IDateRule EveryDay()
        {
            return new FuncDateRule("EveryDay", Time.EachDay);
        }

        /// <summary>
        /// Specifies an event should fire every day the symbol is trading
        /// </summary>
        /// <param name="symbol">The symbol whose exchange is used to determine tradable dates</param>
        /// <returns>A date rule that fires every day the specified symbol trades</returns>
        public IDateRule EveryDay(Symbol symbol)
        {
            var security = GetSecurity(symbol);
            return new FuncDateRule($"{symbol.Value}: EveryDay", (start, end) => Time.EachTradeableDay(security, start, end));
        }

        /// <summary>
        /// Specifies an event should fire on the first of each month + offset
        /// </summary>
        /// <param name="daysOffset"> The amount of days to offset the schedule by; must be between 0 and 30.</param>
        /// <returns>A date rule that fires on the first of each month + offset</returns>
        public IDateRule MonthStart(int daysOffset = 0)
        {
            return MonthStart(null, daysOffset);
        }

        /// <summary>
        /// Specifies an event should fire on the first tradable date + offset for the specified symbol of each month
        /// </summary>
        /// <param name="symbol">The symbol whose exchange is used to determine the first tradable date of the month</param>
        /// <param name="daysOffset"> The amount of days to offset the schedule by; must be between 0 and 30</param>
        /// <returns>A date rule that fires on the first tradable date + offset for the
        /// specified security each month</returns>
        public IDateRule MonthStart(Symbol symbol, int daysOffset = 0)
        {
            // Check that our offset is allowed
            if (daysOffset < 0 || 30 < daysOffset)
            {
                throw new ArgumentOutOfRangeException("daysOffset", "DateRules.MonthStart() : Offset must be between 0 and 30");
            }

            // Create the new DateRule and return it
            return new FuncDateRule(GetName(symbol, "MonthStart", daysOffset), (start, end) => MonthIterator(GetSecurity(symbol), start, end, daysOffset, true));
        }

        /// <summary>
        /// Specifies an event should fire on the last of each month
        /// </summary>
        /// <param name="daysOffset"> The amount of days to offset the schedule by; must be between 0 and 30</param>
        /// <returns>A date rule that fires on the last of each month - offset</returns>
        public IDateRule MonthEnd(int daysOffset = 0)
        {
            return MonthEnd(null, daysOffset);
        }

        /// <summary>
        /// Specifies an event should fire on the last tradable date - offset for the specified symbol of each month
        /// </summary>
        /// <param name="symbol">The symbol whose exchange is used to determine the last tradable date of the month</param>
        /// <param name="daysOffset">The amount of days to offset the schedule by; must be between 0 and 30.</param>
        /// <returns>A date rule that fires on the last tradable date - offset for the specified security each month</returns>
        public IDateRule MonthEnd(Symbol symbol, int daysOffset = 0)
        {
            // Check that our offset is allowed
            if (daysOffset < 0 || 30 < daysOffset)
            {
                throw new ArgumentOutOfRangeException("daysOffset", "DateRules.MonthEnd() : Offset must be between 0 and 30");
            }

            // Create the new DateRule and return it
            return new FuncDateRule(GetName(symbol, "MonthEnd", -daysOffset), (start, end) => MonthIterator(GetSecurity(symbol), start, end, daysOffset, false));
        }

        /// <summary>
        /// Specifies an event should fire on Monday + offset each week
        /// </summary>
        /// <param name="daysOffset">The amount of days to offset monday by; must be between 0 and 6</param>
        /// <returns>A date rule that fires on Monday + offset each week</returns>
        public IDateRule WeekStart(int daysOffset = 0)
        {
            // Check that our offset is allowed
            if (daysOffset < 0 || 6 < daysOffset)
            {
                throw new ArgumentOutOfRangeException("daysOffset", "DateRules.WeekStart() : Offset must be between 0 and 6");
            }

            // Determine our day with Monday being our default
            var scheduledDayOfWeek = daysOffset == 6 ? DayOfWeek.Sunday : DayOfWeek.Monday + daysOffset;

            return new FuncDateRule(GetName(null, "WeekStart", daysOffset), (start, end) => WeekIterator(null, start, end, scheduledDayOfWeek, true));
        }

        /// <summary>
        /// Specifies an event should fire on the first tradable date + offset for the specified
        /// symbol each week;
        /// </summary>
        /// <param name="symbol">The symbol whose exchange is used to determine the first
        /// tradeable date of the week</param>
        /// <param name="daysOffset">The amount of tradable days to offset the first tradable day by</param>
        /// <returns>A date rule that fires on the first + offset tradable date for the specified
        /// security each week</returns>
        public IDateRule WeekStart(Symbol symbol, int daysOffset = 0)
        {
            var security = GetSecurity(symbol);
            var tradableDays = security.Exchange.Hours.MarketHours.Values
                .Where(x => x.IsClosedAllDay == false).OrderBy(x => x.DayOfWeek).ToList();

            // We can't offset further than there are trading days in a week
            if (daysOffset > tradableDays.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysOffset),
                    $"DateRules.WeekStart() : {tradableDays.First().DayOfWeek}+{daysOffset} is out of range for {security.Symbol}'s schedule," +
                    $" please use an offset between 0 - {tradableDays.Count - 1}; Schedule : {string.Join(", ", tradableDays.Select(x => x.DayOfWeek))}");
            }

            var scheduledDayOfWeek = tradableDays[daysOffset].DayOfWeek;

            // Create the new DateRule and return it
            return new FuncDateRule(GetName(symbol, "WeekStart", daysOffset), (start, end) => WeekIterator(security, start, end, scheduledDayOfWeek, true));
        }

        /// <summary>
        /// Specifies an event should fire on Friday - offset
        /// </summary>
        /// <param name="daysOffset"> The amount of days to offset Friday by; must be between 0 and 6 </param>
        /// <returns>A date rule that fires on Friday each week</returns>
        public IDateRule WeekEnd(int daysOffset = 0)
        {
            // Check that our offset is allowed
            if (daysOffset < 0 || 6 < daysOffset)
            {
                throw new ArgumentOutOfRangeException("daysOffset", "DateRules.WeekStart() : Offset must be between 0 and 6");
            }

            // Determine our day with Friday being our default
            var scheduledDayOfWeek = daysOffset == 6 ? DayOfWeek.Saturday : DayOfWeek.Friday - daysOffset;

            return new FuncDateRule(GetName(null, "WeekEnd", -daysOffset), (start, end) => WeekIterator(null, start, end, scheduledDayOfWeek, true));
        }

        /// <summary>
        /// Specifies an event should fire on the last - offset tradable date for the specified
        /// symbol of each week
        /// <param name="symbol">The symbol whose exchange is used to determine the last
        /// tradable date of the week</param>
        /// <param name="daysOffset"> The amount of tradable days to offset the last tradable day by each week</param>
        /// <returns>A date rule that fires on the last - offset tradable date for the specified security each week</returns>
        public IDateRule WeekEnd(Symbol symbol, int daysOffset = 0)
        {
            var security = GetSecurity(symbol);
            var tradableDays = security.Exchange.Hours.MarketHours.Values
                .Where(x => x.IsClosedAllDay == false).OrderBy(x => x.DayOfWeek).ToList();

            // We can't offset further than there are trading days in a week
            if (daysOffset > tradableDays.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysOffset),
                    $"DateRules.WeekEnd() : {tradableDays.Last().DayOfWeek}-{daysOffset} is out of range for {security.Symbol}'s schedule," +
                    $" please use an offset between 0 - {tradableDays.Count - 1}; Schedule : {string.Join(", ", tradableDays.Select(x => x.DayOfWeek))}");
            }

            var scheduledDayOfWeek = tradableDays[(tradableDays.Count - 1) - daysOffset].DayOfWeek;

            // Create the new DateRule and return it
            return new FuncDateRule(GetName(symbol, "WeekEnd", -daysOffset), (start, end) => WeekIterator(security, start, end, scheduledDayOfWeek, false));
        }

        /// <summary>
        /// Gets the security with the specified symbol, or throws an exception if the symbol is not found
        /// </summary>
        /// <param name="symbol">The security's symbol to search for</param>
        /// <returns>The security object matching the given symbol</returns>
        private Security GetSecurity(Symbol symbol)
        {
            // We use this for the rules without a symbol
            if (symbol == null)
            {
                return null;
            }

            Security security;
            if (!_securities.TryGetValue(symbol, out security))
            {
                throw new KeyNotFoundException(symbol.Value + " not found in portfolio. Request this data when initializing the algorithm.");
            }
            return security;
        }

        /// <summary>
        /// Determine the string representation for a given rule 
        /// </summary>
        /// <param name="symbol">Symbol for the rule</param>
        /// <param name="ruleType">Rule type in string form</param>
        /// <param name="offset">The amount of offset on this rule</param>
        /// <returns></returns>
        private static string GetName(Symbol symbol, string ruleType, int offset)
        {
            // Convert our offset to +#, -#, or empty string if 0
            var offsetString = offset.ToString("+#;-#;''", CultureInfo.InvariantCulture);
            var name = symbol == null ? $"{ruleType}{offsetString}" : $"{symbol.Value}: {ruleType}{offsetString}";

            return name;
        }

        /// <summary>
        /// Get the closest trading day to a given DateTime for a given <see cref="SecurityExchangeHours"/>.
        /// </summary>
        /// <param name="securityExchangeHours"><see cref="SecurityExchangeHours"/> object with schedule for this Security</param>
        /// <param name="day">The day to base our search from</param>
        /// <param name="searchForward">Search into the future for the closest day if true; into the past if false</param>
        /// <returns></returns>
        private static DateTime GetClosestTradingDay(SecurityExchangeHours securityExchangeHours, DateTime day, bool searchForward)
        {
            // If the day given is tradable just return the day
            if (securityExchangeHours.IsDateOpen(day))
            {
                return day;
            }

            // Otherwise return the next trading day by searching in the correct direction
            return searchForward
                ? securityExchangeHours.GetNextTradingDay(day)
                : securityExchangeHours.GetPreviousTradingDay(day);
        }

        private static IEnumerable<DateTime> MonthIterator(Security security, DateTime start, DateTime end, int offset, bool searchForward)
        {
            // Get our schedule for this security's exchange
            var securitySchedule = security == null ? SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork) : security.Exchange.Hours;

            // variables for monthly date calculations
            DateTime scheduledDateOfMonth = start;
            var calculateDayOfMonth = true;

            foreach (var date in Time.EachDay(start, end))
            {
                var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

                // Once a month we must calculate the scheduled day for this monthly date rule
                if (calculateDayOfMonth)
                {
                    // Default schedule search forward is the 1st, search backward is the last day of month
                    scheduledDateOfMonth = searchForward 
                        ? new DateTime(date.Year, date.Month, 1) 
                        : new DateTime(date.Year, date.Month, daysInMonth);

                    // Verify our default day is tradable; we expect the offset to be off of a tradable day
                    scheduledDateOfMonth = GetClosestTradingDay(securitySchedule, scheduledDateOfMonth, searchForward);

                    // Offset the scheduled day accordingly
                    for (int i = 0; i < offset; i++)
                    {
                        scheduledDateOfMonth = searchForward
                            ? securitySchedule.GetNextTradingDay(scheduledDateOfMonth)
                            : securitySchedule.GetPreviousTradingDay(scheduledDateOfMonth);
                    }

                    // Enforce edges of month after offset
                    // If the resulting date is before this month we revert to the first tradable day of the month
                    if (scheduledDateOfMonth.Month < date.Month)
                    {
                        scheduledDateOfMonth = GetClosestTradingDay(securitySchedule, new DateTime(date.Year, date.Month, 1), true);
                    }

                    // If the resulting date is after this month we revert to the last tradable day of the month
                    if (scheduledDateOfMonth.Month > date.Month)
                    {
                        scheduledDateOfMonth = GetClosestTradingDay(securitySchedule, new DateTime(date.Year, date.Month, daysInMonth), false);
                    }

                    calculateDayOfMonth = false;
                }

                // On the day we expect to trigger the event return our closest trading day
                if (date == scheduledDateOfMonth)
                {
                    yield return GetClosestTradingDay(securitySchedule, date, searchForward);
                }

                // On the last day of each month reset this flag so we can calculate the new
                // scheduled day for the next month.
                if (date.Day == daysInMonth)
                {
                    calculateDayOfMonth = true;
                }
            }
        }

        private static IEnumerable<DateTime> WeekIterator(Security security, DateTime start, DateTime end, DayOfWeek scheduledDayOfWeek, bool searchForward)
        {
            // Get our schedule for this security's exchange
            var securitySchedule = security == null ? SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork) : security.Exchange.Hours;

            // For every date between start and end on our scheduled day of the week
            foreach (var date in Time.EachDay(start, end).Where(x => x.DayOfWeek == scheduledDayOfWeek))
            {
                // On the day we expect to trigger the event return our closest trading day
                if (date.DayOfWeek == scheduledDayOfWeek)
                {
                    yield return GetClosestTradingDay(securitySchedule, date, searchForward);
                }
            }
        }
    }
}
