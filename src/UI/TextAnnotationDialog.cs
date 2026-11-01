using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShotPin.UI
{
    internal sealed class TextAnnotationDialog : Form
    {
        private readonly TextBox _textBox;

        public string ResultText
        {
            get { return _textBox.Text; }
        }

        public TextAnnotationDialog()
        {
            Text = "添加文字";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(440, 210);
            Font = new Font("Microsoft YaHei UI", 10.0f);

            Label label = new Label();
            label.Text = "输入要写在截图上的文字（可多行）：";
            label.AutoSize = true;
            label.Location = new Point(16, 14);

            _textBox = new TextBox();
            _textBox.Multiline = true;
            _textBox.ScrollBars = ScrollBars.Vertical;
            _textBox.AcceptsReturn = true;
            _textBox.Location = new Point(16, 42);
            _textBox.Size = new Size(408, 110);

            Button ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(260, 166);
            ok.Size = new Size(78, 28);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(346, 166);
            cancel.Size = new Size(78, 28);

            Controls.Add(label);
            Controls.Add(_textBox);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate { _textBox.Focus(); };
        }
    }
}
