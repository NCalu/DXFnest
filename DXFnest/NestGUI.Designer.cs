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
            this.pSplitContainer = new System.Windows.Forms.SplitContainer();
            this.partGrid = new System.Windows.Forms.DataGridView();
            this.editGrid = new System.Windows.Forms.PropertyGrid();
            this.partStrip = new System.Windows.Forms.ToolStrip();
            this.importPartButton = new System.Windows.Forms.ToolStripButton();
            this.removePartButton = new System.Windows.Forms.ToolStripButton();
            this.clearPartsButton = new System.Windows.Forms.ToolStripButton();
            this.editStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.editBevelButton = new System.Windows.Forms.ToolStripButton();
            this.shapeLibTab = new System.Windows.Forms.TabPage();
            this.shSplitContainer = new System.Windows.Forms.SplitContainer();
            this.scriptGrid = new System.Windows.Forms.DataGridView();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.scriptStrip = new System.Windows.Forms.ToolStrip();
            this.execButton = new System.Windows.Forms.ToolStripButton();
            this.sheetTab = new System.Windows.Forms.TabPage();
            this.sheetGrid = new System.Windows.Forms.DataGridView();
            this.sheetStrip = new System.Windows.Forms.ToolStrip();
            this.addSheetButton = new System.Windows.Forms.ToolStripButton();
            this.importSheetButton = new System.Windows.Forms.ToolStripButton();
            this.removeSheetButton = new System.Windows.Forms.ToolStripButton();
            this.clearSheetsButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.loadNestButton = new System.Windows.Forms.ToolStripButton();
            this.nestTab = new System.Windows.Forms.TabPage();
            this.nestGrid = new System.Windows.Forms.DataGridView();
            this.nestStrip = new System.Windows.Forms.ToolStrip();
            this.clearNestsButton = new System.Windows.Forms.ToolStripButton();
            this.optsTab = new System.Windows.Forms.TabPage();
            this.nestOptionsGrid = new System.Windows.Forms.PropertyGrid();
            this.DrawPanel = new DXFnest.NestGUI.DoubleBufferedPanel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.posLabel = new System.Windows.Forms.ToolStripLabel();
            this.runNestButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.tab.SuspendLayout();
            this.partTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pSplitContainer)).BeginInit();
            this.pSplitContainer.Panel1.SuspendLayout();
            this.pSplitContainer.Panel2.SuspendLayout();
            this.pSplitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.partGrid)).BeginInit();
            this.partStrip.SuspendLayout();
            this.shapeLibTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.shSplitContainer)).BeginInit();
            this.shSplitContainer.Panel1.SuspendLayout();
            this.shSplitContainer.Panel2.SuspendLayout();
            this.shSplitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.scriptGrid)).BeginInit();
            this.scriptStrip.SuspendLayout();
            this.sheetTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sheetGrid)).BeginInit();
            this.sheetStrip.SuspendLayout();
            this.nestTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nestGrid)).BeginInit();
            this.nestStrip.SuspendLayout();
            this.optsTab.SuspendLayout();
            this.toolStrip1.SuspendLayout();
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
            this.splitContainer.Panel2.BackColor = System.Drawing.SystemColors.Window;
            this.splitContainer.Panel2.Controls.Add(this.DrawPanel);
            this.splitContainer.Panel2.Controls.Add(this.toolStrip1);
            this.splitContainer.Size = new System.Drawing.Size(784, 320);
            this.splitContainer.SplitterDistance = 393;
            this.splitContainer.SplitterWidth = 8;
            this.splitContainer.TabIndex = 34;
            this.splitContainer.DoubleClick += new System.EventHandler(this.splitContainer_DoubleClick);
            // 
            // tab
            // 
            this.tab.Controls.Add(this.partTab);
            this.tab.Controls.Add(this.shapeLibTab);
            this.tab.Controls.Add(this.sheetTab);
            this.tab.Controls.Add(this.nestTab);
            this.tab.Controls.Add(this.optsTab);
            this.tab.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tab.Location = new System.Drawing.Point(0, 0);
            this.tab.Margin = new System.Windows.Forms.Padding(0);
            this.tab.Multiline = true;
            this.tab.Name = "tab";
            this.tab.SelectedIndex = 0;
            this.tab.Size = new System.Drawing.Size(393, 320);
            this.tab.TabIndex = 46;
            // 
            // partTab
            // 
            this.partTab.BackColor = System.Drawing.SystemColors.Control;
            this.partTab.Controls.Add(this.pSplitContainer);
            this.partTab.Controls.Add(this.partStrip);
            this.partTab.Location = new System.Drawing.Point(4, 22);
            this.partTab.Margin = new System.Windows.Forms.Padding(0);
            this.partTab.Name = "partTab";
            this.partTab.Size = new System.Drawing.Size(385, 294);
            this.partTab.TabIndex = 0;
            this.partTab.Text = "PARTS";
            // 
            // pSplitContainer
            // 
            this.pSplitContainer.BackColor = System.Drawing.SystemColors.Window;
            this.pSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pSplitContainer.Location = new System.Drawing.Point(0, 24);
            this.pSplitContainer.Name = "pSplitContainer";
            // 
            // pSplitContainer.Panel1
            // 
            this.pSplitContainer.Panel1.BackColor = System.Drawing.SystemColors.Control;
            this.pSplitContainer.Panel1.Controls.Add(this.partGrid);
            // 
            // pSplitContainer.Panel2
            // 
            this.pSplitContainer.Panel2.BackColor = System.Drawing.SystemColors.Control;
            this.pSplitContainer.Panel2.Controls.Add(this.editGrid);
            this.pSplitContainer.Size = new System.Drawing.Size(385, 270);
            this.pSplitContainer.SplitterDistance = 181;
            this.pSplitContainer.SplitterWidth = 8;
            this.pSplitContainer.TabIndex = 53;
            // 
            // partGrid
            // 
            this.partGrid.AllowUserToAddRows = false;
            this.partGrid.AllowUserToDeleteRows = false;
            this.partGrid.AllowUserToResizeRows = false;
            this.partGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.partGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.partGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.partGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.partGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.partGrid.Location = new System.Drawing.Point(0, 0);
            this.partGrid.Margin = new System.Windows.Forms.Padding(0);
            this.partGrid.MultiSelect = false;
            this.partGrid.Name = "partGrid";
            this.partGrid.RowHeadersVisible = false;
            this.partGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.partGrid.Size = new System.Drawing.Size(181, 270);
            this.partGrid.TabIndex = 49;
            // 
            // editGrid
            // 
            this.editGrid.CategorySplitterColor = System.Drawing.Color.Black;
            this.editGrid.DisabledItemForeColor = System.Drawing.Color.Black;
            this.editGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editGrid.HelpVisible = false;
            this.editGrid.LineColor = System.Drawing.Color.White;
            this.editGrid.Location = new System.Drawing.Point(0, 0);
            this.editGrid.Margin = new System.Windows.Forms.Padding(0);
            this.editGrid.Name = "editGrid";
            this.editGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.editGrid.Size = new System.Drawing.Size(196, 270);
            this.editGrid.TabIndex = 56;
            this.editGrid.ToolbarVisible = false;
            this.editGrid.ViewBackColor = System.Drawing.SystemColors.Control;
            // 
            // partStrip
            // 
            this.partStrip.BackColor = System.Drawing.SystemColors.Control;
            this.partStrip.Font = new System.Drawing.Font("Arial", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.partStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.partStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.partStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importPartButton,
            this.removePartButton,
            this.clearPartsButton,
            this.editStripSeparator,
            this.editBevelButton});
            this.partStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.partStrip.Location = new System.Drawing.Point(0, 0);
            this.partStrip.Name = "partStrip";
            this.partStrip.Padding = new System.Windows.Forms.Padding(0);
            this.partStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.partStrip.Size = new System.Drawing.Size(385, 24);
            this.partStrip.TabIndex = 45;
            this.partStrip.Text = "toolStrip1";
            // 
            // importPartButton
            // 
            this.importPartButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.importPartButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.importPartButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.importPartButton.Name = "importPartButton";
            this.importPartButton.Size = new System.Drawing.Size(76, 21);
            this.importPartButton.Text = "Import dxf";
            this.importPartButton.ToolTipText = "Import part";
            // 
            // removePartButton
            // 
            this.removePartButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removePartButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.removePartButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removePartButton.Name = "removePartButton";
            this.removePartButton.Size = new System.Drawing.Size(67, 21);
            this.removePartButton.Text = "Remove";
            this.removePartButton.ToolTipText = "Remove part";
            // 
            // clearPartsButton
            // 
            this.clearPartsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearPartsButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.clearPartsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearPartsButton.Name = "clearPartsButton";
            this.clearPartsButton.Size = new System.Drawing.Size(47, 21);
            this.clearPartsButton.Text = "Clear";
            this.clearPartsButton.ToolTipText = "Clear parts";
            // 
            // editStripSeparator
            // 
            this.editStripSeparator.Name = "editStripSeparator";
            this.editStripSeparator.Size = new System.Drawing.Size(6, 23);
            // 
            // editBevelButton
            // 
            this.editBevelButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.editBevelButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.editBevelButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.editBevelButton.Name = "editBevelButton";
            this.editBevelButton.Size = new System.Drawing.Size(75, 21);
            this.editBevelButton.Text = "Edit bevel";
            this.editBevelButton.ToolTipText = "Edit bevel";
            // 
            // shapeLibTab
            // 
            this.shapeLibTab.Controls.Add(this.shSplitContainer);
            this.shapeLibTab.Controls.Add(this.scriptStrip);
            this.shapeLibTab.Location = new System.Drawing.Point(4, 22);
            this.shapeLibTab.Name = "shapeLibTab";
            this.shapeLibTab.Size = new System.Drawing.Size(385, 294);
            this.shapeLibTab.TabIndex = 5;
            this.shapeLibTab.Text = "LIBRARY";
            this.shapeLibTab.UseVisualStyleBackColor = true;
            // 
            // shSplitContainer
            // 
            this.shSplitContainer.BackColor = System.Drawing.SystemColors.Window;
            this.shSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.shSplitContainer.Location = new System.Drawing.Point(0, 24);
            this.shSplitContainer.Name = "shSplitContainer";
            // 
            // shSplitContainer.Panel1
            // 
            this.shSplitContainer.Panel1.BackColor = System.Drawing.SystemColors.Control;
            this.shSplitContainer.Panel1.Controls.Add(this.scriptGrid);
            // 
            // shSplitContainer.Panel2
            // 
            this.shSplitContainer.Panel2.BackColor = System.Drawing.SystemColors.Control;
            this.shSplitContainer.Panel2.Controls.Add(this.propertyGrid);
            this.shSplitContainer.Size = new System.Drawing.Size(385, 270);
            this.shSplitContainer.SplitterDistance = 166;
            this.shSplitContainer.SplitterWidth = 8;
            this.shSplitContainer.TabIndex = 52;
            // 
            // scriptGrid
            // 
            this.scriptGrid.AllowUserToAddRows = false;
            this.scriptGrid.AllowUserToDeleteRows = false;
            this.scriptGrid.AllowUserToResizeRows = false;
            this.scriptGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.scriptGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.scriptGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.scriptGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.scriptGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.scriptGrid.Location = new System.Drawing.Point(0, 0);
            this.scriptGrid.MultiSelect = false;
            this.scriptGrid.Name = "scriptGrid";
            this.scriptGrid.RowHeadersVisible = false;
            this.scriptGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.scriptGrid.Size = new System.Drawing.Size(166, 270);
            this.scriptGrid.TabIndex = 47;
            // 
            // propertyGrid
            // 
            this.propertyGrid.CategorySplitterColor = System.Drawing.Color.Black;
            this.propertyGrid.DisabledItemForeColor = System.Drawing.Color.Black;
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.HelpVisible = false;
            this.propertyGrid.LineColor = System.Drawing.Color.WhiteSmoke;
            this.propertyGrid.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.propertyGrid.Size = new System.Drawing.Size(211, 270);
            this.propertyGrid.TabIndex = 50;
            this.propertyGrid.ToolbarVisible = false;
            // 
            // scriptStrip
            // 
            this.scriptStrip.BackColor = System.Drawing.SystemColors.Control;
            this.scriptStrip.Font = new System.Drawing.Font("Arial", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.scriptStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.scriptStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.scriptStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.execButton});
            this.scriptStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.scriptStrip.Location = new System.Drawing.Point(0, 0);
            this.scriptStrip.Name = "scriptStrip";
            this.scriptStrip.Padding = new System.Windows.Forms.Padding(0);
            this.scriptStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.scriptStrip.Size = new System.Drawing.Size(385, 24);
            this.scriptStrip.TabIndex = 47;
            this.scriptStrip.Text = "toolStrip4";
            // 
            // execButton
            // 
            this.execButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.execButton.Image = ((System.Drawing.Image)(resources.GetObject("execButton.Image")));
            this.execButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.execButton.Name = "execButton";
            this.execButton.Size = new System.Drawing.Size(90, 21);
            this.execButton.Text = "Add to parts";
            // 
            // sheetTab
            // 
            this.sheetTab.Controls.Add(this.sheetGrid);
            this.sheetTab.Controls.Add(this.sheetStrip);
            this.sheetTab.Location = new System.Drawing.Point(4, 22);
            this.sheetTab.Margin = new System.Windows.Forms.Padding(0);
            this.sheetTab.Name = "sheetTab";
            this.sheetTab.Size = new System.Drawing.Size(385, 294);
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
            this.sheetGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.sheetGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.sheetGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.sheetGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sheetGrid.Location = new System.Drawing.Point(0, 24);
            this.sheetGrid.Margin = new System.Windows.Forms.Padding(0);
            this.sheetGrid.MultiSelect = false;
            this.sheetGrid.Name = "sheetGrid";
            this.sheetGrid.RowHeadersVisible = false;
            this.sheetGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.sheetGrid.Size = new System.Drawing.Size(385, 270);
            this.sheetGrid.TabIndex = 47;
            // 
            // sheetStrip
            // 
            this.sheetStrip.BackColor = System.Drawing.SystemColors.Control;
            this.sheetStrip.Font = new System.Drawing.Font("Arial", 11.25F);
            this.sheetStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.sheetStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.sheetStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addSheetButton,
            this.importSheetButton,
            this.removeSheetButton,
            this.clearSheetsButton,
            this.toolStripSeparator3,
            this.loadNestButton});
            this.sheetStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.sheetStrip.Location = new System.Drawing.Point(0, 0);
            this.sheetStrip.Name = "sheetStrip";
            this.sheetStrip.Padding = new System.Windows.Forms.Padding(0);
            this.sheetStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.sheetStrip.Size = new System.Drawing.Size(385, 24);
            this.sheetStrip.TabIndex = 46;
            this.sheetStrip.Text = "toolStrip2";
            // 
            // addSheetButton
            // 
            this.addSheetButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addSheetButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.addSheetButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addSheetButton.Name = "addSheetButton";
            this.addSheetButton.Size = new System.Drawing.Size(37, 21);
            this.addSheetButton.Text = "Add";
            this.addSheetButton.ToolTipText = "Add sheet";
            // 
            // importSheetButton
            // 
            this.importSheetButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.importSheetButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.importSheetButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.importSheetButton.Name = "importSheetButton";
            this.importSheetButton.Size = new System.Drawing.Size(76, 21);
            this.importSheetButton.Text = "Import dxf";
            this.importSheetButton.ToolTipText = "Import sheet";
            // 
            // removeSheetButton
            // 
            this.removeSheetButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removeSheetButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.removeSheetButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removeSheetButton.Name = "removeSheetButton";
            this.removeSheetButton.Size = new System.Drawing.Size(67, 21);
            this.removeSheetButton.Text = "Remove";
            this.removeSheetButton.ToolTipText = "Remove sheet";
            // 
            // clearSheetsButton
            // 
            this.clearSheetsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearSheetsButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.clearSheetsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearSheetsButton.Name = "clearSheetsButton";
            this.clearSheetsButton.Size = new System.Drawing.Size(47, 21);
            this.clearSheetsButton.Text = "Clear";
            this.clearSheetsButton.ToolTipText = "Clear sheets";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 23);
            // 
            // loadNestButton
            // 
            this.loadNestButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.loadNestButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.loadNestButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.loadNestButton.Name = "loadNestButton";
            this.loadNestButton.Size = new System.Drawing.Size(118, 21);
            this.loadNestButton.Text = "Load nesting dxf";
            this.loadNestButton.ToolTipText = "Load nesting";
            // 
            // nestTab
            // 
            this.nestTab.Controls.Add(this.nestGrid);
            this.nestTab.Controls.Add(this.nestStrip);
            this.nestTab.Location = new System.Drawing.Point(4, 22);
            this.nestTab.Margin = new System.Windows.Forms.Padding(0);
            this.nestTab.Name = "nestTab";
            this.nestTab.Size = new System.Drawing.Size(385, 294);
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
            this.nestGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.nestGrid.BackgroundColor = System.Drawing.SystemColors.Control;
            this.nestGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.nestGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nestGrid.Location = new System.Drawing.Point(0, 24);
            this.nestGrid.Margin = new System.Windows.Forms.Padding(0);
            this.nestGrid.MultiSelect = false;
            this.nestGrid.Name = "nestGrid";
            this.nestGrid.RowHeadersVisible = false;
            this.nestGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.nestGrid.Size = new System.Drawing.Size(385, 270);
            this.nestGrid.TabIndex = 51;
            // 
            // nestStrip
            // 
            this.nestStrip.BackColor = System.Drawing.SystemColors.Control;
            this.nestStrip.Font = new System.Drawing.Font("Arial", 11.25F);
            this.nestStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.nestStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.nestStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clearNestsButton});
            this.nestStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.nestStrip.Location = new System.Drawing.Point(0, 0);
            this.nestStrip.Name = "nestStrip";
            this.nestStrip.Padding = new System.Windows.Forms.Padding(0);
            this.nestStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.nestStrip.Size = new System.Drawing.Size(385, 24);
            this.nestStrip.TabIndex = 50;
            this.nestStrip.Text = "toolStrip3";
            // 
            // clearNestsButton
            // 
            this.clearNestsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearNestsButton.Font = new System.Drawing.Font("Arial", 11.25F);
            this.clearNestsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearNestsButton.Name = "clearNestsButton";
            this.clearNestsButton.Size = new System.Drawing.Size(47, 21);
            this.clearNestsButton.Text = "Clear";
            this.clearNestsButton.ToolTipText = "Clear nestings";
            // 
            // optsTab
            // 
            this.optsTab.Controls.Add(this.nestOptionsGrid);
            this.optsTab.Location = new System.Drawing.Point(4, 22);
            this.optsTab.Name = "optsTab";
            this.optsTab.Size = new System.Drawing.Size(385, 294);
            this.optsTab.TabIndex = 4;
            this.optsTab.Text = "OPTIONS";
            this.optsTab.UseVisualStyleBackColor = true;
            // 
            // nestOptionsGrid
            // 
            this.nestOptionsGrid.CategorySplitterColor = System.Drawing.Color.Black;
            this.nestOptionsGrid.DisabledItemForeColor = System.Drawing.Color.Black;
            this.nestOptionsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nestOptionsGrid.HelpVisible = false;
            this.nestOptionsGrid.LineColor = System.Drawing.Color.White;
            this.nestOptionsGrid.Location = new System.Drawing.Point(0, 0);
            this.nestOptionsGrid.Margin = new System.Windows.Forms.Padding(0);
            this.nestOptionsGrid.Name = "nestOptionsGrid";
            this.nestOptionsGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.nestOptionsGrid.Size = new System.Drawing.Size(385, 294);
            this.nestOptionsGrid.TabIndex = 56;
            this.nestOptionsGrid.ToolbarVisible = false;
            this.nestOptionsGrid.ViewBackColor = System.Drawing.SystemColors.Control;
            // 
            // DrawPanel
            // 
            this.DrawPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DrawPanel.Location = new System.Drawing.Point(0, 0);
            this.DrawPanel.Name = "DrawPanel";
            this.DrawPanel.Size = new System.Drawing.Size(383, 300);
            this.DrawPanel.TabIndex = 47;
            // 
            // toolStrip1
            // 
            this.toolStrip1.BackColor = System.Drawing.SystemColors.Control;
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolStrip1.Font = new System.Drawing.Font("Arial", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.posLabel});
            this.toolStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.toolStrip1.Location = new System.Drawing.Point(0, 300);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0);
            this.toolStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip1.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.toolStrip1.Size = new System.Drawing.Size(383, 20);
            this.toolStrip1.TabIndex = 46;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // posLabel
            // 
            this.posLabel.Name = "posLabel";
            this.posLabel.Size = new System.Drawing.Size(140, 17);
            this.posLabel.Text = "X=0.0000 Y=0.0000 ";
            // 
            // runNestButton
            // 
            this.runNestButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.runNestButton.Location = new System.Drawing.Point(12, 326);
            this.runNestButton.Name = "runNestButton";
            this.runNestButton.Size = new System.Drawing.Size(100, 23);
            this.runNestButton.TabIndex = 47;
            this.runNestButton.Text = "Run Nesting";
            this.runNestButton.UseVisualStyleBackColor = true;
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.exportButton.Location = new System.Drawing.Point(118, 326);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(100, 23);
            this.exportButton.TabIndex = 48;
            this.exportButton.Text = "Export DXF";
            this.exportButton.UseVisualStyleBackColor = true;
            // 
            // NestGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(784, 361);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.runNestButton);
            this.Controls.Add(this.splitContainer);
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
            this.pSplitContainer.Panel1.ResumeLayout(false);
            this.pSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pSplitContainer)).EndInit();
            this.pSplitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.partGrid)).EndInit();
            this.partStrip.ResumeLayout(false);
            this.partStrip.PerformLayout();
            this.shapeLibTab.ResumeLayout(false);
            this.shapeLibTab.PerformLayout();
            this.shSplitContainer.Panel1.ResumeLayout(false);
            this.shSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.shSplitContainer)).EndInit();
            this.shSplitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.scriptGrid)).EndInit();
            this.scriptStrip.ResumeLayout(false);
            this.scriptStrip.PerformLayout();
            this.sheetTab.ResumeLayout(false);
            this.sheetTab.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sheetGrid)).EndInit();
            this.sheetStrip.ResumeLayout(false);
            this.sheetStrip.PerformLayout();
            this.nestTab.ResumeLayout(false);
            this.nestTab.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nestGrid)).EndInit();
            this.nestStrip.ResumeLayout(false);
            this.nestStrip.PerformLayout();
            this.optsTab.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        public System.Windows.Forms.SplitContainer splitContainer;
        public System.Windows.Forms.Button runNestButton;
        public System.Windows.Forms.Button exportButton;
        public System.Windows.Forms.TabControl tab;
        private System.Windows.Forms.TabPage partTab;
        public System.Windows.Forms.ToolStrip partStrip;
        public System.Windows.Forms.ToolStripButton importPartButton;
        public System.Windows.Forms.ToolStripButton removePartButton;
        public System.Windows.Forms.ToolStripButton clearPartsButton;
        private System.Windows.Forms.TabPage sheetTab;
        public System.Windows.Forms.DataGridView sheetGrid;
        public System.Windows.Forms.ToolStrip sheetStrip;
        public System.Windows.Forms.ToolStripButton addSheetButton;
        public System.Windows.Forms.ToolStripButton importSheetButton;
        public System.Windows.Forms.ToolStripButton removeSheetButton;
        public System.Windows.Forms.ToolStripButton clearSheetsButton;
        public System.Windows.Forms.ToolStripButton loadNestButton;
        private System.Windows.Forms.TabPage nestTab;
        public System.Windows.Forms.DataGridView nestGrid;
        public System.Windows.Forms.ToolStrip nestStrip;
        public System.Windows.Forms.ToolStripButton clearNestsButton;
        private System.Windows.Forms.TabPage optsTab;
        public System.Windows.Forms.PropertyGrid nestOptionsGrid;
        private System.Windows.Forms.TabPage shapeLibTab;
        public System.Windows.Forms.ToolStrip scriptStrip;
        public System.Windows.Forms.ToolStripButton execButton;
        public System.Windows.Forms.SplitContainer shSplitContainer;
        public System.Windows.Forms.DataGridView scriptGrid;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        public System.Windows.Forms.ToolStripButton editBevelButton;
        private System.Windows.Forms.ToolStripSeparator editStripSeparator;
        public System.Windows.Forms.SplitContainer pSplitContainer;
        public System.Windows.Forms.DataGridView partGrid;
        public System.Windows.Forms.PropertyGrid editGrid;
        public System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel posLabel;
        private DoubleBufferedPanel DrawPanel;
    }
}