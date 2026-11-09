using GalaReconciler.Web.Features.Reconciliation.Domain;
using GalaReconciler.Web.Features.Reconciliation.Parsing;

namespace GalaReconciler.Tests.Parsing;

public class TicketTypeParserTests
{
    [Theory]
    [InlineData("Thursday attending", EventDay.Thursday, AttendanceState.Attending)]
    [InlineData("Friday not attending", EventDay.Friday, AttendanceState.NotAttending)]
    [InlineData("Saturday attending", EventDay.Saturday, AttendanceState.Attending)]
    [InlineData("SUNDAY NOT ATTENDING", EventDay.Sunday, AttendanceState.NotAttending)]
    [InlineData("Grid Gala attending", EventDay.GridGala, AttendanceState.Attending)]
    [InlineData("grid  gala   not attending", EventDay.GridGala, AttendanceState.NotAttending)]
    [InlineData("  Saturday attending  ", EventDay.Saturday, AttendanceState.Attending)]
    public void Parses_day_and_state(string ticketType, EventDay day, AttendanceState state)
    {
        Assert.True(TicketTypeParser.TryParse(ticketType, out var parsedDay, out var parsedState));
        Assert.Equal(day, parsedDay);
        Assert.Equal(state, parsedState);
    }

    [Theory]
    [InlineData("Sunday waitlist")]
    [InlineData("Monday attending")]
    [InlineData("Saturday")]
    [InlineData("VIP")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_unknown_types(string? ticketType) =>
        Assert.False(TicketTypeParser.TryParse(ticketType, out _, out _));
}
