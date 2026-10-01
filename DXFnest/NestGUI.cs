using ACadSharp;
using DeepNestLib;
using DXFnest;
using Microsoft.CSharp;
using NCnetic;
using NCnetic.Cam;
using NCnetic.Nest;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

using static NCnetic.Cam.CAD;
//using static NCnetic.Cam.CAM;
using static System.Net.Mime.MediaTypeNames;

namespace DXFnest
{
    public partial class NestGUI : Form
    {
        string NestOptsFilePath = "";
        string NestLayerOptsFilePath = "";
        string NestScriptFilePath = "";

        public BindingList<ScriptItem> scriptItems = new BindingList<ScriptItem>();

        private Assembly _assembly;
        private object _scriptInstance;
        private Type _scriptType;
        private Type _paramType;
        private object _paramInstance;

        public MouseButtons PAN_BUTTON = MouseButtons.Middle;
        public bool INVERT_WHEEL = false;
        
        public List<string> FilesToProcess = new List<string>();
        public Nesting Result = new Nesting();

        Object currentObj = null;

        static NestEngine Engine = new NestEngine();
        bool ToRestart = false;
        //bool BevelEdit = false;
        EditFunctions EditFct = EditFunctions.NONE;
        EditSelection EditSel = new EditSelection();
        EditBevelOptions EditBevelOpts = new EditBevelOptions();

        public enum EditFunctions
        {
            NONE,
            BEVEL,
        }

        public class Nesting
        {
            public double SheetMinX = 0.0;
            public double SheetMinY = 0.0;
            public double SheetMaxX = 0.0;
            public double SheetMaxY = 0.0;

            public double Thickness = 0.0;

            public List<List<Feature>> Parts = new List<List<Feature>>();
            public List<Feature> Sequence = new List<Feature>();
        }

        #region main

        public NestGUI(List<string> files = null)
        {
            string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string sPath = Path.Combine(AppDataPath, "NCnetic\\");
            Directory.CreateDirectory(sPath);
            NestOptsFilePath = sPath + "NestOptions.xml";
            NestScriptFilePath = sPath + "NestScript.xml";
            NestLayerOptsFilePath = sPath + "NestLayer.xml";

            string exePath = Assembly.GetExecutingAssembly().Location;
            this.Icon = Icon.ExtractAssociatedIcon(exePath);

            InitializeComponent();
            partStrip.Renderer = new CustomSystemRenderer();
            scriptStrip.Renderer = new CustomSystemRenderer();
            sheetStrip.Renderer = new CustomSystemRenderer();
            nestStrip.Renderer = new CustomSystemRenderer();

            splitContainer.SplitterWidth = 8;
            shSplitContainer.SplitterWidth = 8;
            pSplitContainer.SplitterWidth = 8;
            pSplitContainer.Panel2Collapsed = true;

            splitContainer.Paint += new PaintEventHandler(SplitContainerExtensions.Paint);
            splitContainer.SplitterMoved += new SplitterEventHandler(SplitContainerExtensions.SplitterMoved);
            shSplitContainer.Paint += new PaintEventHandler(SplitContainerExtensions.Paint);
            shSplitContainer.SplitterMoved += new SplitterEventHandler(SplitContainerExtensions.SplitterMoved);
            pSplitContainer.Paint += new PaintEventHandler(SplitContainerExtensions.Paint);
            pSplitContainer.SplitterMoved += new SplitterEventHandler(SplitContainerExtensions.SplitterMoved);

            // IN TEST
            //editBevelButton.Visible = false;
            //editStripSeparator.Visible = false;

            Engine.Opts = new Options();
            DerializeLayerOptions();
            DeserializeOptions();
            CadOptionsPropertyUpdateAttributes(Engine.Opts);
            nestOptionsGrid.SelectedObject = Engine.Opts;
            nestOptionsGrid.CollapseAllGridItems();

            sheetGrid.DataSource = Engine.SheetItems;
            partGrid.DataSource = Engine.PartItems;
            nestGrid.DataSource = Engine.NestItems;

            //EditBevelOptionsPropertyUpdateAttributes(EditBevelOpts);
            //editGrid.SelectedObject = EditBevelOpts;
            editGrid.SelectedObject = null;

            IniEventsForm();

            Engine.SheetItems.Add(new SheetItem
            {
                UsedQty = 0,
                IniQty = Engine.Opts.DefaultQty,
                LX = Engine.Opts.DefaultWidth,
                LY = Engine.Opts.DefaultHeight,
            });

            IniScripts();
            
            this.HandleCreated += new EventHandler((s, ea) =>
            {
                BeginInvoke((MethodInvoker)delegate ()
                {
                    if (files != null)
                    {
                        try
                        {
                            UpdateLayersDialog(files);
                            Engine.LoadDxfs(files, NestEngine.LoadType.PART);
                            partGrid.Invalidate();
                        }
                        catch { }
                    }
                });
            });

            Engine.RestartNest();
            DrawPanel.Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (FilesToProcess.Any())
            {
                LoadDxfs(FilesToProcess);
                FilesToProcess.Clear();
            }
        }

        public void LoadDxfs(List<string> files)
        {
            tab.SelectedTab = partTab;
            try
            {
                UpdateLayersDialog(files);
                Engine.LoadDxfs(files, NestEngine.LoadType.PART);
                partGrid.Invalidate();
            }
            catch { }

            zoom = 1f;
            viewOffsetX = 0f;
            viewOffsetY = 0f;
            DrawPanel.Invalidate();
        }

        public void LoadDoc(CadDocument doc)
        {
            if (doc == null) return;

            tab.SelectedTab = partTab;
            Engine.LoadDoc(doc);

            zoom = 1f;
            viewOffsetX = 0f;
            viewOffsetY = 0f;
            DrawPanel.Invalidate();
        }

        public void UpdateLayersDialog(List<string> files)
        {
            BindingList<LayerItem> layers = Engine.GetDxfsLayers(files);
            if (layers.Count > 1)
            {
                Form layerForm = new Form()
                {
                    FormBorderStyle = FormBorderStyle.None,
                    Padding = new Padding(1),
                    StartPosition = FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    AutoScaleMode = AutoScaleMode.Dpi,
                };
                layerForm.Paint += (ss, sea) =>
                {
                    using (Pen pen = new Pen(System.Drawing.Color.Black, 1))
                    {
                        sea.Graphics.DrawRectangle(pen, 0, 0, layerForm.ClientSize.Width - 1, layerForm.ClientSize.Height - 1);
                    }
                };

                Button button = new Button
                {
                    Text = "Import",
                    Dock = DockStyle.Bottom,
                    //Height = 24,
                };
                button.Click += new EventHandler((ss, sea) =>
                {
                    layerForm.Close();
                    Engine.UpdateDxfsLayers(layers);
                });

                DataGridView grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    //Height = layerForm.Height - button.Height,
                    BackgroundColor = SystemColors.Control,
                    ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    RowHeadersVisible = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                };

                layerForm.Controls.Add(button);
                layerForm.Controls.Add(grid);

                layerForm.Load += (s, ea) => {
                    // FIX SCALING
                    layerForm.SuspendLayout();
                    foreach (Control ctrl in layerForm.Controls)
                    {
                        ctrl.Font = SystemFonts.MessageBoxFont;
                    }
                    button.Height = button.Font.Height + (int)(button.Font.Height * 0.4);
                    layerForm.Height = 9 * button.Height;
                    layerForm.Width = 15 * button.Height;
                    layerForm.Location = new System.Drawing.Point(
                        this.Width / 2 - layerForm.Width / 2 + this.Location.X,
                        this.Height / 2 - layerForm.Height / 2 + this.Location.Y
                    );
                    layerForm.ResumeLayout(true);
                };

                grid.DataSource = layers;
                ReplaceEnumColumnsWithComboBoxes(grid, layers);

                layerForm.ShowDialog(this);
            }
        }

        public void ComputeNestResult()
        {
            if (currentObj == null) return;

            //Nesting Result = new Nesting();

            if (currentObj.GetType() == typeof(NestItem))
            {
                NestItem nest = (NestItem)currentObj;
                Result.SheetMaxX = Engine.SheetItems[nest.SheetSource].LX;
                Result.SheetMaxY = Engine.SheetItems[nest.SheetSource].LY;

                Result.Parts = nest.NestData;

                foreach (SheetAssociation sa in Engine.SheetItems[nest.SheetSource].Associated)
                {
                    Result.Parts.Add(RotoTranslatePartXY(Engine.PartItems[sa.partId].Features, sa.T.X, sa.T.Y, sa.T.Rot));
                }
            }
            else if (currentObj.GetType() == typeof(SheetItem))
            {
                SheetItem sheet = (SheetItem)currentObj;
                Result.SheetMaxX = sheet.LX;
                Result.SheetMaxY = sheet.LY;

                Result.Parts = new List<List<Feature>>();

                foreach (SheetAssociation sa in sheet.Associated)
                {
                    Result.Parts.Add(RotoTranslatePartXY(Engine.PartItems[sa.partId].Features, sa.T.X, sa.T.Y, sa.T.Rot));
                }
            }
            else if (currentObj.GetType() == typeof(PartItem))
            {
                PartItem part = (PartItem)currentObj;
                Result.Parts = new List<List<Feature>> { part.Features };
            }
            else if (currentObj.GetType() == typeof(ScriptItem))
            {
                ScriptItem script = (ScriptItem)currentObj;
                Result.Parts = script.Parts;
            }
        }

        private void IniEventsForm()
        {
            this.Resize += new EventHandler((s, ea) => { splitContainer.Invalidate(); });

            this.FormClosing += new FormClosingEventHandler((s, ea) =>
            {
                if (EditFct != EditFunctions.NONE) EndEdit();

                SerializeLayerOptions();
                SerializeOptions();
            });

            this.Load += (s, ea) => {
                // FIX SCALING
                this.SuspendLayout();
                foreach (Control ctrl in this.Controls)
                {
                    ctrl.Font = SystemFonts.MessageBoxFont;
                }
                tab.ItemSize = new Size(tab.ItemSize.Width, tab.Font.Height + (int)(tab.Font.Height * 0.4));
                this.ResumeLayout(true);
            };

            DrawPanel.Paint += new PaintEventHandler((s, e) =>
            {
                try
                {
                    Draw(e);
                }
                catch { }
            });

            DrawPanel.MouseEnter += (s, e) => DrawPanel.Focus();

            DrawPanel.MouseWheel += new MouseEventHandler((s, ea) =>
            {
                float oldZoom = zoom;
                if (ea.Delta > 0)
                {
                    if (INVERT_WHEEL)
                    {
                        zoom *= 1.2f;
                    }
                    else
                    {
                        zoom /= 1.2f;
                    }
                }
                else
                {
                    if (INVERT_WHEEL)
                    {
                        zoom /= 1.2f;
                    }
                    else
                    {
                        zoom *= 1.2f;
                    }
                }
                zoom = Math.Max(0.01f, Math.Min(zoom, 100f));
                float zoomFactor = zoom / oldZoom;
                viewOffsetX = ea.X - (ea.X - viewOffsetX) * zoomFactor;
                viewOffsetY = ea.Y - (ea.Y - viewOffsetY) * zoomFactor;

                DrawPanel.Invalidate();
            });

            DrawPanel.MouseDoubleClick += new MouseEventHandler((s, ea) =>
            {
                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            });

            DrawPanel.MouseDown += new MouseEventHandler((s, ea) =>
            {
                if (ea.Button == PAN_BUTTON)
                {
                    isPanning = true;
                    panStartMouseX = ea.Location.X;
                    panStartMouseY = ea.Location.Y;
                    panStartOffsetX = viewOffsetX;
                    panStartOffsetY = viewOffsetY;
                }
                else if (ea.Button == MouseButtons.Left)
                {
                    if (EditSel.EditPartIndex > -1 && EditSel.EditFeatureIndex > -1)
                    {
                        PartItem p = Engine.PartItems[EditSel.EditPartIndex];
                        Feature f = p.Features[EditSel.EditFeatureIndex];
                        if (EditSel.EditEdgeIndex > -1 && EditBevelOpts.Mode == EditBevelOptions.EditMode.ELEMENT)
                        {
                            f.Edges[EditSel.EditEdgeIndex].Bevel = new BevelDefinition();
                            BevelDefinition def = f.Edges[EditSel.EditEdgeIndex].Bevel;
                            def.Type = EditBevelOpts.Type;
                            def.A = EditBevelOpts.A;
                            def.Hr = EditBevelOpts.Hr / 100.0;
                            def.B = EditBevelOpts.B;
                            def.r = EditBevelOpts.r / 100.0;
                        }
                        else
                        {
                            for (int i = 0; i < f.Edges.Count; i++)
                            {
                                f.Edges[i].Bevel = new BevelDefinition();
                                BevelDefinition def = f.Edges[i].Bevel;
                                def.Type = EditBevelOpts.Type;
                                def.A = EditBevelOpts.A;
                                def.Hr = EditBevelOpts.Hr / 100.0;
                                def.B = EditBevelOpts.B;
                                def.r = EditBevelOpts.r / 100.0;
                            }
                        }

                        Engine.UpdateNestResults();

                        sheetGrid.Invalidate();
                        partGrid.Invalidate();
                        nestGrid.Invalidate();

                        DrawPanel.Invalidate();
                    }
                }
            });

            DrawPanel.MouseUp += new MouseEventHandler((s, ea) =>
            {
                if (ea.Button == PAN_BUTTON)
                {
                    isPanning = false;
                }
            });

            DrawPanel.MouseMove += new MouseEventHandler((s, ea) =>
            {
                if (isPanning)
                {
                    float dx = ea.X - panStartMouseX;
                    float dy = ea.Y - panStartMouseY;
                    viewOffsetX = panStartOffsetX + dx;
                    viewOffsetY = panStartOffsetY + dy;
                    DrawPanel.Invalidate();
                }
                else
                {
                    if (currentObj == null)
                    {
                        posLabel.Text = "X=0.0000 Y=0.0000";
                        return;
                    }

                    float offsetX = (DrawPanel.Width - (maxX - minX) * scale / zoom) / 2f;
                    float offsetY = (DrawPanel.Height - (maxY - minY) * scale / zoom) / 2f;
                    offsetX = viewOffsetX + (offsetX * zoom);
                    offsetY = viewOffsetY + (offsetY * zoom);
                    float modelX = (ea.X - offsetX) / scale + minX;
                    float modelY = -(ea.Y - offsetY) / scale + maxY;

                    posLabel.Text = "X=" + modelX.ToString("#.0000") + " Y=" + modelY.ToString("#.0000");

                    if (EditFct != EditFunctions.NONE)
                    {
                        if (EditSel.EditPartIndex > -1)
                        {
                            //float tol = 5f * zoom / scale;
                            float tol = 5f / scale;

                            for (int i = 0; i < Engine.PartItems[EditSel.EditPartIndex].Features.Count; i++)
                            {
                                Feature f = Engine.PartItems[EditSel.EditPartIndex].Features[i];

                                if (f.Type != Feature.FeatureType.NO_CUT)
                                {
                                    int id = -1;
                                    float pos = 0f;

                                    if (SelectEdge(f, ref id, ref pos, modelX, modelY, tol))
                                    {
                                        if (i != EditSel.EditFeatureIndex || id != EditSel.EditEdgeIndex)
                                        {
                                            EditSel.EditFeatureIndex = i;
                                            EditSel.EditEdgeIndex = id;
                                            EditSel.EditEdgePos = pos;
                                            DrawPanel.Invalidate();
                                        }
                                        return;
                                    }
                                }
                            }

                            if (EditSel.EditFeatureIndex != -1 || EditSel.EditEdgeIndex != -1)
                            {
                                EditSel.EditFeatureIndex = -1;
                                EditSel.EditEdgeIndex = -1;
                                DrawPanel.Invalidate();
                            }
                        }
                    }
                }
            });

            importPartButton.Click += new EventHandler((s, ea) =>
            {
                if (EditFct != EditFunctions.NONE) EndEdit();

                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Multiselect = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    tab.SelectedTab = partTab;
                    try
                    {
                        UpdateLayersDialog(dialog.FileNames.ToList());
                        Engine.LoadDxfs(dialog.FileNames.ToList(), NestEngine.LoadType.PART);
                        partGrid.Invalidate();
                    }
                    catch { }

                    zoom = 1f;
                    viewOffsetX = 0f;
                    viewOffsetY = 0f;
                    DrawPanel.Invalidate();
                }
            });

            removePartButton.Click += new EventHandler((s, ea) =>
            {
                if (EditFct != EditFunctions.NONE) EndEdit();

                if (partGrid.SelectedRows.Count > 0)
                {
                    int index = partGrid.SelectedRows[0].Index;
                    if (Engine.PartItems[index].SheetId == -1)
                    {
                        foreach (SheetItem si in Engine.SheetItems)
                        {
                            foreach (SheetAssociation sa in si.Associated)
                            {
                                if (sa.partId > index) sa.partId--;
                            }
                        }
                        Engine.PartItems.RemoveAt(index);
                        if (!Engine.PartItems.Any())
                        {
                            currentObj = null;
                        }
                        else
                        {
                            if (partGrid.SelectedRows.Count == 0)
                            {
                                partGrid.Rows[partGrid.Rows.Count - 1].Selected = true;
                            }
                        }

                        Engine.RestartNest();

                        zoom = 1f;
                        viewOffsetX = 0f;
                        viewOffsetY = 0f;
                        DrawPanel.Invalidate();
                    }
                }
            });

            clearPartsButton.Click += new EventHandler((s, ea) =>
            {
                if (EditFct != EditFunctions.NONE) EndEdit();

                for (int i = 0; i < Engine.PartItems.Count; i++)
                {
                    if (Engine.PartItems[i].SheetId == -1)
                    {
                        foreach (SheetItem si in Engine.SheetItems)
                        {
                            foreach (SheetAssociation sa in si.Associated)
                            {
                                if (sa.partId > i) sa.partId--;
                            }
                        }
                        Engine.PartItems.RemoveAt(i);
                        i--;
                    }
                }
                if (!Engine.PartItems.Any())
                {
                    currentObj = null;
                }
                else
                {
                    if (partGrid.SelectedRows.Count == 0)
                    {
                        partGrid.Rows[partGrid.Rows.Count - 1].Selected = true;
                    }
                }
                Engine.RestartNest();
                partGrid.Invalidate();
                DrawPanel.Invalidate();
            });

            editBevelButton.Click += new EventHandler((s, ea) =>
            {
                if (partGrid.SelectedRows.Count > 0)
                {
                    if (EditFct == EditFunctions.BEVEL)
                    {
                        EndEdit();
                    }
                    else
                    {
                        EditFct = EditFunctions.BEVEL;

                        pSplitContainer.Panel1Collapsed = true;
                        pSplitContainer.Panel2Collapsed = false;
                        DrawPanel.Cursor = Cursors.Cross;

                        EditSel.Reset();
                        EditSel.EditPartIndex = partGrid.SelectedRows[0].Index;
                        EditBevelOptionsPropertyUpdateAttributes(EditBevelOpts);
                        editGrid.SelectedObject = EditBevelOpts;
                    }

                    DrawPanel.Invalidate();
                }
            });

            editGrid.PropertyValueChanged += new PropertyValueChangedEventHandler((s, ea) =>
            {
                if (EditFct == EditFunctions.BEVEL)
                {
                    EditBevelOptionsPropertyUpdateAttributes(EditBevelOpts);
                    editGrid.SelectedObject = EditBevelOpts;
                }
            });

            tab.Selecting += new TabControlCancelEventHandler((s, ea) =>
            {
                if (EditSel.EditPartIndex > -1)
                {
                    if (EditFct != EditFunctions.NONE) EndEdit();
                }
            });

            addSheetButton.Click += new EventHandler((s, ea) =>
            {
                Engine.SheetItems.Add(new SheetItem
                {
                    UsedQty = 0,
                    IniQty = Engine.Opts.DefaultQty,
                    LX = Engine.Opts.DefaultWidth,
                    LY = Engine.Opts.DefaultHeight,
                });

                Engine.RestartNest();

                //currentObj = sheetGridItems[sheetGridItems.Count - 1];
                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            });

            importSheetButton.Click += new EventHandler((s, ea) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Multiselect = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    tab.SelectedTab = sheetTab;
                    try
                    {
                        UpdateLayersDialog(dialog.FileNames.ToList());
                        Engine.LoadDxfs(dialog.FileNames.ToList(), NestEngine.LoadType.SHEET);
                        sheetGrid.Invalidate();
                    }
                    catch { }

                    zoom = 1f;
                    viewOffsetX = 0f;
                    viewOffsetY = 0f;
                    DrawPanel.Invalidate();
                }
            });

            removeSheetButton.Click += new EventHandler((s, ea) =>
            {
                if (sheetGrid.SelectedRows.Count > 0)
                {
                    int index = sheetGrid.SelectedRows[0].Index;
                    for (int j = 0; j < Engine.PartItems.Count; j++)
                    {
                        if (Engine.PartItems[j].SheetId == index)
                        {
                            foreach (SheetItem si in Engine.SheetItems)
                            {
                                foreach (SheetAssociation sa in si.Associated)
                                {
                                    if (sa.partId > j) sa.partId--;
                                }
                            }
                            Engine.PartItems.RemoveAt(j);
                            j--;
                        }
                        else if (Engine.PartItems[j].SheetId > index)
                        {
                            Engine.PartItems[j].SheetId--;
                        }
                    }
                    Engine.SheetItems.RemoveAt(index);
                    if (!Engine.SheetItems.Any())
                    {
                        currentObj = null;
                    }
                    else
                    {
                        if (sheetGrid.SelectedRows.Count == 0)
                        {
                            sheetGrid.Rows[sheetGrid.Rows.Count - 1].Selected = true;
                        }
                    }

                    Engine.RestartNest();

                    zoom = 1f;
                    viewOffsetX = 0f;
                    viewOffsetY = 0f;
                    DrawPanel.Invalidate();
                }
            });

            clearSheetsButton.Click += new EventHandler((s, ea) =>
            {
                for (int i = 0; i < Engine.SheetItems.Count; i++)
                {
                    for (int j = 0; j < Engine.PartItems.Count; j++)
                    {
                        if (Engine.PartItems[j].SheetId == i)
                        {
                            foreach (SheetItem si in Engine.SheetItems)
                            {
                                foreach (SheetAssociation sa in si.Associated)
                                {
                                    if (sa.partId > j) sa.partId--;
                                }
                            }
                            Engine.PartItems.RemoveAt(j);
                            j--;
                        }
                        else if (Engine.PartItems[j].SheetId > i)
                        {
                            Engine.PartItems[j].SheetId--;
                        }
                    }
                    Engine.SheetItems.RemoveAt(i);
                    i--;
                }
                currentObj = null;

                Engine.RestartNest();

                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            });

            loadNestButton.Click += new EventHandler((s, ea) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Multiselect = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        UpdateLayersDialog(dialog.FileNames.ToList());
                        Engine.LoadDxfs(dialog.FileNames.ToList(), NestEngine.LoadType.NEST);
                        sheetGrid.Invalidate();
                        partGrid.Invalidate();
                    }
                    catch { }

                    zoom = 1f;
                    viewOffsetX = 0f;
                    viewOffsetY = 0f;
                    DrawPanel.Invalidate();

                    if (sheetGrid.Rows.Count > 0)
                    {
                        sheetGrid.Rows[sheetGrid.Rows.Count - 1].Selected = true;
                        sheetGrid.CurrentCell = sheetGrid.Rows[sheetGrid.Rows.Count - 1].Cells[0];
                    }
                }
            });

            clearNestsButton.Click += new EventHandler((s, ea) =>
            {
                Engine.RestartNest();
                currentObj = null;

                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            });

            partGrid.SelectionChanged += (s, ea) =>
            {
                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            };

            sheetGrid.SelectionChanged += (s, ea) =>
            {
                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            };

            nestGrid.SelectionChanged += (s, ea) =>
            {
                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            };

            tab.SelectedIndexChanged += (s, e) =>
            {
                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            };

            sheetGrid.CellValidating += new DataGridViewCellValidatingEventHandler((s, ea) =>
            {
                var grid = (DataGridView)s;
                var cell = grid.Rows[ea.RowIndex].Cells[ea.ColumnIndex];
                var column = grid.Columns[ea.ColumnIndex];
                if (column.Name == "IniQty")
                {
                    if (!Int32.TryParse(ea.FormattedValue.ToString(), out int value) || value <= 0)
                    {
                        ea.Cancel = true;
                    }
                    //if (Engine.SheetItems[ea.RowIndex].Associated.Any())
                    //{
                    //    ea.Cancel = true;
                    //}
                }
                else if (column.Name == "LX" || column.Name == "LY")
                {
                    if (!double.TryParse(ea.FormattedValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
                    {
                        ea.Cancel = true;
                    }
                }
            });

            partGrid.CellValidating += new DataGridViewCellValidatingEventHandler((s, ea) =>
            {
                var grid = (DataGridView)s;
                var cell = grid.Rows[ea.RowIndex].Cells[ea.ColumnIndex];
                var column = grid.Columns[ea.ColumnIndex];
                if (column.Name == "IniQty")
                {
                    if (!Int32.TryParse(ea.FormattedValue.ToString(), out int value) || value <= 0)
                    {
                        ea.Cancel = true;
                    }
                    //if (Engine.PartItems[ea.RowIndex].SheetId > -1)
                    //{
                    //    //ea.Cancel = true;
                    //}
                }
            });

            partGrid.CellValueChanged += new DataGridViewCellEventHandler((s, ea) =>
            {
                ToRestart = true;
            });

            sheetGrid.CellValueChanged += new DataGridViewCellEventHandler((s, ea) =>
            {
                ToRestart = true;
                DrawPanel.Invalidate(); // LX / LY CHANGE
            });

            runNestButton.Click += (s, ea) =>
            {
                if (EditFct != EditFunctions.NONE) EndEdit();

                if (!Engine.PartItems.Any()) return;
                if (!Engine.SheetItems.Any()) return;

                if (ToRestart)
                {
                    Engine.RestartNest();
                    Engine.UpdateNestResults();

                    sheetGrid.Invalidate();
                    partGrid.Invalidate();
                    nestGrid.Invalidate();

                    ToRestart = false;
                }

                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                tab.SelectedTab = nestTab;

                Form progressForm = new Form()
                {
                    //Width = 320,
                    //Height = 60,
                    FormBorderStyle = FormBorderStyle.None,
                    Padding = new Padding(1),
                    StartPosition = FormStartPosition.CenterParent,
                    ShowInTaskbar = false,
                    AutoScaleMode = AutoScaleMode.Dpi,
                };
                progressForm.Paint += (ss, sea) =>
                {
                    using (Pen pen = new Pen(System.Drawing.Color.Black, 1))
                    {
                        sea.Graphics.DrawRectangle(pen, 0, 0, progressForm.ClientSize.Width - 1, progressForm.ClientSize.Height - 1);
                    }
                };

                Label lbl = new Label()
                {
                    Text = "Nesting... (iterations:0 fitness:-1)",
                    Dock = DockStyle.Top,
                    //Height = 20,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                ProgressBar bar = new ProgressBar()
                {
                    Style = ProgressBarStyle.Marquee,
                    Dock = DockStyle.Fill,
                };

                Button btnStop = new Button()
                {
                    Text = "Stop",
                    Dock = DockStyle.Bottom,
                    //Height = 24
                };

                progressForm.Controls.Add(lbl);
                progressForm.Controls.Add(btnStop);
                progressForm.Controls.Add(bar);

                progressForm.Load += (ss, sea) => {
                    // FIX SCALING
                    progressForm.SuspendLayout();
                    foreach (Control ctrl in progressForm.Controls)
                    {
                        ctrl.Font = SystemFonts.MessageBoxFont;
                    }
                    btnStop.Height = btnStop.Font.Height + (int)(btnStop.Font.Height * 0.4);
                    lbl.Height = btnStop.Height;
                    progressForm.Width = 15 * btnStop.Height;
                    progressForm.Height = 3 * btnStop.Height;
                    progressForm.Location = new System.Drawing.Point(
                        this.Width / 2 - progressForm.Width / 2 + this.Location.X,
                        this.Height / 2 - progressForm.Height / 2 + this.Location.Y
                    );
                    progressForm.ResumeLayout(true);
                };

                var cts = new CancellationTokenSource();

                btnStop.Click += (ss, ee) =>
                {
                    btnStop.Enabled = false;
                    lbl.Text = "Stopping...";
                    cts.Cancel();
                };

                var token = cts.Token;
                Task.Run(() =>
                {
                    try
                    {
                        int iteration = 0;

                        while (!token.IsCancellationRequested)
                        {
                            Engine.Context.NestIterate(token);
                            iteration++;

                            if (Engine.Context.Nest.nests != null)
                            {
                                if (Engine.Context.Nest.nests.Fitness < Engine.CurrentFitness)
                                {
                                    Engine.CurrentFitness = (double)Engine.Context.Nest.nests.Fitness;

                                    this.BeginInvoke((MethodInvoker)(() =>
                                    {
                                        Engine.UpdateNestResults();

                                        partGrid.Invalidate();
                                        sheetGrid.Invalidate();
                                        nestGrid.Invalidate();

                                        DrawPanel.Invalidate();
                                    }));
                                }

                                this.BeginInvoke((MethodInvoker)(() =>
                                {
                                    lbl.Text = "Nesting... (iterations:" + iteration.ToString() + " fitness:" + Engine.CurrentFitness.ToString("0") + ")";
                                }));
                            }

                            // Optional: yield briefly to avoid starving UI thread/CPU
                            Thread.Sleep(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke((MethodInvoker)(() =>
                        {
                            MessageBox.Show(this, "Error in optimization: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                    finally
                    {
                        this.BeginInvoke((MethodInvoker)(() =>
                        {
                            if (!progressForm.IsDisposed)
                                progressForm.Close();
                        }));
                    }
                }, token);

                progressForm.ShowDialog(this);
                cts.Dispose();
            };

            exportButton.Click += new EventHandler((s, ea) =>
            {
                if (EditFct != EditFunctions.NONE) EndEdit();

                if (currentObj == null) return;

                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "Dxf Files (*.dxf)|*.dxf|All Files (*.*)|*.*";
                dialog.Title = "Save your file";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (currentObj.GetType() == typeof(ScriptItem))
                    {
                        ScriptItem script = (ScriptItem)currentObj;
                        PartItem part = new PartItem();
                        part.Features = script.Parts.SelectMany(p => p).ToList(); ;
                        Engine.Export(part, dialog.FileName);
                    }
                    else
                    {
                        Engine.Export(currentObj, dialog.FileName);
                    }
                }
            });
        }

        private void SerializeOptions()
        {
            try
            {
                StreamWriter writer = new StreamWriter(NestOptsFilePath);
                XmlSerializer xs = new XmlSerializer(typeof(Options));
                xs.Serialize(writer, Engine.Opts);
                writer.Close();
            }
            catch { }
        }

        private void DeserializeOptions()
        {
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(Options));
                StreamReader reader = new StreamReader(NestOptsFilePath);
                Engine.Opts = (Options)xs.Deserialize(reader);
                reader.Close();
            }
            catch { }
        }

        private void DerializeLayerOptions()
        {
            try
            {
                List<LayerItem> layers;
                using (StreamReader reader = new StreamReader(NestLayerOptsFilePath))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(List<LayerItem>));
                    layers = (List<LayerItem>)xs.Deserialize(reader);
                }
                if (layers != null) Engine.UpdateDxfsLayers(new BindingList<LayerItem>(layers));
            }
            catch { }
        }

        private void SerializeLayerOptions()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(NestLayerOptsFilePath))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(List<LayerItem>));
                    xs.Serialize(writer, Engine.LayerItems.ToList());
                }
            }
            catch { }
        }

        private static void CadOptionsPropertyUpdateAttributes(NCnetic.Nest.Options opts)
        {
            PropertyOverridingTypeDescriptor ctd = new PropertyOverridingTypeDescriptor(TypeDescriptor.GetProvider(opts).GetTypeDescriptor(opts));
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(opts))
            {
                List<Attribute> attributes = new List<Attribute>();

                if (pd.Name == nameof(opts.MergeLayers) ||
                    pd.Name == nameof(opts.NewPartOrigin) ||
                    pd.Name == nameof(opts.MultiplicityMerge) ||
                    pd.Name == nameof(opts.ToolNbFromLayerName))
                {
                    attributes.Add(new EditorAttribute(typeof(CheckEditor), typeof(UITypeEditor)));
                }

                if (pd.Name == nameof(opts.Margins) ||
                    pd.Name == nameof(opts.Spacing) ||
                    pd.Name == nameof(opts.DefaultWidth) ||
                    pd.Name == nameof(opts.DefaultHeight) ||
                    pd.Name == nameof(opts.MinIntArea) ||
                    pd.Name == nameof(opts.Tol0) ||
                    pd.Name == nameof(opts.LinkDist) ||
                    pd.Name == nameof(opts.NestArcSegmentsMaxLength))
                {
                    attributes.Add(new TypeConverterAttribute(typeof(PositiveDoubleTypeConverter)));
                }

                if (pd.Name == nameof(opts.DefaultQty) ||
                    pd.Name == nameof(opts.PaveLimit) ||
                    //pd.Name == nameof(opts.MutationRate) ||
                    //pd.Name == nameof(opts.PopulationSize) ||
                    pd.Name == nameof(opts.DefaultCutToolNb) ||
                    pd.Name == nameof(opts.DefaultMrkToolNb))
                {
                    attributes.Add(new TypeConverterAttribute(typeof(PositiveIntegerTypeConverter)));
                }

                if (attributes.Count > 0)
                {
                    PropertyDescriptor pdNew = TypeDescriptor.CreateProperty(opts.GetType(), pd, attributes.ToArray());
                    ctd.OverrideProperty(pdNew);
                }
            }

            TypeDescriptor.AddProvider(new TypeDescriptorOverridingProvider(ctd), opts);
        }
        
        private void splitContainer_DoubleClick(object sender, EventArgs e)
        {
            this.SuspendLayout();
            if (this.splitContainer.Orientation == Orientation.Horizontal)
            {
                this.splitContainer.Orientation = Orientation.Vertical;
                this.splitContainer.SplitterDistance = this.Width / 2;
            }
            else
            {
                this.splitContainer.Orientation = Orientation.Horizontal;
                this.splitContainer.SplitterDistance = this.Height / 2;
            }
            this.ResumeLayout(true);
        }

        private void ReplaceEnumColumnsWithComboBoxes(DataGridView grid, object dataSource)
        {
            if (dataSource == null) return;

            Type itemType = null;
            var listType = dataSource.GetType();

            if (listType.IsGenericType)
                itemType = listType.GetGenericArguments()[0];
            else if (listType.GetElementType() != null)
                itemType = listType.GetElementType();

            if (itemType == null)
                return;

            foreach (DataGridViewColumn col in grid.Columns.Cast<DataGridViewColumn>().ToList())
            {
                var prop = itemType.GetProperty(col.DataPropertyName);
                if (prop == null) continue;

                if (prop.PropertyType.IsEnum)
                {
                    var combo = new DataGridViewComboBoxColumn
                    {
                        DataPropertyName = col.DataPropertyName,
                        HeaderText = col.HeaderText,
                        DataSource = Enum.GetValues(prop.PropertyType),
                        DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                        FlatStyle = FlatStyle.Flat,
                        ValueType = prop.PropertyType
                    };

                    int index = col.Index;
                    grid.Columns.RemoveAt(index);
                    grid.Columns.Insert(index, combo);
                }
            }
        }

        #endregion

        #region draw

        private float zoom = 1f;
        private float scale = 1f;

        private float minX = 0f;
        private float minY = 0f;
        private float maxX = 0f;
        private float maxY = 0f;
        private float viewOffsetX = 0f;
        private float viewOffsetY = 0f;
        private float offsetX;
        private float offsetY;

        private bool isPanning = false;
        private float panStartMouseX;
        private float panStartMouseY;
        private float panStartOffsetX;
        private float panStartOffsetY;

        private void UpdateView()
        {
            float scaleX = 0.9f * DrawPanel.Width / (maxX - minX);
            float scaleY = 0.9f * DrawPanel.Height / (maxY - minY);
            scale = Math.Min(scaleX, scaleY);
            offsetX = (DrawPanel.Width - (maxX - minX) * scale) / 2f;
            offsetY = (DrawPanel.Height - (maxY - minY) * scale) / 2f;
            scale *= zoom;
            offsetX = viewOffsetX + (offsetX * zoom);
            offsetY = viewOffsetY + (offsetY * zoom);
        }

        private void Draw(PaintEventArgs e)
        {
            int rowIndex;
            if (tab.SelectedTab == partTab)
            {
                if (partGrid.CurrentRow != null)
                {
                    if (EditSel.EditPartIndex > -1)
                    {
                        currentObj = Engine.PartItems[EditSel.EditPartIndex];
                    }
                    else
                    {
                        rowIndex = partGrid.CurrentRow.Index;
                        if (rowIndex >= 0) currentObj = Engine.PartItems[rowIndex];
                    }
                }
            }
            else if (tab.SelectedTab == sheetTab)
            {
                if (sheetGrid.CurrentRow != null)
                {
                    rowIndex = sheetGrid.CurrentRow.Index;
                    if (rowIndex >= 0) currentObj = Engine.SheetItems[rowIndex];
                }
            }
            else if (tab.SelectedTab == nestTab)
            {
                if (nestGrid.CurrentRow != null)
                {
                    rowIndex = nestGrid.CurrentRow.Index;
                    if (rowIndex >= 0) currentObj = Engine.NestItems[rowIndex];
                }
            }
            else if (tab.SelectedTab == shapeLibTab)
            {
                if (scriptGrid.CurrentRow != null)
                {
                    rowIndex = scriptGrid.CurrentRow.Index;
                    if (rowIndex >= 0 && scriptItems[rowIndex].Parts != null) currentObj = scriptItems[rowIndex];
                }
            }

            minX = 0f;
            minY = 0f;
            maxX = 0f;
            maxY = 0f;

            switch (currentObj)
            {
                case PartItem part:
                    if (EditSel.EditPartIndex > -1)
                    {
                        DrawEdit(e.Graphics, part);
                    }
                    else
                    {
                        Draw(e.Graphics, part);
                    }
                    break;

                case SheetItem sheet:
                    Draw(e.Graphics, sheet);
                    break;

                case NestItem nest:
                    Draw(e.Graphics, nest);
                    break;

                case ScriptItem script:
                    Draw(e.Graphics, script);
                    break;

                default:
                    Draw(e.Graphics);
                    break;
            }
        }

        private void Draw(Graphics g, PartItem part)
        {
            GetMinMax(part.Features);
            UpdateView();

            // SIMPLIFIED
            foreach (Feature f in part.Features)
            {
                if ((f.Type == Feature.FeatureType.INT &&
                    GetArea(f.Edges) > Math.Max(Engine.Opts.Spacing * Engine.Opts.Spacing, Engine.Opts.MinIntArea)) || f.Type == Feature.FeatureType.EXT)
                {
                    List<Edge> simplified;
                    if (f.Type == Feature.FeatureType.INT)
                    {
                        simplified = SimplifyForNest(f.Edges, false, true, Engine.Opts.Spacing, -1,
                            Math.PI / 4.0, Engine.Opts.NestArcSegmentsMaxLength, out double dbl, out bool pave);
                    }
                    else
                    {
                        simplified = SimplifyForNest(f.Edges, false, false, Engine.Opts.Spacing, Engine.Opts.PaveLimit * 0.01,
                            Math.PI / 4.0, Engine.Opts.NestArcSegmentsMaxLength, out double dbl, out bool pave);
                    }

                    if (simplified != null)
                    {
                        System.Drawing.Color col = System.Drawing.Color.LightGray;
                        float lineWidth = 1.5f;

                        GraphicsPath path = new GraphicsPath();
                        foreach (Edge edge in simplified)
                        {
                            float x0 = offsetX + (edge.V0.X - minX) * scale;
                            float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                            float x1 = offsetX + (edge.V1.X - minX) * scale;
                            float y1 = offsetY + (maxY - edge.V1.Y) * scale;
                            path.AddLine(x0, y0, x1, y1);
                        }
                        g.DrawPath(new Pen(col, lineWidth), path);
                    }
                }
            }

            foreach (Feature f in part.Features.OrderBy(x => (int)x.Type))
            {
                FillFeature(g, f);
            }
            for (int i = 0; i < part.Features.Count; i++)
            {
                DrawFeature(g, part.Features[i]);
            }
            if (EditBevelOpts.Mode == EditBevelOptions.EditMode.ELEMENT)
            {
                DrawEdgeSelection(g, part);
            }

            DrawAxis(g);
        }

        private void DrawEdit(Graphics g, PartItem part)
        {
            GetMinMax(part.Features);
            UpdateView();

            foreach (Feature f in part.Features.OrderBy(x => (int)x.Type))
            {
                FillFeature(g, f);
            }
            for (int i = 0; i < part.Features.Count; i++)
            {
                Feature f = part.Features[i];

                float nodeSize = 3f;
                float lineWidth = 1f;

                System.Drawing.Color col = System.Drawing.Color.Gray;
                if (f.Type == Feature.FeatureType.EXT ||
                    f.Type == Feature.FeatureType.INT ||
                    f.Type == Feature.FeatureType.OPEN)
                {
                    col = System.Drawing.Color.Black;
                }

                if (i == EditSel.EditFeatureIndex && 
                    EditBevelOpts.Mode == EditBevelOptions.EditMode.CONTOUR)
                {
                    nodeSize = 5.0f;
                    lineWidth = 2.0f;
                }

                for (int j = 0; j < f.Edges.Count; j++)
                {
                    float x = offsetX + (f.Edges[j].V0.X - minX) * scale;
                    float y = offsetY + (maxY - f.Edges[j].V0.Y) * scale;
                    g.FillRectangle(new SolidBrush(col), x - nodeSize / 2f, y - nodeSize / 2f, nodeSize, nodeSize);
                    if (j == 0)
                    {
                        g.DrawRectangle(new Pen(col, lineWidth), x - nodeSize, y - nodeSize, 2f * nodeSize, 2f * nodeSize);
                    }

                    if (f.Edges[j].R > Engine.Opts.Tol0) j += f.Edges[j].NS - 1;
                }

                DrawFeature(g, f, lineWidth);
            }

            if (EditBevelOpts.Mode == EditBevelOptions.EditMode.ELEMENT)
            {
                DrawEdgeSelection(g, part);
            }

            DrawAxis(g);
        }

        private void Draw(Graphics g, SheetItem sheet)
        {
            minX = 0f;
            minY = 0f;
            maxX = (float)sheet.LX;
            maxY = (float)sheet.LY;

            UpdateView();
            DrawSheet(g, (float)sheet.LX, (float)sheet.LY);
            DrawAxis(g, (float)sheet.LX, (float)sheet.LY);

            foreach (Feature f in sheet.Features)
            {
                DrawFeature(g, f);
            }

            foreach (SheetAssociation sa in 
                sheet.Associated.OrderBy(x => Engine.PartItems[x.partId].Features.First().Depth))
            {
                foreach (Feature f in RotoTranslatePartXY(Engine.PartItems[sa.partId].Features, sa.T.X, sa.T.Y, sa.T.Rot).OrderBy(x => (int)x.Type))
                {
                    FillFeature(g, f);
                    DrawFeature(g, f);
                }
            }
        }

        private void Draw(Graphics g, NestItem nest)
        {
            if (Engine.SheetItems[nest.SheetSource].Features.Any())
            {
                GetMinMax(Engine.SheetItems[nest.SheetSource].Features);
            }

            float sheetWidth = (float)Engine.SheetItems[nest.SheetSource].LX;
            float sheetHeight = (float)Engine.SheetItems[nest.SheetSource].LY;
            minX = Math.Min(0f, minX);
            minY = Math.Min(0f, minY);
            maxX = Math.Max(sheetWidth, maxX);
            maxY = Math.Max(sheetHeight, maxY);

            UpdateView();
            DrawSheet(g, sheetWidth, sheetHeight);
            DrawAxis(g, sheetWidth, sheetHeight);

            if (Engine.NestItems.Any())
            {
                foreach (Feature f in Engine.SheetItems[nest.SheetSource].Features)
                {
                    DrawFeature(g, f);
                }

                foreach (SheetAssociation sa in
                    Engine.SheetItems[nest.SheetSource].Associated.OrderBy(
                        x => Engine.PartItems[x.partId].Features.First().Depth))
                {
                    foreach (Feature f in RotoTranslatePartXY(Engine.PartItems[sa.partId].Features, sa.T.X, sa.T.Y, sa.T.Rot).OrderBy(x => (int)x.Type))
                    {
                        FillFeature(g, f);
                        DrawFeature(g, f);
                    }
                }

                foreach (List<Feature> part in nest.NestData.OrderBy(x => x.First().Depth))
                {
                    foreach (Feature f in part.OrderBy(x => (int)x.Type))
                    {
                        FillFeature(g, f);
                        DrawFeature(g, f);
                    }
                }
            }
        }

        private void Draw(Graphics g, ScriptItem script)
        {
            if (script.Parts != null)
            {
                foreach (List<Feature> part in script.Parts)
                {
                    GetMinMax(part);
                }
            }

            UpdateView();

            foreach (List<Feature> part in script.Parts)
            {
                foreach (Feature f in part.OrderBy(x => (int)x.Type))
                {
                    FillFeature(g, f);
                    DrawFeature(g, f);
                }
            }

            DrawAxis(g);
        }

        private void Draw(Graphics g)
        {
            float width = DrawPanel.Width;
            float height = DrawPanel.Height;

            using (Pen axisPen = new Pen(System.Drawing.Color.Gray, 1.5f))
            {
                axisPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(axisPen, -width, width / 2f, width, width / 2f);   // X axis
                g.DrawLine(axisPen, height / 2f, -height, height / 2f, height);  // Y axis
            }
        }

        private void DrawEdgeSelection(Graphics g, PartItem part)
        {
            for (int i = 0; i < part.Features.Count; i++)
            {
                Feature f = part.Features[i];

                if (i == EditSel.EditFeatureIndex && EditSel.EditEdgeIndex > -1)
                {
                    int id = EditSel.EditEdgeIndex;

                    System.Drawing.Color col = System.Drawing.Color.Gray;
                    if (f.Type == Feature.FeatureType.EXT || f.Type == Feature.FeatureType.INT) col = System.Drawing.Color.Black;

                    float nodeSize = 5.0f;
                    float lineWidth = 2f;

                    float x0 = 0f;
                    float y0 = 0f;
                    float x1 = 0f;
                    float y1 = 0f;

                    x0 = offsetX + (f.Edges[id].V0.X - minX) * scale;
                    y0 = offsetY + (maxY - f.Edges[id].V0.Y) * scale;
                    g.FillRectangle(new SolidBrush(col), x0 - nodeSize / 2f, y0 - nodeSize / 2f, nodeSize, nodeSize);

                    if (f.Edges[id].NS > 0)
                    {
                        x1 = offsetX + (f.Edges[id + f.Edges[id].NS - 1].V1.X - minX) * scale;
                        y1 = offsetY + (maxY - f.Edges[id + f.Edges[id].NS - 1].V1.Y) * scale;
                        g.FillRectangle(new SolidBrush(col), x1 - nodeSize / 2f, y1 - nodeSize / 2f, nodeSize, nodeSize);
                    }
                    else
                    {
                        x1 = offsetX + (f.Edges[id].V1.X - minX) * scale;
                        y1 = offsetY + (maxY - f.Edges[id].V1.Y) * scale;
                        g.FillRectangle(new SolidBrush(col), x1 - nodeSize / 2f, y1 - nodeSize / 2f, nodeSize, nodeSize);
                    }

                    if (f.Edges[id].NS > 0)
                    {
                        GraphicsPath path = new GraphicsPath();
                        for (int j = id; j < id + f.Edges[id].NS; j++)
                        {
                            x0 = offsetX + (f.Edges[j].V0.X - minX) * scale;
                            y0 = offsetY + (maxY - f.Edges[j].V0.Y) * scale;
                            x1 = offsetX + (f.Edges[j].V1.X - minX) * scale;
                            y1 = offsetY + (maxY - f.Edges[j].V1.Y) * scale;
                            path.AddLine(x0, y0, x1, y1);
                        }
                        g.DrawPath(new Pen(col, lineWidth), path);
                    }
                    else
                    {
                        x0 = offsetX + (f.Edges[id].V0.X - minX) * scale;
                        y0 = offsetY + (maxY - f.Edges[id].V0.Y) * scale;
                        x1 = offsetX + (f.Edges[id].V1.X - minX) * scale;
                        y1 = offsetY + (maxY - f.Edges[id].V1.Y) * scale;
                        g.DrawLine(new Pen(col, lineWidth), x0, y0, x1, y1);
                    }
                }
            }
        }

        private void DrawFeature(Graphics g, Feature f, float lineWidth = 1f)
        {
            System.Drawing.Color col = System.Drawing.Color.Gray;
            if (f.Type == Feature.FeatureType.EXT || 
                f.Type == Feature.FeatureType.INT ||
                f.Type == Feature.FeatureType.OPEN)
            {
                col = System.Drawing.Color.Black;
            }

            float gx0 = (float)Math.Sqrt(-1);
            float gy0 = (float)Math.Sqrt(-1);

            GraphicsPath path = new GraphicsPath();

            for (int i = 0; i < f.Edges.Count; i++)
            {
                if (f.Edges[i].NS > 0)
                {
                    for (int j = i; j < i + f.Edges[i].NS; j++)
                    {
                        float x0 = offsetX + (f.Edges[j].V0.X - minX) * scale;
                        float y0 = offsetY + (maxY - f.Edges[j].V0.Y) * scale;
                        float x1 = offsetX + (f.Edges[j].V1.X - minX) * scale;
                        float y1 = offsetY + (maxY - f.Edges[j].V1.Y) * scale;
                        path.AddLine(x0, y0, x1, y1);
                    }
                    i += f.Edges[i].NS - 1;
                }
                else
                {
                    float x0 = offsetX + (f.Edges[i].V0.X - minX) * scale;
                    float y0 = offsetY + (maxY - f.Edges[i].V0.Y) * scale;
                    float x1 = offsetX + (f.Edges[i].V1.X - minX) * scale;
                    float y1 = offsetY + (maxY - f.Edges[i].V1.Y) * scale;
                    path.AddLine(x0, y0, x1, y1);
                }
            }

            g.DrawPath(new Pen(col, lineWidth), path);

            for (int i = 0; i < f.Edges.Count; i++)
            {
                BevelDefinition gdef = new BevelDefinition();
                float gx = 0f;
                float gy = 0f;

                if (f.Edges[i].NS > 0)
                {
                    for (int j = i; j < i + f.Edges[i].NS; j++)
                    {
                        if (j == i + f.Edges[i].NS / 2)
                        {
                            gdef = f.Edges[i].Bevel;
                            gx = offsetX + (f.Edges[j].V0.X - minX) * scale;
                            gy = offsetY + (maxY - f.Edges[j].V0.Y) * scale;
                        }
                    }
                    i += f.Edges[i].NS - 1;
                }
                else
                {
                    float x0 = offsetX + (f.Edges[i].V0.X - minX) * scale;
                    float y0 = offsetY + (maxY - f.Edges[i].V0.Y) * scale;
                    float x1 = offsetX + (f.Edges[i].V1.X - minX) * scale;
                    float y1 = offsetY + (maxY - f.Edges[i].V1.Y) * scale;

                    gdef = f.Edges[i].Bevel;
                    gx = (x0 + x1) / 2f;
                    gy = (y0 + y1) / 2f;
                }

                if (gdef.Type != BevelType.I)
                {
                    if (float.IsNaN(gx0) || float.IsNaN(gy0))
                    {
                        if (DrawBevelMarker(g, gdef, gx, gy))
                        {
                            gx0 = gx;
                            gy0 = gy;
                        }
                    }
                    else
                    {
                        if (Math.Pow(gx0 - gx, 2.0) + Math.Pow(gy0 - gy, 2.0) >
                            Math.Pow(0.1 * Math.Max(DrawPanel.Width, DrawPanel.Height), 2.0))
                        {
                            if (DrawBevelMarker(g, gdef, gx, gy))
                            {
                                gx0 = gx;
                                gy0 = gy;
                            }
                        }
                    }
                }
            }
        }

        private void FillFeature(Graphics g, Feature f)
        {
            GraphicsPath path = new GraphicsPath();

            for (int i = 0; i < f.Edges.Count; i++)
            {
                if (f.Edges[i].NS > 0)
                {
                    for (int j = i; j < i + f.Edges[i].NS; j++)
                    {
                        float x0 = offsetX + (f.Edges[j].V0.X - minX) * scale;
                        float y0 = offsetY + (maxY - f.Edges[j].V0.Y) * scale;
                        float x1 = offsetX + (f.Edges[j].V1.X - minX) * scale;
                        float y1 = offsetY + (maxY - f.Edges[j].V1.Y) * scale;
                        path.AddLine(x0, y0, x1, y1);
                    }
                    i += f.Edges[i].NS - 1;
                }
                else
                {
                    float x0 = offsetX + (f.Edges[i].V0.X - minX) * scale;
                    float y0 = offsetY + (maxY - f.Edges[i].V0.Y) * scale;
                    float x1 = offsetX + (f.Edges[i].V1.X - minX) * scale;
                    float y1 = offsetY + (maxY - f.Edges[i].V1.Y) * scale;
                    path.AddLine(x0, y0, x1, y1);
                }
            }

            if (f.Type == Feature.FeatureType.EXT)
            {
                g.FillPath(new SolidBrush(System.Drawing.Color.WhiteSmoke), path);
            }
            else if (f.Type == Feature.FeatureType.INT)
            {
                g.FillPath(new SolidBrush(DrawPanel.BackColor), path);
            }
        }

        private bool DrawBevelMarker(Graphics g, BevelDefinition def, float x, float y)
        {
            string str = "";
            switch (def.Type)
            {
                case BevelType.V:
                    str = "V" + def.A.ToString("0.##");
                    break;

                case BevelType.Y:
                    str = "Y" + def.A.ToString("0.##") + "[" + def.r.ToString("0.###") + "]";
                    break;

                case BevelType.X:
                    str = "X" + def.A.ToString("0.##") + "|" + def.B.ToString("0.##") + "[" + def.r.ToString("0.###") + "]";
                    break;

                case BevelType.K:
                    str = "K" + def.A.ToString("0.##") + "|" + def.B.ToString("0.##") + "[" + def.r.ToString("0.###") + "]" + "[" + def.Hr.ToString("0.###") + "]";
                    break;
            }

            if (str == "") return false;

            Font font = new Font("Arial", 8, FontStyle.Bold);
            //g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            SizeF textSize = g.MeasureString(str, font);

            x = x - textSize.Width / 2f;
            y = y - textSize.Height / 2f;

            RectangleF rect = new RectangleF(x, y, textSize.Width, textSize.Height);
            g.FillRectangle(new SolidBrush(System.Drawing.Color.Black), rect);
            g.DrawString(str, font, new SolidBrush(System.Drawing.Color.White), x, y);

            return true;
        }

        private void DrawAxis(Graphics g)
        {
            using (Pen axisPen = new Pen(System.Drawing.Color.Black, 1.5f))
            {
                axisPen.DashStyle = DashStyle.Dash;
                float ox = offsetX + (-minX) * scale;
                float oy = offsetY + (maxY) * scale;

                g.DrawLine(axisPen, 0f, oy, DrawPanel.Width, oy);
                g.DrawLine(axisPen, ox, 0f, ox, DrawPanel.Height);
            }
        }

        private void DrawAxis(Graphics g, float sheetx, float sheety)
        {
            using (Pen axisPen = new Pen(System.Drawing.Color.Gray, 1.5f))
            {
                axisPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                float ox = offsetX + (-minX) * scale;
                float oy = offsetY + (maxY) * scale;

                if (Engine.Opts.Origin == Options.OriginPosition.XY_MID)
                {
                    ox = offsetX + (-minX + sheetx / 2f) * scale;
                    oy = offsetY + (maxY - sheety / 2f) * scale;
                }

                if (Engine.Opts.Origin == Options.OriginPosition.XY_MAX ||
                    Engine.Opts.Origin == Options.OriginPosition.X_MAX_Y_MIN)
                {
                    ox = offsetX + (-minX + sheetx) * scale;
                }

                if (Engine.Opts.Origin == Options.OriginPosition.XY_MAX ||
                    Engine.Opts.Origin == Options.OriginPosition.X_MIN_Y_MAX)
                {
                    oy = offsetY + (maxY - sheety) * scale;
                }

                g.DrawLine(axisPen, 0f, oy, DrawPanel.Width, oy);
                g.DrawLine(axisPen, ox, 0f, ox, DrawPanel.Height);
            }
        }

        private void DrawSheet(Graphics g, float sheetx, float ly)
        {
            if (sheetx > 0 && ly > 0)
            {
                using (Pen sheetPen = new Pen(System.Drawing.Color.DarkGray, 2f))
                {
                    float sx = offsetX + (-minX) * scale;
                    float sy = offsetY + (maxY - ly) * scale;
                    g.DrawRectangle(sheetPen, sx, sy, sheetx * scale, ly * scale);
                }
            }
        }

        private void GetMinMax(List<Feature> features)
        {
            foreach (Feature f in features)
            {
                foreach (Edge edge in f.Edges)
                {
                    minX = Math.Min(edge.V0.X, Math.Min(edge.V1.X, minX));
                    maxX = Math.Max(edge.V0.X, Math.Max(edge.V1.X, maxX));

                    minY = Math.Min(edge.V0.Y, Math.Min(edge.V1.Y, minY));
                    maxY = Math.Max(edge.V0.Y, Math.Max(edge.V1.Y, maxY));
                }
            }
        }

        #endregion

        #region scripts

        public class ScriptItem
        {
            public List<List<Feature>> Parts = new List<List<Feature>>();

            [Browsable(false)]
            public string Src { get; set; }

            [Browsable(false)]
            public bool CanEdit { get; set; } = false;

            [DisplayName("SHAPE")]
            public string Name { get; set; }
        }

        private void IniScripts()
        {
            DeserializeScripts();

            scriptItems.Add(new ScriptItem
            {
                Name = "RECTANGLE",
                CanEdit = false,
                Src = Resource.Rectangle.Replace("RectangleShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "REGULAR POLYGON",
                CanEdit = false,
                Src = Resource.RegularPoly.Replace("RegularPolyShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "CIRCLE",
                CanEdit = false,
                Src = Resource.Circle.Replace("CircleShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "RING",
                CanEdit = false,
                Src = Resource.Ring,
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "BASE PLATE",
                CanEdit = false,
                Src = Resource.BasePlate,
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "LETTERING",
                CanEdit = false,
                Src = Resource.Lettering.Replace("LetteringShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "MARKING",
                CanEdit = false,
                Src = Resource.Marking.Replace("MarkingShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "OPEN CUT",
                CanEdit = false,
                Src = Resource.OpenCut.Replace("OpenCutShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "BEVEL PLATE",
                CanEdit = false,
                Src = Resource.BevelPlate.Replace("BevelPlateShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "BEVEL HOLE PLATE",
                CanEdit = false,
                Src = Resource.BevelHolePlate,
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "SHAPES",
                CanEdit = false,
                Src = Resource.RandomShape.Replace("RandomShape2D", "Shape2D"),
            });
            scriptItems.Add(new ScriptItem
            {
                Name = "RANDOM NEST",
                CanEdit = false,
                Src = Resource.RandomNest.Replace("RandomNest2D", "Shape2D"),
            });

            scriptGrid.DataSource = scriptItems;

            scriptGrid.RowPrePaint += new DataGridViewRowPrePaintEventHandler((s, ea) =>
            {
                var row = scriptGrid.Rows[ea.RowIndex];
                ScriptItem sh = row.DataBoundItem as ScriptItem;

                if (sh == null) return;

                if (!sh.CanEdit)
                {
                    row.DefaultCellStyle.BackColor = System.Drawing.Color.WhiteSmoke;
                    row.DefaultCellStyle.ForeColor = System.Drawing.Color.DimGray;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = scriptGrid.DefaultCellStyle.BackColor;
                    row.DefaultCellStyle.ForeColor = scriptGrid.DefaultCellStyle.ForeColor;
                }
            });

            scriptGrid.CellBeginEdit += new DataGridViewCellCancelEventHandler((s, ea) =>
            {
                var item = scriptGrid.Rows[ea.RowIndex].DataBoundItem as ScriptItem;
                if (item != null && !item.CanEdit) ea.Cancel = true;
            });

            scriptGrid.CellFormatting += new DataGridViewCellFormattingEventHandler((s, ea) =>
            {
                var item = scriptGrid.Rows[ea.RowIndex].DataBoundItem as ScriptItem;
                if (item == null) return;
                if (!item.CanEdit)
                {
                    ea.CellStyle.BackColor = System.Drawing.Color.WhiteSmoke;
                    ea.CellStyle.ForeColor = System.Drawing.Color.DimGray;
                }
            });

            scriptGrid.SelectionChanged += new EventHandler((s, ea) =>
            {
                if (scriptGrid.SelectedRows.Count <= 0) return;
                int index = scriptGrid.SelectedRows[0].Index;
                //scriptTextBox.Text = scriptItems[index].Src;
                //if (scriptItems[index].CanEdit)
                //{
                //    scriptTextBox.ReadOnly = false;
                //    scriptTextBox.BackColor = System.Drawing.Color.White;
                //}
                //else
                //{
                //    scriptTextBox.ReadOnly = true;
                //    scriptTextBox.BackColor = System.Drawing.Color.WhiteSmoke;
                //}
                CompileSelected();
            });

            propertyGrid.PropertyValueChanged += new PropertyValueChangedEventHandler((s, ea) =>
            {
                if (_scriptInstance == null) return;
                if (scriptGrid.SelectedRows.Count <= 0) return;
                int index = scriptGrid.SelectedRows[0].Index;

                try
                {
                    object result = _scriptType.GetMethod("Run").Invoke(_scriptInstance, new object[] { _paramInstance });
                    scriptItems[index].Parts = ExtractParts(Engine.ExtractFeatures((CadDocument)result),
                        Engine.Opts.Tol0, Engine.Opts.LinkDist, Engine.Opts.MergeLayers, false,
                            Engine.Opts.ToolNbFromLayerName, Engine.Opts.DefaultCutToolNb, Engine.Opts.DefaultMrkToolNb);
                }
                catch
                {
                    scriptItems[index].Parts = null;
                }

                DrawPanel.Invalidate();
            });

            execButton.Click += new EventHandler((s,ea) =>
            {
                try
                {
                    if (_scriptInstance == null) return;
                    object result = _scriptType.GetMethod("Run").Invoke(_scriptInstance, new object[] { _paramInstance });
                    LoadDoc((CadDocument)result);
                }
                catch
                {
                    
                }
            });

            //newScriptButton.Click += new EventHandler((s, ea) =>
            //{
            //    scriptItems.Insert(0, new ScriptItem
            //    {
            //        Name = "CUSTOM_SHAPE",
            //        CanEdit = true,
            //        //Src = Resource.NewShape2D.Replace("NewShape2D", "Shape2D"),
            //    });

            //    scriptGrid.ClearSelection();
            //    scriptGrid.Rows[0].Selected = true;
            //    SerializeScripts();
            //});

            //removeScriptButton.Click += new EventHandler((s, ea) =>
            //{
            //    if (scriptGrid.SelectedRows.Count > 0)
            //    {
            //        int index = scriptGrid.SelectedRows[0].Index;
            //        if (scriptItems[index].CanEdit)
            //        {
            //            if (MessageBox.Show("Remove " + scriptItems[index].Name + "?", "", MessageBoxButtons.YesNo) == DialogResult.Yes)
            //            {
            //                scriptItems.RemoveAt(index);
            //                SerializeScripts();
            //            }
            //        }
            //    }
            //});

            //compileScriptButton.Click += new EventHandler((s, ea) =>
            //{
            //    CompileSelected();
            //});
        }

        private void SerializeScripts()
        {
            try
            {
                var editableItems = scriptItems.Where(p => p.CanEdit).ToList();
                using (StreamWriter writer = new StreamWriter(NestScriptFilePath))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(List<ScriptItem>));
                    xs.Serialize(writer, editableItems);
                }
            }
            catch { }
        }

        private void DeserializeScripts()
        {
            try
            {
                List<ScriptItem> editableItems;
                using (StreamReader reader = new StreamReader(NestScriptFilePath))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(List<ScriptItem>));
                    editableItems = (List<ScriptItem>)xs.Deserialize(reader);
                }
                scriptItems = new BindingList<ScriptItem>(editableItems);
            }
            catch { }
        }

        private void CompileSelected()
        {
            if (scriptGrid.SelectedRows.Count <= 0) return;
            int index = scriptGrid.SelectedRows[0].Index;
            //string src = scriptTextBox.Text;
            string src = scriptItems[index].Src;

            propertyGrid.SelectedObject = null;

            if (src == "" || src == null) return;

            using (CSharpCodeProvider provider = new CSharpCodeProvider())
            {
                CompilerParameters parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false
                };

                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                parameters.ReferencedAssemblies.Add("System.Drawing.dll");
                parameters.ReferencedAssemblies.Add(typeof(ACadSharp.CadDocument).Assembly.Location);

                CompilerResults results = provider.CompileAssemblyFromSource(parameters, src);

                if (results.Errors.HasErrors)
                {
                    MessageBox.Show(results.Errors[0].ToString());
                    return;
                }
                else
                {
                    try
                    {
                        scriptItems[index].Src = src;
                        SerializeScripts();

                        _assembly = results.CompiledAssembly;
                        _scriptType = _assembly.GetType("Shape2D");
                        _paramType = _scriptType.GetNestedType("ShapeParameters");

                        _scriptInstance = Activator.CreateInstance(_scriptType);
                        _paramInstance = Activator.CreateInstance(_paramType);

                        propertyGrid.SelectedObject = _paramInstance;

                        object result = _scriptType.GetMethod("Run").Invoke(_scriptInstance, new object[] { _paramInstance });
                        scriptItems[index].Parts = ExtractParts(Engine.ExtractFeatures((CadDocument)result),
                            Engine.Opts.Tol0, Engine.Opts.LinkDist, Engine.Opts.MergeLayers, false,
                            Engine.Opts.ToolNbFromLayerName, Engine.Opts.DefaultCutToolNb, Engine.Opts.DefaultMrkToolNb);

                        DrawPanel.Invalidate();
                    }
                    catch
                    {
                        _assembly = null;
                        _scriptType = null;
                        _paramType = null;

                        _scriptInstance = null;
                        _paramInstance = null;
                    }
                }
            }
        }

        #endregion

        #region edit
        public class EditSelection
        {
            [Browsable(false)]
            internal int EditPartIndex = -1;
            [Browsable(false)]
            internal int EditFeatureIndex = -1;
            [Browsable(false)]
            internal int EditEdgeIndex = -1;
            [Browsable(false)]
            internal float EditEdgePos = 0f;

            internal void Reset()
            {
                EditPartIndex = -1;
                EditFeatureIndex = -1;
                EditEdgeIndex = -1;
                EditEdgePos = 0f;
            }
        }

        public class EditBevelOptions
        {
            public enum EditMode { CONTOUR, ELEMENT }

            [Category("\tSELECTION MODE")]
            [DisplayName("MODE")]
            public EditMode Mode { get; set; }

            [Category("BEVEL")]
            [DisplayName("TYPE")]
            public BevelType Type { get; set; } = BevelType.V;

            [Category("BEVEL")]
            [DisplayName("ANGLE 1")]
            [TypeConverter(typeof(DoubleTypeConverter))]
            public double A { get; set; } = 45.0;

            [Category("BEVEL")]
            [DisplayName("POSITION RATIO")]
            [TypeConverter(typeof(PositiveDoubleTypeConverter))]
            public double r { get; set; } = 50;

            [Category("BEVEL")]
            [DisplayName("ANGLE 2")]
            [TypeConverter(typeof(DoubleTypeConverter))]
            public double B { get; set; } = 45.0;

            [Category("BEVEL")]
            [DisplayName("K HEEL HEIGHT RATIO")]
            [TypeConverter(typeof(PositiveDoubleTypeConverter))]
            public double Hr { get; set; } = 50;
        }

        private static void EditBevelOptionsPropertyUpdateAttributes(EditBevelOptions opts)
        {
            PropertyOverridingTypeDescriptor ctd = new PropertyOverridingTypeDescriptor(TypeDescriptor.GetProvider(opts).GetTypeDescriptor(opts));
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(opts))
            {
                List<Attribute> attributes = new List<Attribute>();

                if (pd.Name == nameof(opts.A))
                {
                    if (opts.Type == BevelType.I)
                    {
                        attributes.Add(new BrowsableAttribute(false));
                    }
                    else
                    {
                        attributes.Add(new BrowsableAttribute(true));
                    }
                }

                if (pd.Name == nameof(opts.r))
                {
                    if (opts.Type == BevelType.I || opts.Type == BevelType.V)
                    {
                        attributes.Add(new BrowsableAttribute(false));
                    }
                    else
                    {
                        attributes.Add(new BrowsableAttribute(true));
                    }
                }

                if (pd.Name == nameof(opts.B))
                {
                    if (opts.Type == BevelType.I || opts.Type == BevelType.V || opts.Type == BevelType.Y)
                    {
                        attributes.Add(new BrowsableAttribute(false));
                    }
                    else
                    {
                        attributes.Add(new BrowsableAttribute(true));
                    }
                }

                if (pd.Name == nameof(opts.Hr))
                {
                    if (opts.Type != BevelType.K)
                    {
                        attributes.Add(new BrowsableAttribute(false));
                    }
                    else
                    {
                        attributes.Add(new BrowsableAttribute(true));
                    }
                }

                if (attributes.Count > 0)
                {
                    PropertyDescriptor pdNew = TypeDescriptor.CreateProperty(opts.GetType(), pd, attributes.ToArray());
                    ctd.OverrideProperty(pdNew);
                }
            }

            TypeDescriptor.AddProvider(new TypeDescriptorOverridingProvider(ctd), opts);
        }

        private void EndEdit()
        {
            EditFct = EditFunctions.NONE;
            pSplitContainer.Panel1Collapsed = false;
            pSplitContainer.Panel2Collapsed = true;
            editGrid.SelectedObject = null;

            EditSel.Reset();

            DrawPanel.Cursor = Cursors.Default;
        }

        private bool PointNearSegment(float px, float py, ref float pos, float x0, float y0, float x1, float y1, float tol)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;

            if (dx == 0 && dy == 0)
            {
                float distSq = (px - x0) * (px - x0) + (py - y0) * (py - y0);
                pos = 0f;
                return distSq <= tol * tol;
            }

            float t = ((px - x0) * dx + (py - y0) * dy) / (dx * dx + dy * dy);

            t = Math.Max(0f, Math.Min(1f, t));
            pos = t;

            float closestX = x0 + t * dx;
            float closestY = y0 + t * dy;

            float distX = px - closestX;
            float distY = py - closestY;

            float distanceSq = distX * distX + distY * distY;

            return distanceSq <= tol * tol;
        }

        private bool PointNearPoint(float px, float py, float x, float y, float tol)
        {
            float distSq = (px - x) * (px - x) + (py - y) * (py - y);
            return distSq <= tol * tol;
        }

        private bool SelectEdge(Feature f, ref int id, ref float pos, float px, float py, float tol)
        {
            for (int j = 0; j < f.Edges.Count; j++)
            {
                if (f.Edges[j].NS > 0)
                {
                    for (int k = 0; k < f.Edges[j].NS; k++)
                    {
                        Edge edge = f.Edges[j + k];
                        
                        float edgePos = 0f;
                        if (PointNearSegment(px, py, ref edgePos, edge.V0.X, edge.V0.Y, edge.V1.X, edge.V1.Y, tol))
                        {
                            id = j;
                            pos = ((float)(k - 1) + edgePos) / (float)f.Edges[j].NS;
                            return true;
                        }
                    }
                    j += f.Edges[j].NS - 1;
                }
                else
                {
                    Edge edge = f.Edges[j];

                    if (PointNearSegment(px, py, ref pos, edge.V0.X, edge.V0.Y, edge.V1.X, edge.V1.Y, tol))
                    {
                        id = j;
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region custom controls
        public class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel()
            {
                this.DoubleBuffered = true;
                this.ResizeRedraw = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                              ControlStyles.UserPaint |
                              ControlStyles.OptimizedDoubleBuffer, true);
                this.UpdateStyles();
            }
        }

        public class CustomSystemRenderer : ToolStripSystemRenderer
        {
            public CustomSystemRenderer() { }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        }

        public static class SplitContainerExtensions
        {
            public static void Paint(object sender, PaintEventArgs e)
            {
                var control = sender as SplitContainer;
                //paint the three dots'
                System.Drawing.Point[] points = new System.Drawing.Point[3];
                var w = control.Width;
                var h = control.Height;
                var d = control.SplitterDistance;
                var sW = control.SplitterWidth;

                //calculate the position of the points'
                if (control.Orientation == Orientation.Horizontal)
                {
                    points[0] = new System.Drawing.Point((w / 2), d + (sW / 2));
                    points[1] = new System.Drawing.Point(points[0].X - 10, points[0].Y);
                    points[2] = new System.Drawing.Point(points[0].X + 10, points[0].Y);
                }
                else
                {
                    points[0] = new System.Drawing.Point(d + (sW / 2), (h / 2));
                    points[1] = new System.Drawing.Point(points[0].X, points[0].Y - 10);
                    points[2] = new System.Drawing.Point(points[0].X, points[0].Y + 10);
                }

                foreach (System.Drawing.Point p in points)
                {
                    p.Offset(-2, -2);
                    e.Graphics.FillEllipse(SystemBrushes.ControlDark,
                        new Rectangle(p, new Size(3, 3)));

                    p.Offset(1, 1);
                    e.Graphics.FillEllipse(SystemBrushes.ControlLight,
                        new Rectangle(p, new Size(3, 3)));
                }

                using (Pen borderPen = new Pen(System.Drawing.Color.DarkGray, 1))
                {
                    if (control.Orientation == Orientation.Horizontal)
                    {
                        int y = d;
                        e.Graphics.DrawLine(borderPen, 0, y, w, y);             // top edge
                        e.Graphics.DrawLine(borderPen, 0, y + sW - 1, w, y + sW - 1); // bottom edge
                    }
                    else
                    {
                        int x = d;
                        e.Graphics.DrawLine(borderPen, x, 0, x, h);             // left edge
                        e.Graphics.DrawLine(borderPen, x + sW - 1, 0, x + sW - 1, h); // right edge
                    }
                }
            }

            public static void SplitterMoved(object sender, SplitterEventArgs e)
            {
                var control = sender as SplitContainer;

                if (!control.IsHandleCreated) { return; }

                if (control.CanFocus)
                {
                    control.ActiveControl = control.Panel1;
                }
            }
        }

        public class PropertyOverridingTypeDescriptor : CustomTypeDescriptor
        {
            private readonly Dictionary<string, PropertyDescriptor> overridePds = new Dictionary<string, PropertyDescriptor>();

            public PropertyOverridingTypeDescriptor(ICustomTypeDescriptor parent)
                : base(parent)
            { }

            public void OverrideProperty(PropertyDescriptor pd)
            {
                overridePds[pd.Name] = pd;
            }

            public override object GetPropertyOwner(PropertyDescriptor pd)
            {
                object o = base.GetPropertyOwner(pd);

                if (o == null)
                {
                    return this;
                }

                return o;
            }

            public PropertyDescriptorCollection GetPropertiesImpl(PropertyDescriptorCollection pdc)
            {
                List<PropertyDescriptor> pdl = new List<PropertyDescriptor>(pdc.Count + 1);

                foreach (PropertyDescriptor pd in pdc)
                {
                    if (overridePds.ContainsKey(pd.Name))
                    {
                        pdl.Add(overridePds[pd.Name]);
                    }
                    else
                    {
                        pdl.Add(pd);
                    }
                }

                PropertyDescriptorCollection ret = new PropertyDescriptorCollection(pdl.ToArray());

                return ret;
            }

            public override PropertyDescriptorCollection GetProperties()
            {
                return GetPropertiesImpl(base.GetProperties());
            }
            public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
            {
                return GetPropertiesImpl(base.GetProperties(attributes));
            }
        }

        public class TypeDescriptorOverridingProvider : TypeDescriptionProvider
        {
            private readonly ICustomTypeDescriptor ctd;

            public TypeDescriptorOverridingProvider(ICustomTypeDescriptor ctd)
            {
                this.ctd = ctd;
            }

            public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
            {
                return ctd;
            }
        }

        public class CheckEditor : UITypeEditor
        {
            public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                return UITypeEditorEditStyle.Modal;
            }
            public override bool GetPaintValueSupported(ITypeDescriptorContext context)
            {
                return true;
            }
            public override void PaintValue(PaintValueEventArgs e)
            {
                ButtonState State;
                bool res = Convert.ToBoolean((e.Value));
                if (res)
                {
                    State = ButtonState.Checked;
                }
                else
                {
                    State = ButtonState.Normal;
                }
                ControlPaint.DrawCheckBox(e.Graphics, e.Bounds, State);
                e.Graphics.ExcludeClip(e.Bounds);
            }
        }

        public class DoubleTypeConverter : DoubleConverter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return false;
            }
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return true;
            }
            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                return base.ConvertTo(context, CultureInfo.InvariantCulture, value, destinationType);
            }
            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                try
                {
                    string newval = value as string;
                    newval.Replace(",", ".");
                    return double.Parse(newval, NumberStyles.Number, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    var gridItem = context as GridItem;
                    if (gridItem != null)
                    {
                        return gridItem.Value;
                    }
                    else
                    {
                        return 0.0;
                    }
                }
            }
        }

        public class PositiveDoubleTypeConverter : DoubleConverter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return false;
            }
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return true;
            }
            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                return base.ConvertTo(context, CultureInfo.InvariantCulture, value, destinationType);
            }
            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                try
                {
                    string newval = value as string;
                    newval.Replace(",", ".");
                    return Math.Abs(double.Parse(newval as string, NumberStyles.Number, CultureInfo.InvariantCulture));
                }
                catch (Exception)
                {
                    var gridItem = context as GridItem;
                    if (gridItem != null)
                    {
                        return gridItem.Value;
                    }
                    else
                    {
                        return 0.0;
                    }
                }
            }
        }

        public class PositiveIntegerTypeConverter : Int32Converter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return false;
            }
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return true;
            }
            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                try
                {
                    return Math.Abs(Int32.Parse(value as string));
                }
                catch (Exception)
                {
                    var gridItem = context as GridItem;
                    if (gridItem != null)
                    {
                        return gridItem.Value;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }
        }
        #endregion
    }
}
