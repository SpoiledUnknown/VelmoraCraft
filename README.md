# VelmoraCraft

A small voxel rendering experiment built with **C# and OpenTK** while learning **OpenGL**.

It's mainly a **learning project**, so the code and architecture are still being worked on and changed as I learn more.

## What it currently does

* Creates an OpenGL 3.3 window using OpenTK
* Renders 3D voxel terrain
* Generates terrain procedurally using Simplex Noise
* Uses chunks to organize the voxel world
* Generates only visible block faces
* Uses indexed OpenGL rendering
* Uses VAOs, VBOs and IBOs
* Uses GLSL vertex and fragment shaders
* Supports a texture atlas
* Supports different textures for different block faces
* Has a basic first-person free-fly camera
* WASD movement and mouse look
* Depth testing and back-face culling
* Basic `Dirt`, `Grass` and `Empty` block types

## Running

### Requirements

You'll need:

* [.NET SDK](https://dotnet.microsoft.com/download)
* An OpenGL-capable graphics driver
* Git, if cloning the repository

OpenTK is available for Windows, Linux and other platforms, provided the system has suitable graphics support.

### 1. Clone the repository

```bash
git clone <repository-url>
cd VelmoraCraft
```

open the project in **Visual Studio**, **Rider**, or another C# IDE and run it normally.

### Controls

| Key          | Action        |
|--------------|---------------|
| `W A S D`    | Move          |
| `Space`      | Move up       |
| `Left Shift` | Move down     |
| `Mouse`      | Look around   |
| `Escape`     | Release mouse |

## Project Structure

```text
VelmoraCraft/
├── Program.cs
├── Game.cs
├── Shaders/
│   ├── Default.vert
│   └── Default.frag
├── Textures/
│   └── atlas.png
└── src/
    ├── Camera.cs
    ├── Graphics/
    │   ├── Index Buffer Object.cs
    │   ├── Shader Program.cs
    │   ├── Texture Data.cs
    │   ├── Texture.cs
    │   ├── Vertex Array Object.cs
    │   └── Vertex Buffer Object.cs
    └── World/
        ├── Block Data.cs
        ├── Block.cs
        └── Chunk.cs
```

## Why I made this

Mostly to learn.

I wanted to understand what actually happens underneath a game engine when rendering a 3D world — things like shaders,
buffers, textures, cameras, transformations and voxel meshes.

OpenTK is a good fit for this because it provides low-level access to OpenGL without being a game engine itself.

For learning OpenGL with C#, the official [LearnOpenTK tutorials](https://opentk.net/learn/index.html) were also useful
for understanding the basics of windows, buffers, shaders, textures, transformations and cameras.

## Note

This is **not intended to be a Minecraft recreation** or a production-ready engine.

It's just a personal project that I'm building incrementally while learning OpenGL and experimenting with voxel
rendering.
