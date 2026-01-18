using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp;
using DeepNestLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using ACadSharp.Tables;
using CSMath;
using System.Reflection;
//using System.Windows.Forms;

namespace DXFnest
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

        public enum LoadType { PART, SHEET }

        public void UpdateDxfsLayers(List<string> files)
        {
            foreach (string file in files)
            {
                List<CAD.Feature> dxfFeatures = ReadDxfACAD(file);

                foreach (CAD.Feature f in dxfFeatures)
                {
                    if (LayerItems.ToList().Find(x => x.Name == f.Tag) == null)
                    {
                        if (LayerItems.ToList().Find(x => x.Name == f.Tag) == null)
                        {
                            LayerItems.Add(new LayerItem()
                            {
                                Name = f.Tag,
                                Type = LayerItem.LayerType.CUTTING_CTR,
                            });
                        }
                    }
                }
            }
        }

        public void LoadDxfs(List<string> files, LoadType loadtype)
        {
            foreach (string file in files)
            {
                List<List<CAD.Feature>> added = new List<List<CAD.Feature>>();

                if (Path.GetExtension(file).ToLower() == ".dxf")
                {
                    List<CAD.Feature> dxfFeatures = ReadDxfACAD(file);

                    foreach (CAD.Feature f in dxfFeatures)
                    {
                        if (LayerItems.ToList().Find(x => x.Name == f.Tag) == null)
                        {
                            LayerItems.Add(new LayerItem()
                            {
                                Name = f.Tag,
                                Type = LayerItem.LayerType.CUTTING_CTR,
                            });
                        }
                    }

                    List<CAD.Feature> ImportFeatures = new List<CAD.Feature>();
                    foreach (CAD.Feature f in dxfFeatures)
                    {
                        LayerItem layer = LayerItems.ToList().Find(x => x.Name == f.Tag);
                        if (layer != null)
                        {
                            if (layer.Type == LayerItem.LayerType.CUTTING_CTR)
                            {
                                ImportFeatures.Add(f);
                            }
                            else if (layer.Type == LayerItem.LayerType.MARKING_CTR)
                            {
                                f.Type = CAD.Feature.FeatureType.DXF_IGNORE;
                                ImportFeatures.Add(f);
                            }
                        }
                    }

                    added.AddRange(CAD.ExtractParts(ImportFeatures, Opts.Tol0, Opts.LinkDist, Opts.MergeLayers, Opts.MinIntArea));
                }

                if (loadtype == LoadType.SHEET)
                {
                    for (int i = 0; i < added.Count; i++)
                    {
                        float minX = float.MaxValue;
                        float minY = float.MaxValue;
                        float maxX = float.MinValue;
                        float maxY = float.MinValue;
                        foreach (CAD.Feature f in added[i])
                        {
                            foreach (CAD.Edge edge in f.Mesh.Edges)
                            {
                                minX = Math.Min(edge.V0.X, Math.Min(edge.V1.X, minX));
                                maxX = Math.Max(edge.V0.X, Math.Max(edge.V1.X, maxX));

                                minY = Math.Min(edge.V0.Y, Math.Min(edge.V1.Y, minY));
                                maxY = Math.Max(edge.V0.Y, Math.Max(edge.V1.Y, maxY));
                            }
                        }

                        foreach (CAD.Feature f in added[i])
                        {
                            foreach (CAD.Edge edge in f.Mesh.Edges)
                            {
                                edge.V0.X -= minX;
                                edge.V0.Y -= minY;
                                edge.V1.X -= minX;
                                edge.V1.Y -= minY;
                            }
                        }

                        SheetItems.Add(new SheetItem
                        {
                            LX = maxX - minX,
                            LY = maxY - minY,

                            UsedQty = 0,
                            IniQty = 1,

                            Features = added[i],
                            SourceFileName = file,
                        });
                    }
                }
                else if (loadtype == LoadType.PART)
                {
                    for (int i = 0; i < added.Count; i++)
                    {
                        PartItems.Add(new PartItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file) + "_" + i.ToString(),
                            UsedQty = 0,
                            IniQty = 1,

                            Features = added[i],
                            SourceFileName = file,
                        });
                    }
                }
            }

            RestartNest();
        }

        public void LoadNesting(List<string> files)
        {
            foreach (string file in files)
            {
                List<List<CAD.Feature>> added = new List<List<CAD.Feature>>();

                if (Path.GetExtension(file).ToLower() == ".dxf")
                {
                    List<CAD.Feature> dxfFeatures = ReadDxfACAD(file);

                    foreach (CAD.Feature f in dxfFeatures)
                    {
                        if (LayerItems.ToList().Find(x => x.Name == f.Tag) == null)
                        {
                            LayerItems.Add(new LayerItem()
                            {
                                Name = f.Tag,
                                Type = LayerItem.LayerType.CUTTING_CTR,
                            });
                        }
                    }

                    List<CAD.Feature> ImportFeatures = new List<CAD.Feature>();
                    foreach (CAD.Feature f in dxfFeatures)
                    {
                        LayerItem layer = LayerItems.ToList().Find(x => x.Name == f.Tag);
                        if (layer != null)
                        {
                            if (layer.Type == LayerItem.LayerType.CUTTING_CTR)
                            {
                                ImportFeatures.Add(f);
                            }
                            else if (layer.Type == LayerItem.LayerType.MARKING_CTR)
                            {
                                f.Type = CAD.Feature.FeatureType.DXF_IGNORE;
                                ImportFeatures.Add(f);
                            }
                        }
                    }

                    added.AddRange(CAD.ExtractParts(ImportFeatures, Opts.Tol0, Opts.LinkDist, Opts.MergeLayers, Opts.MinIntArea));
                }

                float minX = float.MaxValue;
                float minY = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;

                for (int i = 0; i < added.Count; i++)
                {
                    foreach (CAD.Feature f in added[i])
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

                for (int i = 0; i < added.Count; i++)
                {
                    foreach (CAD.Feature f in added[i])
                    {
                        foreach (CAD.Edge edge in f.Mesh.Edges)
                        {
                            edge.V0.X += (float)Opts.Margins - minX;
                            edge.V0.Y += (float)Opts.Margins - minY;
                            edge.V1.X += (float)Opts.Margins - minX;
                            edge.V1.Y += (float)Opts.Margins - minY;
                        }
                        foreach (CAD.Triangle tri in f.Mesh.Triangles)
                        {
                            tri.V0.X += (float)Opts.Margins - minX;
                            tri.V0.Y += (float)Opts.Margins - minY;
                            tri.V1.X += (float)Opts.Margins - minX;
                            tri.V1.Y += (float)Opts.Margins - minY;
                            tri.V2.X += (float)Opts.Margins - minX;
                            tri.V2.Y += (float)Opts.Margins - minY;
                        }
                    }
                }

                SheetItems.Add(new SheetItem
                {
                    SourceFileName = file,
                    Features = new List<CAD.Feature>(),
                    Associated = added,
                    LX = Opts.DefaultWidth,
                    LY = Opts.DefaultHeight,
                    IniQty = 1,
                    UsedQty = 0,
                }); ;

            }

            RestartNest();
        }

        public void ClearParts()
        {
            for (int i = 0; i < PartItems.Count; i++)
            {
                PartItems.RemoveAt(i);
                i--;
            }
        }

        public List<CAD.Feature> ReadDxfACAD(string filePath)
        {
            List<CAD.Feature> dxfData = new List<CAD.Feature>();

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (DxfReader reader = new DxfReader(fs))
            {
                CAD.Edge edge;

                //try
                //{
                    CadDocument doc = reader.Read();

                    foreach (Entity entity in doc.Entities)
                    {
                        switch (entity.ObjectType)
                        {
                            case ObjectType.POINT:
                                ACadSharp.Entities.Point pt = (ACadSharp.Entities.Point)entity;
                                break;

                            case ObjectType.LINE:
                                ACadSharp.Entities.Line line = (ACadSharp.Entities.Line)entity;

                                edge = new CAD.Edge(line.StartPoint.X, line.StartPoint.Y, 0, line.EndPoint.X, line.EndPoint.Y, 0);
                                dxfData.Add(new CAD.Feature(line.Layer.Name, CAD.Feature.FeatureType.DXF_OPEN_ENTITY));
                                dxfData.Last().Mesh.Edges.Add(edge);
                                break;

                            case ObjectType.CIRCLE:
                                ACadSharp.Entities.Circle circle = (ACadSharp.Entities.Circle)entity;
                                dxfData.Add(new CAD.Feature(circle.Layer.Name, CAD.Feature.FeatureType.DXF_CLOSED_ENTITY));
                                dxfData.Last().Mesh.Edges.AddRange(CAD.GetArcSegments(
                                    circle.Center.X, circle.Center.Y, circle.Radius, 0, Math.PI, Options.angleStep, Options.maxStepL));
                                dxfData.Last().Mesh.Edges.AddRange(CAD.GetArcSegments(
                                    circle.Center.X, circle.Center.Y, circle.Radius, Math.PI, 2 * Math.PI, Options.angleStep, Options.maxStepL));
                                break;

                            case ObjectType.ARC:
                                ACadSharp.Entities.Arc arc = (ACadSharp.Entities.Arc)entity;
                                dxfData.Add(new CAD.Feature(arc.Layer.Name, CAD.Feature.FeatureType.DXF_OPEN_ENTITY));
                                double a0 = arc.StartAngle;
                                double a1 = arc.EndAngle;
                                if (a0 > a1)
                                {
                                    a0 -= 2.0 * Math.PI;
                                }
                                dxfData.Last().Mesh.Edges.AddRange(CAD.GetArcSegments(
                                    arc.Center.X, arc.Center.Y, arc.Radius, a0, a1, Options.angleStep, Options.maxStepL));
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
                                            0,
                                            lwpoly.Vertices[i].Location.X,
                                            lwpoly.Vertices[i].Location.Y,
                                            0);
                                        lwpoly_crt.Add(edge);
                                    }
                                    else
                                    {
                                        lwpoly_crt.AddRange(CAD.GetBulgeSegmented(bulge,
                                            lwpoly.Vertices[i - 1].Location.X, lwpoly.Vertices[i - 1].Location.Y,
                                            lwpoly.Vertices[i].Location.X, lwpoly.Vertices[i].Location.Y, Options.angleStep, Options.maxStepL));
                                    }
                                }

                                double end_bulge_lwpoly = lwpoly.Vertices[lwpoly.Vertices.Count - 1].Bulge;
                                edge = new CAD.Edge(
                                        lwpoly_crt.Last().V1.X,
                                        lwpoly_crt.Last().V1.Y,
                                        0,
                                        lwpoly.Vertices[0].Location.X,
                                        lwpoly.Vertices[0].Location.Y,
                                        0);
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
                                            lwpoly_crt.AddRange(CAD.GetBulgeSegmented(end_bulge_lwpoly,
                                                edge.V0.X, edge.V0.Y, edge.V1.X, edge.V1.Y, Options.angleStep, Options.maxStepL));
                                        }

                                        dxfData.Add(new CAD.Feature(lwpoly.Layer.Name, CAD.Feature.FeatureType.DXF_CLOSED_ENTITY));
                                        dxfData.Last().Mesh.Edges.AddRange(lwpoly_crt);
                                    }
                                    else
                                    {
                                        dxfData.Add(new CAD.Feature(lwpoly.Layer.Name, CAD.Feature.FeatureType.DXF_OPEN_ENTITY));
                                        dxfData.Last().Mesh.Edges.AddRange(lwpoly_crt);
                                    }
                                }
                                else
                                {
                                    dxfData.Add(new CAD.Feature(lwpoly.Layer.Name, CAD.Feature.FeatureType.DXF_CLOSED_ENTITY));
                                    dxfData.Last().Mesh.Edges.AddRange(lwpoly_crt);
                                }
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
                                            0,
                                            poly2.Vertices[i].Location.X,
                                            poly2.Vertices[i].Location.Y,
                                            0);
                                        poly2_crt.Add(edge);
                                    }
                                    else
                                    {
                                        poly2_crt.AddRange(CAD.GetBulgeSegmented(bulge,
                                            poly2.Vertices[i - 1].Location.X, poly2.Vertices[i - 1].Location.Y,
                                            poly2.Vertices[i].Location.X, poly2.Vertices[i].Location.Y, Options.angleStep, Options.maxStepL));
                                    }
                                }

                                double end_bulge_poly2 = poly2.Vertices[poly2.Vertices.Count - 1].Bulge;
                                edge = new CAD.Edge(
                                    poly2_crt.Last().V1.X,
                                    poly2_crt.Last().V1.Y,
                                    0,
                                    poly2.Vertices[0].Location.X,
                                    poly2.Vertices[0].Location.Y,
                                    0);

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
                                            poly2_crt.AddRange(CAD.GetBulgeSegmented(end_bulge_poly2,
                                                edge.V0.X, edge.V0.Y, edge.V1.X, edge.V1.Y, Options.angleStep, Options.maxStepL));
                                        }

                                        dxfData.Add(new CAD.Feature(poly2.Layer.Name, CAD.Feature.FeatureType.DXF_CLOSED_ENTITY));
                                        dxfData.Last().Mesh.Edges.AddRange(poly2_crt);
                                    }
                                    else
                                    {
                                        dxfData.Add(new CAD.Feature(poly2.Layer.Name, CAD.Feature.FeatureType.DXF_OPEN_ENTITY));
                                        dxfData.Last().Mesh.Edges.AddRange(poly2_crt);
                                    }
                                }
                                else
                                {
                                    dxfData.Add(new CAD.Feature(poly2.Layer.Name, CAD.Feature.FeatureType.DXF_CLOSED_ENTITY));
                                    dxfData.Last().Mesh.Edges.AddRange(poly2_crt);
                                }
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
                //}
                //catch { return dxfData; }
            }

            return dxfData;
        }

        public void RestartNest()
        {
            CurrentFitness = double.MaxValue;

            DeepNestLib.Nest.Config.placementType = Opts.PlacementType;
            DeepNestLib.Nest.Config.spacing = Opts.Spacing;
            DeepNestLib.Nest.Config.sheetSpacing = Opts.Margins;
            DeepNestLib.Nest.Config.populationSize = Opts.PopulationSize;
            DeepNestLib.Nest.Config.MutationRate = Opts.MutationRate * 0.01;

            Context = new NestingContext();

            for (int i = 0; i < SheetItems.Count; i++)
            {
                NFP nfpSheet = new NFP();
                bool extDefined = true;
                if (SheetItems[i].Features.Any())
                {
                    extDefined = false;
                    List<CAD.Feature> dxfSheet = SheetItems[i].Features;

                    foreach (CAD.Feature f in dxfSheet)
                    {
                        List<CAD.Edge> simplified;
                        if (f.Type == CAD.Feature.FeatureType.EXT)
                        {
                            extDefined = true;
                            simplified = CAD.SimplifyForNest(f.Mesh.Edges, true, true, Opts.Spacing, -1,
                                Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                            foreach (CAD.Edge edge in simplified)
                            {
                                nfpSheet.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                            }
                        }
                        if (f.Type == CAD.Feature.FeatureType.INT)
                        {
                            simplified = CAD.SimplifyForNest(f.Mesh.Edges, false, true, Opts.Spacing, -1,
                                Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                            if (simplified != null)
                            {
                                if (nfpSheet.children == null) nfpSheet.children = new List<NFP>();
                                NFP nfpHole = new NFP();
                                foreach (CAD.Edge edge in simplified)
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

                foreach (List<CAD.Feature> part in SheetItems[i].Associated)
                {
                    foreach (CAD.Feature f in part)
                    {
                        if (f.Type == CAD.Feature.FeatureType.EXT)
                        {
                            if (nfpSheet.children == null) nfpSheet.children = new List<NFP>();
                            List<CAD.Edge> simplified;
                            simplified = CAD.SimplifyForNest(f.Mesh.Edges, false, false, Opts.Spacing, Opts.PaveLimit * 0.01,
                                Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                            NFP nfpAssociated = new NFP();
                            foreach (CAD.Edge edge in simplified)
                            {
                                nfpAssociated.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                            }
                            nfpSheet.children.Add(nfpAssociated);
                        }
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

                List<CAD.Feature> dxfPart = PartItems[i].Features;

                foreach (CAD.Feature f in dxfPart)
                {
                    List<CAD.Edge> simplified;
                    if (f.Type == CAD.Feature.FeatureType.EXT)
                    {
                        extDefined = true;
                        simplified = CAD.SimplifyForNest(f.Mesh.Edges, false, false, Opts.Spacing, Opts.PaveLimit * 0.01,
                            Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out extMinHrot, out extpave);
                        foreach (CAD.Edge edge in simplified)
                        {
                            nfpPart.AddPoint(new DeepNestLib.Point(edge.V1.X, edge.V1.Y));
                        }
                    }
                    if (f.Type == CAD.Feature.FeatureType.INT && CAD.GetArea(f.Mesh.Edges) > Math.Max(Opts.Spacing * Opts.Spacing, Opts.MinIntArea))
                    {
                        simplified = CAD.SimplifyForNest(f.Mesh.Edges, false, true, Opts.Spacing, -1,
                            Math.PI / 4.0, Opts.NestArcSegmentsMaxLength, out double dbl, out bool inpave);
                        if (simplified != null)
                        {
                            if (nfpPart.children == null) nfpPart.children = new List<NFP>();
                            NFP nfpHole = new NFP();
                            foreach (CAD.Edge edge in simplified)
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

                    if (extpave)
                    {
                        rots = EnabledRotations.PAVE_0_90;
                    }
                    else
                    {
                        switch (Opts.PartRotations)
                        {
                            case Options.Rotations.NONE:
                                rots = EnabledRotations.NONE;
                                break;

                            case Options.Rotations.BY_180:
                                rots = EnabledRotations.BY_180;
                                break;

                            case Options.Rotations.BY_90:
                                rots = EnabledRotations.BY_90;
                                break;

                            case Options.Rotations.ANY:
                                rots = EnabledRotations.ANY;
                                break;
                        }
                    }

                    Context.AddPart(nfpPart, PartItems[i].IniQty, rots, extMinHrot);
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
                item.UsedQty = 0;
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
                        NestData = new List<List<CAD.Feature>>(),
                    });
                    SheetItems[nest.sheetSource].UsedQty++;

                    foreach (PlacementItem pos in nest.sheetplacements)
                    {
                        NestItems.Last().NestData.Add(
                            CAD.RotoTranslatePartXY(PartItems[pos.source - SheetItems.Count].Features,
                            pos.x, pos.y, Math.PI / 180 * pos.rotation));
                        PartItems[pos.source - SheetItems.Count].UsedQty++;
                    }
                }
            }
        }

        public void Export(object obj, string file)
        {
            if (obj == null) return;

            if (obj.GetType() == typeof(NestItem))
            {
                NestItem nest = (NestItem)obj;

                if (NestItems.Any())
                {
                    CadDocument doc = new ACadSharp.CadDocument(ACadVersion.AC1015);

                    foreach (CAD.Feature f in SheetItems[nest.SheetSource].Features)
                    {
                        Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                        if (lay == null)
                        {
                            doc.Layers.Add(new Layer(f.Tag));
                            lay = doc.Layers.Last();
                        }

                        doc.ModelSpace.Entities.Add(GetPoly(f, lay));
                    }

                    foreach (List<CAD.Feature> part in nest.NestData.Concat(SheetItems[nest.SheetSource].Associated))
                    {
                        foreach (CAD.Feature f in part)
                        {
                            Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                            if (lay == null)
                            {
                                doc.Layers.Add(new Layer(f.Tag));
                                lay = doc.Layers.Last();
                            }

                            doc.ModelSpace.Entities.Add(GetPoly(f, lay));
                        }
                    }

                    DxfWriter.Write(file, doc, false);
                }
            }
            else if (obj.GetType() == typeof(PartItem))
            {
                PartItem part = (PartItem)obj;

                CadDocument doc = new ACadSharp.CadDocument(ACadVersion.AC1015);

                foreach (CAD.Feature f in part.Features)
                {
                    Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                    if (lay == null)
                    {
                        doc.Layers.Add(new Layer(f.Tag));
                        lay = doc.Layers.Last();
                    }

                    doc.ModelSpace.Entities.Add(GetPoly(f, lay));
                }

                DxfWriter.Write(file, doc, false);
            }
            else if (obj.GetType() == typeof(SheetItem))
            {
                SheetItem sheet = (SheetItem)obj;

                if (sheet.Features.Any() || sheet.Associated.Any())
                {
                    CadDocument doc = new ACadSharp.CadDocument(ACadVersion.AC1015);

                    foreach (CAD.Feature f in sheet.Features)
                    {
                        Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                        if (lay == null)
                        {
                            doc.Layers.Add(new Layer(f.Tag));
                            lay = doc.Layers.Last();
                        }

                        doc.ModelSpace.Entities.Add(GetPoly(f, lay));
                    }

                    foreach (List<CAD.Feature> part in sheet.Associated)
                    {
                        foreach (CAD.Feature f in part)
                        {
                            Layer lay = doc.Layers.ToList().Find(l => l.Name == f.Tag);
                            if (lay == null)
                            {
                                doc.Layers.Add(new Layer(f.Tag));
                                lay = doc.Layers.Last();
                            }

                            doc.ModelSpace.Entities.Add(GetPoly(f, lay));
                        }
                    }

                    DxfWriter.Write(file, doc, false);
                }
            }

            LwPolyline GetPoly(CAD.Feature feature, Layer layer)
            {
                LwPolyline poly = new LwPolyline() { Layer = layer, };

                List<CAD.Edge> edges = feature.Mesh.Edges;
                for (int i = 0; i < edges.Count; i++)
                {
                    if (i == 0)
                    {
                        poly.Vertices.Add(new LwPolyline.Vertex() { Location = new XY(edges[i].V0.X, edges[i].V0.Y) });
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
                            Location = new XY(edges[i + edges[i].NS - 1].V1.X, edges[i + edges[i].NS - 1].V1.Y)
                        });

                        i += edges[i].NS - 1;
                    }
                    else // LINEAR
                    {
                        poly.Vertices.Add(new LwPolyline.Vertex()
                        {
                            Location = new XY(edges[i].V1.X, edges[i].V1.Y)
                        });
                    }
                }

                return poly;
            }
        }
    }

    public class Options
    {
        public enum OriginPosition
        {
            XY_MIN,
            X_MIN_Y_MID,
            X_MIN_Y_MAX,
            X_MID_Y_MIN,
            XY_MID,
            X_MID_Y_MAX,
            X_MAX_Y_MIN,
            X_MAX_Y_MID,
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

        //[ReadOnly(true)]
        [Category("MATERIAL")]
        [DisplayName("ORIGIN")]
        public OriginPosition Origin { get; set; } = OriginPosition.XY_MIN;

        [Category("MATERIAL")]
        [DisplayName("MARGINS")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double Margins { get; set; } = 5;

        [Category("MATERIAL")]
        [DisplayName("PART SPACING")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double Spacing { get; set; } = 7.5;

        [Category("MATERIAL")]
        [DisplayName("DEFAULT SHEET WIDTH")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double DefaultWidth { get; set; } = 3000.0;

        [Category("MATERIAL")]
        [DisplayName("DEFAULT SHEET HEIGHT")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double DefaultHeight { get; set; } = 1500.0;

        [Category("MATERIAL")]
        [DisplayName("DEFAULT SHEET QUANTITY")]
        //[TypeConverter(typeof(PositiveIntegerTypeConverter))]
        public int DefaultQty { get; set; } = 1;

        [Category("NESTING")]
        [DisplayName("PLACEMENT TYPE")]
        public PlacementTypeEnum PlacementType { get; set; } = PlacementTypeEnum.BOX;

        [Category("NESTING")]
        [DisplayName("ROTATIONS")]
        public Rotations PartRotations { get; set; } = Rotations.ANY;

        [Category("NESTING")]
        [DisplayName("MIN INTERNAL AREAS")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double MinIntArea { get; set; } = 5000.0;

        [Category("NESTING")]
        [DisplayName("PAVE LIMIT (%)")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double PaveLimit { get; set; } = 90;

        [Category("NESTING")]
        [DisplayName("MUTATION RATE (%)")]
        //[TypeConverter(typeof(PositiveIntegerTypeConverter))]
        public double MutationRate { get; set; } = 15;

        [Category("NESTING")]
        [DisplayName("POPULATION SIZE")]
        //[TypeConverter(typeof(PositiveIntegerTypeConverter))]
        public int PopulationSize { get; set; } = 20;

        [Category("DXF IMPORT")]
        [DisplayName("MERGE DISTANCE")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double Tol0 { get; set; } = 1E-6;

        [Category("DXF IMPORT")]
        [DisplayName("LINK DISTANCE")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double LinkDist { get; set; } = 1E-1;

        [Category("DXF IMPORT")]
        [DisplayName("MERGE LAYERS")]
        //[Editor(typeof(CheckEditor), typeof(UITypeEditor))]
        public bool MergeLayers { get; set; } = true;

        [Category("DXF IMPORT")]
        [DisplayName("NEST ARC SEGMENTS MAX LENGTH")]
        //[TypeConverter(typeof(PositiveDoubleTypeConverter))]
        public double NestArcSegmentsMaxLength { get; set; } = 250.0;
    }

    public class PartItem
    {
        public string SourceFileName;

        public List<CAD.Feature> Features = new List<CAD.Feature>();

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

        public string SourceFileName;
        public List<CAD.Feature> Features = new List<CAD.Feature>();
        public List<List<CAD.Feature>> Associated = new List<List<CAD.Feature>>();

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

    public class NestItem
    {
        public int SheetSource = -1;
        //public bool FromSource = false;
        //public string SourceFileName;
        public List<List<CAD.Feature>> NestData = new List<List<CAD.Feature>>();

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

        [DisplayName("TOOL NUMBER")]
        public int ToolNb { get; set; }
    }
}
