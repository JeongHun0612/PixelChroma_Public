using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

namespace PixelChroma.UI
{
    public class UIManager : MonoSingleton<UIManager>
    {
        [SerializeField] private Transform sceneRoot;
        [SerializeField] private Transform popupRoot;
        [SerializeField] private UIRegistrySO registry;

        [Header("Initial Loading")]
        [SerializeField] private UIInitialLoading initialLoading;
        public UIInitialLoading InitialLoading => initialLoading;

        private readonly Dictionary<Type, UIPanel> _panels = new();
        private readonly Stack<UIPopup> _popupStack = new();

        private UIScene _currentScene;

        // 팝업 Show/Hide 애니메이션이 진행 중인지.
        private bool _isTransitioning;

        public UIScene CurrentScene => _currentScene;
        public UIPopup CurrentPopup => _popupStack.Count > 0 ? _popupStack.Peek() : null;
        public bool IsPopupOpen => _popupStack.Count > 0;
        public IEscapeHandler GameplayEscapeHandler { get; set; }
        public IEscapeHandler ChatEscapeHandler { get; set; }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            if (ChatEscapeHandler?.HandleEscape() == true)     // 0순위: 채팅 입력창 닫기
                return;

            if (_popupStack.Count > 0)                         // 1순위: 최상단 팝업 닫기
            {
                if (CurrentPopup != null && !CurrentPopup.CloseOnEscape)
                    return;

                HidePopup().Forget();
                return;
            }

            if (GameplayEscapeHandler?.HandleEscape() == true) // 2순위: 관전/페인트 종료
                return;

            _currentScene?.OnCancel();                         // 3순위: 씬 설정/나가기
        }

        public async UniTask InitializeAsync()
        {
            var tasks = new List<UniTask>();

            foreach (var reference in registry.sceneUIs)
                tasks.Add(LoadAndRegisterPanel(reference, sceneRoot));

            foreach (var reference in registry.popupUIs)
                tasks.Add(LoadAndRegisterPanel(reference, popupRoot));

            await UniTask.WhenAll(tasks);
        }

        public async UniTask<T> ShowScene<T>() where T : UIScene
        {
            if (!TryGetPanel<T>(out var target)) return null;

            if (_currentScene == target) return target;

            if (_currentScene != null)
            {
                await _currentScene.Hide();
                _currentScene.OnAfterHide();
            }

            _currentScene = target;
            _currentScene.OnBeforeShow();
            await _currentScene.Show();

            return target;
        }

        public UniTask<T> ShowPopup<T>() where T : UIPopup => ShowPopup<T>(null);

        // configure는 표시 직전(OnBeforeShow/애니메이션 시작 전)에 호출되어,
        // 팝업이 그려지기 전에 필요한 값을 주입할 수 있게 한다.
        public async UniTask<T> ShowPopup<T>(Action<T> configure) where T : UIPopup
        {
            if (_isTransitioning) return null;

            if (!TryGetPanel<T>(out var target)) return null;

            if (_popupStack.Contains(target)) return target;

            _isTransitioning = true;
            try
            {
                configure?.Invoke(target);

                _popupStack.Push(target);
                target.OnBeforeShow();
                await target.Show();
            }
            catch
            {
                // Show 실패 시 스택을 원상복구한다.
                if (_popupStack.Count > 0 && _popupStack.Peek() == target)
                    _popupStack.Pop();
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            return target;
        }

        public async UniTask HidePopup(bool instant = false)
        {
            if (_isTransitioning) return;

            if (_popupStack.Count == 0) return;

            _isTransitioning = true;
            try
            {
                await HidePopupInternal(instant);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async UniTask HideAllPopups(bool instant = true)
        {
            // 진행 중인 단일 전환이 끝날 때까지 기다린 뒤 한 번에 모두 닫는다.
            // 전환 중이 아닐 때 WaitUntil을 생성하면, 닫을 팝업이 없어도 다음 PlayerLoop까지
            // 호출자가 지연된다. 씬 전환 직전 로딩 팝업은 같은 프레임에 표시되어야 하므로
            // 실제 전환이 진행 중인 경우에만 기다린다.
            if (_isTransitioning)
                await UniTask.WaitUntil(() => !_isTransitioning);

            _isTransitioning = true;
            try
            {
                while (_popupStack.Count > 0)
                    await HidePopupInternal(instant);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async UniTask HidePopupInternal(bool instant)
        {
            var popup = _popupStack.Peek();
            await popup.Hide(instant);
            _popupStack.Pop();
            popup.OnAfterHide();
        }

        public T GetPanel<T>() where T : UIPanel
        {
            _panels.TryGetValue(typeof(T), out var panel);
            return panel as T;
        }


        private async UniTask LoadAndRegisterPanel(AssetReferenceGameObject reference, Transform root)
        {
            var go = await reference.InstantiateAsync(root).ToUniTask();
            var panel = go.GetComponent<UIPanel>();

            if (panel == null)
            {
                Debug.LogError($"[UIManager] 프리팹에 UIPanel 컴포넌트가 없습니다: {go.name}");
                return;
            }

            panel.Initialize();
            panel.RegisterLocalizedTexts();
            panel.gameObject.SetActive(false);
            _panels.Add(panel.GetType(), panel);
        }

        private bool TryGetPanel<T>(out T panel) where T : UIPanel
        {
            if (_panels.TryGetValue(typeof(T), out var found))
            {
                panel = found as T;
                return true;
            }

            Debug.LogError($"[UIManager] 패널을 찾을 수 없습니다: {typeof(T)}");
            panel = null;
            return false;
        }
    }
}
