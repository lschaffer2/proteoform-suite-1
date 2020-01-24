﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProteoformSuiteInternal;
using System.Windows;
using MassSpectrometry;
using Proteomics;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;

namespace ProteoWPFSuite
{
    /// <summary>
    /// Interaction logic for TopDown.xaml
    /// </summary>
    public partial class TopDown : UserControl, ISweetForm, ITabbedMDI
    {
        private Dictionary<ProductType, Color> productTypeToColor;
        private Dictionary<ProductType, double> productTypeToYOffset;
        private Dictionary<string, SolidColorBrush> ModificationAnnotationColors;
        private static List<Modification> mods = new List<Modification>();
        public TopDown()
        {
            InitializeComponent();
            InitializeParameterSet();
        }

        public void InitializeParameterSet()
        {
            tb_tableFilter.TextChanged -= tb_tableFilter_TextChanged;
            tb_tableFilter.Text = "";
            tb_tableFilter.TextChanged += tb_tableFilter_TextChanged;

            nUD_min_score_td.Value = (decimal)Sweet.lollipop.min_score_td;
            cb_biomarker.IsChecked = Sweet.lollipop.biomarker;
            cb_tight_abs_mass.IsChecked = Sweet.lollipop.tight_abs_mass;
            nUD_td_rt_tolerance.Value = (decimal)Sweet.lollipop.td_retention_time_tolerance;
        }

        public List<DataTable> DataTables { get; private set; }
        public ProteoformSweet MDIParent { get; set; }

        public List<DataTable> SetTables()
        {
            DataTables = new List<DataTable>
            {
                DisplayTopDownProteoform.FormatTopDownTable(Sweet.lollipop.topdown_proteoforms.Select(e => new DisplayTopDownProteoform(e)).ToList(), "TopdownProteoforms", false)
            };
            return DataTables;
        }
        public void FillTablesAndCharts()
        {
            DisplayUtility.FillDataGridView(dgv_TD_proteoforms, Sweet.lollipop.topdown_proteoforms.Select(t => new DisplayTopDownProteoform(t)));
            DisplayTopDownProteoform.FormatTopDownTable(dgv_TD_proteoforms, false);
            mods = Sweet.lollipop.topdown_proteoforms.SelectMany(p => p.topdown_ptm_set.ptm_combination).Select(m => m.modification).Distinct().ToList();
            SetUpDictionaries();
            tb_tdProteoforms.Text = Sweet.lollipop.topdown_proteoforms.Count.ToString();
            tb_td_hits.Text = Sweet.lollipop.top_down_hits.Count.ToString();
            tb_unique_PFRs.Text = Sweet.lollipop.topdown_proteoforms.Select(p => p.pfr_accession).Distinct().Count().ToString();
        }
        public void RunTheGamut(bool full_run)
        {
            if (!full_run)
            {
                if (Sweet.lollipop.top_down_hits.Count == 0)
                {
                    ClearListsTablesFigures(true);
                    if (!Sweet.lollipop.input_files.Any(f => f.purpose == Purpose.TopDown))
                    {
                        MessageBox.Show("Go back and load in top-down results.");
                        return;
                    }
                    if (Sweet.lollipop.target_proteoform_community.theoretical_proteoforms.Length == 0)
                    {
                        MessageBox.Show("Go back and construct a theoretical database.");
                        return;
                    }
                    Sweet.lollipop.read_in_td_hits();
                    tb_td_hits.Text = Sweet.lollipop.top_down_hits.Count.ToString();
                }
            }
            else
            {
                Sweet.lollipop.read_in_td_hits();
            }
            ClearListsTablesFigures(true);
            AggregateTdHits();
            if (!full_run)
            {
                List<string> warning_methods = new List<string>() { "Warning:" };
                if (Sweet.lollipop.topdownReader.bad_ptms.Count > 0)
                {
                    warning_methods.Add("Top-down proteoforms with the following modifications were not matched to a modification in the theoretical PTM list: ");
                    warning_methods.Add(string.Join(", ", Sweet.lollipop.topdownReader.bad_ptms.Distinct()));
                }
                if (Sweet.lollipop.topdown_proteoforms_no_theoretical.Count() > 0)
                {
                    warning_methods.Add("Top-down proteoforms with the following accessions were not matched to a theoretical proteoform in the theoretical database: ");
                    warning_methods.Add(string.Join(", ", Sweet.lollipop.topdown_proteoforms_no_theoretical.Select(t => t.accession.Split('_')[0]).Concat(Sweet.lollipop.topdown_proteoforms.SelectMany(t => t.ambiguous_topdown_hits).Select(p => p.accession.Split('_')[0])).Distinct()));
                }
                if (warning_methods.Count > 1)
                {
                    MessageBox.Show(String.Join("\n\n", warning_methods));
                }
            }
            //need to refill theo database --> added theoreticsl
            
            MDIParent.theoreticalDatabase.FillTablesAndCharts();
            FillTablesAndCharts();
        }
        public void ClearListsTablesFigures(bool clear_following)
        {
            if (!clear_following)
            {
                tb_td_hits.Clear();
                Sweet.lollipop.top_down_hits.Clear(); //only want to clear if cleared theo database
            }
            Sweet.lollipop.clear_td();
            dgv_TD_proteoforms.DataSource = null;
            dgv_TD_proteoforms.Rows.Clear();
            tb_tdProteoforms.Clear();
            tb_unique_PFRs.Clear();
            tb_tableFilter.Clear();
            baseSequenceAnnotationCanvas.Children.Clear();
            if (clear_following)
            {
                for (int i = MDIParent.forms.IndexOf(this) + 1; i < MDIParent.forms.Count; i++)
                {
                    ISweetForm sweet = MDIParent.forms[i];
                    if (sweet as RawExperimentalComponents == null)
                        sweet.ClearListsTablesFigures(false);
                }
            }
        }
        public bool ReadyToRunTheGamut()
        {
            return true;
        }

        private void AggregateTdHits()
        {
            if (Sweet.lollipop.top_down_hits.Count > 0)
            {
                Sweet.lollipop.clear_td();
                Sweet.lollipop.topdown_proteoforms = Sweet.lollipop.aggregate_td_hits(Sweet.lollipop.top_down_hits, Sweet.lollipop.min_score_td, Sweet.lollipop.biomarker, Sweet.lollipop.tight_abs_mass);
                Sweet.lollipop.theoretical_database.make_theoretical_proteoforms();
            }
        }

        private void bt_td_relations_Click(object sender, EventArgs e)
        {
            if (ReadyToRunTheGamut())
            {
                RunTheGamut(false);
            }
        }

        public System.Windows.Forms.DataGridView GetTopDownDGV()
        {
            return dgv_TD_proteoforms;
        }

        
        private void dgv_TD_proteoforms_CellContentClick(object sender, System.Windows.Forms.DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                TopDownProteoform p = (TopDownProteoform)((DisplayObject)this.dgv_TD_proteoforms.Rows[e.RowIndex].DataBoundItem).display_object;
                get_proteoform_sequence(p);
            }
        }

        private void get_proteoform_sequence(TopDownProteoform p)
        {
            baseSequenceAnnotationCanvas.Children.Clear();

            Dictionary<string, List<SpectrumMatch>> baseSequenceGroups = new Dictionary<string, List<SpectrumMatch>>();
            baseSequenceGroups.Add(p.sequence, new List<SpectrumMatch>() { p.topdown_hits.First() } );
            foreach(var h in p.ambiguous_topdown_hits)
            {
                if(baseSequenceGroups.ContainsKey(h.sequence))
                {
                    baseSequenceGroups[h.sequence].Add(h);
                }
                else
                {
                    baseSequenceGroups.Add(h.sequence, new List<SpectrumMatch>() { h });
                }
            }

            double xSpacing = 25;
            double ySpacing = 40;
            double minXMargin = baseSequenceGroups.First().Key.Length * xSpacing > baseSequenceAnnotationCanvas.ActualWidth - xSpacing ?
                40 : (baseSequenceAnnotationCanvas.ActualWidth - xSpacing - baseSequenceGroups.First().Key.Length * xSpacing) / 2;

            double x = minXMargin;
            double y = 10;

            foreach (var fullSequenceOptions in baseSequenceGroups)
            {
                bool isAmbiguousBaseSequence = baseSequenceGroups.Count() > 1;
                bool isAmbiguousFullSequence = fullSequenceOptions.Value.Count() > 1;

                string baseSequence = fullSequenceOptions.Key;

                // draw base sequence
                List<TextBlock> drawnAminoAcids = new List<TextBlock>();
                for (int r = 0; r < baseSequence.Length; r++)
                {
                    if (x > baseSequenceAnnotationCanvas.ActualWidth - minXMargin)
                    {
                        x = minXMargin;
                        y += ySpacing;
                    }

                    var drawnAminoAcid = BaseSequenceAnnotation.DrawText(baseSequenceAnnotationCanvas, new Point(x, y), baseSequence[r].ToString(), Brushes.Black);
                    drawnAminoAcids.Add(drawnAminoAcid);

                    x += xSpacing;
                }

                // draw the fragment ion annotations on the base sequence
                foreach (MatchedFragmentIon ion in p.ambiguous_topdown_hits.Concat(p.topdown_hits).SelectMany(h => h.matched_fragment_ions))
                {
                    int zeroBasedAminoAcidIndex = ion.NeutralTheoreticalProduct.TerminusFragment.AminoAcidPosition - 1;

                    for (int i = 0; i < drawnAminoAcids.Count; i++)
                    {
                        if (i == zeroBasedAminoAcidIndex)
                        {
                            AnnotateFragmentIon(drawnAminoAcids[i], ion, p);
                        }
                    }
                }

                // draw modifications
                foreach (var fullSequence in fullSequenceOptions.Value)
                {
                    foreach (var mod in fullSequence.ptm_list)
                    {
                        bool isAmbiguousMod = false;

                        if (isAmbiguousFullSequence)
                        {
                            // figure out if this mod is ambiguous or not
                            isAmbiguousMod = !fullSequenceOptions.Value.All(m => m.ptm_list.Select(a => UnlocalizedModification.LookUpId(a.modification) + "@" + a.position)
                            .Contains(UnlocalizedModification.LookUpId(mod.modification) + "@" + mod.position));
                        }

                        int zeroBasedAminoAcidIndex = mod.position - 2;

                        if (zeroBasedAminoAcidIndex == -1)
                        {
                            // n-term mod
                            AnnotateModification(drawnAminoAcids[0], mod.modification, isAmbiguousMod);
                            continue;
                        }
                        else if (zeroBasedAminoAcidIndex == baseSequence.Length)
                        {
                            // c-term mod
                            AnnotateModification(drawnAminoAcids[baseSequence.Length - 1], mod.modification, isAmbiguousMod);
                            continue;
                        }

                        // side-chain mods
                        for (int i = 0; i < drawnAminoAcids.Count; i++)
                        {
                            if (i == zeroBasedAminoAcidIndex)
                            {
                                AnnotateModification(drawnAminoAcids[i], mod.modification, isAmbiguousMod);
                            }
                        }
                    }
                }

                if (isAmbiguousBaseSequence)
                {
                    y += ySpacing;
                }

                x = minXMargin;
            }

            baseSequenceAnnotationCanvasGrid.Height = y + ySpacing;
        }

        private void AnnotateModification(TextBlock residue, Modification mod, bool ambiguousMods)
        {
            double top = Canvas.GetTop(residue) + 3;
            double left = Canvas.GetLeft(residue) - 0;
            var color = ModificationAnnotationColors.ContainsKey(UnlocalizedModification.LookUpId(mod)) ? ModificationAnnotationColors[UnlocalizedModification.LookUpId(mod)] : Brushes.MediumPurple;
            BaseSequenceAnnotation.DrawModification(baseSequenceAnnotationCanvas, new Point(left, top), color, mod, ambiguousMods);
        }

        private void SetUpDictionaries()
        {
            productTypeToColor = new Dictionary<ProductType, Color>();
            productTypeToYOffset = new Dictionary<ProductType, double>();
            ModificationAnnotationColors = new Dictionary<string, SolidColorBrush>();

            // colors of each fragment to annotate on base sequence
            productTypeToColor = ((ProductType[])Enum.GetValues(typeof(ProductType))).ToDictionary(p => p, p => Colors.Aqua);
            productTypeToColor[ProductType.b] = Colors.Blue;
            productTypeToColor[ProductType.y] = Colors.Purple;
            productTypeToColor[ProductType.zDot] = Colors.Orange;
            productTypeToColor[ProductType.c] = Colors.Gold;

            // offset for annotation on base sequence
            productTypeToYOffset = ((ProductType[])Enum.GetValues(typeof(ProductType))).ToDictionary(p => p, p => 0.0);
            productTypeToYOffset[ProductType.b] = 50;
            productTypeToYOffset[ProductType.y] = 0;
            productTypeToYOffset[ProductType.c] = 50;
            productTypeToYOffset[ProductType.zDot] = 0;

            // colors for modifications
            ModificationAnnotationColors.Add("Acetyl", Brushes.Blue);
            ModificationAnnotationColors.Add("Carbamidomethyl", Brushes.Green);
            ModificationAnnotationColors.Add("Phospho", Brushes.Gold);
            ModificationAnnotationColors.Add("Oxidation", Brushes.Turquoise);
            ModificationAnnotationColors.Add("Methyl", Brushes.DeepPink);
            
        }

        private void AnnotateFragmentIon(TextBlock residue, MatchedFragmentIon ion, TopDownProteoform psm)
        {
            string annotation = ion.NeutralTheoreticalProduct.ProductType + "" + ion.NeutralTheoreticalProduct.TerminusFragment.FragmentNumber;
            Color color = productTypeToColor[ion.NeutralTheoreticalProduct.ProductType];

            if (ion.NeutralTheoreticalProduct.NeutralLoss != 0)
            {
                annotation += "-" + ion.NeutralTheoreticalProduct.NeutralLoss;
            }

            if (ion.NeutralTheoreticalProduct.TerminusFragment.Terminus == FragmentationTerminus.C)
            {
                double top = Canvas.GetTop(residue);
                double left = Canvas.GetLeft(residue);

                BaseSequenceAnnotation.DrawCTerminalIon(baseSequenceAnnotationCanvas,
                    new Point(left - 2, top - 9 + productTypeToYOffset[ion.NeutralTheoreticalProduct.ProductType]),
                    color, annotation);
            }
            else if (ion.NeutralTheoreticalProduct.TerminusFragment.Terminus == FragmentationTerminus.N)
            {
                double top = Canvas.GetTop(residue);
                double left = Canvas.GetLeft(residue);

                BaseSequenceAnnotation.DrawNTerminalIon(baseSequenceAnnotationCanvas,
                    new Point(left + 23, top - 11 + productTypeToYOffset[ion.NeutralTheoreticalProduct.ProductType]),
                    color, annotation);
            }
        }

        private void TopDown_Load(object sender, EventArgs e)
        {

        }

        private void nUD_min_score_td_ValueChanged(object sender, EventArgs e)
        {
            Sweet.lollipop.min_score_td = Convert.ToDouble(nUD_min_score_td.Value);
        }

        private void cb_tight_abs_mass_CheckedChanged(object sender, EventArgs e)
        {
            Sweet.lollipop.tight_abs_mass = (bool)cb_tight_abs_mass.IsChecked;
        }

        private void cb_biomarker_CheckedChanged(object sender, EventArgs e)
        {
            Sweet.lollipop.biomarker = (bool)cb_biomarker.IsChecked;
        }
        private void tb_tableFilter_TextChanged(object sender, EventArgs e)
        {
            IEnumerable<object> selected_td = tb_tableFilter.Text == "" ?
               Sweet.lollipop.topdown_proteoforms :
               ExtensionMethods.filter(Sweet.lollipop.topdown_proteoforms, tb_tableFilter.Text);
            DisplayUtility.FillDataGridView(dgv_TD_proteoforms, selected_td.OfType<TopDownProteoform>().Select(t => new DisplayTopDownProteoform(t)));
            DisplayTopDownProteoform.FormatTopDownTable(dgv_TD_proteoforms, false);

        }

        private void nUD_td_rt_tolerance_ValueChanged(object sender, EventArgs e)
        {
            Sweet.lollipop.td_retention_time_tolerance = (double)nUD_td_rt_tolerance.Value;
        }
        
    }
}
