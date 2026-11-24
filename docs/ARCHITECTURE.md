# Light Engine Architecture & Engineering Design

This document details the internal systems, lifecycle execution pipeline, memory management, and technical architecture of **Light Engine**.

---

## 1. High-Level System Architecture

Light Engine follows a clean decoupled architecture separated into core engine libraries, an interactive development editor, and runtime game projects:

```mermaid
graph TD
    subgraph Host ["Runtime & Native Layer"]
        MG["MonoGame DesktopGL (OpenGL 3.0+)"]
        SDL["libSDL2 (Windowing & Input)"]
        AL["OpenAL (Spatial Audio)"]
        BEPU_NATIVE["SIMD Math Acceleration"]
    end

    subgraph Core ["LightEngine.Core (.NET 9)"]
        SERVICES["Engine.Services (ServiceLocator)"]
        TIME["TimeManager (Fixed & Variable Timestep)"]
        INPUT["InputManager (Key/Mouse/Gamepad Bindings)"]
        ECS["Entity-Component-System (Scene & Hierarchies)"]
        RENDER2D["SpriteBatchRenderer & Camera2D"]
        RENDER3D["Renderer3D & Camera3D (CSM / Lightmaps / Post-FX)"]
        PHYS2D["Physics2D (AABB / SAT / Broadphase)"]
        PHYS3D["Physics3D (BepuPhysics v2.4 Integration)"]
        AUDIO["AudioManager (2D / 3D Spatial Audio)"]
        SCRIPTS["ScriptManager (Roslyn C# In-Memory Hot Reload)"]
        POOLS["ObjectPool<T> (Zero-Allocation Buffers)"]
    end

    subgraph Editor ["LightEngine.Editor"]
        IMGUI["ImGui.NET Docking Branch"]
        DOCK_BUILDER["ImGuiDockBuilder (cimgui P/Invoke)"]
        SCENE_VIEW["Scene Viewport (Gizmo / 3D Fly / Picking)"]
        GAME_VIEW["Game Viewport (Runtime Playback & Aspect Ratio)"]
        HIERARCHY["Scene Hierarchy Panel"]
        INSPECTOR["Reflection Inspector Panel"]
        ASSETS["Asset Browser & File Watcher"]
        CLI["Integrated Console & Command Runner"]
    end

    subgraph Runtime ["Game Application"]
        USER_GAME["Hybrid 2D/3D Game Runtime"]
    end

    Host --> Core
    Core --> Editor
    Core --> Runtime
```

---

## 2. Decoupled Service-Oriented Framework (`Engine.Services`)

All engine subsystems communicate through strictly typed interfaces managed by `ServiceLocator`. This eliminates hard static couplings and allows seamless mocking during test suites:

| Interface | Implementation | Primary Responsibility |
| :--- | :--- | :--- |
| `ITimeService` | `TimeManager` | Manages delta time, variable frame pacing, and fixed timestep accumulator (60 Hz). |
| `IInputService` | `InputManager` | Polls keyboard, mouse, gamepad, and evaluates configurable action maps. |
| `ISceneService` | `SceneManager` | Handles active scene transitions, lifecycle events, and serialization. |
| `IAssetService` | `AssetManager` | Resource caching for textures, meshes, sound effects, and shaders. |
| `IRenderService` | `RenderManager` | 2D batching pipeline with sorting and transformation matrices. |
| `IRender3DService` | `Renderer3D` | 3D rendering pipeline: Blinn-Phong, PBR-lite, Cascaded Shadow Maps, Soft Lightmap Baker, SSAO, Bloom, FXAA. |
| `IPhysics2DService` | `Physics2DManager` | 2D collision detection, broadphase sweep, layer masks, and trigger dispatch. |
| `IPhysics3DService` | `Physics3DManager` | BepuPhysics v2.4 simulation, raycasting, dynamic bodies, and character controller. |
| `IAudioService` | `AudioManager` | Spatial 3D sound listeners, distance attenuation, and BGM streaming. |
| `IScriptService` | `ScriptManager` | Roslyn C# syntax tree compilation with collectible AssemblyLoadContext hot reload. |

---

## 3. Game Loop & Frame Execution Pipeline

The game loop enforces deterministic physics updates while maintaining decoupled variable-rate rendering:

```mermaid
sequenceDiagram
    autonumber
    participant App as LightEngineGame
    participant Time as TimeManager
    participant Input as InputManager
    participant Phys as Physics (2D & 3D)
    participant Scene as Scene Lifecycle
    participant Render as Graphics Pipeline

    Note over App: Main Frame Tick (Update)
    App->>Time: Update(gameTime)
    App->>Input: Poll & Update State
    
    loop While Accumulated Time >= FixedStep (60 Hz)
        App->>Phys: StepSimulation(FixedDeltaTime)
        App->>Scene: FixedUpdate(FixedDeltaTime)
    end

    App->>Scene: Update(DeltaTime) [Scripts, Animators, Emitters]

    Note over App: Render Phase (Draw)
    App->>Render: 3D Geometry Pass (Depth24, Frustum Culling)
    App->>Render: Shadow Map & Lighting Passes
    App->>Render: Post-Processing Pass (SSAO, Bloom, Tone Mapping)
    App->>Render: 2D SpriteBatch & World Overlay Pass
    App->>Render: ImGui Editor DockSpace & GUI Pass
    App->>App: Present Frame (VSync / SwapBuffers)
```

---

## 4. Memory Management & Zero-Allocation Principles

To guarantee stable 60+ FPS on low-spec integrated graphics (e.g. Intel HD Graphics + 4 GB RAM):
- **Object Pooling (`ObjectPool<T>`)**: Heavy or transient objects (collision contact manifolds, particle nodes, raycast hits) are recycled through thread-safe pools to eliminate garbage collection spikes (`GC Gen0`).
- **SIMD Optimized 3D Physics**: BepuPhysics v2 relies heavily on raw memory structures (`BufferPool`) and AVX2/NEON vector instructions, bypassing managed heap overhead.
- **Dynamic C# Hot Reload**: Uses `System.Runtime.Loader.AssemblyLoadContext(isCollectible: true)`. Scripts are compiled entirely in memory through Roslyn stream emitters; obsolete contexts are collected by GC without memory leakage or restarting the editor.
