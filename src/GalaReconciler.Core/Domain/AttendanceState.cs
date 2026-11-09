namespace GalaReconciler.Core.Domain;

/// <summary>
/// A person's answer for one day. The RSVP export gives Attending, NotAttending, or NoResponse (no ticket for
/// that day). The master list gives Attending (Y), NotAttending (N), AwaitingResponse (1) or NotInvited (blank).
/// </summary>
/// <remarks>The order is also the merge priority when a person has duplicate rows: earlier wins.</remarks>
public enum AttendanceState
{
    Attending,
    NotAttending,
    AwaitingResponse,
    NotInvited,
    NoResponse,
}

public static class AttendanceStateExtensions
{
    public static string ToDisplay(this AttendanceState state) => state switch
    {
        AttendanceState.Attending => "Attending",
        AttendanceState.NotAttending => "Not attending",
        AttendanceState.AwaitingResponse => "Invited, no response",
        AttendanceState.NotInvited => "Not invited",
        _ => "No RSVP",
    };
}
