namespace GalaReconciler.Core.Domain;

public enum AttendanceState
{
    Attending,
    NotAttending,
    NoResponse,
}

public static class AttendanceStateExtensions
{
    public static string ToDisplay(this AttendanceState state) => state switch
    {
        AttendanceState.Attending => "Attending",
        AttendanceState.NotAttending => "Not attending",
        _ => "No response",
    };
}
