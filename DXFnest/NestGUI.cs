using DeepNestLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using static System.Net.WebRequestMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace DXFnest
{
    public partial class NestGUI : System.Windows.Forms.Form
    {
        public MouseButtons PAN_BUTTON = MouseButtons.Middle;
        public List<string> FilesToProcess = new List<string>();
        DoubleBufferedPanel DrawPanel;

        Object currentObj = null;

        NestEngine Engine = new NestEngine();
        bool ToRestart = false;

        public NestGUI(List<string> files = null)
        {
            InitializeComponent();

            DrawPanel = new DoubleBufferedPanel();
            DrawPanel.Dock = DockStyle.Fill;
            splitContainer.Panel2.Controls.Add(DrawPanel);

            Engine.Opts = new Options();
            CadOptionsPropertyUpdateAttributes(Engine.Opts);
            optionsGrid.SelectedObject = Engine.Opts;

            sheetGrid.DataSource = Engine.SheetItems;
            partGrid.DataSource = Engine.PartItems;
            nestGrid.DataSource = Engine.NestItems;
            
            //layerGrid.DataSource = Engine.LayerItems;
            //ReplaceEnumColumnsWithComboBoxes(layerGrid, Engine.LayerItems);

            IniEventsForm();

            Engine.SheetItems.Add(new SheetItem
            {
                UsedQty = 0,
                IniQty = Engine.Opts.DefaultQty,
                LX = Engine.Opts.DefaultWidth,
                LY = Engine.Opts.DefaultHeight,
            });

            this.HandleCreated += new EventHandler((s, ea) =>
            {
                BeginInvoke((MethodInvoker)delegate ()
                {
                    if (files != null)
                    {
                        Engine.UpdateDxfsLayers(files);
                        UpdateLayersDialog();
                        Engine.LoadDxfs(files, NestEngine.LoadType.PART);
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

        public void ClearParts()
        {
            Engine.ClearParts();
            Engine.RestartNest();
            partGrid.Invalidate();
            DrawPanel.Invalidate();
        }

        public void LoadDxfs(List<string> files)
        {
            tab.SelectedTab = partTab;
            Engine.UpdateDxfsLayers(files);
            UpdateLayersDialog();
            Engine.LoadDxfs(files, NestEngine.LoadType.PART);

            zoom = 1f;
            viewOffsetX = 0f;
            viewOffsetY = 0f;
            DrawPanel.Invalidate();
        }

        public void UpdateLayersDialog()
        {
            if (Engine.LayerItems.Count > 1)
            {
                Form layerForm = new Form()
                {
                    Width = 360,
                    Height = 180,
                    FormBorderStyle = FormBorderStyle.None,
                    Padding = new Padding(1),
                    StartPosition = FormStartPosition.CenterParent,
                    ShowInTaskbar = false
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
                    Height = 24,
                };
                button.Click += (ss, sea) => layerForm.Close();

                DataGridView grid = new DataGridView
                {
                    Dock = DockStyle.Top,
                    Height = layerForm.Height - button.Height,
                    BackgroundColor = SystemColors.Control,
                    ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    RowHeadersVisible = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                };

                layerForm.Controls.Add(grid);
                layerForm.Controls.Add(button);

                grid.DataSource = Engine.LayerItems;
                ReplaceEnumColumnsWithComboBoxes(grid, Engine.LayerItems);

                layerForm.ShowDialog(this);
            }
        }

        public List<List<CAD.Feature>> GetParts()
        {
            if (currentObj == null) return null;

            if (currentObj.GetType() == typeof(NestItem))
            {
                NestItem nest = (NestItem)currentObj;
                return nest.NestData.Concat(Engine.SheetItems[nest.SheetSource].Associated).ToList();
            }
            else if (currentObj.GetType() == typeof(PartItem))
            {
                PartItem part = (PartItem)currentObj;
                return new List<List<CAD.Feature>> { part.Features };
            }
            else if (currentObj.GetType() == typeof(SheetItem))
            {
                SheetItem sheet = (SheetItem)currentObj;
                return sheet.Associated;
            }

            return null;
        }

        private void IniEventsForm()
        {
            this.Resize += new EventHandler((s, ea) => { splitContainer.Invalidate(); });

            DrawPanel.Paint += new PaintEventHandler((s, e) =>
            {
                try
                {
                    Draw(e);
                }
                catch { }
            });

            DrawPanel.MouseWheel += new MouseEventHandler((s, ea) =>
            {
                float oldZoom = zoom;
                if (ea.Delta > 0)
                {
                    zoom *= 1.2f;
                }
                else
                {
                    zoom /= 1.2f;
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
                        posLabel.Text = "X0.0000" + "\r\n" + "Y0.0000";
                        return;
                    }

                    float offsetX = (DrawPanel.Width - (maxX - minX) * scale / zoom) / 2f;
                    float offsetY = (DrawPanel.Height - (maxY - minY) * scale / zoom) / 2f;
                    offsetX = viewOffsetX + (offsetX * zoom);
                    offsetY = viewOffsetY + (offsetY * zoom);
                    float modelX = (ea.X - offsetX) / scale + minX;
                    float modelY = -(ea.Y - offsetY) / scale + maxY;

                    posLabel.Text = "X" + modelX.ToString("#.0000") + "\r\n" +
                                    "Y" + modelY.ToString("#.0000");
                }
            });

            importPartButton.Click += new EventHandler((s, ea) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Multiselect = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    tab.SelectedTab = partTab;

                    Engine.UpdateDxfsLayers(dialog.FileNames.ToList());
                    UpdateLayersDialog();
                    Engine.LoadDxfs(dialog.FileNames.ToList(), NestEngine.LoadType.PART);

                    zoom = 1f;
                    viewOffsetX = 0f;
                    viewOffsetY = 0f;
                    DrawPanel.Invalidate();
                }
            });

            removePartButton.Click += new EventHandler((s, ea) =>
            {
                if (partGrid.SelectedRows.Count > 0)
                {
                    int index = partGrid.SelectedRows[0].Index;
                    string src = Engine.PartItems[index].SourceFileName;
                    Engine.PartItems.RemoveAt(index);

                    Engine.RestartNest();

                    zoom = 1f;
                    viewOffsetX = 0f;
                    viewOffsetY = 0f;
                    DrawPanel.Invalidate();
                }
            });

            clearPartsButton.Click += new EventHandler((s, ea) =>
            {
                Engine.ClearParts();
                //currentObj = null;
                Engine.RestartNest();
                partGrid.Invalidate();
                DrawPanel.Invalidate();
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

                    Engine.UpdateDxfsLayers(dialog.FileNames.ToList());
                    UpdateLayersDialog();
                    Engine.LoadDxfs(dialog.FileNames.ToList(), NestEngine.LoadType.SHEET);

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
                    Engine.SheetItems.RemoveAt(index);

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
                    Engine.SheetItems.RemoveAt(i);
                    i--;
                }

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
                    Engine.UpdateDxfsLayers(dialog.FileNames.ToList());
                    UpdateLayersDialog();
                    Engine.LoadNesting(dialog.FileNames.ToList());

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

                zoom = 1f;
                viewOffsetX = 0f;
                viewOffsetY = 0f;
                DrawPanel.Invalidate();
            });

            resetOptionsButton.Click += new EventHandler((s, ea) =>
            {
                Engine.Opts = new Options();
                CadOptionsPropertyUpdateAttributes(Engine.Opts);
                optionsGrid.SelectedObject = Engine.Opts;
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
                if (tab.SelectedTab == partTab || tab.SelectedTab == sheetTab || tab.SelectedTab == nestTab)
                {
                    zoom = 1f;
                    viewOffsetX = 0f;
                    viewOffsetY = 0f;
                    DrawPanel.Invalidate();
                }
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
                }
                else if (column.Name == "LX" || column.Name == "LY")
                {
                    if (!double.TryParse(ea.FormattedValue.ToString(), out double value) || value < 0)
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
                    Width = 320,
                    Height = 60,
                    FormBorderStyle = FormBorderStyle.None,
                    Padding = new Padding(1),
                    StartPosition = FormStartPosition.CenterParent,
                    ShowInTaskbar = false
                };
                progressForm.Paint += (ss, sea) =>
                {
                    using (Pen pen = new Pen(System.Drawing.Color.Black, 1))
                    {
                        sea.Graphics.DrawRectangle(pen, 0, 0, progressForm.ClientSize.Width - 1, progressForm.ClientSize.Height - 1);
                    }
                };

                var lbl = new Label()
                {
                    Text = "Nesting...",
                    Dock = DockStyle.Top,
                    Height = 20,
                    TextAlign = ContentAlignment.MiddleCenter
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
                    Height = 24
                };

                progressForm.Controls.Add(lbl);
                progressForm.Controls.Add(btnStop);
                progressForm.Controls.Add(bar);

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
                        while (!token.IsCancellationRequested)
                        {
                            Engine.Context.NestIterate(token);

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
                if (currentObj == null) return;

                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "Dxf Files (*.dxf)|*.dxf|All Files (*.*)|*.*";
                dialog.Title = "Save your file";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Engine.Export(currentObj, dialog.FileName);
                }
            });
        }

        private float zoom = 1f;
        private float scale = 1f;
        private float minX = 0f;
        private float minY = 0f;
        private float maxX = 0f;
        private float maxY = 0f;
        private float viewOffsetX = 0f;
        private float viewOffsetY = 0f;
        private bool isPanning = false;
        private float panStartMouseX;
        private float panStartMouseY;
        private float panStartOffsetX;
        private float panStartOffsetY;

        private void Draw(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            float width = DrawPanel.Width;
            float height = DrawPanel.Height;

            int rowIndex;
            if (tab.SelectedTab == partTab)
            {
                if (partGrid.CurrentRow != null)
                {
                    rowIndex = partGrid.CurrentRow.Index;
                    if (rowIndex >= 0) currentObj = Engine.PartItems[rowIndex];
                }
                else
                {
                    currentObj = null;
                }
            }
            else if (tab.SelectedTab == sheetTab)
            {
                if (sheetGrid.CurrentRow != null)
                {
                    rowIndex = sheetGrid.CurrentRow.Index;
                    if (rowIndex >= 0) currentObj = Engine.SheetItems[rowIndex];
                }
                else
                {
                    currentObj = null;
                }
            }
            else if (tab.SelectedTab == nestTab)
            {
                if (nestGrid.CurrentRow != null)
                {
                    rowIndex = nestGrid.CurrentRow.Index;
                    if (rowIndex >= 0) currentObj = Engine.NestItems[rowIndex];
                }
                else
                {
                    currentObj = null;
                }
            }

            if (currentObj == null)
            {
                using (Pen axisPen = new Pen(System.Drawing.Color.Gray, 1.5f))
                {
                    axisPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawLine(axisPen, -width, width / 2f, width, width / 2f);   // X axis
                    g.DrawLine(axisPen, height / 2f, -height, height / 2f, height);  // Y axis
                }
                return;
            };

            minX = 0f;
            minY = 0f;
            maxX = 0f;
            maxY = 0f;
            float sheetWidth = 0f;
            float sheetHeight = 0f;

            if (currentObj.GetType() == typeof(NestItem))
            {
                NestItem nest = (NestItem)currentObj;

                if (Engine.SheetItems[nest.SheetSource].Features.Any())
                {
                    foreach (CAD.Feature f in Engine.SheetItems[nest.SheetSource].Features)
                    {
                        foreach (CAD.Edge edge in f.Mesh.Edges)
                        {
                            minX = Math.Min(edge.V0.X, Math.Min(edge.V1.X, minX));
                            maxX = Math.Max(edge.V0.X, Math.Max(edge.V1.X, maxX));

                            minY = Math.Min(edge.V0.Y, Math.Min(edge.V1.Y, minY));
                            maxY = Math.Max(edge.V0.Y, Math.Max(edge.V1.Y, maxY));
                        }
                    }
                }
                else
                {
                    sheetWidth = (float)Engine.SheetItems[nest.SheetSource].LX;
                    sheetHeight = (float)Engine.SheetItems[nest.SheetSource].LY;
                    minX = 0f;
                    minY = 0f;
                    maxX = sheetWidth;
                    maxY = sheetHeight;
                }
            }
            else if (currentObj.GetType() == typeof(SheetItem))
            {
                SheetItem sheet = (SheetItem)currentObj;
                sheetWidth = (float)sheet.LX;
                sheetHeight = (float)sheet.LY;
                minX = 0f;
                minY = 0f;
                maxX = sheetWidth;
                maxY = sheetHeight;
            }
            else if (currentObj.GetType() == typeof(PartItem))
            {
                PartItem part = (PartItem)currentObj;

                foreach (CAD.Feature f in part.Features)
                {
                    foreach (CAD.Edge edge in f.Mesh.Edges)
                    {
                        minX = Math.Min(edge.V0.X, Math.Min(edge.V1.X, minX));
                        maxX = Math.Max(edge.V0.X, Math.Max(edge.V1.X, maxX));

                        minY = Math.Min(edge.V0.Y, Math.Min(edge.V1.Y, minY));
                        maxY = Math.Max(edge.V0.Y, Math.Max(edge.V1.Y, maxY));
                    }
                }
            }

            // --- SCALE AND OFFSET ---
            float modelWidth = maxX - minX;
            float modelHeight = maxY - minY;

            float scaleX = 0.9f * width / modelWidth;
            float scaleY = 0.9f * height / modelHeight;

            scale = Math.Min(scaleX, scaleY);
            float offsetX = (width - modelWidth * scale) / 2f;
            float offsetY = (height - modelHeight * scale) / 2f;
            scale *= zoom;
            offsetX = viewOffsetX + (offsetX * zoom);
            offsetY = viewOffsetY + (offsetY * zoom);

            if (currentObj.GetType() == typeof(PartItem))
            {
                // --- DRAW REFERENCE AXES ---
                using (Pen axisPen = new Pen(System.Drawing.Color.Gray, 1.5f))
                {
                    axisPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    float ox = offsetX + (-minX) * scale;
                    float oy = offsetY + (maxY) * scale;

                    g.DrawLine(axisPen, 0f, oy, width, oy);   // X axis
                    g.DrawLine(axisPen, ox, 0f, ox, height);  // Y axis
                }
            }
            else
            {
                // --- DRAW SHEET ---
                if (sheetWidth > 0 && sheetHeight > 0)
                {
                    using (Pen sheetPen = new Pen(System.Drawing.Color.DarkGray, 2f))
                    {
                        float sx = offsetX + (-minX) * scale;
                        float sy = offsetY + (maxY - sheetHeight) * scale;
                        g.DrawRectangle(sheetPen, sx, sy, sheetWidth * scale, sheetHeight * scale);
                    }
                }

                // --- DRAW REFERENCE AXES ---
                using (Pen axisPen = new Pen(System.Drawing.Color.Gray, 1.5f))
                {
                    axisPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    float ox = offsetX + (-minX) * scale;
                    float oy = offsetY + (maxY) * scale;

                    if (Engine.Opts.Origin == Options.OriginPosition.XY_MID ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MID_Y_MIN ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MID_Y_MAX)
                    {
                        ox = offsetX + (-minX + sheetWidth / 2f) * scale;
                    }

                    if (Engine.Opts.Origin == Options.OriginPosition.XY_MAX ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MAX_Y_MIN ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MAX_Y_MID)
                    {
                        ox = offsetX + (-minX + sheetWidth) * scale;
                    }

                    if (Engine.Opts.Origin == Options.OriginPosition.XY_MID ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MIN_Y_MID ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MAX_Y_MID)
                    {
                        oy = offsetY + (maxY - sheetHeight / 2f) * scale;
                    }

                    if (Engine.Opts.Origin == Options.OriginPosition.XY_MAX ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MIN_Y_MAX ||
                        Engine.Opts.Origin == Options.OriginPosition.X_MID_Y_MAX)
                    {
                        oy = offsetY + (maxY - sheetHeight) * scale;
                    }

                    g.DrawLine(axisPen, 0f, oy, width, oy);   // X axis
                    g.DrawLine(axisPen, ox, 0f, ox, height);  // Y axis
                }
            }

            // --- DRAW FEATURES ---

            if (currentObj.GetType() == typeof(NestItem))
            {
                NestItem nest = (NestItem)currentObj;

                if (Engine.NestItems.Any())
                {
                    float s = 1f;
                    System.Drawing.Color col = System.Drawing.Color.Gray;

                    foreach (CAD.Feature f in Engine.SheetItems[nest.SheetSource].Features)
                    {
                        if (f.Type == CAD.Feature.FeatureType.EXT || f.Type == CAD.Feature.FeatureType.INT) col = System.Drawing.Color.Blue;

                        using (Pen edgePen = new Pen(col, s))
                        {
                            GraphicsPath path = new GraphicsPath();

                            foreach (CAD.Edge edge in f.Mesh.Edges)
                            {
                                // Apply offset & scaling
                                float x0 = offsetX + (edge.V0.X - minX) * scale;
                                float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                                float x1 = offsetX + (edge.V1.X - minX) * scale;
                                float y1 = offsetY + (maxY - edge.V1.Y) * scale;

                                //g.DrawLine(edgePen, x0, y0, x1, y1);
                                path.AddLine(x0, y0, x1, y1);
                            }

                            g.DrawPath(edgePen, path);
                        }
                    }

                    foreach (List<CAD.Feature> part in nest.NestData)
                    {
                        foreach (CAD.Feature f in part.OrderByDescending(x => (int)x.Type))
                        {
                            if (f.Type == CAD.Feature.FeatureType.EXT || f.Type == CAD.Feature.FeatureType.INT) col = System.Drawing.Color.Black;

                            using (Pen edgePen = new Pen(col, s))
                            {
                                GraphicsPath path = new GraphicsPath();

                                foreach (CAD.Edge edge in f.Mesh.Edges)
                                {
                                    // Apply offset & scaling
                                    float x0 = offsetX + (edge.V0.X - minX) * scale;
                                    float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                                    float x1 = offsetX + (edge.V1.X - minX) * scale;
                                    float y1 = offsetY + (maxY - edge.V1.Y) * scale;

                                    //g.DrawLine(edgePen, x0, y0, x1, y1);
                                    path.AddLine(x0, y0, x1, y1);
                                }

                                g.DrawPath(edgePen, path);
                            }
                        }
                    }

                    foreach (List<CAD.Feature> part in Engine.SheetItems[nest.SheetSource].Associated)
                    {
                        foreach (CAD.Feature f in part.OrderByDescending(x => (int)x.Type))
                        {
                            if (f.Type == CAD.Feature.FeatureType.EXT || f.Type == CAD.Feature.FeatureType.INT) col = System.Drawing.Color.Blue;

                            using (Pen edgePen = new Pen(col, s))
                            {
                                GraphicsPath path = new GraphicsPath();

                                foreach (CAD.Edge edge in f.Mesh.Edges)
                                {
                                    // Apply offset & scaling
                                    float x0 = offsetX + (edge.V0.X - minX) * scale;
                                    float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                                    float x1 = offsetX + (edge.V1.X - minX) * scale;
                                    float y1 = offsetY + (maxY - edge.V1.Y) * scale;

                                    //g.DrawLine(edgePen, x0, y0, x1, y1);
                                    path.AddLine(x0, y0, x1, y1);
                                }

                                g.DrawPath(edgePen, path);
                            }
                        }
                    }
                }
            }
            else if (currentObj.GetType() == typeof(SheetItem))
            {
                SheetItem sheet = (SheetItem)currentObj;

                foreach (CAD.Feature f in sheet.Features)
                {
                    float s = 1f;
                    System.Drawing.Color col = System.Drawing.Color.Gray;
                    if (f.Type == CAD.Feature.FeatureType.EXT || f.Type == CAD.Feature.FeatureType.INT) col = System.Drawing.Color.Blue;

                    using (Pen edgePen = new Pen(col, s))
                    {
                        GraphicsPath path = new GraphicsPath();

                        foreach (CAD.Edge edge in f.Mesh.Edges)
                        {
                            // Apply offset & scaling
                            float x0 = offsetX + (edge.V0.X - minX) * scale;
                            float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                            float x1 = offsetX + (edge.V1.X - minX) * scale;
                            float y1 = offsetY + (maxY - edge.V1.Y) * scale;

                            //g.DrawLine(edgePen, x0, y0, x1, y1);
                            path.AddLine(x0, y0, x1, y1);
                        }

                        g.DrawPath(edgePen, path);
                    }
                }

                foreach (List<CAD.Feature> part in sheet.Associated)
                {
                    foreach (CAD.Feature f in part.OrderByDescending(x => (int)x.Type))
                    {
                        float s = 1f;
                        System.Drawing.Color col = System.Drawing.Color.Gray;

                        if (f.Type == CAD.Feature.FeatureType.EXT || f.Type == CAD.Feature.FeatureType.INT) col = System.Drawing.Color.Blue;

                        using (Pen edgePen = new Pen(col, s))
                        {
                            GraphicsPath path = new GraphicsPath();

                            foreach (CAD.Edge edge in f.Mesh.Edges)
                            {
                                // Apply offset & scaling
                                float x0 = offsetX + (edge.V0.X - minX) * scale;
                                float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                                float x1 = offsetX + (edge.V1.X - minX) * scale;
                                float y1 = offsetY + (maxY - edge.V1.Y) * scale;

                                //g.DrawLine(edgePen, x0, y0, x1, y1);
                                path.AddLine(x0, y0, x1, y1);
                            }

                            g.DrawPath(edgePen, path);
                        }
                    }
                }
            }
            else if (currentObj.GetType() == typeof(PartItem))
            {
                List<CAD.Feature> features = new List<CAD.Feature>();

                PartItem part = (PartItem)currentObj;
                features = part.Features;

                // DEBUG CTR SIMPLIFY
                foreach (CAD.Feature f in features)
                {
                    if ((f.Type == CAD.Feature.FeatureType.INT && CAD.GetArea(f.Mesh.Edges) > Math.Max(Engine.Opts.Spacing * Engine.Opts.Spacing, Engine.Opts.MinIntArea)) || f.Type == CAD.Feature.FeatureType.EXT)
                    {
                        List<CAD.Edge> simplified;
                        if (f.Type == CAD.Feature.FeatureType.INT)
                        {
                            simplified = CAD.SimplifyForNest(f.Mesh.Edges, false, true, Engine.Opts.Spacing, -1,
                                Math.PI / 4.0, Engine.Opts.NestArcSegmentsMaxLength, out double dbl, out bool pave);
                        }
                        else
                        {
                            simplified = CAD.SimplifyForNest(f.Mesh.Edges, false, false, Engine.Opts.Spacing, Engine.Opts.PaveLimit * 0.01,
                                Math.PI / 4.0, Engine.Opts.NestArcSegmentsMaxLength, out double dbl, out bool pave);
                        }

                        if (simplified != null)
                        {
                            System.Drawing.Color col = System.Drawing.Color.Red;
                            using (Pen edgePen = new Pen(col, 1f))
                            {
                                GraphicsPath path = new GraphicsPath();

                                foreach (CAD.Edge edge in simplified)
                                {
                                    // Apply offset & scaling
                                    float x0 = offsetX + (edge.V0.X - minX) * scale;
                                    float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                                    float x1 = offsetX + (edge.V1.X - minX) * scale;
                                    float y1 = offsetY + (maxY - edge.V1.Y) * scale;

                                    //g.DrawLine(edgePen, x0, y0, x1, y1);
                                    path.AddLine(x0, y0, x1, y1);
                                }

                                g.DrawPath(edgePen, path);
                            }
                        }
                    }
                }

                foreach (CAD.Feature f in features.OrderByDescending(x => (int)x.Type))
                {
                    float nodeSize = 3f;
                    using (SolidBrush nodeBrush = new SolidBrush(System.Drawing.Color.Black))
                    {
                        if (f.Type == CAD.Feature.FeatureType.EXT || f.Type == CAD.Feature.FeatureType.INT)
                        {
                            for (int i = 0; i < f.Mesh.Edges.Count; i++)
                            {
                                float x0 = offsetX + (f.Mesh.Edges[i].V0.X - minX) * scale;
                                float y0 = offsetY + (maxY - f.Mesh.Edges[i].V0.Y) * scale;
                                g.FillRectangle(nodeBrush, x0 - nodeSize / 2f, y0 - nodeSize / 2f, nodeSize, nodeSize);
                                if (f.Mesh.Edges[i].R > Engine.Opts.Tol0) i += f.Mesh.Edges[i].NS - 1;
                            }
                        }
                    }

                    float s = 1f;
                    System.Drawing.Color col = System.Drawing.Color.Gray;
                    if (f.Type == CAD.Feature.FeatureType.EXT || f.Type == CAD.Feature.FeatureType.INT) col = System.Drawing.Color.Black;

                    using (Pen edgePen = new Pen(col, s))
                    {
                        GraphicsPath path = new GraphicsPath();

                        foreach (CAD.Edge edge in f.Mesh.Edges)
                        {
                            // Apply offset & scaling
                            float x0 = offsetX + (edge.V0.X - minX) * scale;
                            float y0 = offsetY + (maxY - edge.V0.Y) * scale;
                            float x1 = offsetX + (edge.V1.X - minX) * scale;
                            float y1 = offsetY + (maxY - edge.V1.Y) * scale;

                            //g.DrawLine(edgePen, x0, y0, x1, y1);
                            path.AddLine(x0, y0, x1, y1);
                        }

                        g.DrawPath(edgePen, path);
                    }
                }
            }
        }

        private static void CadOptionsPropertyUpdateAttributes(Options opts)
        {
            //PropertyOverridingTypeDescriptor ctd = new PropertyOverridingTypeDescriptor(TypeDescriptor.GetProvider(opts).GetTypeDescriptor(opts));
            //foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(opts))
            //{
            //    List<Attribute> attributes = new List<Attribute>();

            //    if (pd.Name == nameof(opts.MergeLayers))
            //    {
            //        attributes.Add(new EditorAttribute(typeof(CheckEditor), typeof(UITypeEditor)));
            //    }

            //    if (pd.Name == nameof(opts.Margins) ||
            //        pd.Name == nameof(opts.Spacing) ||
            //        pd.Name == nameof(opts.DefaultWidth) ||
            //        pd.Name == nameof(opts.DefaultHeight) ||
            //        pd.Name == nameof(opts.MinIntArea) ||
            //        pd.Name == nameof(opts.Tol0) ||
            //        pd.Name == nameof(opts.LinkDist) ||
            //        pd.Name == nameof(opts.NestArcSegmentsMaxLength))
            //    {
            //        attributes.Add(new TypeConverterAttribute(typeof(PositiveDoubleTypeConverter)));
            //    }

            //    if (pd.Name == nameof(opts.DefaultQty) ||
            //        pd.Name == nameof(opts.PaveLimit) ||
            //        pd.Name == nameof(opts.MutationRate) ||
            //        pd.Name == nameof(opts.PopulationSize))
            //    {
            //        attributes.Add(new TypeConverterAttribute(typeof(PositiveIntegerTypeConverter)));
            //    }

            //    if (attributes.Count > 0)
            //    {
            //        PropertyDescriptor pdNew = TypeDescriptor.CreateProperty(opts.GetType(), pd, attributes.ToArray());
            //        ctd.OverrideProperty(pdNew);
            //    }
            //}

            //TypeDescriptor.AddProvider(new TypeDescriptorOverridingProvider(ctd), opts);
        }

        private void splitContainer_Paint(object sender, PaintEventArgs e)
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

        private void splitContainer_SplitterMoved(object sender, SplitterEventArgs e)
        {
            var control = sender as SplitContainer;

            if (!control.IsHandleCreated) { return; }

            if (control.CanFocus)
            {
                control.ActiveControl = control.Panel1;
            }
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
    }
}
