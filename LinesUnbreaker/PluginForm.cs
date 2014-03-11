﻿using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Nikse.SubtitleEdit.PluginLogic
{
    public partial class PluginForm : Form
    {
        //private string path = Path.Combine("Plugins", "SeLinesUnbreaker.xml");
        private Subtitle _subtitle;

        private XElement _xmlSetting = null;
        private bool _allowFixes;
        private int _totalFixed;
        public string FixedSubtitle { get; set; }

        public PluginForm()
        {
            InitializeComponent();
        }

        public PluginForm(Subtitle subtitle, string name, string description, Form parentForm)
            : this()
        {
            // TODO: Complete member initialization
            this._subtitle = subtitle;
            this.Resize += (s, arg) =>
            {
                listView1.Columns[listView1.Columns.Count - 1].Width = -2;
                //this.listViewFixes.Columns.Count -1
            };
            this.FormClosing += (sender, e) =>
            {
                LoadSettingsIfThereIs(false); // the setting will be stored in xml file.
            };
            LoadSettingsIfThereIs(true);
            FindDialogueAndListFixes();
        }

        private void LoadSettingsIfThereIs(bool load)
        {
            string path = GetSettingsFileName();
            if (load)
            {
                if (File.Exists(path))
                {
                    _xmlSetting = XElement.Load(path);
                    decimal val;
                    decimal.TryParse(_xmlSetting.Element("Shorterthan").Value, out val);
                    if (val > 0)
                        numericUpDown1.Value = val;

                    checkBoxSkipDialog.Checked = bool.Parse(_xmlSetting.Element("SkipDialog").Value);
                    checkBoxSkipNarrator.Checked = bool.Parse(_xmlSetting.Element("SkipNarrator").Value);
                    checkBoxMoods.Checked = bool.Parse(_xmlSetting.Element("SkipMoods").Value);
                }
                else
                {
                    _xmlSetting = new XElement("SeLinesUnbreaker",
                        new XElement("Shorterthan", numericUpDown1.Value),
                        new XElement("SkipDialog", true),
                        new XElement("SkipNarrator", true),
                        new XElement("SkipMoods", false)
                        );
                    _xmlSetting.Save(path);
                }
            }
            else
            {
                if (_xmlSetting == null)
                    return;
                _xmlSetting.Element("Shorterthan").Value = numericUpDown1.Value.ToString();
                _xmlSetting.Element("SkipMoods").Value = checkBoxMoods.Checked.ToString();
                _xmlSetting.Element("SkipNarrator").Value = checkBoxSkipNarrator.Checked.ToString();
                _xmlSetting.Element("SkipDialog").Value = checkBoxSkipDialog.Checked.ToString();
                _xmlSetting.Save(path);
            }
        }

        private string GetSettingsFileName()
        {
            // "C:\Users\Ivandrofly\Desktop\SubtitleEdit\Plugins\SeLinesUnbreaker.xml"
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            if (path.StartsWith("file:\\"))
                path = path.Remove(0, 6);
            path = Path.Combine(path, "Plugins");
            if (!Directory.Exists(path))
                path = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Subtitle Edit"), "Plugins");
            return Path.Combine(path, "SeLinesUnbreaker.xml");
        }

        private void FindDialogueAndListFixes()
        {
            _totalFixed = 0;
            listView1.BeginUpdate();
            int shorterThan = (int)numericUpDown1.Value;
            foreach (Paragraph p in _subtitle.Paragraphs)
            {
                if (p.NumberOfLines < 2)
                    continue;
                string oldText = p.Text;
                string text = p.Text;

                text = UnbreakLines(text, shorterThan);
                if (text.Length < shorterThan && text != oldText)
                {
                    text = text.Replace(" " + Environment.NewLine, Environment.NewLine).Trim();
                    text = text.Replace(Environment.NewLine + " ", Environment.NewLine).Trim();

                    if (AllowFix(p))
                    {
                        p.Text = text;
                    }
                    else
                    {
                        if (!_allowFixes)
                        {
                            oldText = Utilities.RemoveHtmlTags(oldText);
                            text = Utilities.RemoveHtmlTags(text);
                            AddFixToListView(p, oldText, text, text.Length.ToString());
                            _totalFixed++;
                        }
                    }
                }
            }

            if (!_allowFixes)
            {
                labelTotal.Text = string.Format("Total: {0}", _totalFixed);
                labelTotal.ForeColor = _totalFixed <= 0 ? Color.Red : Color.Green;
            }
            listView1.EndUpdate();
        }

        private bool AllowFix(Paragraph p)
        {
            if (!_allowFixes)
                return false;
            string ln = p.Number.ToString();
            foreach (ListViewItem item in listView1.Items)
            {
                if (item.SubItems[1].Text == ln)
                    return item.Checked;
            }
            return false;
        }

        private void AddFixToListView(Paragraph p, string before, string after, string lineLength)
        {
            var item = new ListViewItem() { Checked = true, UseItemStyleForSubItems = true };
            var subItem = new ListViewItem.ListViewSubItem(item, p.Number.ToString());
            item.SubItems.Add(subItem);

            subItem = new ListViewItem.ListViewSubItem(item, before.Replace(Environment.NewLine,
                Configuration.ListViewLineSeparatorString));
            item.SubItems.Add(subItem);

            subItem = new ListViewItem.ListViewSubItem(item, after.Replace(Environment.NewLine,
                Configuration.ListViewLineSeparatorString));
            item.SubItems.Add(subItem);

            subItem = new ListViewItem.ListViewSubItem(item, lineLength);
            item.SubItems.Add(subItem);
            item.Tag = p; // save paragraph in Tag
            listView1.Items.Add(item);
        }

        private string UnbreakLines(string s, int lineLength)
        {
            // check if lines isn't dialog - ivandro </br> - ismael!
            var t = Utilities.RemoveHtmlTags(s);
            t = t.Replace("  ", " ").Trim();

            // for one line dialog :)
            if ((t.StartsWith("-") || t.Contains("\r\n-")) && checkBoxSkipDialog.Checked)
                return s;
            else if (Regex.IsMatch(t, @"[\[\{\(]|[\}\]\)]") && checkBoxMoods.Checked)
                return s;
            else if (Regex.IsMatch(t, "\\w+:\\B") && checkBoxSkipNarrator.Checked)
                return s;
            else
            {
                t = t.Replace(Environment.NewLine, " ").Trim();
                t = t.Replace("  ", " ");

                if (t.Length < lineLength)
                {
                    s = s.Replace(Environment.NewLine, " ").Trim();
                    s = s.Replace("  ", " ");
                }
            }
            return s.Trim();
        }

        private void buttonUpdate_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
            _totalFixed = 0;
            FindDialogueAndListFixes();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            _allowFixes = true;
            FindDialogueAndListFixes();
            FixedSubtitle = _subtitle.ToText(new SubRip());
            DialogResult = DialogResult.OK;
        }

        private void PluginForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        private void SelectionHandler(object sender, EventArgs e)
        {
            if (this.listView1.Items.Count <= 0)
                return;

            if ((sender as Button).Text == "Select all")
                foreach (ListViewItem item in this.listView1.Items)
                    item.Checked = true;
            else
                foreach (ListViewItem item in this.listView1.Items)
                    item.Checked = !item.Checked;
        }
    }
}