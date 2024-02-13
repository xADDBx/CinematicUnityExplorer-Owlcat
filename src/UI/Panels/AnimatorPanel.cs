﻿using UnityExplorer.CacheObject;
using UnityExplorer.ObjectExplorer;
using UnityExplorer.Inspectors;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;
using UniverseLib.UI.Widgets.ScrollView;
using UnityEngine;
#if UNHOLLOWER
using UnhollowerRuntimeLib;
#endif
#if INTEROP
using Il2CppInterop.Runtime.Injection;
#endif

#if CPP
using Animator = UnityEngine.Behaviour;
#endif

namespace UnityExplorer.UI.Panels
{
    internal class AnimatorPanel : UEPanel, ICellPoolDataSource<AnimatorCell>
    {
        public AnimatorPanel(UIBase owner) : base(owner)
        {
        }

        public override string Name => "Animator";
        public override UIManager.Panels PanelType => UIManager.Panels.AnimatorPanel;
        public override int MinWidth => 500;
        public override int MinHeight => 200;
        public override Vector2 DefaultAnchorMin => new(0.4f, 0.4f);
        public override Vector2 DefaultAnchorMax => new(0.6f, 0.6f);
        public override bool NavButtonWanted => true;
        public override bool ShouldSaveActiveState => true;

        Dictionary<Animator, Func<bool>> animationEndedFunctions = new Dictionary<Animator, Func<bool>>();

        Toggle masterAnimatorToggle = new Toggle();

        private static ScrollPool<AnimatorCell> animatorScrollPool;
        internal List<Animator> animators = new List<Animator>();
        public int ItemCount => animators.Count;

        private static bool DoneScrollPoolInit;

        public override void SetActive(bool active)
        {
            base.SetActive(active);

            if (active && !DoneScrollPoolInit)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(this.Rect);
                animatorScrollPool.Initialize(this);
                DoneScrollPoolInit = true;
            }

            animatorScrollPool.Refresh(true, false);
        }

        private void FindAllAnimators(){
            // Enable all animators on refresh
            if (animators.Count != 0) {
                masterAnimatorToggle.isOn = true; // Will also trigger "MasterToggleAnimators(true)"
                animators.Clear();
                animationEndedFunctions.Clear();
#if MONO
                foreach (AnimatorCell animatorCell in animatorScrollPool.CellPool)
                {
                    animatorCell.ResetAnimation();
                }
#endif
            }

            Type searchType = ReflectionUtility.GetTypeByName("UnityEngine.Animator");
            searchType = searchType is Type type ? type : searchType.GetActualType();
            animators = RuntimeHelper.FindObjectsOfTypeAll(searchType).Select(obj => obj.TryCast<Animator>())
            .Where(a => a.GetComponentsInChildren<SkinnedMeshRenderer>(false).Length != 0 && a.enabled && a.GetComponentsInChildren<Rigidbody>(false).Length != 0)
            .OrderBy(x=>x.name)
            .ToList();

            animatorScrollPool.Refresh(true, false);
        }

        public void MasterToggleAnimators(bool enable){
            // Load animators for the first time if there are not any
            if (animators.Count == 0) FindAllAnimators();
            foreach (AnimatorCell animatorCell in animatorScrollPool.CellPool)
            {
                if (animatorCell.animator != null && !animatorCell.IgnoreMasterToggle.isOn){
                    animatorCell.AnimatorToggle.isOn = enable;
                }
            }
        }

        public void HotkeyToggleAnimators(){
            masterAnimatorToggle.isOn = !masterAnimatorToggle.isOn;
        }

#if MONO
        public override void Update(){
            foreach (Animator animator in animators){
                animationEndedFunctions[animator]();
            }
        }
#endif
        // ~~~~~~~~ UI construction / callbacks ~~~~~~~~

        protected override void ConstructPanelContent()
        {
            GameObject firstGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "MainOptions", false, false, true, true, 3,
                default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(firstGroup, minHeight: 25, flexibleWidth: 9999);
            //UIElements.Add(horiGroup);

            ButtonRef updateAnimators = UIFactory.CreateButton(firstGroup, "RefreshAnimators", "Refresh Animators");
            UIFactory.SetLayoutElement(updateAnimators.GameObject, minWidth: 150, minHeight: 25);
            updateAnimators.OnClick += FindAllAnimators;

            GameObject animatorObj = UIFactory.CreateToggle(firstGroup, $"Master Animation Toggle", out masterAnimatorToggle, out Text masterAnimatorText);
            UIFactory.SetLayoutElement(animatorObj, minHeight: 25);
            masterAnimatorToggle.isOn = true;
            masterAnimatorToggle.onValueChanged.AddListener(value => MasterToggleAnimators(value));
            masterAnimatorText.text = "Master Toggler";

            GameObject secondGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "MainOptions", false, false, true, true, 3,
            default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(firstGroup, minHeight: 25, flexibleWidth: 9999);

            animatorScrollPool = UIFactory.CreateScrollPool<AnimatorCell>(ContentRoot, "AnimatorsList", out GameObject scrollObj,
                out GameObject scrollContent, new Color(0.03f, 0.03f, 0.03f));
            UIFactory.SetLayoutElement(scrollObj, flexibleWidth: 9999, flexibleHeight: 9999);
        }

        public void SetCell(AnimatorCell cell, int index)
        {
            if (index >= animators.Count)
            {
                cell.Disable();
                return;
            }

            Animator animator = animators[index];
            if (animator == null)
                return;

            cell.animator = animator;
            cell.name.text = animator.gameObject.name;

            cell.MaybeSetIgnoreMasterToggleSet();
#if MONO
            cell.DrawAnimatorPlayer();
            animationEndedFunctions[animator] = cell.IsPlayingSelectedAnimation;
#endif
        }

        public void OnCellBorrowed(AnimatorCell cell) { }
    }
}
