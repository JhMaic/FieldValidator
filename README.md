# FieldValidator for Godot (C#)

**FieldValidator** is a lightweight utility for Godot 4.x C# projects. It ensures that your `[Export]` fields are properly assigned in the Inspector before the game starts, shifting potential runtime crashes to a pre-run validation phase.

If a mandatory field is left empty, the plugin blocks the build and prints a detailed error message in the console, identifying the specific Scene, Node, or Resource.

## 💡 Why Use This?

In standard Godot development, scripts often rely on other nodes or resources. This plugin promotes a safer and cleaner architecture:

1.  **Explicit Dependency Management**: By using `[Export]` combined with `[MustSet]`, you make node dependencies explicit in the Inspector. Anyone looking at the scene instantly knows exactly what a script needs to function.
2.  **Eliminate Brittle `GetNode()` Calls**: Instead of using `GetNode("Path/To/Node")` or `%UniqueName` in `_Ready()`—which can fail silently or crash the game if the tree structure changes—you can assign references via the Inspector and let this plugin guarantee they are valid before the code ever runs.
3.  **Fail Fast**: Catching a missing reference at build-time is much cheaper than debugging a `NullReferenceException` 10 minutes into a playtest.
## ✨ Features

- **Pre-run Validation**: Automatically scans your project when you click "Play".
- **Scene & Resource Support**: Validates nodes within `.tscn` files and properties in `.tres`/`.res` files.
- **Recursive Checking**: Deep-scans nested Resources and Child Nodes.
- **Ignore System**: Easily exclude specific folders (like third-party addons) via a `.fieldignore` file.

> **Note:** The plugin performs a **global check** of the entire project every time you run the game, regardless of which specific scene you are launching. This ensures project-wide consistency but may take a moment in very large projects (use `.fieldignore` to optimize).

## 🚀 Installation

1. Copy the `addons/FieldValidator` folder into your project's `res://addons/` directory.
2. Build your C# solution in Godot.
3. Go to **Project -> Project Settings -> Plugins** and enable **FieldValidator**.

## 🛠 Usage

Simply apply the provided attributes to any exported field or property in your C# scripts:

### 1. `[MustSet]`
Use this on single object references (Nodes, Resources, etc.) that **must** be assigned in the Inspector.

```csharp
using FieldValidator;

public partial class MyPlayer : CharacterBody2D
{
    [Export] [MustSet] 
    private required Sprite2D _sprite = null!; // Build will fail if this is empty
}
```

### 2. `[MemberMustSet]`
Use this on collections (Arrays or Lists) to ensure that every single slot in the collection has been assigned a value.

```csharp
using FieldValidator;

public partial class EnemySpawner : Node
{
    [Export] [MemberMustSet]
    private Godot.Collections.Array<PackedScene> _enemyTypes; // Fails if any index is null
}
```

## 📝 Example Output

When a field is missing, the Godot console will output the following and **block the game from starting**:

```text
[❌ Error] [Resource: res://resources/Actions/AnyActionDef.tres]->_transitions[4] :: _conditions -> Array element at [2] is null (MemberMustSet)
[❌ Error] [Scene: res://scenes/Characters/Player/Player.tscn] :: [Node: MotorComponent] :: TimeContext -> Assignment required (MustSet)
[❌ Error] [Scene: res://scenes/Test/Playground.tscn] :: [Node: MotorComponent] :: TimeContext -> Assignment required (MustSet)

❌ Validation Failed! Found 3 null reference errors. Execution blocked.
Duration: 6.04ms
```


## ⚙️ Configuration (.fieldignore)

The first time the plugin runs, it creates a `.fieldignore` file in your project root (`res://`). You can add paths to this file (one per line) to skip validation for specific folders or files.

**Example `.fieldignore`:**
```text
# Ignore Path
.godot/
.vs/
.vscode/
res://addons/
src/
```

## 🔍 Manual Validation

You can also trigger a full project scan manually at any time:
1. Open `FieldValidatorScript.cs` in the FileSystem dock.
2. Right-click it and select **Run**.
3. View the detailed report in the **Output** bottom panel.

## 📈 Future Outlook
This plugin is designed to be extensible. While it currently focuses on null-checks via `MustSet` and `MemberMustSet`, the architecture allows for future attributes such as range validation, string pattern matching, or custom logic constraints.

