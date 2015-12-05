﻿using System.Data;
using System.Data.SqlServerCe;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ErikEJ.SqlCeScripting;
using System.Windows.Controls;
using System.ComponentModel;
using System;
using Microsoft.Win32;
using System.IO;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;

namespace ErikEJ.SqlCeToolbox.ToolWindows
{
    /// <summary>
    /// Interaction logic for TableDataControl.xaml
    /// </summary>
    public partial class SqlEditorControl
    {
        public string Database { get; set; } //This property must be set by parent window
        
        public SqlEditorControl()
        {
            InitializeComponent();
        }

        #region Toolbar Button events

        private void SqlEditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtVersion.Text = RepoHelper.apiVer;
            SqlTextBox.Focus();
        }

        public string SqlText
        {
            get
            {
                return SqlTextBox.Text;
            }
            set
            {
                if (value != null)
                {
                    if (value.Length > 10000)
                        SqlTextBox.SyntaxHighlighting = null;
                    SqlTextBox.Text = value;
                    if (value.Length <= 10000 && SqlTextBox.SyntaxHighlighting == null)
                        LoadHighlighter();
                    this.Resultspanel.Children.Clear();
                }
                else
                {
                    SqlTextBox.Clear();
                }
            }
        }

        private void LoadHighlighter()
        {
            MemoryStream ms = null;
            try
            {
                ms = new MemoryStream(SqlCeToolbox.Resources.SqlCeSyntax);
                SqlTextBox.SyntaxHighlighting = HighlightingLoader.Load(new XmlTextReader(ms),
                HighlightingManager.Instance);
            }
            finally
            {
                if (ms != null)
                    ms.Dispose();
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenScript();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveScript();
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SqlTextBox.Text))
                return;
            ExecuteSqlScriptInEditor();
        }

        private void ExecuteWithPlanButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SqlTextBox.Text))
                return;
            try
            {
                using (var repository = RepoHelper.CreateRepository(Database))
                {
                    var textBox = new TextBox();
                    textBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                    textBox.FontSize = 14;
                    string sql = GetSqlFromSqlEditorTextBox();
                    string showPlan = string.Empty;
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    var dataset = repository.ExecuteSql(sql, out showPlan);
                    sw.Stop();
                    FormatTime(sw);
                    if (dataset != null)
                    {
                        ParseDataSetResultsToResultsBox(dataset);
                    }
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(showPlan))
                        {
                            // Just try to start SSMS
                            var fileName = System.IO.Path.GetTempFileName();
                            fileName = fileName + ".sqlplan";
                            System.IO.File.WriteAllText(fileName, showPlan);
                            System.Diagnostics.Process.Start(fileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
            catch (SqlCeException sqlException)
            {
                ParseSqlErrorToResultsBox(sqlException);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        private void ParseButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SqlTextBox.Text))
                return;

            using (var repository = RepoHelper.CreateRepository(Database))
            {
                var textBox = new TextBox();
                textBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                textBox.FontSize = 14;
                try
                {
                    string sql = GetSqlFromSqlEditorTextBox();
                    string showPlan = repository.ParseSql(sql);
                    textBox.Text = "Statement(s) in script parsed and seems OK!";
                    this.Resultspanel.Children.Clear();
                    this.Resultspanel.Children.Add(textBox);
                }
                catch (SqlCeException sqlException)
                {
                    ParseSqlErrorToResultsBox(sqlException);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchPanel sPanel = new SearchPanel();
            sPanel.Attach(SqlTextBox.TextArea);
        }

        private void ShowPlanButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SqlTextBox.Text))
                return;

            using (var repository = RepoHelper.CreateRepository(Database))
            {
                var textBox = new TextBox();
                textBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                textBox.FontSize = 14;
                try
                {
                    string sql = GetSqlFromSqlEditorTextBox();
                    string showPlan = repository.ParseSql(sql);
                    if (!string.IsNullOrWhiteSpace(showPlan))
                    {
                        var fileName = System.IO.Path.GetTempFileName();
                        fileName = fileName + ".sqlplan";
                        System.IO.File.WriteAllText(fileName, showPlan);
                        System.Diagnostics.Process.Start(fileName);
                    }
                }
                catch (SqlCeException sqlException)
                {
                    ParseSqlErrorToResultsBox(sqlException);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        #endregion

        private void FormatTime(Stopwatch sw)
        {
            var ts = new TimeSpan(sw.ElapsedTicks);
            this.txtTime.Text = string.Format("Duration: {0:00}:{1:00}.{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds);
        }

        private void OpenScript()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "SQL Server Compact Script (*.sqlce)|*.sqlce|SQL Server Script (*.sql)|*.sql|All Files(*.*)|*.*";
            ofd.CheckFileExists = true;
            ofd.Multiselect = false;
            ofd.ValidateNames = true;
            ofd.Title = "Select Script to Open";
            if (ofd.ShowDialog() == true)
            {
                this.SqlTextBox.Text = System.IO.File.ReadAllText(ofd.FileName);
            }
        }

        private void SaveScript()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "SQL Server Compact Script (*.sqlce)|*.sqlce|SQL Server Script (*.sql)|*.sql|All Files(*.*)|*.*";
            sfd.ValidateNames = true;
            sfd.Title = "Save script as";
            if (sfd.ShowDialog() == true && !string.IsNullOrWhiteSpace(this.SqlTextBox.Text))
            {
                System.IO.File.WriteAllText(sfd.FileName, this.SqlTextBox.Text);
            }
        }

        private void ExecuteSqlScriptInEditor()
        {
            Debug.Assert(!string.IsNullOrEmpty(Database), "Database property of this control has not been set by parent window or control");

            using (var repository = RepoHelper.CreateRepository(Database))
            {
                try
                {
                    var sql = GetSqlFromSqlEditorTextBox();
                    if (string.IsNullOrWhiteSpace(sql)) return;
                    sql = sql.Replace("\r", " \r");
                    sql = sql.Replace("GO  \r", "GO\r");
                    sql = sql.Replace("GO \r", "GO\r");
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    var dataset = repository.ExecuteSql(sql);
                    sw.Stop();
                    FormatTime(sw);
                    if (dataset != null)
                    {
                        ParseDataSetResultsToResultsBox(dataset);
                    }
                }
                catch (SqlCeException sqlException)
                {
                    ParseSqlErrorToResultsBox(sqlException);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private string GetSqlFromSqlEditorTextBox()
        {
            var sql = SqlTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(SqlTextBox.SelectedText))
                sql = SqlTextBox.SelectedText;
            if (!sql.EndsWith("\n\rGO"))
                sql = sql + "\n\rGO";
            return sql;
        }

        private void ParseSqlErrorToResultsBox(SqlCeException sqlException)
        {
            this.Resultspanel.Children.Clear();
            var textBox = new TextBox();
            textBox.Foreground = Brushes.Red;
            textBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            textBox.Text = Helpers.DataConnectionHelper.ShowErrors(sqlException);
            this.Resultspanel.Children.Add(textBox);
        }

        private void ParseDataSetResultsToResultsBox(DataSet dataset)
        {
            this.Resultspanel.Children.Clear();

            foreach (DataTable table in dataset.Tables)
            {
                this.txtTime.Text = this.txtTime.Text + " / " + table.Rows.Count.ToString() + " rows ";
                var textBox = new TextBox();
                textBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                textBox.FontSize = 14;
                DockPanel.SetDock(textBox, Dock.Top);
                if (table.Rows.Count == 0)
                {
                    textBox.Text = string.Format("{0} rows affected", table.MinimumCapacity);
                    this.Resultspanel.Children.Add(textBox);
                }
                else
                {
                    if (Properties.Settings.Default.ShowResultInGrid)
                    {
                        var grid = new DataGrid();
                        grid.AutoGenerateColumns = true;
                        grid.AutoGeneratingColumn += new EventHandler<DataGridAutoGeneratingColumnEventArgs>(grid_AutoGeneratingColumn);
                        grid.IsReadOnly = true;
                        grid.FontSize = 14;
                        grid.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                        grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
                        grid.ItemsSource = ((IListSource)table).GetList();
                        DockPanel.SetDock(grid, Dock.Top);
                        this.Resultspanel.Children.Add(grid);
                    }
                    else
                    {
                        textBox.Foreground = Brushes.Black;
                        var results = new StringBuilder();
                        foreach (var column in table.Columns)
                        {
                            results.Append(column + "\t");
                        }
                        results.AppendLine("\n--------------------------------------------------------------------------------------");

                        foreach (DataRow row in table.Rows)
                        {
                            foreach (var item in row.ItemArray)
                            {
                                if (Properties.Settings.Default.ShowBinaryValuesInResult == true)
                                {
                                    //This formatting is optional (causes perf degradation)
                                    if (item.GetType() == typeof(Byte[]))
                                    {
                                        Byte[] buffer = (Byte[])item;
                                        results.Append("0x");
                                        for (int i = 0; i < buffer.Length; i++)
                                        {
                                            results.Append(buffer[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                                        }
                                        results.Append("\t");
                                    }
                                    else if (item.GetType() == typeof(DateTime))
                                    {
                                        results.Append(((DateTime)item).ToString("O") + "\t");
                                    }
                                    else if (item.GetType() == typeof(Double) || item.GetType() == typeof(Single))
                                    {
                                        string intString = Convert.ToDouble(item).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                                        results.Append(intString + "\t");
                                    }
                                    else
                                    {
                                        results.Append(item + "\t");
                                    }
                                }
                                else
                                {
                                    if (item.GetType() == typeof(DateTime))
                                    {
                                        results.Append(((DateTime)item).ToString("O") + "\t");
                                    }
                                    else if (item.GetType() == typeof(Double) || item.GetType() == typeof(Single))
                                    {
                                        string intString = Convert.ToDouble(item).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                                        results.Append(intString + "\t");
                                    }
                                    else
                                    {
                                        results.Append(item + "\t");
                                    }
                                }
                            }
                            results.Append("\n");
                        }
                        results.AppendLine("\n");
                        textBox.Text = results.ToString();

                        this.Resultspanel.Children.Add(textBox);
                    }
                }
            }
        }

        void grid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var pos = e.PropertyName.IndexOf("_");
            if (pos > 0 && e.Column.Header != null)
            {
                e.Column.Header = e.Column.Header.ToString().Replace("_", "__");
            }
        }

        #region Hotkey events and management

        private void SqlTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.E && IsModifierPressed(ModifierKeys.Control) ||
                e.Key == Key.Enter && IsModifierPressed(ModifierKeys.Shift) ||
                e.Key == Key.F5)
            {
                ExecuteSqlScriptInEditor();
                e.Handled = true;
            }
        }

        private bool IsModifierPressed(ModifierKeys modifier)
        {
            return (Keyboard.Modifiers & modifier) == modifier;
        }

        #endregion
    }
}