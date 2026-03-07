using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Persists student identity and class connection using MAUI Preferences
/// (survives app restarts, stored in platform key-value store).
/// </summary>
public sealed class StudentSettingsService
{
    private const string KeyName = "student_name";
    private const string KeyTeacherEmail = "teacher_email";
    private const string KeyInviteCode = "class_invite_code";

    public StudentSettings Load() => new()
    {
        StudentName   = Preferences.Default.Get(KeyName, string.Empty),
        TeacherEmail  = Preferences.Default.Get(KeyTeacherEmail, string.Empty),
        ClassInviteCode = Preferences.Default.Get(KeyInviteCode, string.Empty)
    };

    public void Save(StudentSettings settings)
    {
        Preferences.Default.Set(KeyName, settings.StudentName);
        Preferences.Default.Set(KeyTeacherEmail, settings.TeacherEmail);
        Preferences.Default.Set(KeyInviteCode, settings.ClassInviteCode);
    }

    public void Clear()
    {
        Preferences.Default.Remove(KeyName);
        Preferences.Default.Remove(KeyTeacherEmail);
        Preferences.Default.Remove(KeyInviteCode);
    }
}