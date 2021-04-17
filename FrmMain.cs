/*
Copyright (C) 2020-2021 Tris Shores

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using marc_common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using viewmarc_ui.Properties;

namespace viewmarc_ui
{
    internal partial class c_FrmMain : Form
    {
        #region init

        internal static c_FrmMessageBox v_frmMessageBox;
        private static c_FrmAbout v_frmAbout;
        private int[] v_colPadding;
        private string v_MrcOpenFilePathArg;
        private Color v_rowBackColor;
        private Color v_tabHighlightColor = Color.FromArgb(204, 245, 255);
        private bool v_highlightFixedFields = true;
        private bool v_highlightMediaFields = true;
        private string v_CurrentDir;

        private BindingList<c_PropertyItem> v_PropertyList = new BindingList<c_PropertyItem>();
        private BindingList<c_LeaderItem> v_LeaderList = new BindingList<c_LeaderItem>();
        private BindingList<c_DirectoryItem> v_DirectoryList = new BindingList<c_DirectoryItem>();
        private BindingList<c_ControlItem> v_ControlList = new BindingList<c_ControlItem>();
        private BindingList<c_VardataItem> v_VardataList = new BindingList<c_VardataItem>();

        private enum v_Tab { v_Leader, v_Directory, v_Control, v_Vardata, v_NotSet }
        private v_Tab v_tab = v_Tab.v_NotSet;  // initial view.

        internal c_FrmMain(string v_mrcOpenFilePathArg = null)
        {
            InitializeComponent();

            // defaults:
            c_SettingsTools2.m_Init();
            v_frmMessageBox = new c_FrmMessageBox(this);
            v_frmAbout = new c_FrmAbout(this);
            this.v_MrcOpenFilePathArg = v_mrcOpenFilePathArg;
            m_SetScreenPosition();
            m_InitializeDgv();
            AllowDrop = true;
            v_frmAbout.v_pbxLogo.Image = Resources.viewmarc_256;
            v_btnToggleHighlightFixedFields.Enabled = false;
            v_btnToggleHighlightMediaFields.Enabled = false;
            v_btnLeader.Enabled = false;
            v_btnDirectory.Enabled = false;
            v_btnControl.Enabled = false;
            v_btnVardata.Enabled = false;
            v_lblMain.SendToBack();
            v_pbxMain.SendToBack();

            // events:
            LocationChanged += m_FrmMain_LocationChanged;
            Shown += m_FrmMain_Shown;
            DragEnter += m_Ui_DragEnter;
            DragDrop += m_Ui_DragDrop;
            DragLeave += m_Ui_DragLeave;
            v_btnToggleHighlightFixedFields.Click += m_btnToggleHighlightFixedFields_Click;
            v_btnToggleHighlightMediaFields.Click += m_btnToggleHighlightMediaFields_Click;
            v_btnOpen.Click += m_BtnOpen_Click;
            v_btnLeader.Click += (v_s, v_e) => { m_PopulateDgv(v_btnLeader); };
            v_btnDirectory.Click += (v_s, v_e) => { m_PopulateDgv(v_btnDirectory); };
            v_btnControl.Click += (v_s, v_e) => { m_PopulateDgv(v_btnControl); };
            v_btnVardata.Click += (v_s, v_e) => { m_PopulateDgv(v_btnVardata); };
            v_btnAbout.Click += m_LnkAbout_Click;
        }

        internal void m_FrmMain_Shown(object v_sender, EventArgs v_e)
        {
            // visual cues:
            m_LockCommonUi(v_lockUi: true, v_padlockEnable: true);
            m_SetStatusMessage("Opening file...");
            m_ShowStatusBarSpinner(v_show: true);

            m_SetStatusMessage("Ready");

            try
            {
                if (File.Exists(v_MrcOpenFilePathArg)) m_OpenMrcFile(v_MrcOpenFilePathArg);
                v_MrcOpenFilePathArg = "";
            }
            finally
            {
                m_LockCommonUi(v_lockUi: false);
                m_ShowStatusBarSpinner(v_show: false);
            }

            v_CurrentDir = c_SettingsTools2.m_WorkspaceDir;
        }

        #endregion

        #region help

        private List<c_HelpItem> m_GetHelpUrls(string v_fieldId, string v_mnemonic)
        {
            var v_helpItemList = new List<c_HelpItem>();
            if (v_fieldId.m_IsEmpty()) return v_helpItemList;

            void m_AddFixedFieldHelp(string v_mnemonic)
            {
                if (v_mnemonic.m_IsEmpty()) return;
                if (v_mnemonic == "Rec stat") v_mnemonic = "Rec";
                if (v_mnemonic == "S/L") v_mnemonic = "Succ";
                v_helpItemList.Add(new c_HelpItem($"Help page for '{v_mnemonic}' fixed field (OCLC)", $"https://www.oclc.org/bibformats/en/fixedfield/{v_mnemonic.ToLower()}.html"));
            }

            if (v_fieldId.StartsWith("LDR"))
            {
                v_helpItemList.Add(new c_HelpItem($"Help page for Leader field (LC)", "https://www.loc.gov/marc/bibliographic/concise/bdleader.html"));
                m_AddFixedFieldHelp(v_mnemonic);
            }
            else if (v_fieldId.StartsWith("DIR"))
            {
                v_helpItemList.Add(new c_HelpItem($"Help page for Directory field (LC)", "https://www.loc.gov/marc/bibliographic/bddirectory.html"));
            }
            else
            {
                var v_fieldNum = int.Parse(v_fieldId);
                if (v_fieldNum < 10)
                {
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field (LC)", $"https://www.loc.gov/marc/bibliographic/bd{v_fieldId}.html"));
                    m_AddFixedFieldHelp(v_mnemonic);
                }
                else if (v_fieldNum >= 10 && v_fieldNum < 900)
                {
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field (LC)", $"https://www.loc.gov/marc/bibliographic/bd{v_fieldId}.html"));
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field (OCLC)", $"https://www.oclc.org/bibformats/en/{v_fieldId.Substring(0, 1)}xx/{v_fieldId}.html"));
                }
                else if (v_fieldNum >= 900 && v_fieldNum < 907)
                {
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field (OCLC)", "https://www.oclc.org/bibformats/en/9xx/901-907.html"));
                }
                else if (v_fieldNum >= 945 && v_fieldNum <= 949)
                {
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field (OCLC)", "https://www.oclc.org/bibformats/en/9xx/945-949.html"));
                }
                else if (new[] { 936, 938, 956, 987, 994 }.Any(v_x => v_x == v_fieldNum))
                {
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field (OCLC)", $"https://www.oclc.org/bibformats/en/9xx/{v_fieldId}.html"));
                }
                else if (v_fieldId.StartsWith("9"))
                {
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field (OCLC)", "https://www.oclc.org/bibformats/en/9xx.html"));
                }
                else
                {
                    v_helpItemList.Add(new c_HelpItem($"Help page for '{v_fieldId}' field: help unavailable", null));
                }
            }
            return v_helpItemList;
        }

        internal class c_HelpItem
        {
            internal string v_Desc;
            internal string v_Url;

            public c_HelpItem(string v_desc, string v_url)
            {
                v_Desc = v_desc;
                v_Url = v_url;
            }
        }

        private void m_LnkAbout_Click(object v_sender, EventArgs v_e)
        {
            v_frmAbout.ShowDialog(this);
        }

        #endregion

        #region open mrc

        private void m_BtnOpen_Click(object v_sender, EventArgs v_e)
        {
            var v_mrcOpenFilePath = "";
            using (var v_openFileDialog = new OpenFileDialog())
            {
                const string v_ext = "mrc";
                v_openFileDialog.Title = "Open mrc file";
                v_openFileDialog.DefaultExt = $"*.{v_ext}";
                v_openFileDialog.Filter = $"mrc file (*.{v_ext})|*.{v_ext}";
                v_openFileDialog.InitialDirectory = v_CurrentDir;
                if (!Directory.Exists(v_openFileDialog.InitialDirectory))
                {
                    v_openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                if (v_openFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    v_CurrentDir = Path.GetDirectoryName(v_openFileDialog.FileName);
                    c_SettingsTools2.m_WorkspaceDir = v_CurrentDir;

                    if (Path.GetExtension(v_openFileDialog.FileName).Equals(".mrc", StringComparison.OrdinalIgnoreCase))
                    {
                        v_mrcOpenFilePath = v_openFileDialog.FileName;
                    }
                }
            }
            if (!File.Exists(v_mrcOpenFilePath)) return;

            m_OpenMrcFile(v_mrcOpenFilePath);
        }

        private void m_OpenMrcFile(string v_mrcOpenFilePath)
        {
            void m_HandleFailure(string v_msg)
            {
                v_dgv.Visible = false;
                v_btnLeader.Enabled = false;
                v_btnLeader.BackColor = default;
                v_btnDirectory.Enabled = false;
                v_btnDirectory.BackColor = default;
                v_btnControl.Enabled = false;
                v_btnControl.BackColor = default;
                v_btnVardata.Enabled = false;
                v_btnVardata.BackColor = default;
                m_SetTitle(null);
                c_FrmMessageBox.m_Singleton(this).m_OpenDialog(v_msg + ".", new[] { "OK" }, Resources.error);
            }

            m_LockCommonUi(v_lockUi: true);
            m_SetStatusMessage("Opening mrc file...");
            m_ShowStatusBarSpinner(v_show: true);

            try
            {
                var v_exitCode = m_DisplayMrcData_(v_mrcOpenFilePath);
                if (v_exitCode == 0)
                {
                    v_dgv.Visible = true;
                    v_isDgvVisible = null;
                }
                else
                {
                    if (v_exitCode == (int)c_StatusTools.c_StatusCode.MarcFileParseError) m_HandleFailure($"Error parsing {v_mrcOpenFilePath}");
                    else if (v_exitCode == (int)c_StatusTools.c_StatusCode.UnsupportedMarcFile) m_HandleFailure($"Unsupported MARC file");
                    else m_HandleFailure(c_StatusTools.m_GetErrorText(v_exitCode));
                }
            }
            catch (Exception v_e)
            {
                m_HandleFailure($"Unknown error");
            }
            finally
            {
                m_LockCommonUi(v_lockUi: false);
                m_ShowStatusBarSpinner(v_show: false);
            }

            v_dgv.ClearSelection();
        }

        private int m_DisplayMrcData_(string v_mrcOpenFilePath)
        {
            // clear any existing row data:
            v_PropertyList.Clear();
            v_LeaderList.Clear();
            v_DirectoryList.Clear();
            v_ControlList.Clear();
            v_VardataList.Clear();
            v_dgv.Rows.Clear();

            // call engine to generate dat file from the mrc file:
            var lib = new viewmarc_lib.c_Engine();
            var v_exitCode = lib.RunMrcToDat(v_mrcOpenFilePath, out string[] v_lines);

            if (v_exitCode != 0) return v_exitCode;

            foreach (var v_line in v_lines)
            {
                if (v_line.StartsWith("PRP")) v_PropertyList.Add(new c_PropertyItem(v_line.Substring(4).Split('\u23F5')));
                if (v_line.StartsWith("LDR")) v_LeaderList.Add(new c_LeaderItem(v_line.Substring(4).Split('\u23F5')));
                if (v_line.StartsWith("DIR")) v_DirectoryList.Add(new c_DirectoryItem(v_line.Substring(4).Split('\u23F5')));
                if (v_line.StartsWith("CTR")) v_ControlList.Add(new c_ControlItem(v_line.Substring(4).Split('\u23F5')));
                if (v_line.StartsWith("VAR")) v_VardataList.Add(new c_VardataItem(v_line.Substring(4).Split('\u23F5')));
            }

            // populate datagridview with default view:
            v_btnLeader.Enabled = true;
            v_btnDirectory.Enabled = true;
            v_btnControl.Enabled = true;
            v_btnVardata.Enabled = true;

            if (v_tab == v_Tab.v_NotSet) v_tab = v_Tab.v_Vardata;

            if (v_tab == v_Tab.v_Leader) v_btnLeader.PerformClick();
            if (v_tab == v_Tab.v_Directory) v_btnDirectory.PerformClick();
            if (v_tab == v_Tab.v_Control) v_btnControl.PerformClick();
            if (v_tab == v_Tab.v_Vardata) v_btnVardata.PerformClick();

            // update form title:
            m_SetTitle(v_mrcOpenFilePath);

            return 0;
        }

        private void m_SetTitle(string v_mrcFilePath)
        {
            if (v_mrcFilePath.m_IsEmpty())
            {
                Text = c_AssemblyTools.v_ProductName;
                m_SetStatusMessage("");
                return;
            }
            var v_materialType = v_PropertyList.Single(v_x => v_x.v_Key == "MaterialType").v_Value;
            var v_formattedMaterialType = Regex.Replace(v_materialType, "([A-Z]{1})", m => $" {m.Groups[1].Value.ToLower()}");
            Text = $"{c_AssemblyTools.v_ProductName}   {Path.GetFileNameWithoutExtension(v_mrcFilePath)} ({v_formattedMaterialType.Trim()})";
            m_SetStatusMessage($"{Path.GetFileNameWithoutExtension(v_mrcFilePath)}");
        }

        #endregion

        #region async visual

        internal void m_SetStatusMessage(string v_msg)
        {
            v_lblStatus.Text = v_msg;
        }

        internal void m_ShowStatusBarSpinner(bool v_show)
        {
            v_pbxSpinner.Enabled = v_show;
            v_pbxSpinner.Visible = v_show;
        }

        internal void m_LockCommonUi(bool v_lockUi, bool v_padlockEnable = false)
        {
            if (v_lockUi)
            {
                v_btnOpen.Enabled = false;
                v_btnHelp.Enabled = false;
            }
            else
            {
                v_btnOpen.Enabled = true;
                v_btnHelp.Enabled = true;
            }
        }

        #endregion

        #region window position

        private void m_FrmMain_LocationChanged(object v_sender, EventArgs v_e)
        {
            c_SettingsTools2.m_Coordinate = Location;
        }

        public void m_SetScreenPosition()
        {
            var v_location = c_SettingsTools2.m_Coordinate;
            if (v_location != null)
            {
                var v_windowRectangle = new Rectangle((Point)v_location, new Size(Width, Height));
                foreach (var v_screen in Screen.AllScreens)
                {
                    if (v_screen.WorkingArea.Contains(v_windowRectangle))
                    {
                        StartPosition = FormStartPosition.Manual;
                        Location = (Point)v_location;
                        return;
                    }
                }
            }

            StartPosition = FormStartPosition.CenterScreen;
        }

        #endregion

        #region drag drop file

        private bool? v_isDgvVisible;

        private void m_Ui_DragEnter(object v_sender, DragEventArgs v_e)
        {
            if (v_e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                v_isDgvVisible = v_dgv.Visible;

                var v_files = (string[])v_e.Data.GetData(DataFormats.FileDrop);
                if (v_files.Length == 1 && Path.GetExtension(v_files[0]).Equals(".mrc", StringComparison.OrdinalIgnoreCase) && File.Exists(v_files[0]))
                {
                    v_e.Effect = DragDropEffects.Link;
                    v_btnOpen.BackColor = Color.FromArgb(137, 209, 133);
                    v_dgv.Visible = false;
                }
                else
                {
                    v_e.Effect = DragDropEffects.None;
                }
            }
        }

        private void m_Ui_DragDrop(object v_sender, DragEventArgs v_e)
        {
            v_btnOpen.BackColor = default;
            if (v_e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var v_files = (string[])v_e.Data.GetData(DataFormats.FileDrop);
                if (v_files.Length == 1 && Path.GetExtension(v_files[0]).Equals(".mrc", StringComparison.OrdinalIgnoreCase) && File.Exists(v_files[0]))
                {
                    m_OpenMrcFile(v_files[0]);
                }
                else
                {
                    v_dgv.Visible = false;
                }
            }

            v_dgv.ClearSelection();
        }

        private void m_Ui_DragLeave(object v_sender, EventArgs v_e)
        {
            v_btnOpen.BackColor = default;
            v_dgv.Visible = v_isDgvVisible ?? false;
        }

        #endregion

        #region datagridview

        private void m_InitializeDgv()
        {
            // defaults:
            v_dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            v_dgv.SelectionMode = DataGridViewSelectionMode.CellSelect;
            v_dgv.MultiSelect = true;
            v_dgv.ReadOnly = true;
            v_dgv.AllowUserToAddRows = false;
            v_dgv.AllowUserToDeleteRows = false;
            v_dgv.AllowUserToResizeRows = false;
            v_dgv.ColumnHeadersVisible = true;
            v_dgv.RowHeadersVisible = false;
            v_dgv.AutoGenerateColumns = true;
            v_dgv.BackgroundColor = Color.FromArgb(250, 250, 250);
            v_dgv.BorderStyle = BorderStyle.None;
            v_dgv.ReadOnly = true;
            v_dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(160, 160, 160);
            v_dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Verdana", 11.25F, FontStyle.Bold);
            v_dgv.RowsDefaultCellStyle.Font = new Font("Verdana", 14F, FontStyle.Regular);
            v_rowBackColor = Color.FromArgb(254, 255, 254);

            // events:
            v_dgv.Resize += m_Dgv_Resize;
            //v_dgv.MouseEnter += (v_s, v_e) => { v_dgv.Focus(); };
            //v_dgv.MouseLeave += (v_s, v_e) => { lblMain.Focus(); };
            v_dgv.CellClick += m_Dgv_CellClick;
            //v_dgv.SelectionChanged += v_dgv_SelectionChanged;
            v_dgv.CellPainting += m_Dgv_CellPainting;
            v_dgv.CellContextMenuStripNeeded += m_Dgv_CellContextMenuStripNeeded;
        }

        private void m_PopulateDgv(ToolStripButton v_tsb)
        {
            v_dgv.SelectionChanged -= m_Dgv_SelectionChanged;

            // name column headers
            if (v_tsb == v_btnLeader)
            {
                v_tab = v_Tab.v_Leader;
                v_dgv.DataSource = v_LeaderList;
                v_colPadding = new[] { 10, 5, 5, 0 };
                v_btnToggleHighlightFixedFields.Enabled = true;
                v_btnToggleHighlightMediaFields.Enabled = false;
                m_IndicateFixedFields();
            }
            else if (v_tsb == v_btnDirectory)
            {
                v_tab = v_Tab.v_Directory;
                v_dgv.DataSource = v_DirectoryList;
                v_colPadding = new[] { 10, 5, 5, 0 };
                v_btnToggleHighlightFixedFields.Enabled = false;
                v_btnToggleHighlightMediaFields.Enabled = false;
            }
            else if (v_tsb == v_btnControl)
            {
                v_tab = v_Tab.v_Control;
                v_dgv.DataSource = v_ControlList;
                v_colPadding = new[] { -35, 5, 5, 0 };
                v_btnToggleHighlightFixedFields.Enabled = true;
                v_btnToggleHighlightMediaFields.Enabled = true;
                m_IndicateFixedFields();
                m_IndicateMediaFields();
            }
            else if (v_tsb == v_btnVardata)
            {
                v_tab = v_Tab.v_Vardata;
                v_dgv.DataSource = v_VardataList;
                v_colPadding = new[] { -15, -20, -20, 0 };
                v_btnToggleHighlightFixedFields.Enabled = false;
                v_btnToggleHighlightMediaFields.Enabled = false;
                v_dgv.Columns["v_Value"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }
            else throw new Exception("Unknown tab selection");

            // set column defaults:
            foreach (DataGridViewColumn v_col in v_dgv.Columns)
            {
                v_col.SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            // row stuff
            for (var v_i = 0; v_i < v_dgv.Rows.Count; v_i++)
            {
                // set row height:
                v_dgv.Rows[v_i].MinimumHeight = 30;
            }

            // tooltip for blank symbol:
            m_IndicateBlankSymbol();

            // handle column widths and horiz scrollbar:
            m_Dgv_Resize(null, null);

            // set selected tab backcolor:
            v_btnLeader.ForeColor = Color.FromArgb(220, 220, 220);
            v_btnDirectory.ForeColor = Color.FromArgb(220, 220, 220);
            v_btnControl.ForeColor = Color.FromArgb(220, 220, 220);
            v_btnVardata.ForeColor = Color.FromArgb(220, 220, 220);
            v_tsb.ForeColor = v_tabHighlightColor;

            v_dgv.ClearSelection();

            v_dgv.SelectionChanged += m_Dgv_SelectionChanged;
        }

        private void m_Dgv_Resize(object v_sender, EventArgs v_e)
        {
            v_dgv.SelectionChanged -= m_Dgv_SelectionChanged;

            // expand last column to fill table:
            var v_widthList = new List<int>();

            // set column mode and store widths:
            for (var v_i = 0; v_i < v_dgv.Columns.Count; v_i++)
            {
                v_dgv.Columns[v_i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                v_widthList.Add(v_dgv.Columns[v_i].Width);
            }

            // remove padding from columns:
            for (var v_i = 0; v_i < v_widthList.Count; v_i++)
            {
                v_dgv.Columns[v_i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                v_dgv.Columns[v_i].Width = v_widthList[v_i] + v_colPadding[v_i];
            }

            // reset column modes:
            for (var v_i = 0; v_i < v_dgv.Columns.Count - 1; v_i++)
            {
                v_dgv.Columns[v_i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }

            // fill last column width:
            if (v_widthList.Sum() < v_dgv.Width && v_dgv.Columns.Count > 0)
            {
                v_dgv.Columns[v_dgv.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                v_widthList.RemoveAt(v_dgv.Columns.Count - 1);
            }
            else
            {
                if (v_dgv.Columns.Count > 0)
                {
                    // add padding back to last column:
                    v_dgv.Columns[v_dgv.Columns.Count - 1].Width -= v_colPadding.Sum();
                }
            }

            v_dgv.SelectionChanged += m_Dgv_SelectionChanged;
        }

        private void m_Dgv_CellClick(object v_sender, DataGridViewCellEventArgs v_e)
        {
            if (v_e.RowIndex == -1)
            {
                // user click on any column header clears all selected cells in table:
                v_dgv.ClearSelection();
            }
        }

        private void m_Dgv_SelectionChanged(object v_sender, EventArgs v_e)
        {
            // triggered when cells are selected by user.
            // triggered once for single-cell selection and twice for multi-cell selection.
            m_ClearAllRows();
            if (v_dgv.SelectedCells.Count == 0) return;
            v_dgv.SelectedCells.Cast<DataGridViewCell>().ToList().ForEach(v_x => m_HighlightRow(v_x.RowIndex));
        }

        private void m_ClearAllRows()
        {
            foreach (DataGridViewRow v_row in v_dgv.Rows)
            {
                v_row.DefaultCellStyle.BackColor = v_rowBackColor;
            }
        }

        private void m_HighlightRow(int v_rowIndex)
        {
            v_dgv.Rows[v_rowIndex].DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#C2E0FF");
        }

        private void m_IndicateFixedFields()
        {
            if (v_tab == v_Tab.v_Directory || v_tab == v_Tab.v_Vardata || v_dgv.ColumnCount < 4) return;

            foreach (DataGridViewRow v_row in v_dgv.Rows)
            {
                if (v_row.Cells["v_Mnemonic"].Value.ToString().m_IsEmpty()) continue;
                v_row.Cells["v_Mnemonic"].Style.BackColor = v_highlightFixedFields ? Color.FromArgb(252, 248, 189) : v_rowBackColor;
                v_row.Cells["v_Mnemonic"].ToolTipText = "Fixed Field";
            }
        }

        private void m_btnToggleHighlightFixedFields_Click(object v_sender, EventArgs v_e)
        {
            v_highlightFixedFields = !v_highlightFixedFields;
            m_IndicateFixedFields();
        }

        private void m_IndicateMediaFields()
        {
            if (v_tab == v_Tab.v_Directory || v_tab == v_Tab.v_Vardata || v_dgv.ColumnCount < 4) return;

            foreach (DataGridViewRow v_row in v_dgv.Rows)
            {
                var v_value = v_row.Cells["v_Field"]?.Value?.ToString();
                var v_mediaMatch = Regex.Match(v_value, @"008 \[(\d\d)");
                if (v_mediaMatch.Success)
                {
                    var v_posIndex = int.Parse(v_mediaMatch.Groups[1].Value);
                    if (v_posIndex < 18 || v_posIndex > 34) continue;

                    // set backcolor:
                    v_row.Cells["v_Field"].Style.BackColor = v_highlightMediaFields ? Color.FromArgb(215, 237, 216) : v_rowBackColor;
                    v_row.Cells["v_Name"].Style.BackColor = v_highlightMediaFields ? Color.FromArgb(215, 237, 216) : v_rowBackColor;

                    // set tooltip:
                    var v_materialType = v_PropertyList.Single(v_x => v_x.v_Key == "MaterialType").v_Value;
                    v_row.Cells["v_Field"].ToolTipText = $"Material type: {v_materialType}";
                    v_row.Cells["v_Name"].ToolTipText = $"Material type: {v_materialType}";
                }
            }
        }

        private void m_btnToggleHighlightMediaFields_Click(object v_sender, EventArgs v_e)
        {
            v_highlightMediaFields = !v_highlightMediaFields;
            m_IndicateMediaFields();
        }

        private void m_IndicateBlankSymbol()
        {
            if (v_tab == v_Tab.v_Leader || v_tab == v_Tab.v_Control)
            {
                foreach (DataGridViewRow v_row in v_dgv.Rows)
                {
                    if (v_row.Cells["v_Value"].Value.ToString() == c_MarcSymbols.c_Human.v_Blank.ToString())
                    {
                        v_row.Cells["v_Value"].ToolTipText = "Blank symbol";
                    }
                }
            }
            if (v_tab == v_Tab.v_Vardata)
            {
                foreach (DataGridViewRow v_row in v_dgv.Rows)
                {
                    if (v_row.Cells["v_Ind1"].Value.ToString() == c_MarcSymbols.c_Human.v_Blank.ToString())
                    {
                        v_row.Cells["v_Ind1"].ToolTipText = "Blank symbol";
                    }
                    if (v_row.Cells["v_Ind2"].Value.ToString() == c_MarcSymbols.c_Human.v_Blank.ToString())
                    {
                        v_row.Cells["v_Ind2"].ToolTipText = "Blank symbol";
                    }
                }
            }
        }

        private void m_Dgv_CellPainting(object v_sender, DataGridViewCellPaintingEventArgs v_e)
        {
            if (v_e.RowIndex == -1)
            {
                v_e.Paint(v_e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Border);
                using (Pen v_customPen = new Pen(SystemColors.Control, 1))
                {
                    Rectangle v_rect = v_e.CellBounds;
                    v_rect.Width -= 2;
                    v_rect.Height -= 2;
                    v_e.Graphics.DrawRectangle(v_customPen, v_rect);
                }
                v_e.Handled = true;
            }
        }

        private void m_Dgv_CellContextMenuStripNeeded(object v_sender, DataGridViewCellContextMenuStripNeededEventArgs v_e)
        {
            if (v_e.RowIndex < 0) return;
            v_dgv.ClearSelection();
            v_dgv.Rows[v_e.RowIndex].Cells[v_e.ColumnIndex].Selected = true;
            m_HighlightRow(v_e.RowIndex);

            var v_cms = new ContextMenuStrip
            {
                //ShowCheckMargin = false,
                //ShowImageMargin = false
            };
            if (v_e.RowIndex > -1)
            {
                var v_fieldVal = v_dgv.Rows[v_e.RowIndex].Cells[0].Value?.ToString() ?? "";
                var v_fieldId = v_fieldVal.Length >= 3 ? v_fieldVal.Substring(0, 3) : "";
                var v_tempVal = v_dgv.Rows[v_e.RowIndex].Cells[2]?.Value?.ToString();
                var v_mnemonic = (v_tab == v_Tab.v_Leader || v_tab == v_Tab.v_Control) && v_tempVal.m_IsNotEmpty() ? v_tempVal : null;
                var v_helpList = m_GetHelpUrls(v_fieldId, v_mnemonic);
                foreach (var v_helpItem in v_helpList)
                {
                    ToolStripMenuItem v_tsItem;
                    if (v_helpItem.v_Url.m_IsEmpty()) v_tsItem = new ToolStripMenuItem(v_helpItem.v_Desc, image: null, onClick: null);
                    else v_tsItem = new ToolStripMenuItem(v_helpItem.v_Desc, Resources.help, (v_s, v_e) => { c_HelpTools.m_OpenUrl(v_helpItem.v_Url); });
                    v_tsItem.ImageScaling = ToolStripItemImageScaling.None;
                    v_cms.Items.Add(v_tsItem);
                }
            }
            v_e.ContextMenuStrip = v_cms;
        }

        #endregion

        #region item classes

        public class c_PropertyItem
        {
            public string v_Key { get; set; }

            public string v_Value { get; set; }

            public c_PropertyItem(string[] v_val)
            {
                v_Key = v_val[0];
                v_Value = v_val[1];
            }
        }

        public class c_LeaderItem
        {
            [DisplayName("Field")]
            public string v_Field { get; set; }

            [DisplayName("Name")]
            public string v_Name { get; set; }

            [DisplayName("Mnemonic")]
            public string v_Mnemonic { get; set; }

            [DisplayName("Value")]
            public string v_Value { get; set; }

            public c_LeaderItem(string[] v_val)
            {
                v_Field = v_val[0];
                v_Name = v_val[1];
                v_Mnemonic = v_val[2];
                v_Value = v_val[3].Replace(' ', c_MarcSymbols.c_Human.v_Blank);
            }
        }

        public class c_DirectoryItem
        {
            [DisplayName("Field")]
            public string v_Field { get; set; }

            [DisplayName("ID")]
            public string v_Id { get; set; }

            [DisplayName("Length")]
            public string v_Length { get; set; }

            [DisplayName("Start")]
            public string v_Start { get; set; }

            public c_DirectoryItem(string[] v_val)
            {
                v_Field = v_val[0];
                v_Id = v_val[1];
                v_Length = v_val[2];
                v_Start = v_val[3];
            }
        }

        public class c_ControlItem
        {
            [DisplayName("Field")]
            public string v_Field { get; set; }

            [DisplayName("Name")]
            public string v_Name { get; set; }

            [DisplayName("Mnemonic")]
            public string v_Mnemonic { get; set; }

            [DisplayName("Value")]
            public string v_Value { get; set; }

            public c_ControlItem(string[] v_val)
            {
                v_Field = v_val[0];
                v_Name = v_val[1];
                v_Mnemonic = v_val[2];
                v_Value = v_val[3].Replace(' ', c_MarcSymbols.c_Human.v_Blank);
            }
        }

        public class c_VardataItem
        {
            [DisplayName("Field")]
            public string v_Field { get; set; }

            [DisplayName("I\u2081")]
            public string v_Ind1 { get; set; }

            [DisplayName("I\u2082")]
            public string v_Ind2 { get; set; }

            [DisplayName("Value")]
            public string v_Value { get; set; }

            public c_VardataItem(string[] v_val)
            {
                v_Field = v_val[0];
                v_Ind1 = v_val[1].Replace(' ', c_MarcSymbols.c_Human.v_Blank);
                v_Ind2 = v_val[2].Replace(' ', c_MarcSymbols.c_Human.v_Blank);
                v_Value = v_val[3];
            }
        }

        #endregion
    }
}