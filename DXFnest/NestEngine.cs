using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp;
using DeepNestLib;
using NCnetic.Cam;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using static NCnetic.Cam.CAD;
using ACadSharp.Tables;
using CSMath;
using ACadSharp.XData;
using ACadSharp.Objects;
using static NCnetic.Nest.NestEngine;

namespace NCnetic.Nest
{
    public class NestEngine
    {
        public Options Opts;
        public BindingList<PartItem> PartItems = new BindingList<PartItem>();
        public BindingList<SheetItem> SheetItems = new BindingList<SheetItem>();
        public BindingList<NestItem> NestItems = new BindingList<NestItem>();
        public BindingList<LayerItem> LayerItems = new BindingList<LayerItem>();
        public NestingContext Context;
        public double CurrentFitness;

        public enum LoadType { PART, SHEET, NEST }

        public BindingList<LayerItem> GetDxfsLayers(List<string> files)
        {
            BindingList<LayerItem> layers = new BindingList<LayerItem>();

            foreach (string file in files)
            {
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DxfReader reader = new DxfReader(fs))
                {
                    List<Feature> dxfFeatures = ExtractFeatures(reader.Read());

                    foreach (Feature f in dxfFeatures)
                    {
                        if (layers.ToList().Find(x => x.Name == f.Tag) == null)
                        {
                            if (LayerItems.ToList().Find(x => x.Name == f.Tag) == null)
                            {
                                //int nb = 0;
                                //int.TryParse(f.Tag, out nb);
                                //if (nb < 0) nb = 0;

                                LayerItems.Add(new LayerItem()
                                {
                                    Name = f.Tag,
                                    Type = LayerItem.LayerType.CUTTING_CTR,
                                    //ToolNb = nb,
                                });
                            }
                            else
                            {
                                layers.Add(LayerItems.ToList().Find(x => x.Name == f.Tag));
                            }
                        }
                    }
                }
            }

            return layers;
        }

        public void UpdateDxfsLayers(BindingList<LayerItem> layers)
        {
            foreach (LayerItem layer in layers.ToList())
            {
                if (LayerItems.ToList().Find(x => x.Name == layer.Name) == null)
                {
                    LayerItems.Add(layer);
                }
                else
                {
                    int id = LayerItems.ToList().FindIndex(x => x.Name == layer.Name);
                    LayerItems[id] = layer;
                }
            }
        }

        public void LoadDxfs(List<string> files, LoadType loadtype)
        {
            foreach (string file in files)
            {
                List<List<Feature>> added = new List<List<Feature>>();

                if (Path.GetExtension(file).ToLower() == ".dxf")
                {
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (DxfReader reader = new DxfReader(fs))
                    {
                        List<Feature> dxfFeatures = ExtractFeatures(reader.Read());

                        foreach (Feature f in dxfFeatures)
                        {
                            f.Source = file;

                            if (LayerItems.ToList().Find(x => x.Name == f.Tag) == null)
                            {
                                LayerItems.Add(new LayerItem()
                                {
                                    Name = f.Tag,
                                    Type = LayerItem.LayerType.CUTTING_CTR,
                                });
                            }
                        }

                        List<Feature> ImportFeatures = new List<Feature>();
                        foreach (Feature f in dxfFeatures)
                        {
                            LayerItem layer = LayerItems.ToList().Find(x => x.Name == f.Tag);
                            if (layer != null)
                            {
                                //f.ToolNb = layer.ToolNb;
                                if (layer.Type == LayerItem.LayerType.CUTTING_CTR)
                                {
                                    ImportFeatures.Add(f);
                                }
                                else if (layer.Type == LayerItem.LayerType.MARKING_CTR)
                                {
                                    f.ImportType = Feature.ImportDocType.NO_CUT_ENTITY;
                                    ImportFeatures.Add(f);
                                }
                            }
                        }

                        added.AddRange(ExtractParts(ImportFeatures, Opts.Tol0, Opts.LinkDist, Opts.MergeLayers, false, 
                            Opts.ToolNbFromLayerName, Opts.DefaultCutToolNb, Opts.DefaultMrkToolNb));
                    }
                }

                List<SheetAssociation> associations = new List<SheetAssociation>();

                for (int i = 0; i < added.Count; i++)
                {
                    float minX = float.MaxValue;
                    float minY = float.MaxValue;
                    float maxX = float.MinValue;
                    float maxY = float.MinValue;
                    foreach (Feature f in added[i])
                    {
                        foreach (Edge edge in f.Edges)
                        {
                            minX = Math.Min(edge.V0.X, Math.Min(edge.V1.X, minX));
                            maxX = Math.Max(edge.V0.X, Math.Max(edge.V1.X, maxX));

                            minY = Math.Min(edge.V0.Y, Math.Min(edge.V1.Y, minY));
                            maxY = Math.Max(edge.V0.Y, Math.Max(edge.V1.Y, maxY));
                        }
                    }

                    if (loadtype == LoadType.SHEET)
                    {
                        float dx = -minX;
                        float dy = -minY;

                        foreach (Feature f in added[i])
                        {
                            foreach (Edge edge in f.Edges)
                            {
                                edge.V0.X += dx;
                                edge.V0.Y += dy;
                                edge.V1.X += dx;
                                edge.V1.Y += dy;
                            }
                        }

                        SheetItems.Add(new SheetItem
                        {
                            LX = maxX - minX,
                            LY = maxY - minY,
                            UsedQty = 0,
                            IniQty = 1,
                            Features = added[i],
                        });
                    }
                    else if (loadtype == LoadType.PART || loadtype == LoadType.NEST)
                    {
                        if (loadtype == LoadType.NEST) 
                        {
                            bool merge = false;
                            if (Opts.MultiplicityMerge)
                            {
                                for (int j = 0; j < PartItems.Count; j++) // (PartItem p in PartItems)
                                {
                                    if (PartItems[j].SheetId == SheetItems.Count && !merge)
                                    {
                                        Transform2d t = GetTransform(PartItems[j].Features, added[i], Opts.MultiplicityTol);
                                        if (t != null)
                                        {
                                            PartItems[j].UsedQty += 1;
                                            PartItems[j].IniQty += 1;

                                            associations.Add(new SheetAssociation
                                            {
                                                partId = j,
                                                T = t,
                                            });

                                            merge = true;
                                        }
                                    }
                                }
                            }

                            if (!merge)
                            {
                                float dx = 0f;
                                float dy = 0f;

                                if (Opts.NewPartOrigin)
                                {
                                    dx = -minX;
                                    dy = -minY;

                                    if (Opts.Origin == Options.OriginPosition.XY_MID)
                                    {
                                        dx = -(maxX + minX) / 2f;
                                        dy = -(maxY + minY) / 2f;
                                    }

                                    if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                                        Opts.Origin == Options.OriginPosition.X_MAX_Y_MIN)
                                    {
                                        dx = -maxX;
                                    }

                                    if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                                        Opts.Origin == Options.OriginPosition.X_MIN_Y_MAX)
                                    {
                                        dy = -maxY;
                                    }

                                    foreach (Feature f in added[i])
                                    {
                                        foreach (Edge edge in f.Edges)
                                        {
                                            edge.V0.X += dx;
                                            edge.V0.Y += dy;
                                            edge.V1.X += dx;
                                            edge.V1.Y += dy;
                                        }
                                    }
                                }

                                associations.Add(new SheetAssociation
                                {
                                    partId = PartItems.Count,
                                    T = new Transform2d(-dx, -dy, 0.0),
                                });

                                PartItems.Add(new PartItem
                                {
                                    SheetId = SheetItems.Count,
                                    Name = Path.GetFileNameWithoutExtension(file) + "_" + i.ToString(),
                                    UsedQty = 1,
                                    IniQty = 1,
                                    Features = added[i],
                                });
                            }
                        }
                        else
                        {
                            bool merge = false;
                            if (Opts.MultiplicityMerge)
                            {
                                foreach (PartItem p in PartItems)
                                {
                                    if (p.SheetId == -1 && !merge)
                                    {
                                        Transform2d t = GetTransform(p.Features, added[i], Opts.MultiplicityTol);
                                        if (t != null)
                                        {
                                            p.IniQty += 1;
                                            merge = true;
                                        }
                                    }
                                }
                            }

                            if (!merge)
                            {
                                float dx = 0f;
                                float dy = 0f;

                                if (Opts.NewPartOrigin)
                                {
                                    dx = -minX;
                                    dy = -minY;

                                    if (Opts.Origin == Options.OriginPosition.XY_MID)
                                    {
                                        dx = -(maxX + minX) / 2f;
                                        dy = -(maxY + minY) / 2f;
                                    }

                                    if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                                        Opts.Origin == Options.OriginPosition.X_MAX_Y_MIN)
                                    {
                                        dx = -maxX;
                                    }

                                    if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                                        Opts.Origin == Options.OriginPosition.X_MIN_Y_MAX)
                                    {
                                        dy = -maxY;
                                    }

                                    foreach (Feature f in added[i])
                                    {
                                        foreach (Edge edge in f.Edges)
                                        {
                                            edge.V0.X += dx;
                                            edge.V0.Y += dy;
                                            edge.V1.X += dx;
                                            edge.V1.Y += dy;
                                        }
                                    }
                                }

                                PartItems.Add(new PartItem
                                {
                                    SheetId = -1,
                                    Name = Path.GetFileNameWithoutExtension(file) + "_" + i.ToString(),
                                    UsedQty = 0,
                                    IniQty = 1,
                                    Features = added[i],
                                });
                            }
                        }
                    }
                }

                if (loadtype == LoadType.NEST)
                {
                    SheetItems.Add(new SheetItem
                    {
                        Features = new List<Feature>(),
                        Associated = associations,
                        LX = Opts.DefaultWidth,
                        LY = Opts.DefaultHeight,
                        IniQty = 1,
                        UsedQty = 0,
                    });
                }
            }

            RestartNest();
        }
        
        public void LoadDoc(CadDocument doc)
        {
            List<List<Feature>> parts = ExtractParts(ExtractFeatures(doc), Opts.Tol0, Opts.LinkDist, Opts.MergeLayers, false,
                Opts.ToolNbFromLayerName, Opts.DefaultCutToolNb, Opts.DefaultMrkToolNb);

            for (int i = 0; i < parts.Count; i++)
            {
                if (Opts.NewPartOrigin)
                {
                    float minX = float.MaxValue;
                    float minY = float.MaxValue;
                    float maxX = float.MinValue;
                    float maxY = float.MinValue;
                    foreach (Feature f in parts[i])
                    {
                        foreach (Edge edge in f.Edges)
                        {
                            minX = Math.Min(edge.V0.X, Math.Min(edge.V1.X, minX));
                            maxX = Math.Max(edge.V0.X, Math.Max(edge.V1.X, maxX));

                            minY = Math.Min(edge.V0.Y, Math.Min(edge.V1.Y, minY));
                            maxY = Math.Max(edge.V0.Y, Math.Max(edge.V1.Y, maxY));
                        }

                        float dx = -minX;
                        float dy = -minY;

                        if (Opts.Origin == Options.OriginPosition.XY_MID)
                        {
                            dx = -(maxX + minX) / 2f;
                            dy = -(maxY + minY) / 2f;
                        }

                        if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                            Opts.Origin == Options.OriginPosition.X_MAX_Y_MIN)
                        {
                            dx = -maxX;
                        }

                        if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                            Opts.Origin == Options.OriginPosition.X_MIN_Y_MAX)
                        {
                            dy = -maxY;
                        }

                        foreach (Edge edge in f.Edges)
                        {
                            edge.V0.X += dx;
                            edge.V0.Y += dy;
                            edge.V1.X += dx;
                            edge.V1.Y += dy;
                        }
                    }
                }

                PartItems.Add(new PartItem
                {
                    Name = "SCRIPT" + "_" + PartItems.Count.ToString(),
                    UsedQty = 0,
                    IniQty = 1,
                    Features = parts[i],
                });
            }

            RestartNest();
        }

        public List<Feature> ExtractFeatures(CadDocument doc)
        {
            List<Feature> dxfData = new List<Feature>();

            if (doc.Entities == null) return dxfData;

            Edge edge;
            foreach (Entity entity in doc.Entities)
            {
                string tag = entity.Layer.Name;

                BevelDefinition bevel = new BevelDefinition();
                AppId appId = doc.AppIds.FirstOrDefault(a => a.Name == "BEVEL");
                if (appId != null)
                {
                    if (entity.ExtendedData.TryGet(appId, out ExtendedData xdata))
                    {
                        bevel = new BevelDefinition();

                        if (xdata.Records.Count > 0)
                        {
                            ExtendedDataString rec0 = xdata.Records[0] as ExtendedDataString;
                            if (rec0 != null) bevel.Type = (BevelType)Enum.Parse(typeof(BevelType), (string)rec0.Value);
                        }
                        if (xdata.Records.Count > 1)
                        {
                            ExtendedDataReal rec1 = xdata.Records[1] as ExtendedDataReal;
                            if (rec1 != null) bevel.A = (double)rec1.Value;
                        }
                        if (xdata.Records.Count > 2)
                        {
                            ExtendedDataReal rec2 = xdata.Records[2] as ExtendedDataReal;
                            if (rec2 != null) bevel.Hr = (double)rec2.Value;
                        }
                        if (xdata.Records.Count > 3)
                        {
                            ExtendedDataReal rec3 = xdata.Records[3] as ExtendedDataReal;
                            if (rec3 != null) bevel.B = (double)rec3.Value;
                        }
                        if (xdata.Records.Count > 4)
                        {
                            ExtendedDataReal rec4 = xdata.Records[4] as ExtendedDataReal;
                            if (rec4 != null) bevel.r = (double)rec4.Value;
                        }
                    }
                }

                //int tool = 0;
                //if (LayerItems.ToList().Find(x => x.Name == tag) != null)
                //{
                //    tool = LayerItems.ToList().Find(x => x.Name == tag).ToolNb;
                //}

                switch (entity.ObjectType)
                {
                    case ObjectType.POINT:
                        ACadSharp.Entities.Point pt = (ACadSharp.Entities.Point)entity;
                        break;

                    case ObjectType.LINE:
                        ACadSharp.Entities.Line line = (ACadSharp.Entities.Line)entity;

                        edge = new CAD.Edge(line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y);
                        edge.Bevel = bevel;
                        dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.OPEN_ENTITY));
                        dxfData.Last().Edges.Add(edge);
                        break;

                    case ObjectType.CIRCLE:
                        ACadSharp.Entities.Circle circle = (ACadSharp.Entities.Circle)entity;
                        dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.CLOSED_ENTITY));
                        //dxfData.Last().Edges.AddRange(CAD.GetArcSegments(
                        //    circle.Center.X, circle.Center.Y, circle.Radius, 0, Math.PI / 2.0, Options.angleStep, Options.maxStepL));
                        //dxfData.Last().Edges.AddRange(CAD.GetArcSegments(
                        //    circle.Center.X, circle.Center.Y, circle.Radius, Math.PI / 2.0, Math.PI, Options.angleStep, Options.maxStepL));
                        //dxfData.Last().Edges.AddRange(CAD.GetArcSegments(
                        //    circle.Center.X, circle.Center.Y, circle.Radius, Math.PI, 3.0 * Math.PI / 2.0, Options.angleStep, Options.maxStepL));
                        //dxfData.Last().Edges.AddRange(CAD.GetArcSegments(
                        //    circle.Center.X, circle.Center.Y, circle.Radius, 3.0 * Math.PI / 2.0, 2 * Math.PI, Options.angleStep, Options.maxStepL));

                        dxfData.Last().Edges.AddRange(CAD.GetArcSegments(
                            circle.Center.X, circle.Center.Y, circle.Radius, 0, 2.0 * Math.PI, Options.angleStep, Options.maxStepL));
                        foreach (Edge e in dxfData.Last().Edges) e.Bevel = bevel;
                        break;

                    case ObjectType.ARC:
                        ACadSharp.Entities.Arc arc = (ACadSharp.Entities.Arc)entity;
                        dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.OPEN_ENTITY));
                        double a0 = arc.StartAngle;
                        double a1 = arc.EndAngle;
                        if (a0 > a1)
                        {
                            a0 -= 2.0 * Math.PI;
                        }
                        dxfData.Last().Edges.AddRange(CAD.GetArcSegments(
                            arc.Center.X, arc.Center.Y, arc.Radius, a0, a1, Options.angleStep, Options.maxStepL));
                        foreach (Edge e in dxfData.Last().Edges) e.Bevel = bevel;
                        break;

                    case ObjectType.ELLIPSE:
                        ACadSharp.Entities.Ellipse ellipse = (ACadSharp.Entities.Ellipse)entity;
                        break;

                    case ObjectType.LWPOLYLINE:
                        ACadSharp.Entities.LwPolyline lwpoly = (ACadSharp.Entities.LwPolyline)entity;
                        if (lwpoly.Vertices.Count <= 1)
                        {
                            break;
                        }

                        List<CAD.Edge> lwpoly_crt = new List<CAD.Edge>();
                        for (int i = 1; i < lwpoly.Vertices.Count; i++)
                        {
                            double bulge = lwpoly.Vertices[i - 1].Bulge;
                            if (Math.Abs(bulge) < Opts.Tol0)
                            {
                                edge = new CAD.Edge(
                                    lwpoly.Vertices[i - 1].Location.X,
                                    lwpoly.Vertices[i - 1].Location.Y,
                                    lwpoly.Vertices[i].Location.X,
                                    lwpoly.Vertices[i].Location.Y);
                                lwpoly_crt.Add(edge);
                            }
                            else
                            {
                                lwpoly_crt.AddRange(CAD.GetBulgeSegments(bulge,
                                    lwpoly.Vertices[i - 1].Location.X, lwpoly.Vertices[i - 1].Location.Y,
                                    lwpoly.Vertices[i].Location.X, lwpoly.Vertices[i].Location.Y, Options.angleStep, Options.maxStepL));
                            }
                        }

                        double end_bulge_lwpoly = lwpoly.Vertices[lwpoly.Vertices.Count - 1].Bulge;
                        edge = new CAD.Edge(
                                lwpoly_crt.Last().V1.X,
                                lwpoly_crt.Last().V1.Y,
                                lwpoly.Vertices[0].Location.X,
                                lwpoly.Vertices[0].Location.Y);
                        if (Math.Sqrt(Math.Pow(edge.V0.X - edge.V1.X, 2) + Math.Pow(edge.V0.Y - edge.V1.Y, 2)) > Opts.Tol0)
                        {
                            if (lwpoly.IsClosed)
                            {
                                if (Math.Abs(end_bulge_lwpoly) < Opts.Tol0)
                                {
                                    lwpoly_crt.Add(edge);
                                }
                                else
                                {
                                    lwpoly_crt.AddRange(CAD.GetBulgeSegments(end_bulge_lwpoly,
                                        edge.V0.X, edge.V0.Y, edge.V1.X, edge.V1.Y, Options.angleStep, Options.maxStepL));
                                }

                                dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.CLOSED_ENTITY));
                                dxfData.Last().Edges.AddRange(lwpoly_crt);
                            }
                            else
                            {
                                dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.OPEN_ENTITY));
                                dxfData.Last().Edges.AddRange(lwpoly_crt);
                            }
                        }
                        else
                        {
                            dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.CLOSED_ENTITY));
                            dxfData.Last().Edges.AddRange(lwpoly_crt);
                        }
                        foreach (Edge e in dxfData.Last().Edges) e.Bevel = bevel;
                        break;

                    case ObjectType.POLYLINE_2D:
                        ACadSharp.Entities.Polyline2D poly2 = (ACadSharp.Entities.Polyline2D)entity;
                        if (poly2.Vertices.Count <= 1)
                        {
                            break;
                        }

                        List<CAD.Edge> poly2_crt = new List<CAD.Edge>();
                        for (int i = 1; i < poly2.Vertices.Count; i++)
                        {
                            double bulge = poly2.Vertices[i - 1].Bulge;
                            if (Math.Abs(bulge) < Opts.Tol0)
                            {
                                edge = new CAD.Edge(
                                    poly2.Vertices[i - 1].Location.X,
                                    poly2.Vertices[i - 1].Location.Y,
                                    poly2.Vertices[i].Location.X,
                                    poly2.Vertices[i].Location.Y);
                                poly2_crt.Add(edge);
                            }
                            else
                            {
                                poly2_crt.AddRange(CAD.GetBulgeSegments(bulge,
                                    poly2.Vertices[i - 1].Location.X, poly2.Vertices[i - 1].Location.Y,
                                    poly2.Vertices[i].Location.X, poly2.Vertices[i].Location.Y, Options.angleStep, Options.maxStepL));
                            }
                        }

                        double end_bulge_poly2 = poly2.Vertices[poly2.Vertices.Count - 1].Bulge;
                        edge = new CAD.Edge(
                            poly2_crt.Last().V1.X,
                            poly2_crt.Last().V1.Y,
                            poly2.Vertices[0].Location.X,
                            poly2.Vertices[0].Location.Y);

                        if (Math.Sqrt(Math.Pow(edge.V0.X - edge.V1.X, 2) + Math.Pow(edge.V0.Y - edge.V1.Y, 2)) > Opts.Tol0)
                        {
                            if (poly2.IsClosed)
                            {
                                if (Math.Abs(end_bulge_poly2) < Opts.Tol0)
                                {
                                    poly2_crt.Add(edge);
                                }
                                else
                                {
                                    poly2_crt.AddRange(CAD.GetBulgeSegments(end_bulge_poly2,
                                        edge.V0.X, edge.V0.Y, edge.V1.X, edge.V1.Y, Options.angleStep, Options.maxStepL));
                                }

                                dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.CLOSED_ENTITY));
                                dxfData.Last().Edges.AddRange(poly2_crt);
                            }
                            else
                            {
                                dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.OPEN_ENTITY));
                                dxfData.Last().Edges.AddRange(poly2_crt);
                            }
                        }
                        else
                        {
                            dxfData.Add(new CAD.Feature(tag, CAD.Feature.ImportDocType.CLOSED_ENTITY));
                            dxfData.Last().Edges.AddRange(poly2_crt);
                        }
                        foreach (Edge e in dxfData.Last().Edges) e.Bevel = bevel;
                        break;

                    case ObjectType.SPLINE:
                        ACadSharp.Entities.Spline spline = (ACadSharp.Entities.Spline)entity;
                        // TODO => BI-ARCS TO IMPORT INKSCAPE SPLINES (NO FLATTEN BEZIERS) 
                        break;

                    case ObjectType.POLYLINE_3D:
                        ACadSharp.Entities.Polyline3D poly3 = (ACadSharp.Entities.Polyline3D)entity;
                        break;

                    case ObjectType.TEXT:
                        break;

                    case ObjectType.MTEXT:
                        break;

                    default:
                        break;
                }
            }

            return dxfData;
        }

        public void RestartNest()
        {
            CurrentFitness = double.MaxValue;

            DeepNestLib.Nest.Config.placementType = PlacementTypeEnum.gravity;
            DeepNestLib.Nest.Config.spacing = Opts.Spacing;
            DeepNestLib.Nest.Config.sheetSpacing = Opts.Margins;
            //DeepNestLib.Nest.Config.populationSize = Opts.PopulationSize;
            //DeepNestLib.Nest.Config.MutationRate = Opts.MutationRate * 0.01;

            DeepNestLib.Nest.Config.populationSize = Options.PopulationSize;
            DeepNestLib.Nest.Config.MutationRate = Options.MutationRate * 0.01;

            Context = new NestingContext();

            for (int i = 0; i < SheetItems.Count; i++)
            {
                NFP nfpSheet = new NFP();
                bool extDefined = true;
                if (SheetItems[i].Features.Any())
                {
                    extDefined = false;
                    List<Feature> dxfSheet = SheetItems[i].Features;

                    foreach (Feature f in dxfSheet)
                    {
                        List<Edge> simplified;
                        if (f.Type == Feature.FeatureType.EXT)
                        {
                            extDefined = true;
                            simplified = SimplifyForNest(f.Edges, true, true, Opts.Spacing, -1,
                                Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                            foreach (Edge edge in simplified)
                            {
                                nfpSheet.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                            }
                        }
                        if (f.Type == Feature.FeatureType.INT)
                        {
                            simplified = SimplifyForNest(f.Edges, false, true, Opts.Spacing, -1,
                                Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                            if (simplified != null)
                            {
                                if (nfpSheet.children == null) nfpSheet.children = new List<NFP>();
                                NFP nfpHole = new NFP();
                                foreach (Edge edge in simplified)
                                {
                                    nfpHole.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                                }
                                nfpSheet.children.Add(nfpHole);
                            }
                        }
                    }
                }
                else
                {
                    nfpSheet.AddPoint(new DeepNestLib.Point(0, 0));
                    nfpSheet.AddPoint(new DeepNestLib.Point(SheetItems[i].LX, 0));
                    nfpSheet.AddPoint(new DeepNestLib.Point(SheetItems[i].LX, SheetItems[i].LY));
                    nfpSheet.AddPoint(new DeepNestLib.Point(0, SheetItems[i].LY));
                }

                foreach (SheetAssociation sa in SheetItems[i].Associated)
                {
                    NFP nfpAssociated = new NFP();
                    bool associatedExtDefined = false;
                    bool associatedExtpave = false;

                    foreach (Feature f in RotoTranslatePartXY(PartItems[sa.partId].Features, sa.T.X, sa.T.Y, sa.T.Rot))
                    {
                        List<Edge> simplified;
                        if (f.Type == Feature.FeatureType.EXT)
                        {
                            associatedExtDefined = true;
                            if (nfpSheet.children == null) nfpSheet.children = new List<NFP>();
                            simplified = SimplifyForNest(f.Edges, false, false, Opts.Spacing, Opts.PaveLimit * 0.01,
                                Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out associatedExtpave);
                            foreach (Edge edge in simplified)
                            {
                                nfpAssociated.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                            }
                        }
                        if (f.Type == Feature.FeatureType.INT && GetArea(f.Edges) > Math.Max(Opts.Spacing * Opts.Spacing, Opts.MinIntArea))
                        {
                            simplified = SimplifyForNest(f.Edges, false, true, Opts.Spacing, -1,
                                Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                            if (simplified != null)
                            {
                                if (nfpAssociated.children == null) nfpAssociated.children = new List<NFP>();
                                NFP nfpHole = new NFP();
                                foreach (Edge edge in simplified)
                                {
                                    nfpHole.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                                }
                                nfpAssociated.children.Add(nfpHole);
                            }
                        }
                    }

                    if (associatedExtDefined)
                    {
                        nfpSheet.children.Add(nfpAssociated);
                    }
                }

                if (extDefined)
                {
                    Context.AddSheet(nfpSheet, SheetItems[i].IniQty);
                }
            }

            for (int i = 0; i < PartItems.Count; i++)
            {
                NFP nfpPart = new NFP();
                bool extDefined = false;
                bool extpave = false;
                double extMinHrot = 0.0;

                List<Feature> dxfPart = PartItems[i].Features;

                foreach (Feature f in dxfPart)
                {
                    List<Edge> simplified;
                    if (f.Type == Feature.FeatureType.EXT)
                    {
                        extDefined = true;
                        simplified = SimplifyForNest(f.Edges, false, false, Opts.Spacing, Opts.PaveLimit * 0.01,
                            Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out extMinHrot, out extpave);
                        foreach (Edge edge in simplified)
                        {
                            nfpPart.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                        }
                    }
                    if (f.Type == Feature.FeatureType.INT && GetArea(f.Edges) > Math.Max(Opts.Spacing * Opts.Spacing, Opts.MinIntArea))
                    {
                        simplified = SimplifyForNest(f.Edges, false, true, Opts.Spacing, -1,
                            Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                        if (simplified != null)
                        {
                            if (nfpPart.children == null) nfpPart.children = new List<NFP>();
                            NFP nfpHole = new NFP();
                            foreach (Edge edge in simplified)
                            {
                                nfpHole.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                            }
                            nfpPart.children.Add(nfpHole);
                        }
                    }
                }

                if (extDefined)
                {
                    extMinHrot = extMinHrot * 180.0 / Math.PI;
                    EnabledRotations rots = EnabledRotations.NONE;

                    switch (Opts.PartRotations)
                    {
                        case Options.Rotations.NONE:
                            rots = EnabledRotations.NONE;
                            break;

                        case Options.Rotations.BY_180:
                            if (extpave)
                            {
                                rots = EnabledRotations.NONE;
                            }
                            else
                            {
                                rots = EnabledRotations.BY_180;
                            }
                            break;

                        case Options.Rotations.BY_90:
                            rots = EnabledRotations.BY_90;
                            break;

                        case Options.Rotations.ANY:
                            if (extpave)
                            {
                                rots = EnabledRotations.PAVE_0_90;
                            }
                            else
                            {
                                rots = EnabledRotations.ANY;
                            }
                            break;
                    }

                    if (PartItems[i].SheetId == -1)
                    {
                        Context.AddPart(nfpPart, PartItems[i].IniQty, rots, extMinHrot);
                    }
                    else
                    {
                        Context.AddPart(nfpPart, 0, rots, extMinHrot);
                    }
                }
            }

            UpdateNestResults();
        }

        public void UpdateNestResults()
        {
            for (int i = 0; i < NestItems.Count; i++)
            {
                NestItems.RemoveAt(i);
                i--;
            }

            if (Context.Nest == null) return;

            foreach (PartItem item in PartItems)
            {
                if (item.SheetId == -1)
                {
                    item.UsedQty = 0;
                }
                else
                {
                    item.IniQty = item.UsedQty;
                }
            }
            foreach (SheetItem item in SheetItems)
            {
                item.UsedQty = 0;
            }

            if (Context.Nest.nests != null)
            {
                SheetPlacement result = Context.Nest.nests;
                foreach (SheetPlacementItem nest in result.placements.First())
                {
                    NestItems.Add(new NestItem
                    {
                        SheetSource = nest.sheetSource,
                        Name = "NEST_" + NestItems.Count,
                        NestData = new List<List<Feature>>(),
                    });
                    SheetItems[nest.sheetSource].UsedQty++;

                    foreach (PlacementItem pos in nest.sheetplacements)
                    {
                        NestItems.Last().NestData.Add(
                            RotoTranslatePartXY(PartItems[pos.source - SheetItems.Count].Features,
                            pos.x, pos.y, Math.PI / 180 * pos.rotation));
                        if (PartItems[pos.source - SheetItems.Count].SheetId == -1)
                        {
                            PartItems[pos.source - SheetItems.Count].UsedQty++;
                        }
                    }
                }
            }
        }

        public void Export(object obj, string file, ACadVersion version = ACadVersion.AC1015)
        {
            if (obj == null) return;

            double offsetX = 0.0;
            double offsetY = 0.0; 
            
            if (obj.GetType() == typeof(PartItem))
            {
                PartItem part = (PartItem)obj;

                CadDocument doc = new ACadSharp.CadDocument(version);
                doc.AppIds.Add(new AppId("BEVEL"));

                foreach (Feature f in part.Features)
                {
                    Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                    if (lay == null)
                    {
                        doc.Layers.Add(new Layer(f.Tag));
                        lay = doc.Layers.Last();
                    }

                    doc.ModelSpace.Entities.AddRange(GetPolys(f, lay, offsetX, offsetY));
                }

                DxfWriter.Write(file, doc, false);
            }
            else if (obj.GetType() == typeof(NestItem))
            {
                NestItem nest = (NestItem)obj;

                if (Opts.Origin == Options.OriginPosition.XY_MID)
                {
                    offsetX = -SheetItems[nest.SheetSource].LX / 2.0;
                    offsetY = -SheetItems[nest.SheetSource].LY / 2.0;
                }

                if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                    Opts.Origin == Options.OriginPosition.X_MAX_Y_MIN)
                {
                    offsetX = -SheetItems[nest.SheetSource].LX;
                }

                if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                    Opts.Origin == Options.OriginPosition.X_MIN_Y_MAX)
                {
                    offsetY = -SheetItems[nest.SheetSource].LY;
                }

                if (NestItems.Any())
                {
                    CadDocument doc = new ACadSharp.CadDocument(version);
                    AppId appId = new AppId("BEVEL");
                    doc.AppIds.Add(appId);

                    foreach (Feature f in SheetItems[nest.SheetSource].Features)
                    {
                        Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                        if (lay == null)
                        {
                            doc.Layers.Add(new Layer(f.Tag));
                            lay = doc.Layers.Last();
                        }

                        doc.ModelSpace.Entities.AddRange(GetPolys(f, lay, offsetX, offsetY));
                    }

                    foreach (SheetAssociation sa in SheetItems[nest.SheetSource].Associated)
                    {
                        foreach (Feature f in RotoTranslatePartXY(PartItems[sa.partId].Features, sa.T.X, sa.T.Y, sa.T.Rot))
                        {
                            Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                            if (lay == null)
                            {
                                doc.Layers.Add(new Layer(f.Tag));
                                lay = doc.Layers.Last();
                            }

                            doc.ModelSpace.Entities.AddRange(GetPolys(f, lay, offsetX, offsetY));
                        }
                    }

                    foreach (List<Feature> part in nest.NestData)
                    {
                        foreach (Feature f in part)
                        {
                            Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                            if (lay == null)
                            {
                                doc.Layers.Add(new Layer(f.Tag));
                                lay = doc.Layers.Last();
                            }

                            doc.ModelSpace.Entities.AddRange(GetPolys(f, lay, offsetX, offsetY));
                        }
                    }

                    DxfWriter.Write(file, doc, false);
                }
            }
            else if (obj.GetType() == typeof(SheetItem))
            {
                SheetItem sheet = (SheetItem)obj;

                if (Opts.Origin == Options.OriginPosition.XY_MID)
                {
                    offsetX = -sheet.LX / 2.0;
                    offsetY = -sheet.LY / 2.0;
                }

                if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                    Opts.Origin == Options.OriginPosition.X_MAX_Y_MIN)
                {
                    offsetX = -sheet.LX;
                }

                if (Opts.Origin == Options.OriginPosition.XY_MAX ||
                    Opts.Origin == Options.OriginPosition.X_MIN_Y_MAX)
                {
                    offsetY = -sheet.LY;
                }

                if (sheet.Features.Any() || sheet.Associated.Any())
                {
                    CadDocument doc = new ACadSharp.CadDocument(version);
                    AppId appId = new AppId("BEVEL");
                    doc.AppIds.Add(appId);

                    foreach (Feature f in sheet.Features)
                    {
                        Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                        if (lay == null)
                        {
                            doc.Layers.Add(new Layer(f.Tag));
                            lay = doc.Layers.Last();
                        }

                        doc.ModelSpace.Entities.AddRange(GetPolys(f, lay, offsetX, offsetY));
                    }

                    foreach (SheetAssociation sa in sheet.Associated)
                    {
                        foreach (Feature f in RotoTranslatePartXY(PartItems[sa.partId].Features, sa.T.X, sa.T.Y, sa.T.Rot))
                        {
                            Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                            if (lay == null)
                            {
                                doc.Layers.Add(new Layer(f.Tag));
                                lay = doc.Layers.Last();
                            }

                            doc.ModelSpace.Entities.AddRange(GetPolys(f, lay, offsetX, offsetY));
                        }
                    }

                    DxfWriter.Write(file, doc, false);
                }
            }

            List<LwPolyline> GetPolys(Feature feature, Layer layer, double dx = 0, double dy = 0)
            {
                List<LwPolyline> polys = new List<LwPolyline>();
                LwPolyline poly = new LwPolyline() { Layer = layer, };
                List<Edge> edges = feature.Edges;

                if (!edges.Any()) new List<LwPolyline>();

                BevelDefinition curBevel = edges.First().Bevel;
                ExtendedData data = new ExtendedData();
                data.Records.Add(new ExtendedDataString(curBevel.Type.ToString()));
                data.Records.Add(new ExtendedDataReal(curBevel.A));
                data.Records.Add(new ExtendedDataReal(curBevel.Hr));
                data.Records.Add(new ExtendedDataReal(curBevel.B));
                data.Records.Add(new ExtendedDataReal(curBevel.r));
                poly.ExtendedData.Add(new AppId("BEVEL"), data);

                for (int i = 0; i < edges.Count; i++)
                {
                    if (i == 0)
                    {
                        poly.Vertices.Add(new LwPolyline.Vertex() { Location = new XY(edges[i].V0.X + dx, edges[i].V0.Y + dy) });
                    }
                    else
                    {
                        if (!edges[i].Bevel.Equals(curBevel))
                        {
                            polys.Add(poly);
                            poly = new LwPolyline() { Layer = layer, };
                            poly.Vertices.Add(new LwPolyline.Vertex() { Location = new XY(edges[i].V0.X + dx, edges[i].V0.Y + dy) });

                            data = new ExtendedData();
                            data.Records.Add(new ExtendedDataString(edges[i].Bevel.Type.ToString()));
                            data.Records.Add(new ExtendedDataReal(edges[i].Bevel.A));
                            data.Records.Add(new ExtendedDataReal(edges[i].Bevel.Hr));
                            data.Records.Add(new ExtendedDataReal(edges[i].Bevel.B));
                            data.Records.Add(new ExtendedDataReal(edges[i].Bevel.r));
                            poly.ExtendedData.Add(new AppId("BEVEL"), data);
                        }
                        curBevel = edges[i].Bevel;
                    }

                    if (Math.Abs(edges[i].R) > Opts.Tol0)
                    {
                        Vector2 p0 = new Vector2(edges[i].V0.X, edges[i].V0.Y);
                        int p1Id = Math.Max(1, edges[i].NS / 3);
                        Vector2 p1 = new Vector2(edges[i + p1Id - 1].V1.X, edges[i + p1Id - 1].V1.Y);
                        int p2Id = Math.Max(2, 2 * edges[i].NS / 3);
                        Vector2 p2 = new Vector2(edges[i + p2Id - 1].V1.X, edges[i + p2Id - 1].V1.Y);
                        Vector2 c = MATH.GetArcCenter(p0, p1, p2);

                        double a0 = Math.Atan2(edges[i].V0.Y - c.Y, edges[i].V0.X - c.X);
                        double a1 = Math.Atan2(edges[i + edges[i].NS - 1].V1.Y - c.Y, edges[i + edges[i].NS - 1].V1.X - c.X);
                        if (a0 < 0) a0 += 2 * Math.PI;
                        if (a1 < 0) a1 += 2 * Math.PI;
                        if (Math.Abs(2 * Math.PI - a0) < Opts.Tol0) a0 = 0;
                        if (Math.Abs(2 * Math.PI - a1) < Opts.Tol0) a1 = 0;
                        double da = a1 - a0;
                        float cross = Vector2.Cross(p1 - p0, p2 - p1);

                        if (Math.Abs(cross) < 1E-1) // UNDEFINED
                        {
                        }
                        else if (cross < 0) // ARC_CW
                        {
                            if (da > 0) da -= 2 * Math.PI;
                            poly.Vertices.Last().Bulge = Math.Tan(da / 4);
                        }
                        else // ARC_CCW
                        {
                            if (da < 0) da += 2 * Math.PI;
                            poly.Vertices.Last().Bulge = Math.Tan(da / 4);
                        }

                        poly.Vertices.Add(new LwPolyline.Vertex()
                        {
                            Location = new XY(edges[i + edges[i].NS - 1].V1.X + dx, edges[i + edges[i].NS - 1].V1.Y + dy)
                        });

                        i += edges[i].NS - 1;
                    }
                    else // LINEAR
                    {
                        poly.Vertices.Add(new LwPolyline.Vertex()
                        {
                            Location = new XY(edges[i].V1.X + dx, edges[i].V1.Y + dy)
                        });
                    }
                }

                polys.Add(poly);

                return polys;
            }
        }
    }

    public class Options
    {
        public enum OriginPosition
        {
            XY_MIN,
            X_MIN_Y_MAX,
            XY_MID,
            X_MAX_Y_MIN,
            XY_MAX,
        }

        public enum Rotations
        {
            ANY,
            BY_180,
            BY_90,
            NONE,
        }

        public const double angleStep = Math.PI / 32.0;
        public const double maxStepL = 50.0;

        public const double MutationRate = 15;
        public const int PopulationSize = 20;

        //[ReadOnly(true)]
        [Category("\t\t\t\t\t\t\tSHEET")]
        [DisplayName("ORIGIN")]
        public OriginPosition Origin { get; set; } = OriginPosition.XY_MIN;

        [Category("\t\t\t\t\t\t\tSHEET")]
        [DisplayName("MARGINS")]
        public double Margins { get; set; } = 5;

        [Category("\t\t\t\t\t\t\tSHEET")]
        [DisplayName("PART SPACING")]
        public double Spacing { get; set; } = 15.0;

        [Category("\t\t\t\t\t\t\tSHEET")]
        [DisplayName("DEFAULT SHEET WIDTH")]
        public double DefaultWidth { get; set; } = 3000.0;

        [Category("\t\t\t\t\t\t\tSHEET")]
        [DisplayName("DEFAULT SHEET HEIGHT")]
        public double DefaultHeight { get; set; } = 1500.0;

        [Category("\t\t\t\t\t\t\tSHEET")]
        [DisplayName("DEFAULT SHEET QUANTITY")]
        public int DefaultQty { get; set; } = 1;

        [Category("\t\t\t\t\t\tNESTING")]
        [DisplayName("ROTATIONS")]
        public Rotations PartRotations { get; set; } = Rotations.ANY;

        [Category("\t\t\t\t\t\tNESTING")]
        [DisplayName("MIN INTERNAL AREAS")]
        public double MinIntArea { get; set; } = 5000.0;

        [Category("\t\t\t\t\t\tNESTING")]
        [DisplayName("PAVE LIMIT (%)")]
        public double PaveLimit { get; set; } = 90;

        //[Browsable(false)]
        //[Category("\t\t\t\t\t\tNESTING")]
        //[DisplayName("MUTATION RATE (%)")]
        //public double MutationRate { get; set; } = 15;

        //[Browsable(false)]
        //[Category("\t\t\t\t\t\tNESTING")]
        //[DisplayName("POPULATION SIZE")]
        //public int PopulationSize { get; set; } = 20;

        [Category("\tDXF IMPORT")]
        [DisplayName("MERGE DISTANCE")]
        public double Tol0 { get; set; } = 1E-6;

        [Category("\tDXF IMPORT")]
        [DisplayName("LINK DISTANCE")]
        public double LinkDist { get; set; } = 1E-1;

        [Category("\tDXF IMPORT")]
        [DisplayName("SET NEW ORIGIN")]
        public bool NewPartOrigin { get; set; } = true;

        //[Browsable(false)]
        [Category("\tDXF IMPORT")]
        [DisplayName("MULTIPLICITY MERGE")]
        public bool MultiplicityMerge { get; set; } = true;

        //[Browsable(false)]
        [Category("\tDXF IMPORT")]
        [DisplayName("MULTIPLICITY MERGE TOLERANCE")]
        public double MultiplicityTol { get; set; } = 1.0;

        [Category("\tDXF IMPORT")]
        [DisplayName("MERGE LAYERS")]
        public bool MergeLayers { get; set; } = true;

        [Browsable(false)]
        [Category("\tDXF IMPORT")]
        [DisplayName("NEST ARC SEGMENTS MAX LENGTH")]
        public double NestArcSegmentsMaxLength { get; set; } = 250.0;

        [Browsable(false)]
        [Category("LAYERS")]
        [DisplayName("TOOL NB FROM LAYER NAME")]
        public bool ToolNbFromLayerName { get; set; } = true;

        [Category("LAYERS")]
        [DisplayName("DEFAULT CUTTING LAYER")]
        public int DefaultCutToolNb { get; set; } = 0;

        [Category("LAYERS")]
        [DisplayName("DEFAULT MARKING LAYER")]
        public int DefaultMrkToolNb { get; set; } = 105;
    }

    public class PartItem
    {
        //public string SourceFileName;
        public int SheetId = -1;

        public List<Feature> Features = new List<Feature>();

        [ReadOnly(true)]
        [DisplayName("PART")]
        public string Name { get; set; }

        [ReadOnly(true)]
        [DisplayName("NEST QTY")]
        public int UsedQty { get; set; }

        [DisplayName("INITAL QTY")]
        //[TypeConverter(typeof(PositiveIntegerTypeConverter))]
        public int IniQty { get; set; }
    }

    public class SheetItem
    {
        //[ReadOnly(true)]
        //[DisplayName("SHEET")]
        //public string Name { get; set; }

        //public string SourceFileName;
        public List<Feature> Features = new List<Feature>();
        //public List<List<Feature>> Associated = new List<List<Feature>>();
        public List<SheetAssociation> Associated = new List<SheetAssociation>();

        [DisplayName("LENGTH X")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double LX { get; set; }

        [DisplayName("WIDTH Y")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double LY { get; set; }

        [ReadOnly(true)]
        [DisplayName("NEST QTY")]
        public int UsedQty { get; set; }

        [DisplayName("INITAL QTY")]
        //[TypeConverter(typeof(PositiveIntegerTypeConverter))]
        public int IniQty { get; set; }
    }

    public class SheetAssociation
    {
        public int partId = -1;
        public Transform2d T = new Transform2d(0, 0, 0);
    }

    public class NestItem
    {
        public int SheetSource = -1;
        //public bool FromSource = false;
        //public string SourceFileName;
        public List<List<Feature>> NestData = new List<List<Feature>>();

        [ReadOnly(true)]
        [DisplayName("NEST")]
        public string Name { get; set; }
    }

    public class LayerItem
    {
        [ReadOnly(true)]
        [DisplayName("LAYER")]
        public string Name { get; set; }

        public enum LayerType { CUTTING_CTR, MARKING_CTR, NOT_IMPORTED }

        [DisplayName("TYPE")]
        public LayerType Type { get; set; }
    }
}
