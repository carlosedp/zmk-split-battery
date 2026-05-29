namespace ZMKSplit
{
    partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.DevicesListView = new System.Windows.Forms.ListView();
            this.NameColumn = new System.Windows.Forms.ColumnHeader();
            this.ConnectedColumn = new System.Windows.Forms.ColumnHeader();
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.TrayContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.showContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refreshNowContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.disconnectContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ReloadButton = new System.Windows.Forms.Button();
            this.RefreshBatteryButton = new System.Windows.Forms.Button();
            this.StatusStrip1 = new System.Windows.Forms.StatusStrip();
            this.StatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.LastUpdatedLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.ConnectButton = new System.Windows.Forms.Button();
            this.ReconnectTimer = new System.Windows.Forms.Timer(this.components);
            this.PollingTimer = new System.Windows.Forms.Timer(this.components);
            this.AutoRunCheckBox = new System.Windows.Forms.CheckBox();
            this.RefreshIntervalLabel = new System.Windows.Forms.Label();
            this.RefreshIntervalNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.StartMinimizedCheckBox = new System.Windows.Forms.CheckBox();
            this.LowBatteryThresholdLabel = new System.Windows.Forms.Label();
            this.LowBatteryThresholdNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.TrayContextMenu.SuspendLayout();
            this.StatusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RefreshIntervalNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.LowBatteryThresholdNumericUpDown)).BeginInit();
            this.SuspendLayout();
            //
            // DevicesListView
            //
            this.DevicesListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DevicesListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.NameColumn,
            this.ConnectedColumn});
            this.DevicesListView.FullRowSelect = true;
            this.DevicesListView.GridLines = true;
            this.DevicesListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.DevicesListView.HideSelection = false;
            this.DevicesListView.Location = new System.Drawing.Point(12, 12);
            this.DevicesListView.MultiSelect = false;
            this.DevicesListView.Name = "DevicesListView";
            this.DevicesListView.ShowGroups = false;
            this.DevicesListView.Size = new System.Drawing.Size(382, 369);
            this.DevicesListView.TabIndex = 0;
            this.DevicesListView.UseCompatibleStateImageBehavior = false;
            this.DevicesListView.View = System.Windows.Forms.View.Details;
            this.DevicesListView.DoubleClick += new System.EventHandler(this.DevicesListView_DoubleClick);
            this.DevicesListView.SelectedIndexChanged += new System.EventHandler(this.DevicesListView_SelectedIndexChanged);
            //
            // NameColumn
            //
            this.NameColumn.Text = "Name";
            this.NameColumn.Width = 295;
            //
            // ConnectedColumn
            //
            this.ConnectedColumn.Text = "Connected";
            this.ConnectedColumn.Width = 75;
            this.ConnectedColumn.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            //
            // NotifyIcon
            //
            this.NotifyIcon.ContextMenuStrip = this.TrayContextMenu;
            this.NotifyIcon.Visible = true;
            this.NotifyIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.NotifyIcon_MouseDoubleClick);
            //
            // TrayContextMenu
            //
            this.TrayContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showContextMenuItem,
            this.refreshNowContextMenuItem,
            this.disconnectContextMenuItem,
            this.exitContextMenuItem});
            this.TrayContextMenu.Name = "trayContextMenu";
            this.TrayContextMenu.Size = new System.Drawing.Size(153, 92);
            this.TrayContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.TrayContextMenu_Opening);
            //
            // showContextMenuItem
            //
            this.showContextMenuItem.Name = "showContextMenuItem";
            this.showContextMenuItem.Size = new System.Drawing.Size(103, 22);
            this.showContextMenuItem.Text = "Show";
            this.showContextMenuItem.Click += new System.EventHandler(this.ShowContextMenuItem_Click);
            //
            // refreshNowContextMenuItem
            //
            this.refreshNowContextMenuItem.Name = "refreshNowContextMenuItem";
            this.refreshNowContextMenuItem.Size = new System.Drawing.Size(153, 22);
            this.refreshNowContextMenuItem.Text = "Refresh Now";
            this.refreshNowContextMenuItem.Click += new System.EventHandler(this.RefreshNowContextMenuItem_Click);
            //
            // disconnectContextMenuItem
            //
            this.disconnectContextMenuItem.Name = "disconnectContextMenuItem";
            this.disconnectContextMenuItem.Size = new System.Drawing.Size(153, 22);
            this.disconnectContextMenuItem.Text = "Disconnect";
            this.disconnectContextMenuItem.Click += new System.EventHandler(this.DisconnectContextMenuItem_Click);
            //
            // exitContextMenuItem
            //
            this.exitContextMenuItem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.exitContextMenuItem.Name = "exitContextMenuItem";
            this.exitContextMenuItem.Size = new System.Drawing.Size(103, 22);
            this.exitContextMenuItem.Text = "E&xit";
            this.exitContextMenuItem.Click += new System.EventHandler(this.ExitContextMenuItem_Click);
            //
            // ReloadButton
            //
            this.ReloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ReloadButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.ReloadButton.Location = new System.Drawing.Point(407, 12);
            this.ReloadButton.Name = "ReloadButton";
            this.ReloadButton.Size = new System.Drawing.Size(158, 32);
            this.ReloadButton.TabIndex = 3;
            this.ReloadButton.Text = "Refresh Devices";
            this.ReloadButton.UseVisualStyleBackColor = true;
            this.ReloadButton.MouseClick += new System.Windows.Forms.MouseEventHandler(this.ReloadButton_MouseClick);
            //
            // RefreshBatteryButton
            //
            this.RefreshBatteryButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.RefreshBatteryButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.RefreshBatteryButton.Location = new System.Drawing.Point(407, 88);
            this.RefreshBatteryButton.Name = "RefreshBatteryButton";
            this.RefreshBatteryButton.Size = new System.Drawing.Size(158, 32);
            this.RefreshBatteryButton.TabIndex = 10;
            this.RefreshBatteryButton.Text = "Refresh Battery";
            this.RefreshBatteryButton.UseVisualStyleBackColor = true;
            this.RefreshBatteryButton.Click += new System.EventHandler(this.RefreshBatteryButton_Click);
            //
            // StatusStrip1
            //
            this.StatusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.StatusLabel,
            this.LastUpdatedLabel});
            this.StatusStrip1.Location = new System.Drawing.Point(0, 384);
            this.StatusStrip1.Name = "StatusStrip1";
            this.StatusStrip1.Size = new System.Drawing.Size(577, 22);
            this.StatusStrip1.TabIndex = 4;
            this.StatusStrip1.Text = "statusStrip1";
            //
            // StatusLabel
            //
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(39, 17);
            this.StatusLabel.Spring = true;
            this.StatusLabel.Text = "Ready";
            //
            // LastUpdatedLabel
            //
            this.LastUpdatedLabel.Name = "LastUpdatedLabel";
            this.LastUpdatedLabel.Size = new System.Drawing.Size(130, 17);
            this.LastUpdatedLabel.Text = "";
            this.LastUpdatedLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // ConnectButton
            //
            this.ConnectButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ConnectButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.ConnectButton.Location = new System.Drawing.Point(407, 50);
            this.ConnectButton.Name = "ConnectButton";
            this.ConnectButton.Size = new System.Drawing.Size(158, 32);
            this.ConnectButton.TabIndex = 5;
            this.ConnectButton.Text = "Connect";
            this.ConnectButton.UseVisualStyleBackColor = true;
            this.ConnectButton.Click += new System.EventHandler(this.ConnectButton_Click);
            //
            // ReconnectTimer
            //
            this.ReconnectTimer.Interval = 1000;
            this.ReconnectTimer.Tick += new System.EventHandler(this.ReconnectTimer_Tick);
            //
            // PollingTimer
            //
            this.PollingTimer.Interval = 300000;
            this.PollingTimer.Tick += new System.EventHandler(this.PollingTimer_Tick);
            //
            // AutoRunCheckBox
            //
            this.AutoRunCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.AutoRunCheckBox.AutoSize = true;
            this.AutoRunCheckBox.Location = new System.Drawing.Point(408, 130);
            this.AutoRunCheckBox.Name = "AutoRunCheckBox";
            this.AutoRunCheckBox.Size = new System.Drawing.Size(152, 19);
            this.AutoRunCheckBox.TabIndex = 6;
            this.AutoRunCheckBox.Text = "Run at Windows startup";
            this.AutoRunCheckBox.UseVisualStyleBackColor = true;
            this.AutoRunCheckBox.CheckedChanged += new System.EventHandler(this.AutoRunCheckBox_CheckedChanged);
            //
            // RefreshIntervalLabel
            //
            this.RefreshIntervalLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.RefreshIntervalLabel.AutoSize = true;
            this.RefreshIntervalLabel.Location = new System.Drawing.Point(408, 160);
            this.RefreshIntervalLabel.Name = "RefreshIntervalLabel";
            this.RefreshIntervalLabel.Size = new System.Drawing.Size(120, 15);
            this.RefreshIntervalLabel.TabIndex = 7;
            this.RefreshIntervalLabel.Text = "Refresh every (min):";
            //
            // RefreshIntervalNumericUpDown
            //
            this.RefreshIntervalNumericUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.RefreshIntervalNumericUpDown.Location = new System.Drawing.Point(408, 180);
            this.RefreshIntervalNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.RefreshIntervalNumericUpDown.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
            this.RefreshIntervalNumericUpDown.Value = new decimal(new int[] { 5, 0, 0, 0 });
            this.RefreshIntervalNumericUpDown.Name = "RefreshIntervalNumericUpDown";
            this.RefreshIntervalNumericUpDown.Size = new System.Drawing.Size(158, 23);
            this.RefreshIntervalNumericUpDown.TabIndex = 8;
            this.RefreshIntervalNumericUpDown.ValueChanged += new System.EventHandler(this.RefreshIntervalNumericUpDown_ValueChanged);
            //
            // StartMinimizedCheckBox
            //
            this.StartMinimizedCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.StartMinimizedCheckBox.AutoSize = true;
            this.StartMinimizedCheckBox.Location = new System.Drawing.Point(408, 214);
            this.StartMinimizedCheckBox.Name = "StartMinimizedCheckBox";
            this.StartMinimizedCheckBox.Size = new System.Drawing.Size(152, 19);
            this.StartMinimizedCheckBox.TabIndex = 9;
            this.StartMinimizedCheckBox.Text = "Start minimized to tray";
            this.StartMinimizedCheckBox.UseVisualStyleBackColor = true;
            this.StartMinimizedCheckBox.CheckedChanged += new System.EventHandler(this.StartMinimizedCheckBox_CheckedChanged);
            //
            // LowBatteryThresholdLabel
            //
            this.LowBatteryThresholdLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.LowBatteryThresholdLabel.AutoSize = true;
            this.LowBatteryThresholdLabel.Location = new System.Drawing.Point(408, 248);
            this.LowBatteryThresholdLabel.Name = "LowBatteryThresholdLabel";
            this.LowBatteryThresholdLabel.Size = new System.Drawing.Size(158, 15);
            this.LowBatteryThresholdLabel.TabIndex = 11;
            this.LowBatteryThresholdLabel.Text = "Low battery alert (%):";
            //
            // LowBatteryThresholdNumericUpDown
            //
            this.LowBatteryThresholdNumericUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.LowBatteryThresholdNumericUpDown.Location = new System.Drawing.Point(408, 268);
            this.LowBatteryThresholdNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.LowBatteryThresholdNumericUpDown.Maximum = new decimal(new int[] { 99, 0, 0, 0 });
            this.LowBatteryThresholdNumericUpDown.Value = new decimal(new int[] { 20, 0, 0, 0 });
            this.LowBatteryThresholdNumericUpDown.Name = "LowBatteryThresholdNumericUpDown";
            this.LowBatteryThresholdNumericUpDown.Size = new System.Drawing.Size(158, 23);
            this.LowBatteryThresholdNumericUpDown.TabIndex = 12;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(577, 440);
            this.Controls.Add(this.AutoRunCheckBox);
            this.Controls.Add(this.RefreshIntervalLabel);
            this.Controls.Add(this.RefreshIntervalNumericUpDown);
            this.Controls.Add(this.StartMinimizedCheckBox);
            this.Controls.Add(this.LowBatteryThresholdLabel);
            this.Controls.Add(this.LowBatteryThresholdNumericUpDown);
            this.Controls.Add(this.RefreshBatteryButton);
            this.Controls.Add(this.ConnectButton);
            this.Controls.Add(this.StatusStrip1);
            this.Controls.Add(this.ReloadButton);
            this.Controls.Add(this.DevicesListView);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "ZMK Split Battery Status";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.TrayContextMenu.ResumeLayout(false);
            this.StatusStrip1.ResumeLayout(false);
            this.StatusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RefreshIntervalNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.LowBatteryThresholdNumericUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView DevicesListView;
        private System.Windows.Forms.NotifyIcon NotifyIcon;
        private System.Windows.Forms.Button ReloadButton;
        private System.Windows.Forms.Button RefreshBatteryButton;
        private System.Windows.Forms.ColumnHeader NameColumn;
        private System.Windows.Forms.ColumnHeader ConnectedColumn;
        private System.Windows.Forms.StatusStrip StatusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel StatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel LastUpdatedLabel;
        private System.Windows.Forms.Button ConnectButton;
        private System.Windows.Forms.ContextMenuStrip TrayContextMenu;
        private System.Windows.Forms.ToolStripMenuItem exitContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refreshNowContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem disconnectContextMenuItem;
        private System.Windows.Forms.Timer ReconnectTimer;
        private System.Windows.Forms.Timer PollingTimer;
        private System.Windows.Forms.CheckBox AutoRunCheckBox;
        private System.Windows.Forms.Label RefreshIntervalLabel;
        private System.Windows.Forms.NumericUpDown RefreshIntervalNumericUpDown;
        private System.Windows.Forms.CheckBox StartMinimizedCheckBox;
        private System.Windows.Forms.Label LowBatteryThresholdLabel;
        private System.Windows.Forms.NumericUpDown LowBatteryThresholdNumericUpDown;
    }
}
