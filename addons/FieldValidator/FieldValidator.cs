#if TOOLS
using Godot;

namespace FieldValidator;

[Tool]
public partial class FieldValidator : EditorPlugin
{
    // Toggle: If the project becomes very large and scanning is too slow, 
    // it can be temporarily disabled here.
    private readonly bool _isEnabled = true;

    public override void _EnterTree()
    {
        GD.Print("FieldValidator plugin loaded. [MustSet] properties will be checked before each run.");
    }

    public override void _ExitTree()
    {
        // Cleanup code (if any)
    }

    /// <summary>
    ///     Called when the editor builds the project (usually triggered after clicking the Play button).
    ///     Returning false will cancel the build and prevent the game from running.
    /// </summary>
    public override bool _Build()
    {
        if (!_isEnabled)
            return true;

        // Call the static method of FieldValidatorScript
        // If it returns false (validation failed), execution is blocked.
        var passed = FieldValidatorScript.RunFullValidation();

        return passed;
    }
}
#endif