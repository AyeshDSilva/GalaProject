using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Parsing;

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

    [Theory]
    [InlineData("RSVP - Attending", AttendanceState.Attending)]
    [InlineData("RSVP - Not attending", AttendanceState.NotAttending)]
    [InlineData("rsvp - not  attending", AttendanceState.NotAttending)]
    [InlineData("RSVP – Attending", AttendanceState.Attending)]
    [InlineData("RSVP Attending", AttendanceState.Attending)]
    [InlineData("Grid Gala not attending", AttendanceState.NotAttending)]
    public void Gala_export_ticket_types_are_grid_gala(string ticketType, AttendanceState state)
    {
        Assert.True(TicketTypeParser.TryParseGala(ticketType, out var day, out var parsedState));
        Assert.Equal(EventDay.GridGala, day);
        Assert.Equal(state, parsedState);
    }

    [Theory]
    [InlineData("RSVP - Waitlist")]
    [InlineData("RSVP")]
    [InlineData("")]
    public void Gala_export_rejects_unknown_types(string ticketType) =>
        Assert.False(TicketTypeParser.TryParseGala(ticketType, out _, out _));

    [Fact]
    public void Main_export_does_not_read_gala_rsvp_types() =>
        Assert.False(TicketTypeParser.TryParse("RSVP - Attending", out _, out _));
}
