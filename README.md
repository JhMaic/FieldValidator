
# FieldValidator for Godot (C#)

**A strict, pre-run dependency checker for Godot C#.**

FieldValidator ensures that all your critical `[Export]` fields are assigned before the game starts. It scans your Resources and Scenes, enforcing assignments via attributes, and prevents the project from running if dependencies are missing.

## Why use this?

Godot's built-in `_GetConfigurationWarnings` is powerful but often overkill for simple null checks:

1. **Boilerplate:** Writing a unique validation method for every single field is tedious.
2. **The `[Tool]` Virus:** To see warnings in the editor, your scripts must use `[Tool]`. This forces *every* dependency (other Nodes/Resources) to also be `[Tool]`, which can break complex logic or cause instantiation issues (see [Godot Issue #80298](https://github.com/godotengine/godot/issues/80298)).

**FieldValidator solves this.** It runs purely as an Editor Plugin. Your runtime scripts remain clean, standard C# classes—no `[Tool]` required.

## Key Features

* **Zero Runtime Overhead:** Validation happens only when you click "Build/Play".
* **Decoupled Logic:** Works on any Node or Resource, regardless of whether it uses `[Tool]`.
* **Build Blocking:** If a required field is null, the build is cancelled immediately, preventing runtime crashes.
* **Recursive Scanning:** Checks Scenes, Nodes, and even Resources nested inside other Resources.
* **Collection Support:** Validates that arrays/lists are not only assigned but that their *contents* are not null.

## Installation

1. Copy the `FieldValidator` folder into your project's `addons/` directory.
2. Go to **Project -> Project Settings -> Plugins** and enable **FieldValidator**.

## Usage

Simply add the namespace and tag your exported fields with the provided attributes.

### 1. Enforce Single Assignments (`[MustSet]`)

Use `[MustSet]` to ensure a field is not null.

```csharp
using Godot;
using FieldValidator;

public partial class MyPlayer : Node
{
    // If this is null in the Inspector, the game will refuse to start.
    [Export] [MustSet] 
    public required PackedScene BulletPrefab { get; set; } = null!;

    [Export] [MustSet] 
    public required NodePath EnemyPath = null!; 
}

```

### 2. Enforce Collection Integrity (`[MemberMustSet]`)

Use `[MemberMustSet]` to ensure a collection is not null **AND** that none of its elements are null.

```csharp
public partial class Inventory : Resource
{
    // Fails if the array is null OR if any item inside calls IsInstanceValid() == false
    [Export] [MustSet] [MemberMustSet]
    public Godot.Collections.Array<ItemResource> StartingItems { get; set; }
}

```

## Workflow

1. **Automatic Check:** Whenever you build the project (Play), the plugin scans all `.tscn` and `.tres` files.
* **Pass:** The game starts normally.
* **Fail:** The build is aborted. The Output panel lists exactly which file, node, and property caused the error.


2. **Manual Check:** Open `addons/FieldValidator/FieldValidatorScript.cs` in the Script Editor and go to **File > Run**. This performs a validation scan without trying to launch the game.

## Example Output

When a field is missing, the Godot console will output the following and **block the game from starting**:

```text
[❌ Error] [Resource: res://resources/Actions/AnyActionDef.tres]->_transitions[4] :: _conditions -> Array element at [2] is null (MemberMustSet)
[❌ Error] [Scene: res://scenes/Characters/Player/Player.tscn] :: [Node: MotorComponent] :: TimeContext -> Assignment required (MustSet)
[❌ Error] [Scene: res://scenes/Test/Playground.tscn] :: [Node: MotorComponent] :: TimeContext -> Assignment required (MustSet)

❌ Validation Failed! Found 3 null reference errors. Execution blocked.
Duration: 6.04ms
```



## Configuration (.fieldignore)

To prevent the validator from scanning specific folders (e.g., third-party addons), create a file named `.fieldignore` in your project root (`res://`).

Format:

```text
# Ignore specific folders
addons/
test_assets/

# Ignore specific files
res://Scenes/Draft/WipLevel.tscn

```

*Note: `.godot/`, `.vs/`, and `.vscode/` are ignored by default.*

## License

MIT License.