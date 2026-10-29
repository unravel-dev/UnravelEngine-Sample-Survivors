# UnravelEngine C# API digest - for a 3D survivors-like game

Source of truth: engine_data/data/scripts/**.cs (namespace Unravel.Core; math types are GLOBAL namespace).
Signatures below are copied from source. "//" = one-line semantics. Verified 2026-10-28 on working tree.

Project basics (editor/editor/editing/editor_actions.cpp, AGENTS.template):
- Backend CoreCLR (.NET 10 shared framework ships; System.Text.Json available). ImplicitUsings DISABLED,
  Nullable disabled, AllowUnsafeBlocks true -> write `using System; using System.Collections;
  using System.Collections.Generic; using Unravel.Core;` yourself.
- Scripts live under app:/data/** (outside data/ not compiled). Asset keys: "app:/data/Foo/Bar.ext".
- New-script template: engine_data/data/templates/TemplateComponent.cs.in (`[ScriptSourceFile] class X : ScriptComponent`).

---------------------------------------------------------------------------------------------------
## 1. ScriptComponent lifecycle, fields, attributes

```
[ScriptSourceFile]                                   // keep on every ScriptComponent class (inspector Script row needs it)
public abstract class ScriptComponent : Component
public virtual void OnCreate()                       // once per instance when created/instantiated (play mode only)
public virtual void OnEnable()                       // script/entity enabled
public virtual void OnDisable()                      // entity deactivated; ALSO stops all coroutines of this script
public virtual void OnStart()                        // once, before first update; not called while disabled
public virtual void OnUpdate()                       // every frame
public virtual void OnFixedUpdate()                  // physics step (Time.fixedDeltaTime)
public virtual void OnLateUpdate()                   // after all updates (SampleHorde draws here)
public virtual void OnDestroy()                      // also ends UI subscriptions, Screen.resized handlers, data-model bindings
public virtual void OnSensorEnter(Collision collision)
public virtual void OnSensorExit(Collision collision)
public virtual void OnCollisionEnter(Collision collision)
public virtual void OnCollisionExit(Collision collision)
public virtual void OnAnimationEvent(AnimationEvent animationEvent)  // before OnUpdate of the frame crossed; a public
                                                     // method named exactly like the event is ALSO called (no args, the
                                                     // AnimationEvent, or its string/float/int parameter)
public Coroutine StartCoroutine(IEnumerator routine) // runs to first yield now, then one step/frame after updates
public void StopCoroutine(Coroutine coroutine)
public void StopAllCoroutines()
public Coroutine Invoke(float delay, Action action)                          // NOTE arg order: delay first
public Coroutine InvokeRepeating(float delay, float interval, Action action) // interval <= 0 -> every frame
```
Component base (scene/Components.cs):
```
public Entity owner { get; internal set; }           // [Hide]
public TransformComponent transform                  // cached owner.transform
public override bool IsValid()                       // owner valid and still has this component
```
- Order: OnCreate -> OnEnable -> OnStart -> OnUpdate / OnFixedUpdate / OnLateUpdate -> OnDisable / OnDestroy.
- No `enabled` property on ScriptComponent (only on Model/Light/ParticleEmitter/UIDocument). Toggle via entity.SetActive.
- Update dispatch: only types that override OnUpdate/OnFixedUpdate/OnLateUpdate are registered (System.cs ScriptComponentManager).

Serialized members (engine/engine/meta/ecs/components/script_component.cpp):
- PUBLIC, non-static fields and public properties WITH a setter; [Hide] excludes. Private fields are never saved
  (no [SerializeField]).
- Types: sbyte byte short ushort int uint long ulong bool float double char string Entity Vector2 Vector3 Vector4
  Quaternion Color LayerMask, assets Texture Material Mesh AnimationClip AnimationController Prefab Scene
  PhysicsMaterial AudioClip Font; enums; T[] and List<T> of those; Dictionary<K,V>; nested class/struct marked
  [System.Serializable]. Not saved: Gradient, Curve, MinMaxCurve, Range<T>, Rect, Bounds, VisualTree, StyleSheet
  (none is [Serializable]).
- Editor/MCP set fields by name: assets as keys ("app:/data/Prefabs/Enemy.pfb"), Entity as id, enums by name.

Attributes (system/Attributes.cs; Field targets unless noted):
```
public RangeAttribute(float min, float max)
public MinAttribute(float min)
public MaxAttribute(float max)
public StepAttribute(float step)
public TooltipAttribute(string tooltip)
public HeaderAttribute(string header)                // field or property, AllowMultiple
public FoldableAttribute()                           // nested serializable object as foldout
public HideAttribute()                               // hide from inspector AND from serialization
[AutoStaticsCleanup]                                 // class whose statics must reset on script domain unload (hot reload)
```

---------------------------------------------------------------------------------------------------
## 2. Entity / Scene / transforms / pooling

Entity (scene/Entity.cs) - a STRUCT; never compare to null:
```
public static readonly Entity Invalid                // id 0
public readonly uint Id;
public bool active { get; }                          // active in hierarchy
public bool activeLocal { get; }
public void SetActive(bool active)                   // sets local active
public string name { get; set; }
public string tag { get; set; }
public LayerMask layers { get; set; }
public TransformComponent transform { get; }
public bool IsValid()                                // also: if (entity) / if (!entity)
public Component AddComponent(Type type)
public T AddComponent<T>() where T : Component, new()
public bool HasComponent<T>() where T : Component
public T GetComponent<T>() where T : Component       // null when missing
public T[] GetComponents<T>() where T : Component
public T GetComponentInChildren<T>() where T : Component        // self included, depth first
public T[] GetComponentsInChildren<T>() where T : Component
public bool RemoveComponent(Component component)
public bool RemoveComponent(Component component, float secondsDelay)
public bool RemoveComponent<T>() where T : Component
public bool RemoveComponent<T>(float secondsDelay) where T : Component
```
Scene (scene/Scene.cs) - all static:
```
public static void LoadScene(string key)             // deferred (queued, applies later in the frame)
public static void LoadScene(Scene scene)
public static void ReloadScene()
public static SceneInstance LoadSceneAdditive(string key)        // entities stay until UnloadScene/LoadScene/ReloadScene
public static SceneInstance LoadSceneAdditive(Scene scene)
public static void UnloadScene(SceneInstance instance)
public static bool DontDestroyOnLoad(Entity entity)  // root entities only; false if it has a parent
public static Entity Instantiate(Prefab prefab)
public static Entity Instantiate(string key)
public static Entity Instantiate(Prefab prefab, Entity parent)
public static Entity Instantiate(string key, Entity parent)
public static Entity Instantiate(Prefab prefab, Vector3 position)                 // world position
public static Entity Instantiate(string key, Vector3 position)
public static Entity Instantiate(Prefab prefab, Vector3 position, Entity parent)
public static Entity Instantiate(string key, Vector3 position, Entity parent)
public static Entity Instantiate(Prefab prefab, Vector3 position, Quaternion rotation, Entity parent)  // world pose
public static Entity Instantiate(string key, Vector3 position, Quaternion rotation, Entity parent)
public static Entity CloneEntity(Entity e)
public static Entity CreateEntity(string name = "Unnamed")
public static void DestroyEntity(Entity entity, float seconds = 0.0f)   // queued: entity stays valid this frame
public static void DestroyEntityImmediate(Entity entity)
public static bool IsEntityValid(Entity entity)
public static Entity FindEntityByTag(string tag)     // Entity.Invalid when none
public static Entity[] FindEntitiesByTag(string tag)
public static Entity FindEntityByName(string name)
public static Entity[] FindEntitiesByName(string name)
public static Entity[] FindEntitiesWithComponent<T>() where T : Component
public readonly struct SceneInstance { public bool IsValid { get; } }
```
- No rotation overload without parent: pass Entity.Invalid as parent (binding maps id 0 to "no parent").
- Prefab/Scene/Mesh/... assets: `Assets.GetAsset<T>(string key)` / `Assets.GetAsset<T>(Guid uid)` (assets/Assets.cs).

TransformComponent (scene/components/TransformComponent.cs):
```
public Entity parent { get; set; }
public Entity[] children { get; }
public Entity FindChild(string path, bool recursive = false)    // name or "a/b" path
public void SetParent(Entity parent, bool globalPositionStays)
public Vector3 position / localPosition                          // world / parent-relative
public Vector3 eulerAngles / localEulerAngles
public Quaternion rotation / localRotation
public Vector3 scale / localScale
public Vector3 skew / localSkew
public Vector3 right / localRight / up / localUp / forward / localForward   // settable
public void MoveBy(Vector3 amount)                  public void MoveByLocal(Vector3 amount)
public void ScaleBy(Vector3 amount)                 public void ScaleByLocal(Vector3 amount)
public void RotateBy(Quaternion amount)             public void RotateByLocal(Quaternion amount)
public void RotateByEuler(Vector3 amount)           public void RotateByEulerLocal(Vector3 amount)
public void RotateAxis(float degrees, Vector3 axis)
public void LookAt(Vector3 point)                   public void LookAt(Vector3 point, Vector3 up)
public void LookAt(Entity target)                   public void LookAt(Entity target, Vector3 up)
public void LookAt(TransformComponent target)       public void LookAt(TransformComponent target, Vector3 up)
public Vector3 TransformVector(Vector3 vector)      public Vector3 InverseTransformVector(Vector3 vector)
public Vector3 TransformDirection(Vector3 direction) public Vector3 InverseTransformDirection(Vector3 direction)
public void RotateAround(Vector3 point, Vector3 axis, float angle)   (+ Entity / TransformComponent overloads)
public void MoveTowards(Entity target, float maxDistanceDelta)
public static Entity FindClosestBone(this TransformComponent root, Vector3 point)   // extension
```
LayerMask (scene/LayerMask.cs): `public int value;` implicit to/from int; `+`/`-` with layer index or mask;
`public static string LayerToName(int layer)`, `public static int NameToLayer(string layerName)`,
`public static int GetMask(params string[] layerNames)`; consts `Nothing = 0`, `Everything = -1`.

EntityPool (scene/EntityPool.cs):
```
public interface IPoolable { void OnAcquire(); void OnRelease(); }   // on a script of the instance ROOT
public EntityPool(Prefab prefab, int prewarm = 0)
public static EntityPool For(Prefab prefab)          // one shared pool per prefab
public int InactiveCount                             public int TotalCount
public void Prewarm(int count)
public Entity Acquire()                              // activates
public Entity Acquire(Vector3 position, Quaternion rotation)
public Entity Acquire(Entity parent)
public void Release(Entity entity)                   // stops root audio, deactivates; double release no-op
public void Release(Entity entity, float delay)      // game time; later Release replaces pending
public void Clear()                                  // destroys all pooled entities
```

---------------------------------------------------------------------------------------------------
## 3. Time, Random, math, Log, Profiler, Gizmos, Screen

Time (system/System.cs) - static fields:
```
public static float time;                 // since play began
public static float deltaTime;            // scaled; 0 on the FIRST simulation frame (engine.cpp)
public static float unscaledDeltaTime;
public static float realtimeSinceStartup;
public static float fixedDeltaTime;
public static long frameCount;
public static float timeScale { get; set; }   // native sim time scale: engine frame dt is scaled (pause = 0);
                                          // coroutine WaitForSeconds and EntityPool delays use game time
```
Random (math/Random.cs) - static, per-thread generator (safe on workers):
```
public static void Seed(ulong seed)                  // seeds the CALLING thread only
public static void SeedWithTime()
public static ulong UInt64()       public static float Float()      public static double Double()
public static Vector3 Vec3()       public static float SignF()
public static float Range(float minValue, float maxValue)          // [min, max)
public static int Range(int minValue, int maxValue)                // [min, max)
public static Color color          public static Vector3 onUnitSphere
public static Vector3 insideUnitSphere   public static Vector2 insideUnitCircle
```
RandomGenerator (math/RandomGenerator.cs) - struct xoshiro256+, one per job for determinism:
```
public RandomGenerator(ulong seed)       public static RandomGenerator CreateFromTime()
public ulong NextUInt64()  public float NextFloat()  public double NextDouble()  public float NextSign()
public float Range(float minValue, float maxValue)   public int Range(int minValue, int maxValue)
public Vector3 NextVector3()  public Color NextColor()  public Vector3 OnUnitSphere()
public Vector3 InsideUnitSphere()  public Vector2 InsideUnitCircle()
```
Vector3 (math/Vector3.cs): x y z, this[int], normalized, magnitude, sqrMagnitude; statics zero one forward back up
down left right positiveInfinity negativeInfinity; Slerp, SlerpClamped, Lerp, LerpUnclamped,
`MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)`,
`SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)`
(no shorter overload), Scale, Cross, Reflect, Normalize, Dot, Project, ProjectOnPlane, Angle,
`SignedAngle(Vector3 from, Vector3 to, Vector3 axis)`, Distance, ClampMagnitude, Magnitude, SqrMagnitude, Min, Max;
ctors `Vector3(float x, float y, float z)`, `Vector3(float x, float y)`; ops + - * / (no Vector3*Vector3).
Vector2: same family + Perpendicular, `SignedAngle(Vector2 from, Vector2 to)`, component-wise * and /.
Implicit Vector2<->Vector3 (xy, z dropped/0), Vector4<->Vector3/Vector2, Color<->Vector4.
Quaternion (pure C#): identity, eulerAngles, normalized, FromToRotation, Conjugate, Inverse, Slerp, SlerpUnclamped,
Lerp, LerpUnclamped, `AngleAxis(float angle, Vector3 axis)` (degrees), `LookRotation(Vector3 forward)`,
`LookRotation(Vector3 forward, Vector3 upwards)`, Dot, Angle, `Euler(float x, float y, float z)`, `Euler(Vector3 euler)`,
`RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)`, Normalize; `operator *(Quaternion rotation, Vector3 point)`.
Matrix4x4 (math/Matrix4x4.cs, column-major m00 m10 m20 m30 m01 ...):
```
public Matrix4x4(Vector4 column0, Vector4 column1, Vector4 column2, Vector4 column3)
public static Matrix4x4 identity / zero
public static Matrix4x4 TRS(Vector3 position, Quaternion rotation, Vector3 scale)
public static Matrix4x4 Translate(Vector3 vector)  Scale(Vector3 vector)  Rotate(Quaternion q)
public Vector3 MultiplyPoint(Vector3 point)  MultiplyPoint3x4(Vector3 point)  MultiplyVector(Vector3 vector)
public Vector3 position   public Matrix4x4 transpose / inverse   public float determinant
public Vector4 GetColumn(int index)  public void SetColumn(int index, Vector4 column)  public Vector4 GetRow(int index)
```
Mathf (math/Mathf.cs): PI Infinity NegativeInfinity Deg2Rad Rad2Deg Epsilon; Sin Cos Tan Asin Acos Atan Atan2 Sqrt
Abs Min Max (params) Pow Exp Log Log10 Ceil Floor Round CeilToInt FloorToInt RoundToInt Sign Clamp Clamp01 Lerp
LerpUnclamped LerpAngle MoveTowards MoveTowardsAngle SmoothStep PerlinNoise(float x, float y) LinearToGammaSpace
GammaToLinearSpace FloatToHalf HalfToFloat Approximately SmoothDamp SmoothDampAngle (all 6 args) Repeat PingPong
InverseLerp DeltaAngle ClosestPowerOfTwo IsPowerOfTwo NextPowerOfTwo.
Color (math/Color.cs): r g b a; red green blue white black yellow cyan magenta gray grey clear; grayscale,
maxColorComponent; `Color(float r, float g, float b, float a)`, `Color(float r, float g, float b)`,
`Color(int r, int g, int b, int a)`, `Color(int r, int g, int b)`; Lerp, LerpUnclamped,
`RGBToHSV(Color rgbColor, out float H, out float S, out float V)`, `HSVToRGB(float H, float S, float V)`,
`HSVToRGB(float H, float S, float V, bool hdr)`; ops + - * /.
Gradient/Curve/MinMaxCurve/Vector3Curve (math/Gradient.cs): `new Gradient(params GradientKey[] keys)`,
`new GradientKey(float time, Color color)`, `new Curve(params CurveKey[] keys)`, `new CurveKey(float time, float value)`,
`new MinMaxCurve(float min, float max)`, `.Evaluate(float time)`. `Range<T>(T min, T max)` with Min/Max (Constructs.cs).
Rect (y down, top-left): x y width height, `Contains(Vector2 point)`, `Overlaps(Rect other)`. Bounds: Min Max, AddPoint,
ContainsPoint, GetCenter, GetExtents, Intersect overloads.

Log (logger/Logger.cs): `public static void Trace|Info|Warning|Error(string message, [CallerMemberName] string func = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)`.
Profiler: `public static void AddRecord(string name, float timeMs)`, `public static ProfilerScope Scope(string name)` (using).
Gizmos (debug only): `public static void AddSphere(Color color, Vector3 position, float radius)`,
`public static void AddRay(Color color, Vector3 position, Vector3 direction, float maxDistance = 99999.0f)`.
Screen (system/Screen.cs): `public static int width`, `height`, `float aspect`, `float dpi` (96 = 100 %),
`Rect safeArea` (top-left pixels), `public static event Action resized` (ends with subscribing script).
Application: `public static string persistentDataPath`, `public static string temporaryCachePath`, `public static void Quit()`.

---------------------------------------------------------------------------------------------------
## 4. Input

Input (input/Input.cs) - ACTION NAMES from project input settings, not keys:
```
public static Vector2 mousePosition                  // pixels, top-left origin (same space as Screen / UI rects)
public static int touchCount
public static bool touchSupported
public static bool simulateMouseWithTouches          // default on; play end turns it back on
public static Touch GetTouch(int index)              // excludes fingers held by a PointerZone or pressed on UI
public static float GetAxis(string action)
public static float GetAnalogValue(string action)
public static bool GetDigitalValue(string action)
public static bool IsPressed(string action)          // pressed THIS frame (= Unity GetButtonDown)
public static bool IsReleased(string action)         // released this frame
public static bool IsDown(string action)             // held
public static bool IsPressed(KeyCode code) / IsReleased(KeyCode code) / IsDown(KeyCode code)
public static bool IsPressed(MouseButton button) / IsReleased(MouseButton button) / IsDown(MouseButton button)
```
- Default actions: Horizontal, Vertical, Jump, Run, Submit, Cancel, Mouse X, Mouse Y, Mouse ScrollWheel,
  Mouse Left, Mouse Right, Mouse Middle (keyboard WASD/arrows/space + gamepad left stick, South = Jump).
- Unmapped action silently reads 0/false. Add actions in the editor input settings or MCP
  `input_map_action {"action","device":"keyboard|mouse|gamepad","key"|"button"|"axis",...}` (edit mode only).
  No C# API to add/rebind actions at runtime.
- MouseButton: Left Right Middle Button4..Button11. GamepadAxis: LeftX LeftY RightX RightY LeftTrigger RightTrigger.
  GamepadButton: South East West North Back Guide Start LeftStick RightStick LeftShoulder RightShoulder DPadUp DPadDown
  DPadLeft DPadRight Touchpad. TouchPhase: Began Moved Stationary Ended Canceled.
- Touch struct: `int FingerId; TouchPhase Phase; Vector2 Position; Vector2 DeltaPosition; float Pressure;`
Cursor: `public static CursorLockMode lockState` (None, Locked, Confined), `public static bool visible`.
VirtualGamepad (static; read by input settings like a real pad, so GetAxis("Horizontal") works):
```
public static void SetAxis(GamepadAxis axis, float value)
public static void SetStick(bool rightStick, Vector2 value)      // x right, y DOWN (screen lean)
public static void SetButton(GamepadButton button, bool down)
public static void ReleaseAll()
```
PointerZone (screen area owning a finger/left-mouse press until lift; game mouse/GetTouch never see it):
```
public PointerZone()      public PointerZone(Rect rect)
public Rect rect          public bool isHeld
public bool TryGetPointer(out Touch pointer)         // FingerId -1 when the mouse holds it
public void Dispose()
```
Sample: Unravel.Samples.SampleTouchJoystick (samples/SampleTouchJoystick.cs) - add to its own entity; draws
engine:/data/ui/sample_touch_joystick.rhtml (adds a UI Document if missing), drives VirtualGamepad left stick +
face buttons. Fields: RightStick, Radius, DeadZone, Floating, AreaWidth, AreaHeight, SouthLabel/EastLabel/WestLabel/
NorthLabel (empty hides), ButtonSize, Show (TouchScreen|Always), TapsAreNotClicks.

---------------------------------------------------------------------------------------------------
## 5. Rendering from script

Graphics (rendering/Graphics.cs) - entity-less instanced draws, THIS FRAME ONLY (call every frame, main thread;
engine copies the arrays; culled per 8 m ground tile; shadows + motion vectors):
```
public static void DrawMeshInstanced(Mesh mesh,
                                     Material material,                        // null = mesh's own materials
                                     ReadOnlySpan<Matrix4x4> transforms,
                                     ReadOnlySpan<Matrix4x4> previousTransforms = default,  // empty or same length
                                     bool castShadows = true,
                                     int layerMask = 1,                         // 1 = Default layer
                                     MaterialInstance materialInstance = null)  // from new MaterialInstance(material)
// static overload draws only the mesh's NON-skinned submeshes
public static void DrawMeshInstanced(BakedAnimation animation,
                                     Material material,
                                     ReadOnlySpan<Matrix4x4> transforms,
                                     ReadOnlySpan<BakedAnimationState> states,  // same length as transforms
                                     ReadOnlySpan<Matrix4x4> previousTransforms = default,
                                     ReadOnlySpan<BakedAnimationState> previousStates = default,
                                     bool castShadows = true,
                                     int layerMask = 1,
                                     MaterialInstance materialInstance = null)
// baked overload needs vertex storage buffers: draws NOTHING on OpenGL ES
```
- material must be null or materialInstance.sharedMaterial, else ArgumentException. One material instance per call:
  instances that differ (e.g. hurt flash) go in a second call (SampleHorde pattern).

BakedAnimation (rendering/BakedAnimation.cs) - main thread only:
```
public enum BakedAnimationRetarget { Copy, RestAligned }
[StructLayout(LayoutKind.Sequential)] public struct BakedAnimationState { public int clip; public float time;
    public BakedAnimationState(int clip, float time) }       // clip = index in baked list; time clamps (wrap yourself)
public static BakedAnimation Bake(Mesh mesh, ReadOnlySpan<AnimationClip> clips, float framesPerSecond = 30.0f,
                                  BakedAnimationRetarget retarget = BakedAnimationRetarget.Copy)
        // throws ArgumentException (no bones / missing clip); slow: bake at level load
public Mesh mesh { get; }   public float framesPerSecond { get; }   public int clipCount   public bool isValid
public float GetLength(int clip)                     // 0 for bad index
public void Dispose()                                // also released when play mode ends
```
- Root motion dropped (in-place); rigid parts on bones (weapons) follow.

MaterialInstance (scene/MaterialInstance.cs):
```
public MaterialInstance(Material sharedMaterial)     // instance of NO renderer, for Graphics draws; Dispose it
public uint slot { get; }                            // 0 for no-renderer instance
public Material sharedMaterial
public void Dispose()                                // no-op for a renderer's instance
public Color baseColor / emissiveColor / subsurfaceColor          // per instance (keeps shared batch)
public float emissiveIntensity / roughness / metalness / bumpiness // per instance
public float alphaCutoff   public Vector2 tiling                    // per material (own draw)
public Texture colorMap / normalMap / roughnessMap / metalnessMap / emissiveMap / aoMap   // per material
public MaterialAlphaMode alphaMode   public MaterialCullMode cullMode                     // per material
public bool hasOverrides   public bool sharesDraw
public bool IsOverridden(MaterialProperty property)  public void Revert(MaterialProperty property)  public void RevertAll()
public Color GetColor(MaterialProperty property)     public void SetColor(MaterialProperty property, Color value)
public float GetFloat(MaterialProperty property)     public void SetFloat(MaterialProperty property, float value)
public Vector2 GetVector(MaterialProperty property)  public void SetVector(MaterialProperty property, Vector2 value)
public Texture GetTexture(MaterialProperty property) public void SetTexture(MaterialProperty property, Texture texture)
```
Material (shared asset, assets/Assets.cs): same properties + Get/Set* (changes EVERY renderer using it; in the
editor a script's change lasts until play stops). Mesh, Texture, AnimationClip (`float length`, `string name`),
AnimationController, AudioClip (`float length`), Prefab, Scene, Font, VisualTree, StyleSheet, PhysicsMaterial =
asset handles with `Guid uid`.

ModelComponent:
```
public bool enabled   public bool castsShadow   public bool isStatic   public Mesh mesh   public float lodSelectionBias
public int GetSharedMaterialsCount()
public Material GetSharedMaterial(uint slot = 0)
public void SetSharedMaterial(Material material, uint slot = 0)
public MaterialInstance GetMaterialInstance(uint slot = 0)   // this renderer only
```
SubmeshComponent: `GetEnabled(int index)`, `SetEnabled(int index, bool enabled)`, `SetCastsShadow(int index, bool castsShadow)`.
LightComponent: `bool enabled` (false = off, no shadows/GI; intensity 0 still renders shadow maps), `Color color`,
`LightType type`, `float intensity`, `indirectIntensity`, `volumetricIntensity`, `bool castsShadows`, `float range`,
`spotOuterAngle`, `spotInnerAngle`, `sourceRadius`, `sourceLength`.
CameraComponent:
```
public float fov  nearClipPlane  farClipPlane  orthographicSize   public ProjectionMode projectionMode
public LayerMask cullingMask / excludeMask   public Vector2 viewportSize { get; }   public float aspect { get; }
public bool ScreenPointToRay(Vector2 pos, out Ray ray)
public Vector3 ScreenPointToWorld(Vector2 pos)                 // on near plane
public Vector3 ScreenPointToWorld(Vector3 pos)                 // inverse of WorldToScreenPoint
public Vector3 WorldToScreenPoint(Vector3 world)               // x,y pixels top-left (mousePosition space);
                                                               // z = distance in front; z <= 0 -> behind camera
public Vector3 WorldToViewportPoint(Vector3 world)             // (0,0) top-left .. (1,1)
public Vector3 ViewportToWorldPoint(Vector3 viewport)
public bool ViewportPointToRay(Vector2 viewport, out Ray ray)
```
- No Camera.main: `Scene.FindEntityByName("Main Camera").GetComponent<CameraComponent>()` (SampleCharacterController).
TextComponent (3D world text): `string text`, `Font font`, `uint fontSize`, `Color color`, `float opacity`,
`Color outlineColor`, `float outlineWidth`, `Alignment alignment`, `Vector2 area`, `bool isRichText`, shadow*.
VolumeComponent (post-process volume): `VolumeMode mode`, `int priority`, `float weight` (0..1), `blendDistance`,
`Vector3 extents`. Individual effects (bloom, vignette, tonemap, ...) are NOT exposed to C#: animate a volume's
weight to fade an authored effect in/out.

---------------------------------------------------------------------------------------------------
## 6. Animation on entities

AnimationComponent (scene/components/AnimationComponent.cs):
```
public float Speed   public bool autoplay   public bool applyRootMotion
public AnimationClip animation                      // default clip (used when no controller)
public AnimationController controller               // while set, drives animation (clip ignored)
public bool SetFloat(string name, float value)      public float GetFloat(string name)
public bool SetInt(string name, int value)          public int GetInt(string name)
public bool SetBool(string name, bool value)        public bool GetBool(string name)
public bool SetTrigger(string name)                 public bool ResetTrigger(string name)
public string GetCurrentStateName(int layer = 0)    public string GetNextStateName(int layer = 0)
public bool IsInState(string state, int layer = 0)  public bool IsInTag(string tag, int layer = 0)
public bool IsInTransition(int layer = 0)           public float GetStateTime(int layer = 0)   // normalized
public float GetNextStateTime(int layer = 0)        public float GetTransitionProgress(int layer = 0)
public bool CrossFade(string state, float seconds, int layer = 0, float normalizedTime = 0.0f)
public bool PlayState(string state, int layer = 0, float normalizedTime = 0.0f)
public int GetLayerIndex(string name)               public int layerCount
public bool isPlaying   public bool isPaused
public void Blend(AnimationClip clip, float seconds, bool loop, bool phaseSync)       // no controller: play a clip
public void BlendLayer(int layer, AnimationClip clip, float seconds, bool loop, bool phaseSync)
public void StopLayer(int layer, float seconds)
public float GetCurve(string name)   public bool HasCurve(string name)
public void SetLayerWeight(int layer, float weight)  public float GetLayerWeight(int layer)
public void SetLayerAdditive(int layer, bool additive)  public bool IsLayerAdditive(int layer)
public void Play()  public void Pause()  public void Resume()  public void Stop()
```
- Play a clip asset without a controller: `anim.Blend(clip, 0.15f, true, false);` then `if (!anim.isPlaying) anim.Play();`
  (Blend only sets the player's target clip; Play "starts the currently blended animation").
AnimationEvent struct (animation/AnimationEvent.cs): `string name; string stringParameter; float floatParameter;
int intParameter; int layer; float weight; AnimationClip clip; string clipName;` Events/curves are authored in clip
import settings (clip inspector Events/Curves tab or MCP assets_set_import_settings "events":[{name,position 0..1,...}]).
Delivered to scripts on the AnimationComponent entity (or a child without scripts of its own).
IK helpers: static class IK (kinematics/Kinematics.cs) + TwoBoneIK/ChainIK/LookAtIK/FootIK components.

---------------------------------------------------------------------------------------------------
## 7. Particles

ParticleEmitterComponent (scene/components/ParticleEmitterComponent.cs), methods:
```
public void Play()  public void Stop()  public void StopAndReset()  public void Pause()  public void Resume()
public void ResetEmitter()                           // clears particles + state
public void Play(bool withChildren)  Stop(bool withChildren)  StopAndReset(bool withChildren)
public void Pause(bool withChildren)  Resume(bool withChildren)
public void Emit(int count)                          // next update, ignores rate/delay/loop/play state; capped by
                                                     // maxParticles; wakes a culled emitter; disabled emitter ignores
public void Emit(int count, EmitParams parameters)
```
EmitParams (struct; only fields you SET apply):
`Vector3 position` (simulation space: world, or local when simulationSpace Local), `Vector3 velocity` (m/s),
`Color color` (tint), `float size`, `float lifetime`.
-> burst at a world position: `fx.Emit(20, new EmitParams { position = p });` on a World-space emitter.
Key props: `bool enabled`, `uint maxParticles` (rebuilds), `float emissionRate`, `bool loop`, `bool autoPlay`,
`bool prewarm`, `float duration`, `float startDelay` (clears), `ParticleSimulationSpace simulationSpace` (clears),
`Range<float> lifetime`, `Range<float> startSpeed`, `Range<float> startRotation`, `Range<float> spinSpeed`,
`MinMaxCurve sizeOverLife`, `Gradient colorGradient`, `Curve speedOverLife`, `float gravityScale`, `float drag`,
`Vector3 acceleration`, `EmitterShape shape`, `ParticleRenderMode renderMode` (Trail: trailLength, trailWidthEnd;
Ribbon), `ParticleBlendMode blendMode`, `ParticleLighting lighting`, `uint lightCount` (+lightIntensity, lightRange),
`ParticleBurst[] bursts`, `ParticleSubEmitter[] subEmitters`, `ParticleCollisionMode collision` + `Vector4[] collisionPlanes`,
`Guid texture` (a Guid, not Texture), `int sortPriority`, `ParticleCullingMode cullingMode`.
State (read-only): `uint numParticles`, `bool isPlaying`, `isPaused`, `isStopped`, `isSimulated`, `isAlive`, `Bounds worldBounds`.
```
public ParticleBurst(float time, float count)
public ParticleBurst(float time, float count, uint cycles, float interval)
public ParticleSubEmitter(ParticleEventType trigger, Entity target, float count)   // + inheritVelocity, countMax...
```
- Build bursts/sub-emitters with constructors: default struct has probability 0 and never fires.

---------------------------------------------------------------------------------------------------
## 8. Audio

Audio (audio/Audio.cs):
```
public static int maxVoices { get; }                 // soft cap shared by one-shots + AudioSources
public static float masterVolume { get; set; }
public static float GetBusVolume(AudioBus bus)
public static void SetBusVolume(AudioBus bus, float volume)
public static void FadeBusVolume(AudioBus bus, float target, float seconds)        // unscaled time
public static AudioVoice PlayOneShot(AudioClip clip, float volume = 1.0f, float pitch = 1.0f,
                                     AudioBus bus = AudioBus.Sfx, int priority = 0)            // 2D
public static AudioVoice PlayOneShot(AudioClip clip, Vector3 position, float volume = 1.0f, float pitch = 1.0f,
                                     AudioBus bus = AudioBus.Sfx, int priority = 0)            // 3D, 1 m..20 m linear
public static void SetClipLimit(AudioClip clip, int maxVoices, float minInterval = 0.0f)    // anti-spam
public enum AudioBus : byte { Sfx = 0, Music = 1, Ui = 2 }
```
AudioVoice (readonly struct; calls on a finished/stolen voice do nothing):
```
public bool started   public bool isPlaying
public void Stop()
public void SetVolume(float volume)                  // 0..1, ends a fade
public void SetPitch(float pitch)                    // 0..5
public void SetDistance(float minDistance, float maxDistance)   // 3D only
public void FadeTo(float volume, float seconds, bool stopAtEnd = false)   // unscaled time
```
- Steal order at cap: non-looping before looping, lower priority, one-shots before sources, oldest.
AudioSourceComponent: `AudioClip clip`, `bool loop`, `float volume`, `float pitch`, `float volumeRolloff`,
`float minDistance`, `float maxDistance`, `bool mute`, `bool spatial` (false = 2D music/UI), `AudioBus bus`,
`int priority`, `bool isPlaying`, `bool isPaused`, `float time`; `Play()`, `Stop()`, `Pause()`, `Resume()`,
`public void FadeTo(float volume, float seconds, bool stopAtEnd = false)`. No PlayOneShot on the component.
AudioListenerComponent: marker; scene needs one (presets put it on Main Camera) or audio is silent.

---------------------------------------------------------------------------------------------------
## 9. UI (RmlUi)

UIDocumentComponent (scene/components/UIDocumentComponent.cs):
```
public VisualTree asset   public bool loaded   public bool visible { get; set; }   public bool enabled
public UIDocument document                           // null until loaded / after Close
public event Action<UIDocument> documentLoaded       // fires for current doc on subscribe, then after each reload
public string title
public void Close()   public bool Reload()           // Reload = fresh initial markup; also reopens closed
public UIDataModel GetModel(string name)             // exists before load, survives reloads: bind once in OnCreate/OnStart
public UIDocument GetDocument()
```
- Render mode (screen_space_overlay | world_space, pixels_per_world_unit) is set in the editor/MCP, not from C#.
- Show/hide screens: `ui.visible = false/true` (keeps state), or element `visible`, or classes.
UIDocument (ui/UIDocument.cs):
```
public Entity Owner  public string Title  public bool IsVisible  public void Show()  public void Hide()  public void Close()
public UIElement GetElementById(string elementId)
public UIElement QuerySelector(string selector)      public UIElement Q(string selector)
public T Q<T>(string selector) where T : UIElement   public List<UIElement> QueryAll(string selector)
public UIElement root
public UIElement CreateElement(string tag)
public UIElement ElementAt(Vector2 point)
public UIDataModel GetModel(string name)
public float pixelsPerDp                             // style.left = screenPoint.x / document.pixelsPerDp
public void UpdateLayout()
```
UIElement (ui/UIElement.cs):
```
public string ElementId  public string InnerRml { get; set; }  public bool IsVisible / visible { get; set; }
public string text  public string tagName  public string value  public bool disabled
public void SetValueWithoutNotify(string newValue)  public void SetText(string text)
public T As<T>() where T : UIElement   public bool Is<T>() where T : UIElement
public string GetAttribute(string attributeName)  public void SetAttribute(string attributeName, string value)
public void RemoveAttribute(string attributeName) public bool HasAttribute(string attributeName)
public void SetClass(string className, bool activate)  public bool IsClassSet(string className)
public void EnableClass(string className, bool enable) public bool ToggleClass(string className)  public bool HasClass(string className)
public void SetPseudoClass(string pseudoClass, bool active)  public bool HasPseudoClass(string pseudoClass)
public void SyncTransformToEntity(Entity transformEntity)
public void Focus()  public void Blur()  public void Click()  public void ScrollIntoView(bool alignWithTop = true)
public UIElement parent   public IReadOnlyList<UIElement> children
public UIElement Q(string selector)  public T Q<T>(string selector)  public List<UIElement> QueryAll(string selector)
public UIElement Closest(string selector)
public UIElement AppendChild(UIElement child)  public UIElement InsertBefore(UIElement child, UIElement before)
public void Remove()  public UIElement Clone()
public UIStyle style                                 // style["opacity"] = "0.5"; style.opacity/left/top/width/height (dp)
public string GetComputed(string property)
public Rect rect / paddingRect / contentRect         // document pixels, y down
public float scrollLeft / scrollTop  scrollWidth / scrollHeight   public void ScrollTo(Vector2 offset, bool smooth = true)
public UIAnimation Animate(string property, string target, float duration, UIEasing easing = UIEasing.Linear,
                           int loops = 1, bool alternate = false, float delay = 0.0f)   // loops -1 = forever
public void RestartAnimation()                       // replay RCSS keyframes
public UISubscription On<T>(Action<T> callback, EventPhase phase = EventPhase.Bubble) where T : UIEventBase
public UISubscription On<T>(string eventType, Action<T> callback, EventPhase phase = EventPhase.Bubble) where T : UIEventBase
public UISubscription Once<T>(Action<T> callback) where T : UIEventBase
public UISubscription Once<T>(string eventType, Action<T> callback) where T : UIEventBase
public void RegisterCallback(string eventType, UIEventCallback callback)
public event Action clicked
```
UIAnimation: `string property`, `bool isRunning`, `event Action completed`, `void Stop()`.
Controls (typed subclasses): UIButton, UIInput, UISlider (value/min/max/step), UIToggle (isChecked), UISelect,
UIProgress (value, max), UITabSet. Events: UIClickEvent, UIChangeEvent, UIAnimationEvent, UISubmitEvent,
UIScrollEvent, UITextInputEvent, UIPointerEvent ("mousemove"), UIKeyEvent, UIFocusEvent, UIDragEvent, UIEventBase.
UIDataModel (ui/UIDataModel.cs):
```
public string name { get; }   public Entity owner { get; }   public bool autoDirty { get; set; }
public void Bind(object source)                      // public fields/props -> variables, public methods -> events
public void Bind<T>(string variable, Func<T> getter, Action<T> setter = null)
public void BindEvent(string eventName, Action<UIEventBase, object[]> handler)
public void AddTransform(string transformName, Func<object, object[], object> transform)
public void AddTransform<T>(string transformName, Func<T, object> transform)   // {{ v | name }}
public void Dirty(string variable)   public void DirtyAll()
```
Markup: `<body data-model="hud">{{ score }}`, data-if, data-visible, data-for="row, i : rows", data-class-x,
data-style-prop, data-attr-x, data-value, data-checked, data-event-click="Fn(1,'a')", onclick="Pause" (public method
on a script of the document's entity). Values: bool, numbers, string, enums, Color; nested classes/structs; lists/arrays.
Damage numbers over the world (screen-space doc): `var p = cam.WorldToScreenPoint(world); if (p.z > 0)
{ el.style.left = p.x / doc.pixelsPerDp; el.style.top = p.y / doc.pixelsPerDp; }` (absolutely positioned element of
the body). Alternatives: world-space TextComponent entities, or SyncTransformToEntity.
Samples: samples/SampleHudController.cs + engine:/data/ui/sample_hud.rhtml; samples/SampleUIWrapperController.cs.

---------------------------------------------------------------------------------------------------
## 10. Physics

Physics (scene/physics/Physics.cs) - main thread only; *All and SphereOverlap allocate a new array per call:
```
public const int DefaultRaycastLayers = -1;
public static RaycastHit? Raycast(Ray ray, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static RaycastHit? Raycast(Vector3 origin, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static RaycastHit[] RaycastAll(Ray ray, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static RaycastHit? SphereCast(Ray ray, float radius, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static RaycastHit? SphereCast(Vector3 origin, Vector3 direction, float radius, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static RaycastHit[] SphereCastAll(Ray ray, float radius, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static RaycastHit[] SphereCastAll(Vector3 origin, Vector3 direction, float radius, float maxDistance = Mathf.Infinity, int layerMask = DefaultRaycastLayers, bool querySensors = false)
public static Entity[] SphereOverlap(Vector3 origin, float radius, int layerMask = DefaultRaycastLayers, bool querySensors = false)
```
RaycastHit: `Entity entity; Vector3 point; Vector3 normal; float distance;`. Ray: `Vector3 origin; Vector3 direction;`.
Collision (class): `Entity entity; ContactPoint[] contacts; ContactEndReason reason;`. ContactPoint: point normal distance impulse.
CharacterControllerComponent (capsule, sweep; excludes PhysicsComponent on the same entity):
```
public bool isGrounded { get; }  public bool canJump { get; }  public Vector3 velocity { get; }
public Vector3 linearVelocity { get; set; }
public float radius  height  stepHeight  slopeLimit  skinWidth  gravityScale  terminalVelocity  linearDamping
public Vector3 center                                // center.y = height / 2 puts capsule base at entity origin
public LayerMask includeLayers / excludeLayers   public LayerMask collisionLayers { get; }
public void Move(Vector3 displacement)               // applied once on next physics step: Move(v * Time.deltaTime) per frame
public void Jump(float speed)   public void Jump(Vector3 velocity)
public void ApplyImpulse(Vector3 impulse)
public void Warp(Vector3 position)                   // teleport without collision
```
PhysicsComponent: `bool isSensor`, `RigidbodyType bodyType` (Static/Kinematic/Dynamic), `float mass`, `bool useGravity`,
`Vector3 velocity`, `Vector3 angularVelocity`, `LayerMask includeLayers/excludeLayers`, `ContactEventFlags contactEventFlags`,
`bool sensorEventsEnabled`, `collisionEventsEnabled`, `sensorExitOnDestroy`, `collisionExitOnDestroy`;
`ApplyForce(Vector3 force, ForceMode mode = ForceMode.Force)`, `ApplyTorque(...)`,
`ApplyExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0.0f, ForceMode mode = ForceMode.Force)`.
- Default contact flags = SensorEvents | CollisionEvents | SensorExitOnDestroy (physics_types.h). A sensor needs a shape;
  the other side needs a body (a CharacterController counts).

---------------------------------------------------------------------------------------------------
## 11. Threading

- `System.Threading.Tasks.Parallel.For(0, taskCount, task => { ... })` works (SampleHorde). Workers may run PURE C#
  only: any engine call (Entity access, components, Physics, Graphics, Audio, UI, Assets) throws
  InvalidOperationException "<api> must be called from the main thread; worker threads may only run pure C# code."
  (script_glue_common.h require_script_thread). Uncaught worker exceptions are logged (System.cs).
- Pattern: read engine state on main thread -> Parallel.For over chunked arrays (each task writes only its rows) ->
  apply/draw on main thread (Graphics.DrawMeshInstanced in OnLateUpdate).
- Random is per-thread (safe); RandomGenerator struct for deterministic per-job streams.
SpatialHashGrid (system/SpatialHashGrid.cs) - XZ plane only; build single-threaded, query from any thread:
```
public interface ISpatialHashVisitor { void Visit(int item, float distance); }   // implement on a struct: no alloc
public SpatialHashGrid(float cellSize)               // ~2x usual query radius
public float cellSize   public int count { get; }
public void Build(ReadOnlySpan<Vector3> positions, ReadOnlySpan<bool> include = default)   // copies positions
public void Visit<TVisitor>(Vector3 center, float radius, ref TVisitor visitor) where TVisitor : struct, ISpatialHashVisitor
public int Query(Vector3 center, float radius, Span<int> results)                // indices, unordered, capped
```
SampleHorde (samples/SampleHorde.cs, global namespace) - ready-made script horde (arrays, Parallel.For, baked anim,
hurt flash). Fields: `Mesh mesh; Material material; Entity target; int capacity = 2048; float scale, radius = 0.4f,
maxSpeed = 2, acceleration = 12, turnRate = 540, health = 10, targetRadius = 1, separation = 1; bool castShadows,
stepInParallel; AnimationClip walkClip, attackClip; float bakeFramesPerSecond = 30; bool retargetClips;
float meshYawOffset; Color hurtFlashColor; float hurtFlashSeconds = 0.15f`. API:
```
public struct Member { public int index; public uint generation; public static Member Invalid; public bool isValid; }
public struct Hit { public Member member; public Vector3 position; public float distance; }
public struct Event { public Member member; public Vector3 position; public int userValue; }
public enum DamageResult { Missed, Hurt, Killed }
public int count / deathCount / contactCount
public Member Spawn(Vector3 position, int userValue = 0)      // Member.Invalid when full
public bool Despawn(Member member)   public bool Kill(Member member)   public bool IsAlive(Member member)
public bool TryGetPosition(Member member, out Vector3 position)   public bool SetPosition(Member member, Vector3 position)
public DamageResult Damage(Member member, float amount)
public int DamageRadius(Vector3 center, float damageRadius, float amount, Span<Hit> hits = default)
public int Query(Vector3 center, float queryRadius, Span<Hit> hits)
public bool QueryNearest(Vector3 center, float maxRadius, out Hit hit)
public int GetDeaths(Span<Event> buffer)             // deaths published by the last step (drop XP gems here)
public int GetContacts(Span<Event> buffer)           // members touching target at last step (player damage)
public void Clear()
```

---------------------------------------------------------------------------------------------------
## 12. Persistence

- No PlayerPrefs. Use `Application.persistentDataPath` (exists, forward slashes, no trailing slash; same folder in
  editor play and deployed game; Android internal storage included in backup) + System.IO.File + System.Text.Json.
- `Application.temporaryCachePath` for rebuildable data (may be deleted by the user/system).

---------------------------------------------------------------------------------------------------
## 13. Coroutines / async

Coroutine (system/Coroutine.cs):
```
public sealed class Coroutine { public bool isRunning { get; } public void Stop(); }
public WaitForSeconds(float seconds)                 // game time (timeScale)
public WaitForSecondsRealtime(float seconds)
public WaitForFrames(int frames)
public WaitUntil(Func<bool> condition)
public WaitWhile(Func<bool> condition)
// also yield null (next frame), another IEnumerator (runs inline), a Coroutine (waits for it)
```
- Coroutines step after the scripts' updates, pause with the game, stop on OnDisable/OnDestroy.
- No async/await support: no SynchronizationContext is installed, so continuations after `await` resume on the
  thread pool where engine calls throw. Use coroutines for sequencing.

---------------------------------------------------------------------------------------------------
## 14. Traps (from docs, comments, code)

- `Random` resolves to the ENGINE's static Random (global namespace), even with `using System;`: write `System.Random`
  or `using SystemRandom = System.Random;`.
- Entity is a struct: `if (entity)` / `entity.IsValid()`, never `== null`. NativeObject `==`/bool conversion call
  IsValid (native call); use ReferenceEquals(x, null) on hot paths.
- GetComponent<T>() returns null when missing.
- Public get-only properties on a ScriptComponent: no longer saved (save skips them), but scenes saved earlier can log
  "Script member X did not load ... keeps its current value". Keep helper properties internal/private anyway.
- Changing a field initializer in code does NOT change already-saved instances: loaded values overwrite initializers.
- ImplicitUsings off: missing `using System.Collections;` breaks IEnumerator coroutines.
- Time.deltaTime is 0 on the first simulation frame.
- Scene.DestroyEntity / LoadScene / LoadSceneAdditive are queued: the entity is still valid in the same frame.
- Pooling: OnStart runs once per instance; reset in IPoolable.OnAcquire. Release deactivates -> OnDisable -> the
  instance's coroutines stop.
- Invoke is `Invoke(float delay, Action action)` (not Unity's string name).
- Vector2 <-> Vector3 implicit conversion is xy (z dropped); for ground-plane math build `new Vector2(v.x, v.z)`.
- Vector3/Mathf SmoothDamp need all 6 args (maxSpeed, deltaTime).
- Input action names only; unmapped = 0. IsPressed = this frame, IsDown = held.
- Character Controller and Physics component exclude each other; teleport a CC with Warp.
- Light off = `enabled = false`; intensity 0 still renders shadows.
- No AudioListener in a hand-built scene = silence.
- Particle bursts/sub-emitters: use constructors (default probability 0). Setting simulationSpace or startDelay clears
  particles; maxParticles rebuilds the emitter.
- Graphics.DrawMeshInstanced is per frame; static overload skips skinned submeshes; baked overload draws nothing on GLES.
  BakedAnimation and no-renderer MaterialInstance: Dispose in OnDestroy (also freed at play end).
- MaterialInstance textures/tiling/modes are per material (break instancing); colors/floats keep the shared draw.
- Shared Material edits from script affect every renderer (and in the editor persist until play stops).
- UI: RCSS colour alpha 0-255; lengths in dp; no `solid` keyword; during play documents keep the markup they started
  with; keys reach a document only while a text field has focus; event objects are reused; out of play mode
  data-style-* logs syntax errors; `focus: none` on clickable/ancestor breaks clicks; data-for template element is in
  QueryAll results (filter by visible).
- Hot reload: avoid static caches of Type/delegates/script instances; use [AutoStaticsCleanup].
- Worker threads: any engine call throws (see 11).

---------------------------------------------------------------------------------------------------
## MISSING for a survivors-like (each verified by search of engine_data/data/scripts)

- No Camera.main / main-camera accessor (find "Main Camera" by name or FindEntitiesWithComponent<CameraComponent>()).
- No per-instance color/custom data in DrawMeshInstanced (only one MaterialInstance per call -> bucket instances).
- Baked animated instancing unavailable on OpenGL ES devices.
- Physics: no NonAlloc variants, no OverlapBox/OverlapCapsule/BoxCast/CapsuleCast; SphereOverlap returns Entity[]
  (allocates). Use SpatialHashGrid for enemy proximity instead of physics.
- No PlayerPrefs / JSON helper in engine API (use System.IO + System.Text.Json + Application.persistentDataPath).
- No async/await main-thread SynchronizationContext.
- No GetComponentInParent; no ScriptComponent.enabled toggle.
- No Instantiate(prefab, position, rotation) without parent (pass Entity.Invalid).
- No runtime input rebinding / action creation from C#.
- No navmesh / pathfinding.
- No haptics / vibration / gamepad rumble.
- No C# access to individual post-process effects (only VolumeComponent weight/priority).
- No UIDocument render-mode switch from C# (screen vs world space set in editor).
- No line renderer / trail component (particle Trail/Ribbon render modes only); Gizmos are sphere + ray debug only.
- No AudioSourceComponent.PlayOneShot (use static Audio.PlayOneShot).
- PRESENT (not missing): Time.timeScale + unscaledDeltaTime, CameraComponent.WorldToScreenPoint, animation events
  (OnAnimationEvent + method-by-name), animation curves, coroutines/Invoke, EntityPool, additive scenes,
  DontDestroyOnLoad, VirtualGamepad/PointerZone touch input, Screen.safeArea.
