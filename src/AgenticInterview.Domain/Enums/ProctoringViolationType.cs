namespace AgenticInterview.Domain.Enums;

/// <summary>
/// Represents the type of proctoring violation detected during an interview.
/// </summary>
public enum ProctoringViolationType
{
    TabSwitch,
    WindowBlur,
    CopyPaste,
    RightClick,
    ClipboardAccess,
    DevToolsOpen,
    FaceNotVisible,
    MultipleFaces,
    LookingAway,
    BlockedKeyboardShortcut
}
