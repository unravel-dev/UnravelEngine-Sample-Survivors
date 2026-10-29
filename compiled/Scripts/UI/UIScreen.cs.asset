using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// One UI document on its own entity under the run's UI root: finds (or creates) the entity and its UI Document,
    /// binds the document's data model, tracks the loaded document, shows and hides it and gives menus keyboard /
    /// gamepad focus. Main thread.
    /// <para>
    /// Adding a screen: derive from this class with the entity name and the document key, declare the data model
    /// with <see cref="BindModel"/> in the constructor (its public fields are the document's variables, its public
    /// methods the events of data-event-click), fetch elements the model does not cover in <see cref="OnLoaded"/>,
    /// write the current state in <see cref="Refresh"/> and react to menu input through <see cref="Activate"/>,
    /// <see cref="Back"/> or <see cref="HandleInput"/>. GameUI opens it with OpenModal.
    /// </para>
    /// <para>
    /// Document conventions: link engine:/data/ui/rml.rcss, app:/data/UI/theme.rcss, the screen's own sheet (its
    /// phone rules last), then app:/data/UI/phone.rcss; give the body the screen's class (title, hud, levelup...)
    /// for rules that must not reach other documents; put full-screen layers in ".safe" elements (GameUI keeps
    /// them inside the safe area); mark focus stops with ".nav"; an element with id "intro" replays its RCSS
    /// animation each time the screen opens. The UI Document's sort order in the scene decides which document
    /// draws on top and gets the pointer first (manifest_ui.md lists them).
    /// </para>
    /// </summary>
    internal abstract class UIScreen
    {
        protected const string INTRO_ID = "#intro";
        private const string NAV_SELECTOR = ".nav";
        private const string DENY_CLASS = "deny";

        protected readonly GameUI ui;
        protected readonly UINavigator nav = new UINavigator();
        protected UIDocumentComponent component;
        protected UIDocument document;
        protected UIElement root;
        private string modelName;
        private object modelSource;
        private long shownFrame = -1;
        private bool navDirty;
        private int navCollectFrame = -1;

        /// <summary>Entity name under the UI root.</summary>
        public readonly string entityName;
        /// <summary>Document asset key.</summary>
        public readonly string key;

        /// <summary>Document size the last layout pass measured (GameUI lays the screen out again when it changes).</summary>
        internal Vector2 layoutSize;
        /// <summary>Document dp ratio the last layout pass measured (GameUI lays the screen out again when it changes).</summary>
        internal float layoutPixelsPerDp;

        protected UIScreen(GameUI ui, string entityName, string key)
        {
            this.ui = ui;
            this.entityName = entityName;
            this.key = key;
        }

        public bool isShown { get; private set; }

        /// <summary>Whether the document is loaded and its elements fetched.</summary>
        public bool isReady => !ReferenceEquals(document, null) && !ReferenceEquals(root, null) && document.IsValid();

        /// <summary>The document's component, for screens that share it (damage numbers on the overlay).</summary>
        public UIDocumentComponent documentComponent => component;

        protected Run run => ui.run;

        /// <summary>The bound data model (null without <see cref="BindModel"/>): Dirty the variables a change touches.</summary>
        protected UIDataModel dataModel { get; private set; }

        /// <summary>Pause closes the screen like Cancel (menus over the paused game).</summary>
        protected virtual bool closesOnPause => false;

        /// <summary>
        /// Declares the document's data model: the public fields, properties and methods of <paramref name="source"/>
        /// bind to data-model="<paramref name="name"/>" when the screen attaches (once; bindings survive reloads).
        /// </summary>
        protected void BindModel(string name, object source)
        {
            modelName = name;
            modelSource = source;
        }

        /// <summary>Finds the entity named <see cref="entityName"/> under <paramref name="uiRoot"/>, or creates it with a
        /// UI Document of <see cref="key"/>, hides it, binds its data model and waits for its document.</summary>
        public void Attach(Entity uiRoot)
        {
            component = FindOrCreateComponent(uiRoot);
            component.visible = false;
            if (modelSource != null)
            {
                dataModel = component.GetModel(modelName);
                dataModel.Bind(modelSource);
                OnModelBound();
            }
            component.documentLoaded += OnDocumentLoaded;
        }

        private UIDocumentComponent FindOrCreateComponent(Entity uiRoot)
        {
            Entity entity = uiRoot.IsValid() ? uiRoot.transform.FindChild(entityName) : Entity.Invalid;
            if (!entity.IsValid())
            {
                entity = Scene.CreateEntity(entityName);
                if (uiRoot.IsValid())
                {
                    entity.transform.SetParent(uiRoot, false);
                }
            }
            UIDocumentComponent found = entity.GetComponent<UIDocumentComponent>();
            if (!ReferenceEquals(found, null))
            {
                return found;
            }
            found = entity.AddComponent<UIDocumentComponent>();
            found.asset = Assets.GetAsset<VisualTree>(key);
            Log.Info($"UI: created {entityName} with {key} (set it up in the scene to control its render order).");
            return found;
        }

        /// <summary>Once, right after the data model is bound: add transforms here.</summary>
        protected virtual void OnModelBound()
        {
        }

        private void OnDocumentLoaded(UIDocument loaded)
        {
            document = loaded;
            root = loaded?.root;
            if (!isReady)
            {
                return;
            }
            ui.ApplyLayout(this);
            ui.ApplyDeviceClasses(root);
            root.On<UIPointerEvent>("mouseover", OnPointerOver);
            OnLoaded(loaded);
            nav.Clear();
            if (isShown)
            {
                Refresh();
                MarkNavDirty();
            }
        }

        /// <summary>Fetches elements and adds listeners for a freshly loaded document (screens without a data model
        /// for them).</summary>
        protected virtual void OnLoaded(UIDocument loaded)
        {
        }

        /// <summary>Writes the screen's current state into the document.</summary>
        protected virtual void Refresh()
        {
        }

        public UIElement GetRoot() => isReady ? root : null;

        public UIDocument GetDocument() => isReady ? document : null;

        /// <summary>
        /// Shows the document and refreshes it; the intro animation replays only when it was hidden. Showing a shown
        /// screen refreshes it (new offers while the card picker is up).
        /// </summary>
        public virtual void Show()
        {
            bool wasShown = isShown;
            isShown = true;
            shownFrame = Time.frameCount;
            if (!ReferenceEquals(component, null))
            {
                component.visible = true;
            }
            if (!isReady)
            {
                return;
            }
            Refresh();
            MarkNavDirty();
            if (!wasShown)
            {
                root.Q(INTRO_ID)?.RestartAnimation();
            }
        }

        public virtual void Hide()
        {
            isShown = false;
            if (!ReferenceEquals(component, null))
            {
                component.visible = false;
            }
        }

        /// <summary>Hides the screen, then hands the result to the caller's callback (taken first: the callback may
        /// open the screen again with a new one).</summary>
        protected void HideAndInvoke(ref Action callback)
        {
            Action taken = callback;
            callback = null;
            Hide();
            taken?.Invoke();
        }

        /// <summary>Hides the screen, then hands <paramref name="result"/> to the caller's callback (taken first).</summary>
        protected void HideAndInvoke<T>(ref Action<T> callback, T result)
        {
            Action<T> taken = callback;
            callback = null;
            Hide();
            taken?.Invoke(result);
        }

        /// <summary>Animations and timers; every frame while shown, unscaled time.</summary>
        public virtual void Tick(float unscaledDt)
        {
        }

        /// <summary>Menu input; only for the screen that has the input, never in the frame it opened.</summary>
        public void ProcessInput(UIInput input)
        {
            if (!isReady || Time.frameCount <= shownFrame)
            {
                return;
            }
            if (navDirty && Time.frameCount > navCollectFrame)
            {
                navDirty = false;
                CollectNav();
            }
            HandleInput(input);
        }

        /// <summary>Directions move the focus, Submit activates it, Cancel (and Pause where
        /// <see cref="closesOnPause"/>) goes back.</summary>
        protected virtual void HandleInput(UIInput input)
        {
            if (MoveFocus(input))
            {
                return;
            }
            if (input.submit)
            {
                Activate();
            }
            else if (input.cancel || (input.pause && closesOnPause))
            {
                Back();
            }
        }

        /// <summary>Moves the focus by the frame's direction input; returns whether there was any.</summary>
        protected bool MoveFocus(UIInput input)
        {
            if (input.direction == NavDirection.None)
            {
                return false;
            }
            if (nav.Move(input.direction))
            {
                ui.PlaySound("ui_hover");
            }
            OnNavigated();
            return true;
        }

        /// <summary>After direction input (the focus may have moved): screens that follow the focus.</summary>
        protected virtual void OnNavigated()
        {
        }

        /// <summary>Clicks the focused element (Submit); focuses the default one when nothing is focused.</summary>
        protected virtual void Activate()
        {
            UIElement focused = nav.focused;
            if (focused == null)
            {
                nav.EnsureFocus(GetDefaultFocus());
                return;
            }
            focused.Click();
        }

        /// <summary>Cancel: close or go back.</summary>
        protected virtual void Back()
        {
        }

        /// <summary>Index of the ".nav" element focused first.</summary>
        protected virtual int GetDefaultFocus() => 0;

        /// <summary>The element whose ".nav" descendants take the focus (the whole document unless a panel is open).</summary>
        protected virtual UIElement GetNavScope() => root;

        /// <summary>Asks for a fresh focus list next frame (after data views rebuilt their elements).</summary>
        protected void MarkNavDirty()
        {
            navDirty = true;
            navCollectFrame = (int)Time.frameCount;
        }

        /// <summary>Collects the focus stops; the keyboard and gamepad get a focused element at once.</summary>
        protected virtual void CollectNav()
        {
            nav.Collect(GetNavScope() ?? root, NAV_SELECTOR);
            if (ui.input.isFocusDevice)
            {
                nav.EnsureFocus(GetDefaultFocus());
            }
        }

        private void OnPointerOver(UIPointerEvent e)
        {
            UIElement target = e.target;
            UIElement item = target?.Closest(NAV_SELECTOR);
            if (item == null || !nav.Contains(item))
            {
                return;
            }
            if (nav.SetFocus(item))
            {
                ui.PlaySound("ui_hover");
            }
        }

        /// <summary>Plays the deny animation on an element (the "deny" class shakes it) and the error sound.</summary>
        protected void Deny(UIElement element)
        {
            ui.PlaySound("ui_error");
            if (element == null)
            {
                return;
            }
            element.SetClass(DENY_CLASS, true);
            element.RestartAnimation();
        }

        /// <summary>Queries elements in the document, skipping data-for templates (hidden).</summary>
        protected List<UIElement> QueryVisible(string selector)
        {
            var result = new List<UIElement>();
            if (!isReady)
            {
                return result;
            }
            foreach (UIElement element in root.QueryAll(selector))
            {
                if (element.IsVisible)
                {
                    result.Add(element);
                }
            }
            return result;
        }
    }
}
