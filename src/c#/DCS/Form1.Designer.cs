namespace DongaDCS
{
    partial class Form1
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.Button_Exit = new System.Windows.Forms.Button();
            this.Timer10 = new System.Windows.Forms.Timer(this.components);
            this.ui_timer10 = new System.Windows.Forms.Timer(this.components);
            this.checkbox_openCV = new System.Windows.Forms.CheckBox();
            this.pictureBox_logo = new System.Windows.Forms.PictureBox();
            this.imageBox1 = new Emgu.CV.UI.ImageBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_logo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // Button_Exit
            // 
            this.Button_Exit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Button_Exit.Location = new System.Drawing.Point(725, -1);
            this.Button_Exit.Name = "Button_Exit";
            this.Button_Exit.Size = new System.Drawing.Size(77, 46);
            this.Button_Exit.TabIndex = 0;
            this.Button_Exit.TabStop = false;
            this.Button_Exit.Text = "종료";
            this.Button_Exit.UseVisualStyleBackColor = true;
            this.Button_Exit.Click += new System.EventHandler(this.Button_Exit_Click);
            // 
            // Timer10
            // 
            this.Timer10.Interval = 10;
            this.Timer10.Tick += new System.EventHandler(this.Timer10_Tick);
            // 
            // ui_timer10
            // 
            this.ui_timer10.Interval = 10;
            this.ui_timer10.Tick += new System.EventHandler(this.ui_timer10_Tick);
            // 
            // checkbox_openCV
            // 
            this.checkbox_openCV.AutoSize = true;
            this.checkbox_openCV.Checked = true;
            this.checkbox_openCV.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkbox_openCV.Font = new System.Drawing.Font("나눔고딕", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.checkbox_openCV.Location = new System.Drawing.Point(12, 12);
            this.checkbox_openCV.Name = "checkbox_openCV";
            this.checkbox_openCV.Size = new System.Drawing.Size(137, 35);
            this.checkbox_openCV.TabIndex = 2;
            this.checkbox_openCV.Text = "OpenCV";
            this.checkbox_openCV.UseVisualStyleBackColor = true;
            // 
            // pictureBox_logo
            // 
            this.pictureBox_logo.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox_logo.Image")));
            this.pictureBox_logo.Location = new System.Drawing.Point(652, 301);
            this.pictureBox_logo.Name = "pictureBox_logo";
            this.pictureBox_logo.Size = new System.Drawing.Size(150, 150);
            this.pictureBox_logo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox_logo.TabIndex = 3;
            this.pictureBox_logo.TabStop = false;
            // 
            // imageBox1
            // 
            this.imageBox1.Enabled = false;
            this.imageBox1.Location = new System.Drawing.Point(621, -1);
            this.imageBox1.Name = "imageBox1";
            this.imageBox1.Size = new System.Drawing.Size(98, 48);
            this.imageBox1.TabIndex = 2;
            this.imageBox1.TabStop = false;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.imageBox1);
            this.Controls.Add(this.pictureBox_logo);
            this.Controls.Add(this.checkbox_openCV);
            this.Controls.Add(this.Button_Exit);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_logo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button Button_Exit;
        private System.Windows.Forms.Timer Timer10;
        /*
        private System.Windows.Forms.Label distanceLabel1;
        private System.Windows.Forms.Label distanceLabel2;
        private System.Windows.Forms.Label tempLabel;
        private System.Windows.Forms.Label humidityLabel;
        private System.Windows.Forms.Label powerLabel;
        */
        private System.Windows.Forms.Timer ui_timer10;
        private System.Windows.Forms.CheckBox checkbox_openCV;
        private System.Windows.Forms.PictureBox pictureBox_logo;
        private Emgu.CV.UI.ImageBox imageBox1;
        private System.Windows.Forms.Timer timer1;
    }
}

