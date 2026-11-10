using System;
using System.Drawing;
using System.Windows.Forms;

namespace K2
{
    // Небольшой диалог после смены языка: перезапустить K2 сейчас или позже.
    public class RestartPrompt : Form
    {
        public static bool Ask(IWin32Window owner)
        {
            using (RestartPrompt f = new RestartPrompt())
                return f.ShowDialog(owner) == DialogResult.OK;
        }

        public RestartPrompt()
        {
            Text = Loc.T("Restart K2");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(20, 26, 34);
            ForeColor = Color.FromArgb(232, 240, 245);
            ClientSize = new Size(430, 150);
            Font = new Font("Segoe UI", 10f);
            try { Icon = MainForm.AppIcon(); } catch { }

            Label text = new Label();
            text.Text = Loc.T("Restart K2 to apply the language?");
            text.ForeColor = ForeColor;
            text.BackColor = Color.Transparent;
            text.Font = new Font("Segoe UI", 10.5f);
            text.SetBounds(20, 20, 390, 48);
            Controls.Add(text);

            Button later = MakeButton(Loc.T("Later"), false);
            later.SetBounds(160, 100, 110, 34);
            later.DialogResult = DialogResult.Cancel;
            Controls.Add(later);

            Button restart = MakeButton(Loc.T("Restart"), true);
            restart.SetBounds(280, 100, 130, 34);
            restart.DialogResult = DialogResult.OK;
            Controls.Add(restart);

            AcceptButton = restart;
            CancelButton = later;
        }

        static Button MakeButton(string text, bool accent)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = accent ? Color.FromArgb(0, 191, 166) : Color.FromArgb(30, 41, 54);
            b.ForeColor = accent ? Color.FromArgb(4, 22, 20) : Color.FromArgb(232, 240, 245);
            b.FlatAppearance.BorderColor = accent ? Color.FromArgb(0, 191, 166) : Color.FromArgb(52, 70, 92);
            b.FlatAppearance.BorderSize = 1;
            return b;
        }
    }
}
