# AvaGine Template
A minimal template for an Evergine environment that uses Avalonia UI.

## Parameters
* `EvergineVersion`: The Evergine version to use. The default is `2026.5.26.2553`.
* `AvaloniaVersion`: The Avalonia version to use. The default is `12.1.2`.

## Installation
1. `git clone` (or download and extract) this repository to a suitable location `path\to\AvaGineTemplate\...`.
2. Install the template using `dotnet new install "path\to\AvaGineTemplate\`
3. Create the template using `dotnet new avagine-minimal --name <EnvName>`. Replace `<EnvName>` with your own name. The contents of the template will be placed in a new folder named `<EnvName>`.

## Structure
There are five important folders included:

### `EvergineContent`
Assets (scenes, shader, textures, models, ...) for Evergine.

### `<EnvName>`
Evergine project. Responisble for the actual Evergine logic (like scenes, entities, ...). Referenced by: `<EnvName>.Avalonia`, `<EnvName>.Windows`, `<EnvName>.Editor`; References: `none`.

### `<EnvName>.Avalonia`
Avalonia entry point. Responsible for everything Avalonia related (including window creation & management). Referenced by: `none`; References: `<EnvName>`.

### `<EnvName>.Windows`
Project required by the Evergine Editor. Manages Evergine setup for the Windows platform. Referenced by: `none`; References: `<EnvName>`, `<EnvName>.Editor`.

### `<EnvName>.Editor`
Project for Evergine Editor Extensions. Referenced by: `<EnvName>.Windows`; References: `<EnvName>`.

## Building
Build solutions using your IDE or this command from within your `<EnvName>` folder: `dotnet build .\<Solution>`. There are two solutions available:

### `<EnvName>.Avalonia.sln`
This builds the version using Avalonia. Projects Affected:
* `<EnvName>`
* `<EnvName>.Avalonia`

### `<EnvName>.Windows.sln`
This builds the raw Evergine version _without_ Avalonia. This solution is required for the Editor to work. Projects Affected:
* `<EnvName>`
* `<EnvName>.Editor`
* `<EnvName>.Windows`