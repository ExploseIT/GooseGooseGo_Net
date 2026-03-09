namespace GGG_WinFormer
{
    partial class GGG_Winformer
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            tb_output = new TextBox();
            lblLatency = new Label();
            dgvPositions = new DataGridView();
            updateTimer = new System.Windows.Forms.Timer(components);
            ((System.ComponentModel.ISupportInitialize)dgvPositions).BeginInit();
            SuspendLayout();
            // 
            // tb_output
            // 
            tb_output.AcceptsReturn = true;
            tb_output.Location = new Point(770, 12);
            tb_output.Multiline = true;
            tb_output.Name = "tb_output";
            tb_output.Size = new Size(461, 265);
            tb_output.TabIndex = 0;
            // 
            // lblLatency
            // 
            lblLatency.AutoSize = true;
            lblLatency.Location = new Point(335, 278);
            lblLatency.Name = "lblLatency";
            lblLatency.Size = new Size(0, 15);
            lblLatency.TabIndex = 1;
            // 
            // dgvPositions
            // 
            dgvPositions.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPositions.Location = new Point(2, 296);
            dgvPositions.Name = "dgvPositions";
            dgvPositions.Size = new Size(1229, 253);
            dgvPositions.TabIndex = 2;
            // 
            // GGG_Winformer
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1243, 561);
            Controls.Add(dgvPositions);
            Controls.Add(lblLatency);
            Controls.Add(tb_output);
            Name = "GGG_Winformer";
            Text = "GGG Winformer";
            ((System.ComponentModel.ISupportInitialize)dgvPositions).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox tb_output;
        private Label lblLatency;
        private DataGridView dgvPositions;
        private System.Windows.Forms.Timer updateTimer;
    }
}
