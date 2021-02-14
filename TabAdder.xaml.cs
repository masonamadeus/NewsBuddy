﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using Ookii.Dialogs.Wpf;

namespace NewsBuddy
{
    /// <summary>
    /// Interaction logic for TabAdder.xaml
    /// </summary>
    public partial class TabAdder : Page
    {
        MainWindow homeBase = Application.Current.Windows[0] as MainWindow;
        public TabAdder()
        {
            InitializeComponent();
            PopulateList();
            MonitorDirectory();
        }

        public string chosenFile;
        FileSystemWatcher fs1;

        private void MonitorDirectory()
        {
            fs1 = new FileSystemWatcher();
            fs1.Path = Settings.Default.ScriptsDirectory;
            fs1.IncludeSubdirectories = true;
            fs1.EnableRaisingEvents = true;

            fs1.Changed += new FileSystemEventHandler(Reloader);
        }

        private void Reloader(object sender, FileSystemEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                PopulateList();
            });
        }
        private void PopulateList()
        {
            TemplatesList.Items.Clear();
            string[] templates = Directory.GetFiles(Settings.Default.TemplatesDirectory);
            ListBoxItem blank = new ListBoxItem();
            blank.Content = "Blank Script";
            blank.Tag = "/EmptyScript.xaml";
            TemplatesList.Items.Add(blank);
            TemplatesList.Items.Add(new Separator());
            foreach (string file in templates)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = System.IO.Path.GetFileNameWithoutExtension(file);
                item.Tag = file;
                TemplatesList.Items.Add(item);
            }
            TemplatesList.Items.Add(new Separator());
            ListBoxItem open = new ListBoxItem();
            open.Content = "Open a Script...";
            open.Name = "Opener";
            TemplatesList.Items.Add(open);
        }

        private void TemplatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem chosen = TemplatesList.SelectedItem as ListBoxItem;
            if (chosen != null)
            {
                if (chosen.Name == "Opener")
                {
                    OpenFileDialog opn = new OpenFileDialog();
                    opn.InitialDirectory = Settings.Default.ScriptsDirectory;
                    opn.RestoreDirectory = true;
                    if ((bool)opn.ShowDialog())
                    {
                        if (opn.CheckFileExists)
                        {
                            chosenFile = opn.FileName;
                        }
                    }
                    else
                    {
                        TemplatesList.SelectedItem = null;
                        return;
                    }
                }
                else
                {
                    chosenFile = chosen.Tag.ToString();

                }

                this.Dispatcher.Invoke(() =>
                {
                    homeBase.AddNewTabFromFrame(chosenFile);
                });

                TemplatesList.SelectedItem = null;

            } else { return; }


        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("reloaded with page_loaded");
            this.Dispatcher.Invoke(() =>
            {
                PopulateList();
            });
        }
    }


}
