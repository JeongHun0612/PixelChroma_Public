using UnityEngine.InputSystem;

namespace PixelChroma
{
    public class PlayerInputActions
    {
        private const string ActionMapName = "PlayerControll";
        private const string ActionMove = "Move";
        private const string ActionTogglePaint = "TogglePaint";
        private const string ActionSelectBrush = "SelectBrush";
        private const string ActionSelectEraser = "SelectEraser";
        private const string ActionHoldEyedropper = "HoldEyedropper";
        private const string ActionSelectFill = "SelectFill";
        private const string ActionBrushSizeModifier = "BrushSizeModifier";
        private const string ActionExposeSkill = "ExposeSkill";
        private const string ActionToggleSpectate = "ToggleSpectate";
        private const string ActionSpectateSeeker = "SpectateSeeker";
        private const string ActionSpectateRunner = "SpectateRunner";
        private const string ActionSpectateFree = "SpectateFree";
        private const string ActionCyclePrev = "CyclePrev";
        private const string ActionCycleNext = "CycleNext";
        private const string ActionUndo = "Undo";
        private const string ActionRedo = "Redo";
        private const string ActionChatToggle = "ChatToggle";
        private const string ActionLike = "Like";
        private const string ActionFormWheel = "FormWheel";
        private const string ActionSkipVote = "SkipVote";
        private readonly InputActionMap _map;

        public InputAction Move { get; }
        public InputAction TogglePaint { get; }
        public InputAction SelectBrush { get; }
        public InputAction SelectEraser { get; }
        public InputAction HoldEyedropper { get; }
        public InputAction SelectFill { get; }
        public InputAction BrushSizeModifier { get; }
        public InputAction ExposeSkill { get; }
        public InputAction ToggleSpectate { get; }
        public InputAction SpectateSeeker { get; }
        public InputAction SpectateRunner { get; }
        public InputAction SpectateFree { get; }
        public InputAction CyclePrev { get; }
        public InputAction CycleNext { get; }
        public InputAction Undo { get; }
        public InputAction Redo { get; }
        public InputAction ChatToggle { get; }
        public InputAction Like { get; }
        public InputAction FormWheel { get; }
        public InputAction SkipVote { get; }
        public PlayerInputActions(InputActionAsset asset)
        {
            _map = asset.FindActionMap(ActionMapName);
            Move = _map.FindAction(ActionMove);
            TogglePaint = _map.FindAction(ActionTogglePaint);
            SelectBrush = _map.FindAction(ActionSelectBrush);
            SelectEraser = _map.FindAction(ActionSelectEraser);
            HoldEyedropper = _map.FindAction(ActionHoldEyedropper);
            SelectFill = _map.FindAction(ActionSelectFill);
            BrushSizeModifier = _map.FindAction(ActionBrushSizeModifier);
            ExposeSkill = _map.FindAction(ActionExposeSkill);
            ToggleSpectate = _map.FindAction(ActionToggleSpectate);
            SpectateSeeker = _map.FindAction(ActionSpectateSeeker);
            SpectateRunner = _map.FindAction(ActionSpectateRunner);
            SpectateFree = _map.FindAction(ActionSpectateFree);
            CyclePrev = _map.FindAction(ActionCyclePrev);
            CycleNext = _map.FindAction(ActionCycleNext);
            Undo = _map.FindAction(ActionUndo);
            Redo = _map.FindAction(ActionRedo);
            ChatToggle = _map.FindAction(ActionChatToggle);
            Like = _map.FindAction(ActionLike);
            FormWheel = _map.FindAction(ActionFormWheel);
            SkipVote = _map.FindAction(ActionSkipVote);
        }

        public void EnableAll() => _map.Enable();
        public void DisableAll() => _map.Disable();
    }
}
