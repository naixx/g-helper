using GHelper.Helpers;
using GHelper.Peripherals.Mouse;

namespace GHelper.UI
{
    // Captures a key combination. The key box takes every key press while it has focus, so Enter,
    // Esc and Tab can be part of the combination. Combinations Windows handles itself (Alt + Tab,
    // Win + D) never reach the box and are composed with the checkboxes instead.
    public class RKeyComboPicker : RForm
    {
        // InputDispatcher.RunKeyCommand presses at most four keys
        private const int MaxKeys = 4;

        public Keys[] Combo { get; private set; } = [];

        private readonly KeyBox keyBox;
        private readonly CheckBox checkCtrl, checkShift, checkAlt, checkWin;
        private readonly RButton ok;
        private readonly float scale;
        private Keys key = Keys.None;

        private int S(int v) => (int)Math.Round(v * scale);

        public RKeyComboPicker()
        {
            Font = new Font("Segoe UI", 9F);
            Text = "Key combination";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;

            using (var g = CreateGraphics()) scale = g.DpiX / 96f;

            const int pad = 12, width = 280, checkY = 68, buttonsY = 102;

            var hint = new Label { Text = "Press a key combination", Location = new Point(S(pad), S(pad)), AutoSize = true };

            keyBox = new KeyBox { Location = new Point(S(pad), S(pad + 24)), Size = new Size(S(width), S(23)), ReadOnly = true, TextAlign = HorizontalAlignment.Center };
            keyBox.KeyCaptured += OnKey;

            checkCtrl = AddCheck("Ctrl", pad, checkY);
            checkShift = AddCheck("Shift", pad + 70, checkY);
            checkAlt = AddCheck("Alt", pad + 140, checkY);
            checkWin = AddCheck("Win", pad + 210, checkY);

            ok = new RButton { Text = "OK", Size = new Size(S(84), S(28)), Location = new Point(S(pad + width - 84 - 8 - 84), S(buttonsY)), DialogResult = DialogResult.OK, Enabled = false };
            var cancel = new RButton { Text = "Cancel", Size = new Size(S(84), S(28)), Location = new Point(S(pad + width - 84), S(buttonsY)), DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] { hint, keyBox, ok, cancel });

            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(S(pad + width + pad), S(buttonsY + 28 + pad));
            ActiveControl = keyBox;

            InitTheme();
        }

        private CheckBox AddCheck(string text, int x, int y)
        {
            var check = new CheckBox { Text = text, Location = new Point(S(x), S(y)), AutoSize = true };
            check.CheckedChanged += (s, e) => { UpdateCombo(); keyBox.Focus(); };
            Controls.Add(check);
            return check;
        }

        private void OnKey(Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            if (code is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
            // F13-F24 carry the combos themselves, a combo made of one would trigger itself
            if (code is >= Keys.F13 and <= Keys.F24) return;

            key = code;

            bool win = ((User32.GetAsyncKeyState((int)Keys.LWin) | User32.GetAsyncKeyState((int)Keys.RWin)) & 0x8000) != 0;
            if (win || (keyData & Keys.Modifiers) != Keys.None)
            {
                checkCtrl.Checked = keyData.HasFlag(Keys.Control);
                checkShift.Checked = keyData.HasFlag(Keys.Shift);
                checkAlt.Checked = keyData.HasFlag(Keys.Alt);
                checkWin.Checked = win;
            }

            UpdateCombo();
        }

        private void UpdateCombo()
        {
            var keys = new List<Keys>();
            if (checkCtrl.Checked) keys.Add(Keys.LControlKey);
            if (checkShift.Checked) keys.Add(Keys.LShiftKey);
            if (checkAlt.Checked) keys.Add(Keys.LMenu);
            if (checkWin.Checked) keys.Add(Keys.LWin);
            if (key != Keys.None) keys.Add(key);

            Combo = keys.ToArray();
            keyBox.Text = AsusMouse.KeysLabel(keys);
            ok.Enabled = key != Keys.None && keys.Count <= MaxKeys;
        }

        private class KeyBox : RTextBox
        {
            public event Action<Keys>? KeyCaptured;

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                KeyCaptured?.Invoke(keyData);
                return true;
            }
        }
    }
}
