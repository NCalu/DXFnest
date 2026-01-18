namespace DXFnest
{
    partial class NestGUI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NestGUI));
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.tab = new System.Windows.Forms.TabControl();
            this.partTab = new System.Windows.Forms.TabPage();
            this.partGrid = new System.Windows.Forms.DataGridView();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.importPartButton = new System.Windows.Forms.ToolStripButton();
            this.removePartButton = new System.Windows.Forms.ToolStripButton();
            this.clearPartsButton = new System.Windows.Forms.ToolStripButton();
            this.sheetTab = new System.Windows.Forms.TabPage();
            this.sheetGrid = new System.Windows.Forms.DataGridView();
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.addSheetButton = new System.Windows.Forms.ToolStripButton();
            this.importSheetButton = new System.Windows.Forms.ToolStripButton();
            this.removeSheetButton = new System.Windows.Forms.ToolStripButton();
            this.clearSheetsButton = new System.Windows.Forms.ToolStripButton();
            this.loadNestButton = new System.Windows.Forms.ToolStripButton();
            this.nestTab = new System.Windows.Forms.TabPage();
            this.nestGrid = new System.Windows.Forms.DataGridView();
            this.toolStrip3 = new System.Windows.Forms.ToolStrip();
            this.clearNestsButton = new System.Windows.Forms.ToolStripButton();
            this.optsTab = new System.Windows.Forms.TabPage();
            this.optionsGrid = new System.Windows.Forms.PropertyGrid();
            this.toolStrip5 = new System.Windows.Forms.ToolStrip();
            this.resetOptionsButton = new System.Windows.Forms.ToolStripButton();
            this.posLabel = new System.Windows.Forms.Label();
            this.runNestButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.tab.SuspendLayout();
            this.partTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.partGrid)).BeginInit();
            this.toolStrip1.SuspendLayout();
            this.sheetTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sheetGrid)).BeginInit();
            this.toolStrip2.SuspendLayout();
            this.nestTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nestGrid)).BeginInit();
            this.toolStrip3.SuspendLayout();
            this.optsTab.SuspendLayout();
            this.toolStrip5.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer.BackColor = System.Drawing.SystemColors.Window;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.tab);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.BackColor = System.Drawing.SystemColors.Control;
            this.splitContainer.Panel2.Controls.Add(this.posLabel);
            this.splitContainer.Size = new System.Drawing.Size(784, 320);
            this.splitContainer.SplitterDistance = 392;
            this.splitContainer.SplitterWidth = 8;
            this.splitContainer.TabIndex = 34;
            this.splitContainer.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.splitContainer_SplitterMoved);
            this.splitContainer.Paint += new System.Windows.Forms.PaintEventHandler(this.splitContainer_Paint);
            // 
            // tab
            // 
            this.tab.Controls.Add(this.partTab);
            this.tab.Controls.Add(this.sheetTab);
            this.tab.Controls.Add(this.nestTab);
            this.tab.Controls.Add(this.optsTab);
            this.tab.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tab.ItemSize = new System.Drawing.Size(0, 21);
            this.tab.Location = new System.Drawing.Point(0, 0);
            this.tab.Margin = new System.Windows.Forms.Padding(0);
            this.tab.Multiline = true;
            this.tab.Name = "tab";
            this.tab.SelectedIndex = 0;
            this.tab.Size = new System.Drawing.Size(392, 320);
            this.tab.TabIndex = 46;
            // 
            // partTab
            // 
            this.partTab.BackColor = System.Drawing.SystemColors.Control;
            this.partTab.Controls.Add(this.partGrid);
            this.partTab.Controls.Add(this.toolStrip1);
            this.partTab.Location = new System.Drawing.Point(4, 25);
            this.partTab.Margin = new System.Windows.Forms.Padding(0);
            this.partTab.Name = "partTab";
            this.partTab.Size = new System.Drawing.Size(384, 291);
            this.partTab.TabIndex = 0;
            this.partTab.Text = "PARTS";
            // 
            // partGrid
            // 
            this.partGrid.AllowUserToAddRows = false;
            this.partGrid.AllowUserToDeleteRows = false;
            this.partGrid.AllowUserToResizeRows = false;
            this.partGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.partGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.partGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.partGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.partGrid.Location = new System.Drawing.Point(0, 25);
            this.partGrid.Margin = new System.Windows.Forms.Padding(0);
            this.partGrid.MultiSelect = false;
            this.partGrid.Name = "partGrid";
            this.partGrid.RowHeadersVisible = false;
            this.partGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.partGrid.Size = new System.Drawing.Size(384, 266);
            this.partGrid.TabIndex = 46;
            // 
            // toolStrip1
            // 
            this.toolStrip1.BackColor = System.Drawing.SystemColors.Control;
            this.toolStrip1.Font = new System.Drawing.Font("Arial", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importPartButton,
            this.removePartButton,
            this.clearPartsButton});
            this.toolStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0);
            this.toolStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip1.Size = new System.Drawing.Size(384, 25);
            this.toolStrip1.TabIndex = 45;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // importPartButton
            // 
            this.importPartButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.importPartButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.importPartButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.importPartButton.Name = "importPartButton";
            this.importPartButton.Size = new System.Drawing.Size(55, 22);
            this.importPartButton.Text = "Import";
            this.importPartButton.ToolTipText = "Import part";
            // 
            // removePartButton
            // 
            this.removePartButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removePartButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removePartButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removePartButton.Name = "removePartButton";
            this.removePartButton.Size = new System.Drawing.Size(70, 22);
            this.removePartButton.Text = "Remove";
            this.removePartButton.ToolTipText = "Remove part";
            // 
            // clearPartsButton
            // 
            this.clearPartsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearPartsButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clearPartsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearPartsButton.Name = "clearPartsButton";
            this.clearPartsButton.Size = new System.Drawing.Size(50, 22);
            this.clearPartsButton.Text = "Clear";
            this.clearPartsButton.ToolTipText = "Clear parts";
            // 
            // sheetTab
            // 
            this.sheetTab.Controls.Add(this.sheetGrid);
            this.sheetTab.Controls.Add(this.toolStrip2);
            this.sheetTab.Location = new System.Drawing.Point(4, 25);
            this.sheetTab.Margin = new System.Windows.Forms.Padding(0);
            this.sheetTab.Name = "sheetTab";
            this.sheetTab.Size = new System.Drawing.Size(384, 291);
            this.sheetTab.TabIndex = 1;
            this.sheetTab.Text = "SHEETS";
            this.sheetTab.UseVisualStyleBackColor = true;
            // 
            // sheetGrid
            // 
            this.sheetGrid.AllowUserToAddRows = false;
            this.sheetGrid.AllowUserToDeleteRows = false;
            this.sheetGrid.AllowUserToResizeRows = false;
            this.sheetGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.sheetGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.sheetGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.sheetGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sheetGrid.Location = new System.Drawing.Point(0, 25);
            this.sheetGrid.Margin = new System.Windows.Forms.Padding(0);
            this.sheetGrid.MultiSelect = false;
            this.sheetGrid.Name = "sheetGrid";
            this.sheetGrid.RowHeadersVisible = false;
            this.sheetGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.sheetGrid.Size = new System.Drawing.Size(384, 266);
            this.sheetGrid.TabIndex = 47;
            // 
            // toolStrip2
            // 
            this.toolStrip2.BackColor = System.Drawing.SystemColors.Control;
            this.toolStrip2.Font = new System.Drawing.Font("Arial", 8.25F);
            this.toolStrip2.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip2.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addSheetButton,
            this.importSheetButton,
            this.removeSheetButton,
            this.clearSheetsButton,
            this.loadNestButton});
            this.toolStrip2.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.toolStrip2.Location = new System.Drawing.Point(0, 0);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Padding = new System.Windows.Forms.Padding(0);
            this.toolStrip2.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip2.Size = new System.Drawing.Size(384, 25);
            this.toolStrip2.TabIndex = 46;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // addSheetButton
            // 
            this.addSheetButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addSheetButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addSheetButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addSheetButton.Name = "addSheetButton";
            this.addSheetButton.Size = new System.Drawing.Size(41, 22);
            this.addSheetButton.Text = "Add";
            this.addSheetButton.ToolTipText = "Add sheet";
            // 
            // importSheetButton
            // 
            this.importSheetButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.importSheetButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.importSheetButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.importSheetButton.Name = "importSheetButton";
            this.importSheetButton.Size = new System.Drawing.Size(55, 22);
            this.importSheetButton.Text = "Import";
            this.importSheetButton.ToolTipText = "Import sheet";
            // 
            // removeSheetButton
            // 
            this.removeSheetButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removeSheetButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeSheetButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removeSheetButton.Name = "removeSheetButton";
            this.removeSheetButton.Size = new System.Drawing.Size(70, 22);
            this.removeSheetButton.Text = "Remove";
            this.removeSheetButton.ToolTipText = "Remove sheet";
            // 
            // clearSheetsButton
            // 
            this.clearSheetsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearSheetsButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clearSheetsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearSheetsButton.Name = "clearSheetsButton";
            this.clearSheetsButton.Size = new System.Drawing.Size(50, 22);
            this.clearSheetsButton.Text = "Clear";
            this.clearSheetsButton.ToolTipText = "Clear sheets";
            // 
            // loadNestButton
            // 
            this.loadNestButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.loadNestButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.loadNestButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.loadNestButton.Name = "loadNestButton";
            this.loadNestButton.Size = new System.Drawing.Size(102, 22);
            this.loadNestButton.Text = "Load nesting";
            this.loadNestButton.ToolTipText = "Load nesting";
            // 
            // nestTab
            // 
            this.nestTab.Controls.Add(this.nestGrid);
            this.nestTab.Controls.Add(this.toolStrip3);
            this.nestTab.Location = new System.Drawing.Point(4, 25);
            this.nestTab.Margin = new System.Windows.Forms.Padding(0);
            this.nestTab.Name = "nestTab";
            this.nestTab.Size = new System.Drawing.Size(384, 291);
            this.nestTab.TabIndex = 2;
            this.nestTab.Text = "NESTINGS";
            this.nestTab.UseVisualStyleBackColor = true;
            // 
            // nestGrid
            // 
            this.nestGrid.AllowUserToAddRows = false;
            this.nestGrid.AllowUserToDeleteRows = false;
            this.nestGrid.AllowUserToResizeRows = false;
            this.nestGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.nestGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.nestGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.nestGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nestGrid.Location = new System.Drawing.Point(0, 25);
            this.nestGrid.Margin = new System.Windows.Forms.Padding(0);
            this.nestGrid.MultiSelect = false;
            this.nestGrid.Name = "nestGrid";
            this.nestGrid.RowHeadersVisible = false;
            this.nestGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.nestGrid.Size = new System.Drawing.Size(384, 266);
            this.nestGrid.TabIndex = 51;
            // 
            // toolStrip3
            // 
            this.toolStrip3.BackColor = System.Drawing.SystemColors.Control;
            this.toolStrip3.Font = new System.Drawing.Font("Arial", 8.25F);
            this.toolStrip3.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip3.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip3.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clearNestsButton});
            this.toolStrip3.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.toolStrip3.Location = new System.Drawing.Point(0, 0);
            this.toolStrip3.Name = "toolStrip3";
            this.toolStrip3.Padding = new System.Windows.Forms.Padding(0);
            this.toolStrip3.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip3.Size = new System.Drawing.Size(384, 25);
            this.toolStrip3.TabIndex = 50;
            this.toolStrip3.Text = "toolStrip3";
            // 
            // clearNestsButton
            // 
            this.clearNestsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearNestsButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clearNestsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearNestsButton.Name = "clearNestsButton";
            this.clearNestsButton.Size = new System.Drawing.Size(50, 22);
            this.clearNestsButton.Text = "Clear";
            this.clearNestsButton.ToolTipText = "Clear nestings";
            // 
            // optsTab
            // 
            this.optsTab.Controls.Add(this.optionsGrid);
            this.optsTab.Controls.Add(this.toolStrip5);
            this.optsTab.Location = new System.Drawing.Point(4, 25);
            this.optsTab.Margin = new System.Windows.Forms.Padding(0);
            this.optsTab.Name = "optsTab";
            this.optsTab.Size = new System.Drawing.Size(384, 291);
            this.optsTab.TabIndex = 3;
            this.optsTab.Text = "OPTIONS";
            this.optsTab.UseVisualStyleBackColor = true;
            // 
            // optionsGrid
            // 
            this.optionsGrid.CategorySplitterColor = System.Drawing.Color.Black;
            this.optionsGrid.DisabledItemForeColor = System.Drawing.Color.Black;
            this.optionsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.optionsGrid.HelpVisible = false;
            this.optionsGrid.LineColor = System.Drawing.Color.White;
            this.optionsGrid.Location = new System.Drawing.Point(0, 25);
            this.optionsGrid.Margin = new System.Windows.Forms.Padding(0);
            this.optionsGrid.Name = "optionsGrid";
            this.optionsGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.optionsGrid.Size = new System.Drawing.Size(384, 266);
            this.optionsGrid.TabIndex = 55;
            this.optionsGrid.ToolbarVisible = false;
            this.optionsGrid.ViewBackColor = System.Drawing.SystemColors.Control;
            // 
            // toolStrip5
            // 
            this.toolStrip5.BackColor = System.Drawing.SystemColors.Control;
            this.toolStrip5.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.toolStrip5.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip5.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.resetOptionsButton});
            this.toolStrip5.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.toolStrip5.Location = new System.Drawing.Point(0, 0);
            this.toolStrip5.Name = "toolStrip5";
            this.toolStrip5.Padding = new System.Windows.Forms.Padding(0);
            this.toolStrip5.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip5.Size = new System.Drawing.Size(384, 25);
            this.toolStrip5.TabIndex = 54;
            this.toolStrip5.Text = "toolStrip5";
            // 
            // resetOptionsButton
            // 
            this.resetOptionsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.resetOptionsButton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.resetOptionsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.resetOptionsButton.Name = "resetOptionsButton";
            this.resetOptionsButton.Size = new System.Drawing.Size(53, 22);
            this.resetOptionsButton.Text = "Reset";
            this.resetOptionsButton.ToolTipText = "Reset";
            // 
            // posLabel
            // 
            this.posLabel.AutoSize = true;
            this.posLabel.BackColor = System.Drawing.Color.Transparent;
            this.posLabel.Location = new System.Drawing.Point(8, 9);
            this.posLabel.Name = "posLabel";
            this.posLabel.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.posLabel.Size = new System.Drawing.Size(50, 26);
            this.posLabel.TabIndex = 0;
            this.posLabel.Text = "X0.0000 \r\nY0.0000";
            // 
            // runNestButton
            // 
            this.runNestButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.runNestButton.Location = new System.Drawing.Point(12, 326);
            this.runNestButton.Name = "runNestButton";
            this.runNestButton.Size = new System.Drawing.Size(120, 23);
            this.runNestButton.TabIndex = 47;
            this.runNestButton.Text = "Run Nesting";
            this.runNestButton.UseVisualStyleBackColor = true;
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Location = new System.Drawing.Point(652, 326);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(120, 23);
            this.exportButton.TabIndex = 48;
            this.exportButton.Text = "Export DXF";
            this.exportButton.UseVisualStyleBackColor = true;
            // 
            // NestGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(784, 361);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.runNestButton);
            this.Controls.Add(this.splitContainer);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "NestGUI";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "NCnetic - DXF Nest";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.tab.ResumeLayout(false);
            this.partTab.ResumeLayout(false);
            this.partTab.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.partGrid)).EndInit();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.sheetTab.ResumeLayout(false);
            this.sheetTab.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sheetGrid)).EndInit();
            this.toolStrip2.ResumeLayout(false);
            this.toolStrip2.PerformLayout();
            this.nestTab.ResumeLayout(false);
            this.nestTab.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nestGrid)).EndInit();
            this.toolStrip3.ResumeLayout(false);
            this.toolStrip3.PerformLayout();
            this.optsTab.ResumeLayout(false);
            this.optsTab.PerformLayout();
            this.toolStrip5.ResumeLayout(false);
            this.toolStrip5.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        public System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Label posLabel;
        public System.Windows.Forms.Button runNestButton;
        public System.Windows.Forms.Button exportButton;
        public System.Windows.Forms.TabControl tab;
        private System.Windows.Forms.TabPage partTab;
        public System.Windows.Forms.DataGridView partGrid;
        public System.Windows.Forms.ToolStrip toolStrip1;
        public System.Windows.Forms.ToolStripButton importPartButton;
        public System.Windows.Forms.ToolStripButton removePartButton;
        public System.Windows.Forms.ToolStripButton clearPartsButton;
        private System.Windows.Forms.TabPage sheetTab;
        public System.Windows.Forms.DataGridView sheetGrid;
        public System.Windows.Forms.ToolStrip toolStrip2;
        public System.Windows.Forms.ToolStripButton addSheetButton;
        public System.Windows.Forms.ToolStripButton importSheetButton;
        public System.Windows.Forms.ToolStripButton removeSheetButton;
        public System.Windows.Forms.ToolStripButton clearSheetsButton;
        public System.Windows.Forms.ToolStripButton loadNestButton;
        private System.Windows.Forms.TabPage nestTab;
        public System.Windows.Forms.DataGridView nestGrid;
        public System.Windows.Forms.ToolStrip toolStrip3;
        public System.Windows.Forms.ToolStripButton clearNestsButton;
        private System.Windows.Forms.TabPage optsTab;
        public System.Windows.Forms.PropertyGrid optionsGrid;
        public System.Windows.Forms.ToolStrip toolStrip5;
        public System.Windows.Forms.ToolStripButton resetOptionsButton;
    }
}