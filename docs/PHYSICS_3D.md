# 3D Physics Architecture: BepuPhysics v2.4 Integration

Light Engine integrates **BepuPhysics v2.4**, a state-of-the-art C# physics engine utilizing raw memory buffers and SIMD instructions for maximum throughput on lower-end CPUs.

---

## 1. Integration Highlights

- **Direct Memory Allocation (`BufferPool`)**: Memory used for broadphase trees, contact listeners, and body solver batches is allocated from reusable unmanaged buffer pools, preventing Gen0/Gen1 GC allocations during active simulation steps.
- **Fixed Timestep Synchronization**: Physics updates run at a guaranteed fixed rate (default 60 Hz). Transform synchronizations smoothly interpolate into `Transform3D` matrices for judder-free camera tracking.
- **Dynamic Collider Shapes**:
  - `Box` (Dimensions: X, Y, Z)
  - `Sphere` (Radius)
  - `Capsule` (Height, Radius - essential for character controllers)
  - `Static Mesh / Terrain` (Triangular heightfield surfaces)

---

## 2. Character Controller 3D

The `CharacterController3D` component handles realistic first-person and third-person physics:
- **Upright Constrained Orientation**: Maintains upright posture across slopes using angular velocity dampening.
- **Ground Detection & Snapping**: Continuous sphere-cast downward to prevent bouncing when descending ramps.
- **Slope Handling**: Automatically calculates walkable angles (up to 45 degrees); sliding occurs on steeper inclines.
- **Jump & Momentum**: Impulse-based vertical velocity addition with air drag friction.

---

## 3. High-Performance Raycasting

`IPhysics3DService.Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit3D hit)`:
- Queries BepuPhysics' tree structures in $O(\log N)$ time.
- Used in the Editor for 3D Viewport mouse ray picking (converting screen space mouse clicks directly into selected 3D world entities).
